using Application.Common;
using Application.DTOs;
using Application.Validators;
using Application.Mappings;
using Application.Services.Interface;
using Domain.Entities.Academic;
using Infrastructure.Repositories.Interface;
using Infrastructure.UnitOfWork;

namespace Application.Services.Implement;

public sealed class AcademicYearService(IUnitOfWork uow) : IAcademicYearService
{
    public async Task<ServiceResult<AcademicYearListItem>> CreateAsync(
        CreateAcademicYearRequest request,
        CancellationToken cancellationToken)
    {
        var validation = AcademicYearValidator.Validate(request);
        if (!validation.IsValid)
        {
            return ServiceResult<AcademicYearListItem>.Failure(
                "VALIDATION_ERROR",
                "Dữ liệu năm học không hợp lệ.",
                validation.Errors);
        }

        if (request.Terms is not null)
        {
            var termValidation = AcademicYearValidator.ValidateConfigureTerms(new ConfigureTermsRequest(request.Terms));
            if (!termValidation.IsValid)
                return ServiceResult<AcademicYearListItem>.Failure("VALIDATION_ERROR", "Dữ liệu học kỳ không hợp lệ.", termValidation.Errors);
        }

        var academicYear = new AcademicYear
        {
            Name = request.Name.Trim(),
            Status = "DRAFT",
            Semesters =
            [
                new Semester { Order = 1, Name = "Học kỳ I" },
                new Semester { Order = 2, Name = "Học kỳ II" }
            ]
        };
        if (request.Terms is not null)
            foreach (var term in request.Terms)
                academicYear.ConfigureTerm(term.Order, term.Name.Trim());

        var createOutcome = await uow.AcademicYears.TryAddAsync(academicYear, cancellationToken);

        if (createOutcome == AcademicYearCreateOutcome.Conflict)
        {
            return ServiceResult<AcademicYearListItem>.Failure(
                "ACADEMIC_YEAR_CONFLICT",
                "Năm học bị trùng tên với năm học đã có.");
        }

        return ServiceResult<AcademicYearListItem>.Success(academicYear.ToListItem());
    }

    public async Task<AcademicYearPage> ListAsync(
        AcademicYearListQuery query,
        CancellationToken cancellationToken)
    {
        var normalizedQuery = query with
        {
            Search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim()
        };
        var (items, totalCount) = await uow.AcademicYears.ListAsync(
            normalizedQuery.ToFilter(),
            cancellationToken);

        return new AcademicYearPage(
            items.Select(year => year.ToListItem()).ToArray(),
            normalizedQuery.Page,
            normalizedQuery.PageSize,
            totalCount);
    }

    public async Task<ServiceResult<AcademicYearDetailDto>> GetByIdAsync(
        ulong id,
        CancellationToken cancellationToken)
    {
        var year = await uow.AcademicYears.GetByIdWithSemestersAsync(id, cancellationToken);
        return year is null
            ? ServiceResult<AcademicYearDetailDto>.Failure(
                "ACADEMIC_YEAR_NOT_FOUND",
                "Không tìm thấy năm học.")
            : ServiceResult<AcademicYearDetailDto>.Success(year.ToDetailDto());
    }

    public async Task<ServiceResult<AcademicYearDetailDto>> UpdateAsync(
        ulong id,
        UpdateAcademicYearRequest request,
        CancellationToken cancellationToken)
    {
        var validation = AcademicYearValidator.ValidateUpdate(request);
        if (!validation.IsValid)
        {
            return ServiceResult<AcademicYearDetailDto>.Failure(
                "VALIDATION_ERROR",
                "Dữ liệu cập nhật năm học không hợp lệ.",
                validation.Errors);
        }

        var year = await uow.AcademicYears.GetByIdWithSemestersAsync(id, cancellationToken);
        if (year is null)
        {
            return ServiceResult<AcademicYearDetailDto>.Failure(
                "ACADEMIC_YEAR_NOT_FOUND",
                "Không tìm thấy năm học.");
        }

        try
        {
            if (request.Terms is null)
                year.EnsureCanUpdateSchedule();
            else
            {
                year.EnsureCanConfigureTerms();
                var termValidation = AcademicYearValidator.ValidateConfigureTerms(new ConfigureTermsRequest(request.Terms));
                if (!termValidation.IsValid)
                    return ServiceResult<AcademicYearDetailDto>.Failure("VALIDATION_ERROR", "Dữ liệu học kỳ không hợp lệ.", termValidation.Errors);
                EnsureTermsCanChange(year, request.Terms);
            }
        }
        catch (AcademicCalendarDomainException exception)
        {
            return DomainFailure<AcademicYearDetailDto>(exception);
        }

        var name = request.Name.Trim();
        if (name != year.Name && await uow.AcademicYears.HasConflictExceptCurrentAsync(year.Id, name, cancellationToken))
        {
            return ServiceResult<AcademicYearDetailDto>.Failure(
                "ACADEMIC_YEAR_CONFLICT",
                "Năm học bị trùng tên với năm học khác.");
        }

        if (request.Terms is not null) ApplyTerms(year, request.Terms);
        year.UpdateSchedule(name);
        if (!await uow.AcademicYears.UpdateAsync(year, cancellationToken))
        {
            return ServiceResult<AcademicYearDetailDto>.Failure(
                "ACADEMIC_YEAR_CONFLICT",
                "Xung đột dữ liệu khi cập nhật năm học.");
        }

        return ServiceResult<AcademicYearDetailDto>.Success(year.ToDetailDto());
    }

    public async Task<ServiceResult<AcademicYearDetailDto>> ActivateAsync(
        ulong id,
        CancellationToken cancellationToken)
    {
        var year = await uow.AcademicYears.GetByIdWithSemestersAsync(id, cancellationToken);
        if (year is null)
        {
            return ServiceResult<AcademicYearDetailDto>.Failure(
                "ACADEMIC_YEAR_NOT_FOUND",
                "Không tìm thấy năm học.");
        }

        try
        {
            year.EnsureCanActivate();
        }
        catch (AcademicCalendarDomainException exception)
        {
            return DomainFailure<AcademicYearDetailDto>(exception);
        }

        if (!await uow.AcademicYears.TryActivateAsync(year, cancellationToken))
            return WriteConflict<AcademicYearDetailDto>();

        return ServiceResult<AcademicYearDetailDto>.Success(year.ToDetailDto());
    }

    public async Task<ServiceResult<AcademicYearDetailDto>> CloseAsync(
        ulong id,
        CancellationToken cancellationToken)
    {
        var year = await uow.AcademicYears.GetByIdWithSemestersAsync(id, cancellationToken);
        if (year is null)
        {
            return ServiceResult<AcademicYearDetailDto>.Failure(
                "ACADEMIC_YEAR_NOT_FOUND",
                "Không tìm thấy năm học.");
        }

        try
        {
            year.Close();
        }
        catch (AcademicCalendarDomainException exception)
        {
            return DomainFailure<AcademicYearDetailDto>(exception);
        }

        if (!await uow.AcademicYears.UpdateAsync(year, cancellationToken))
            return WriteConflict<AcademicYearDetailDto>();
        return ServiceResult<AcademicYearDetailDto>.Success(year.ToDetailDto());
    }

    public async Task<ServiceResult<AcademicYearDetailDto>> ConfigureTermsAsync(
        ulong id,
        ConfigureTermsRequest request,
        CancellationToken cancellationToken)
    {
        var year = await uow.AcademicYears.GetByIdWithSemestersAsync(id, cancellationToken);
        if (year is null)
        {
            return ServiceResult<AcademicYearDetailDto>.Failure(
                "ACADEMIC_YEAR_NOT_FOUND",
                "Không tìm thấy năm học.");
        }

        try
        {
            year.EnsureCanConfigureTerms();
        }
        catch (AcademicCalendarDomainException exception)
        {
            return DomainFailure<AcademicYearDetailDto>(exception);
        }

        var validation = AcademicYearValidator.ValidateConfigureTerms(request);
        if (!validation.IsValid)
        {
            return ServiceResult<AcademicYearDetailDto>.Failure(
                "VALIDATION_ERROR",
                "Dữ liệu học kỳ không hợp lệ.",
                validation.Errors);
        }

        try
        {
            EnsureTermsCanChange(year, request.Terms);
            ApplyTerms(year, request.Terms);
        }
        catch (AcademicCalendarDomainException exception)
        {
            return DomainFailure<AcademicYearDetailDto>(exception);
        }

        if (!await uow.AcademicYears.UpdateAsync(year, cancellationToken))
            return WriteConflict<AcademicYearDetailDto>();
        return ServiceResult<AcademicYearDetailDto>.Success(year.ToDetailDto());
    }

    // v2 semesters have no status: closing a single term no longer exists.
    public Task<ServiceResult<SemesterDto>> CloseTermAsync(
        ulong yearId,
        ulong termId,
        CancellationToken cancellationToken) =>
        Task.FromResult(ServiceResult<SemesterDto>.Failure(
            "TERM_STATUS_REMOVED",
            "Học kỳ không còn trạng thái riêng; hãy kết thúc cả năm học."));

    private static void EnsureTermsCanChange(AcademicYear year, IReadOnlyList<ConfigureTermItem> terms)
    {
        foreach (var item in terms)
            year.EnsureCanConfigureTerm(item.Order);
    }

    private static void ApplyTerms(AcademicYear year, IReadOnlyList<ConfigureTermItem> terms)
    {
        foreach (var item in terms)
            year.ConfigureTerm(item.Order, item.Name.Trim());
    }

    private static ServiceResult<T> WriteConflict<T>() => ServiceResult<T>.Failure(
        "CONCURRENCY_CONFLICT", "Dữ liệu đã được thay đổi. Vui lòng tải lại trước khi lưu.");

    private static ServiceResult<T> DomainFailure<T>(AcademicCalendarDomainException exception) =>
        ServiceResult<T>.Failure(exception.Code, exception.Message);
}

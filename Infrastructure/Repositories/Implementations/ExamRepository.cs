using Domain.Entities.Examination;
using Infrastructure.Context;
using Infrastructure.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories.Implement;

public sealed class ExamRepository(ApplicationDbContext context) : IExamRepository
{
    public async Task<(IReadOnlyList<ExamListData> Items, int TotalCount)> ListAsync(
        ExamListFilter filter,
        CancellationToken cancellationToken)
    {
        var query = context.Exams.AsNoTracking().AsQueryable();

        if (filter.Keyword is not null)
        {
            query = query.Where(exam => exam.Name.Contains(filter.Keyword));
        }

        if (filter.SemesterId.HasValue)
        {
            query = query.Where(exam => exam.SemesterId == filter.SemesterId.Value);
        }

        if (filter.SchoolBranchId.HasValue)
        {
            query = query.Where(exam => exam.SchoolBranchId == filter.SchoolBranchId.Value);
        }

        if (filter.AcademicYearId.HasValue)
        {
            query = query.Where(exam => exam.Semester.AcademicYearId == filter.AcademicYearId.Value);
        }

        if (filter.Status is not null)
        {
            query = query.Where(exam => exam.Status == filter.Status);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(exam => exam.EndDate >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(exam => exam.StartDate <= filter.ToDate.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        query = ApplySorting(query, filter.SortBy, filter.SortDirection);

        var items = await query
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(exam => new ExamListData(
                exam.Id,
                exam.Name,
                exam.SemesterId,
                exam.Semester.Name,
                exam.Semester.AcademicYearId,
                exam.Semester.AcademicYear.Name,
                exam.GradeLevelId,
                exam.GradeLevel == null ? null : exam.GradeLevel.Name,
                exam.SubjectId,
                exam.Subject == null ? null : exam.Subject.Name,
                exam.SchoolBranchId,
                exam.SchoolBranch.Code,
                exam.SchoolBranch.Name,
                exam.StartDate,
                exam.StartTime,
                exam.EndDate,
                exam.EndTime,
                exam.Status))
            .ToArrayAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<ExamDetailData?> GetDetailAsync(
        ulong id,
        CancellationToken cancellationToken) =>
        context.Exams
            .AsNoTracking()
            .Where(exam => exam.Id == id)
            .Select(exam => new ExamDetailData(
                exam.Id,
                exam.Name,
                exam.SemesterId,
                exam.Semester.Name,
                exam.Semester.AcademicYearId,
                exam.Semester.AcademicYear.Name,
                exam.GradeLevelId,
                exam.GradeLevel == null ? null : exam.GradeLevel.Name,
                exam.SubjectId,
                exam.Subject == null ? null : exam.Subject.Name,
                exam.SchoolBranchId,
                exam.SchoolBranch.Code,
                exam.SchoolBranch.Name,
                exam.StartDate,
                exam.StartTime,
                exam.EndDate,
                exam.EndTime,
                exam.Status,
                exam.Subjects.Count,
                exam.Subjects.SelectMany(subject => subject.GradeLevels)
                    .SelectMany(gradeLevel => gradeLevel.Sessions).Count(),
                exam.Rooms.Count,
                exam.Subjects.SelectMany(subject => subject.GradeLevels)
                    .SelectMany(gradeLevel => gradeLevel.Registrations).Count(),
                exam.Proctors.Count))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Exam?> GetByIdAsync(ulong id, CancellationToken cancellationToken) =>
        context.Exams.FirstOrDefaultAsync(exam => exam.Id == id, cancellationToken);

    public async Task<Exam?> GetForDeleteAsync(ulong id, CancellationToken cancellationToken)
    {
        var matches = await context.Exams
            .FromSqlInterpolated($"SELECT * FROM exams WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken);
        return matches.SingleOrDefault();
    }

    public async Task<ExamReferenceData> GetReferenceDataAsync(
        ulong semesterId,
        ulong schoolBranchId,
        ulong gradeLevelId,
        ulong subjectId,
        CancellationToken cancellationToken)
    {
        var academicYearId = await context.Semesters.AsNoTracking()
            .Where(semester => semester.Id == semesterId)
            .Select(semester => (ulong?)semester.AcademicYearId)
            .FirstOrDefaultAsync(cancellationToken);
        var semesterExists = academicYearId.HasValue;
        var schoolBranchExists = await context.SchoolBranches.AsNoTracking()
            .AnyAsync(branch => branch.Id == schoolBranchId, cancellationToken);
        var gradeLevelExists = await context.GradeLevels.AsNoTracking()
            .AnyAsync(item => item.Id == gradeLevelId && item.Status == "ACTIVE", cancellationToken);
        var subjectExists = await context.Subjects.AsNoTracking()
            .AnyAsync(item => item.Id == subjectId && item.Status == "ACTIVE", cancellationToken);

        return new ExamReferenceData(semesterExists, schoolBranchExists, gradeLevelExists, subjectExists);
    }

    public async Task<ExamFormOptionsData> GetFormOptionsAsync(CancellationToken cancellationToken)
    {
        var academicYears = await context.AcademicYears.AsNoTracking()
            .OrderByDescending(item => item.Id)
            .Select(item => new ExamAcademicYearOptionData(
                item.Id,
                item.Name,
                item.Status,
                item.Semesters
                    .OrderBy(semester => semester.Order)
                    .Select(semester => new ExamSemesterOptionData(semester.Id, semester.Name))
                    .ToArray()))
            .ToArrayAsync(cancellationToken);
        var gradeLevels = await context.GradeLevels.AsNoTracking()
            .Where(item => item.Status == "ACTIVE")
            .OrderBy(item => item.Name)
            .Select(item => new ExamOptionData(item.Id, item.Name))
            .ToArrayAsync(cancellationToken);
        var subjects = await context.Subjects.AsNoTracking()
            .Where(item => item.Status == "ACTIVE")
            .OrderBy(item => item.Name)
            .Select(item => new ExamOptionData(item.Id, item.Name))
            .ToArrayAsync(cancellationToken);

        return new ExamFormOptionsData(academicYears, gradeLevels, subjects);
    }

    public Task<int> CountCandidatesAsync(
        ulong academicYearId,
        ulong schoolBranchId,
        ulong gradeLevelId,
        CancellationToken cancellationToken) =>
        context.StudentEnrollments.AsNoTracking()
            .CountAsync(enrollment =>
                enrollment.SchoolClass.AcademicYearId == academicYearId &&
                enrollment.EndedOn == null &&
                enrollment.Student.User.Status == Domain.Entities.Identity.StudentStatusCodes.Active &&
                enrollment.SchoolClass.Status == "ACTIVE" &&
                enrollment.SchoolClass.SchoolBranchId == schoolBranchId &&
                enrollment.SchoolClass.GradeLevelId == gradeLevelId,
                cancellationToken);

    public async Task<bool> HasDependentDataAsync(
        ulong id,
        CancellationToken cancellationToken) =>
        await context.ExamSubjects.AsNoTracking().AnyAsync(item => item.ExamId == id, cancellationToken) ||
        await context.ExamRooms.AsNoTracking().AnyAsync(item => item.ExamId == id, cancellationToken) ||
        await context.ExamProctors.AsNoTracking().AnyAsync(item => item.ExamId == id, cancellationToken);

    public Task AddAsync(Exam exam, CancellationToken cancellationToken) =>
        context.Exams.AddAsync(exam, cancellationToken).AsTask();

    public void Delete(Exam exam) => context.Exams.Remove(exam);

    private static IQueryable<Exam> ApplySorting(
        IQueryable<Exam> query,
        string sortBy,
        string sortDirection)
    {
        var descending = sortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase);
        return (sortBy.ToLowerInvariant(), descending) switch
        {
            ("name", false) => query.OrderBy(exam => exam.Name).ThenBy(exam => exam.Id),
            ("name", true) => query.OrderByDescending(exam => exam.Name).ThenByDescending(exam => exam.Id),
            ("enddate", false) => query.OrderBy(exam => exam.EndDate).ThenBy(exam => exam.Id),
            ("enddate", true) => query.OrderByDescending(exam => exam.EndDate).ThenByDescending(exam => exam.Id),
            ("status", false) => query.OrderBy(exam => exam.Status).ThenBy(exam => exam.Id),
            ("status", true) => query.OrderByDescending(exam => exam.Status).ThenByDescending(exam => exam.Id),
            ("semester", false) => query.OrderBy(exam => exam.Semester.Name).ThenBy(exam => exam.Id),
            ("semester", true) => query.OrderByDescending(exam => exam.Semester.Name).ThenByDescending(exam => exam.Id),
            ("schoolbranch", false) => query.OrderBy(exam => exam.SchoolBranch.Name).ThenBy(exam => exam.Id),
            ("schoolbranch", true) => query.OrderByDescending(exam => exam.SchoolBranch.Name).ThenByDescending(exam => exam.Id),
            ("startdate", false) => query.OrderBy(exam => exam.StartDate).ThenBy(exam => exam.Id),
            _ => query.OrderByDescending(exam => exam.StartDate).ThenByDescending(exam => exam.Id)
        };
    }
}

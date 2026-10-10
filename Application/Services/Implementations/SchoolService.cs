using Application.Common;
using Application.DTOs;
using Application.Services.Interface;
using Domain.Entities.Organization;
using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Application.Validators;

namespace Application.Services.Implement;

public sealed class SchoolService(ApplicationDbContext db) : ISchoolService
{
    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static SchoolListItem ToListItem(School school) => new(
        school.Id,
        school.Code,
        school.Name,
        school.Status,
        school.Branches.Count);

    private static SchoolDetailDto ToDetail(School school) => new(
        school.Id,
        school.Code,
        school.Name,
        school.Status,
        school.ProvinceCode,
        school.Branches
            .Select(b => new SchoolBranchItem(b.Id, b.Code, b.Name, b.Address, b.Status))
            .ToArray());

    // ─── List ─────────────────────────────────────────────────────────────────

    public async Task<SchoolListPage> ListAsync(string? search, CancellationToken cancellationToken)
    {
        var query = db.Schools
            .AsNoTracking()
            .Include(s => s.Branches)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(s =>
                s.Name.Contains(term) ||
                s.Code.Contains(term));
        }

        var schools = await query
            .OrderBy(s => s.Name)
            .ToArrayAsync(cancellationToken);

        var items = schools.Select(ToListItem).ToArray();
        return new SchoolListPage(items, items.Length);
    }

    // ─── Get by ID ────────────────────────────────────────────────────────────

    public async Task<ServiceResult<SchoolDetailDto>> GetByIdAsync(ulong id, CancellationToken cancellationToken)
    {
        var school = await db.Schools
            .Include(s => s.Branches)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (school is null)
            return ServiceResult<SchoolDetailDto>.Failure("SCHOOL_NOT_FOUND", "Không tìm thấy trường.");

        return ServiceResult<SchoolDetailDto>.Success(ToDetail(school));
    }

    // ─── Create ───────────────────────────────────────────────────────────────

    public async Task<ServiceResult<SchoolListItem>> CreateAsync(
        CreateSchoolRequest request,
        CancellationToken cancellationToken)
    {
        var isCodeConflict = !string.IsNullOrWhiteSpace(request.Code) && 
                             await db.Schools.AnyAsync(s => s.Code == request.Code.Trim().ToUpperInvariant(), cancellationToken);

        var validationResult = SchoolValidator.ValidateCreate(request, isCodeConflict);
        if (validationResult != null)
            return validationResult;

        var code = request.Code.Trim().ToUpperInvariant();

        var school = new School
        {
            Code = code,
            Name = request.Name.Trim(),
            ProvinceCode = request.ProvinceCode?.Trim(),
            Status = string.IsNullOrWhiteSpace(request.Status) ? "ACTIVE" : request.Status.Trim(),
        };

        db.Schools.Add(school);
        await db.SaveChangesAsync(cancellationToken);

        // Reload with Branches (empty at this point) for accurate count
        await db.Entry(school).Collection(s => s.Branches).LoadAsync(cancellationToken);
        return ServiceResult<SchoolListItem>.Success(ToListItem(school));
    }

    // ─── Update ───────────────────────────────────────────────────────────────

    public async Task<ServiceResult<SchoolListItem>> UpdateAsync(
        ulong id,
        UpdateSchoolRequest request,
        CancellationToken cancellationToken)
    {
        var school = await db.Schools
            .Include(s => s.Branches)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (school is null)
            return ServiceResult<SchoolListItem>.Failure("SCHOOL_NOT_FOUND", "Không tìm thấy trường.");

        var validationResult = SchoolValidator.ValidateUpdate(request);
        if (validationResult != null)
            return validationResult;

        school.Name = request.Name.Trim();
        school.ProvinceCode = request.ProvinceCode?.Trim();
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            school.Status = request.Status.Trim();
        }

        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<SchoolListItem>.Success(ToListItem(school));
    }
}

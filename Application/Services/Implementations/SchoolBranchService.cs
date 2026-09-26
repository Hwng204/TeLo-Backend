using Application.Common;
using Application.DTOs;
using Application.Services.Interface;
using Domain.Entities.Organization;
using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Implement;

public sealed class SchoolBranchService(ApplicationDbContext db) : ISchoolBranchService
{
    private static SchoolBranchDetailDto ToDetailDto(SchoolBranch branch) => new(
        branch.Id,
        branch.SchoolId,
        branch.Code,
        branch.Name,
        branch.Address,
        branch.Status);

    public async Task<ServiceResult<SchoolBranchDetailDto>> CreateAsync(
        ulong schoolId,
        CreateSchoolBranchRequest request,
        CancellationToken cancellationToken)
    {
        var school = await db.Schools.FirstOrDefaultAsync(s => s.Id == schoolId, cancellationToken);
        if (school is null)
            return ServiceResult<SchoolBranchDetailDto>.Failure("SCHOOL_NOT_FOUND", "Không tìm thấy trường học.");

        var existingCount = await db.SchoolBranches.CountAsync(b => b.SchoolId == schoolId, cancellationToken);
        var suffix = (char)('A' + existingCount);
        var code = $"{school.Code}-{suffix}";

        var branch = new SchoolBranch
        {
            SchoolId = schoolId,
            Code = code,
            Name = request.Name.Trim(),
            Address = request.Address?.Trim(),
            Status = string.IsNullOrWhiteSpace(request.Status) ? "ACTIVE" : request.Status.Trim()
        };

        db.SchoolBranches.Add(branch);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<SchoolBranchDetailDto>.Success(ToDetailDto(branch));
    }

    public async Task<ServiceResult<SchoolBranchDetailDto>> UpdateAsync(
        ulong id,
        UpdateSchoolBranchRequest request,
        CancellationToken cancellationToken)
    {
        var branch = await db.SchoolBranches.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (branch is null)
            return ServiceResult<SchoolBranchDetailDto>.Failure("BRANCH_NOT_FOUND", "Không tìm thấy cơ sở.");

        branch.Name = request.Name.Trim();
        branch.Address = request.Address?.Trim();
        
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            branch.Status = request.Status.Trim();
        }

        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<SchoolBranchDetailDto>.Success(ToDetailDto(branch));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(ulong id, CancellationToken cancellationToken)
    {
        var branch = await db.SchoolBranches.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (branch is null)
            return ServiceResult<bool>.Failure("BRANCH_NOT_FOUND", "Không tìm thấy cơ sở.");

        db.SchoolBranches.Remove(branch);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Success(true);
    }
}

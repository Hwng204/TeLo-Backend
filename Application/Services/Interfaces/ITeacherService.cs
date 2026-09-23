using Application.DTOs;
using Infrastructure.Repositories.Interface;

namespace Application.Services.Interface;

public interface ITeacherService
{
    Task<TeacherPage<TeacherListItem>> ListAsync(TeacherScope scope, TeacherListQuery query, CancellationToken ct);
    Task<TeacherDetailDto> GetAsync(TeacherScope scope, ulong id, CancellationToken ct);
    Task<TeacherReferenceData> ReferencesAsync(TeacherScope scope, CancellationToken ct);
    Task<TeacherPage<TeacherOption>> SchoolsAsync(ulong actorId, TeacherSelectionQuery query, CancellationToken ct);
    Task<TeacherPage<TeacherOption>> BranchesAsync(ulong actorId, ulong schoolId, TeacherSelectionQuery query, CancellationToken ct);
    Task<TeacherDetailDto> CreateAsync(TeacherScope scope, CreateTeacherRequest request, CancellationToken ct);
    Task<TeacherDetailDto> UpdateAsync(TeacherScope scope, ulong id, UpdateTeacherRequest request, CancellationToken ct);
    Task<TeacherDetailDto> DeleteAsync(TeacherScope scope, ulong id, uint version, CancellationToken ct);
    Task<TeacherAccountDto> AccountAsync(TeacherScope scope, ulong id, CancellationToken ct);
    Task<TeacherAccountDto> UpdateAccountAsync(TeacherScope scope, ulong id, UpdateTeacherAccountRequest request, CancellationToken ct);
    Task<TeacherAccountDto> ResetPasswordAsync(TeacherScope scope, ulong id, ResetTeacherPasswordRequest request, CancellationToken ct);
}

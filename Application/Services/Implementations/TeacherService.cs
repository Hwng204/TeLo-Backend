using Application.Common;
using Application.DTOs;
using Application.Mappings;
using Application.Services.Interface;
using Domain.Entities.Identity;
using Infrastructure.Repositories.Interface;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Application.Services.Implement;

public sealed class TeacherService(
    ITeacherRepository repository, IPasswordHasher<User> passwordHasher,
    TimeProvider timeProvider, ILogger<TeacherService> logger) : ITeacherService
{
    public async Task<TeacherPage<TeacherListItem>> ListAsync(TeacherScope scope, TeacherListQuery query, CancellationToken ct)
    {
        var filter = TeacherValidator.Filter(query);
        var resolved = await repository.ResolveScopeAsync(scope, false, ct);
        var (items, total) = await repository.ListAsync(resolved, filter, ct);
        return new(items.Select(x => x.ToListItem()).ToArray(), query.Page, query.PageSize, total);
    }

    public async Task<TeacherDetailDto> GetAsync(TeacherScope scope, ulong id, CancellationToken ct) =>
        (await repository.GetAsync(await repository.ResolveScopeAsync(scope, false, ct), id, false, ct)).ToDetail();

    public async Task<TeacherReferenceData> ReferencesAsync(TeacherScope scope, CancellationToken ct)
    {
        var rows = await repository.ReferencesAsync(await repository.ResolveScopeAsync(scope, false, ct), ct);
        return new(rows.Subjects.Select(x => x.ToOption()).ToArray(), rows.Departments,
            rows.Branches.Select(x => x.ToOption()).ToArray(), TeacherValidator.EmploymentStatuses, TeacherValidator.AccountStatuses);
    }

    public async Task<TeacherPage<TeacherOption>> SchoolsAsync(ulong actorId, TeacherSelectionQuery query, CancellationToken ct)
    {
        TeacherValidator.Page(query.Page, query.PageSize);
        var (items, total) = await repository.SchoolsAsync(actorId,
            TeacherValidator.Optional(query.Search, 200, "Từ khóa"), query.Page, query.PageSize, ct);
        return new(items.Select(x => x.ToOption()).ToArray(), query.Page, query.PageSize, total);
    }

    public async Task<TeacherPage<TeacherOption>> BranchesAsync(ulong actorId, ulong schoolId, TeacherSelectionQuery query, CancellationToken ct)
    {
        TeacherValidator.Page(query.Page, query.PageSize);
        var (items, total) = await repository.BranchesAsync(actorId, schoolId,
            TeacherValidator.Optional(query.Search, 200, "Từ khóa"), query.Page, query.PageSize, ct);
        return new(items.Select(x => x.ToOption()).ToArray(), query.Page, query.PageSize, total);
    }

    public async Task<TeacherDetailDto> CreateAsync(TeacherScope scope, CreateTeacherRequest request, CancellationToken ct)
    {
        var resolved = await repository.ResolveScopeAsync(scope, true, ct);
        EnsureActiveUnit(resolved);
        var profile = ValidateProfile(request.Profile);
        var username = TeacherValidator.Username(request.Username);
        var email = TeacherValidator.Email(request.Email);
        TeacherValidator.Password(request.Password);
        await repository.ValidateReferencesAsync(profile, null, ct);
        await repository.ValidateUniqueAsync(null, null, profile.StaffCode, username, email, ct);
        var role = await repository.GetTeacherRoleAsync(ct);
        var user = new User
        {
            Username = username, Email = email, FullName = profile.FullName,
            SchoolBranchId = resolved.BranchId, CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
            Status = profile.EmploymentStatus is "RESIGNED" or "INACTIVE" ? "INACTIVE" : "ACTIVE"
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        user.UserRoles.Add(new UserRole { User = user, Role = role, RoleId = role.Id });
        var teacher = new Teacher { User = user };
        teacher.ApplyProfile(profile);
        repository.Add(teacher);
        await repository.SaveAsync(ct);
        Audit("Create", scope, teacher.Id);
        return (await repository.GetAsync(resolved, teacher.Id, false, ct)).ToDetail();
    }

    public async Task<TeacherDetailDto> UpdateAsync(TeacherScope scope, ulong id, UpdateTeacherRequest request, CancellationToken ct)
    {
        var resolved = await repository.ResolveScopeAsync(scope, true, ct);
        var teacher = await MutableAsync(resolved, id, request.Version, ct);
        var profile = ValidateProfile(request.Profile);
        await repository.ValidateReferencesAsync(profile, teacher.MainSubjectId, ct);
        await repository.ValidateUniqueAsync(id, teacher.UserId, profile.StaffCode, teacher.User.Username, teacher.User.Email, ct);
        teacher.ApplyProfile(profile);
        if (profile.EmploymentStatus is "RESIGNED" or "INACTIVE" && teacher.User.Status != "INACTIVE")
        {
            teacher.User.Status = "INACTIVE";
            teacher.User.SecurityVersion++;
        }
        teacher.Version++;
        await repository.SaveAsync(ct);
        Audit("UpdateProfile", scope, id);
        return (await repository.GetAsync(resolved, id, false, ct)).ToDetail();
    }

    public async Task<TeacherDetailDto> DeleteAsync(TeacherScope scope, ulong id, uint version, CancellationToken ct)
    {
        var resolved = await repository.ResolveScopeAsync(scope, true, ct);
        var teacher = await MutableAsync(resolved, id, version, ct);
        teacher.EmploymentStatus = "INACTIVE";
        teacher.User.Status = "INACTIVE";
        teacher.Version++;
        teacher.User.SecurityVersion++;
        await repository.SaveAsync(ct);
        Audit("Deactivate", scope, id);
        return teacher.ToDetail();
    }

    public async Task<TeacherAccountDto> AccountAsync(TeacherScope scope, ulong id, CancellationToken ct)
    {
        var resolved = await repository.ResolveScopeAsync(scope, false, ct);
        if (!resolved.IsAdmin) throw new TeacherManagementException("FORBIDDEN", "Chỉ admin được xem tài khoản đăng nhập.");
        return (await repository.GetAsync(resolved, id, false, ct)).ToAccount();
    }

    public async Task<TeacherAccountDto> UpdateAccountAsync(TeacherScope scope, ulong id, UpdateTeacherAccountRequest request, CancellationToken ct)
    {
        var resolved = await repository.ResolveScopeAsync(scope, true, ct);
        var teacher = await MutableAsync(resolved, id, request.Version, ct);
        var username = TeacherValidator.Username(request.Username);
        var email = TeacherValidator.Email(request.Email);
        var status = request.Status?.Trim().ToUpperInvariant();
        if (status is null || !TeacherValidator.AccountStatuses.Contains(status))
            throw new TeacherManagementException("VALIDATION_ERROR", "Trạng thái tài khoản không hợp lệ.");
        if (status == "ACTIVE") EnsureActiveUnit(resolved);
        if (status == "ACTIVE" && teacher.EmploymentStatus is "RESIGNED" or "INACTIVE")
            throw new TeacherManagementException("EMPLOYMENT_INACTIVE", "Cần cập nhật trạng thái công tác trước khi mở tài khoản.");
        await repository.ValidateUniqueAsync(id, teacher.UserId, teacher.StaffCode, username, email, ct);
        teacher.User.Username = username;
        teacher.User.Email = email;
        teacher.User.Status = status;
        teacher.User.SecurityVersion++;
        teacher.Version++;
        await repository.SaveAsync(ct);
        Audit("UpdateAccount", scope, id);
        return teacher.ToAccount();
    }

    public async Task<TeacherAccountDto> ResetPasswordAsync(TeacherScope scope, ulong id, ResetTeacherPasswordRequest request, CancellationToken ct)
    {
        var resolved = await repository.ResolveScopeAsync(scope, true, ct);
        var teacher = await MutableAsync(resolved, id, request.Version, ct);
        TeacherValidator.Password(request.NewPassword);
        teacher.User.PasswordHash = passwordHasher.HashPassword(teacher.User, request.NewPassword);
        teacher.User.SecurityVersion++;
        teacher.Version++;
        await repository.SaveAsync(ct);
        Audit("ResetPassword", scope, id);
        return teacher.ToAccount();
    }

    private TeacherProfileValues ValidateProfile(TeacherProfileRequest profile) =>
        TeacherValidator.Profile(profile, DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime));

    private static void EnsureActiveUnit(TeacherResolvedScope scope)
    {
        if (!scope.IsUnitActive)
            throw new TeacherManagementException("UNIT_INACTIVE", "Trường hoặc phân hiệu đã ngừng hoạt động; không thể tạo mới hoặc mở tài khoản.");
    }

    private async Task<Teacher> MutableAsync(TeacherResolvedScope scope, ulong id, uint version, CancellationToken ct)
    {
        if (version == 0) throw new TeacherManagementException("VALIDATION_ERROR", "version là bắt buộc và phải lớn hơn 0.");
        var teacher = await repository.GetAsync(scope, id, true, ct);
        repository.EnsureManageable(teacher);
        if (teacher.Version != version)
            throw new TeacherManagementException("CONCURRENCY_CONFLICT", "Dữ liệu đã thay đổi. Vui lòng tải lại.");
        return teacher;
    }

    private void Audit(string action, TeacherScope scope, ulong id) => logger.LogInformation(
        "Teacher operation {Action} by {ActorId}, school {SchoolId}, branch {BranchId}, teacher {TeacherId}",
        action, scope.ActorUserId, scope.SchoolId, scope.BranchId, id);
}

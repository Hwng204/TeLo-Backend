using Application.DTOs;
using Domain.Entities.Identity;
using Infrastructure.Repositories.Interface;

namespace Application.Mappings;

public static class TeacherMappingExtensions
{
    public static TeacherListItem ToListItem(this TeacherListRow row) => new(
        row.Id, row.StaffCode, row.FullName, row.Department, row.MainSubjectId,
        row.MainSubjectName, row.Gender, row.JoinedOn, row.EmploymentStatus,
        row.AccountStatus, row.SchoolId, row.SchoolBranchId, row.SchoolBranchName, row.Version);

    public static TeacherListItem ToListItem(this Teacher teacher) => new(
        teacher.Id, teacher.StaffCode, teacher.User.FullName, teacher.Department,
        teacher.MainSubjectId, teacher.MainSubject?.Name, teacher.Gender,
        teacher.JoinedOn, teacher.EmploymentStatus, teacher.User.Status,
        teacher.User.SchoolBranch!.SchoolId, teacher.User.SchoolBranchId!.Value,
        teacher.User.SchoolBranch.Name, teacher.Version);

    public static TeacherDetailDto ToDetail(this Teacher teacher) => new(
        teacher.ToListItem(), teacher.Specialization, teacher.Position, teacher.Phone,
        teacher.Email, teacher.DateOfBirth, teacher.ClassId, teacher.SchoolClass?.Name,
        teacher.User.UserRoles.Select(x => x.Role.Code).OrderBy(x => x).ToArray());

    public static TeacherAccountDto ToAccount(this Teacher teacher) => new(
        teacher.Id, teacher.UserId, teacher.User.Username, teacher.User.Email,
        teacher.User.Status, teacher.User.UserRoles.Select(x => x.Role.Code).OrderBy(x => x).ToArray(),
        teacher.User.CreatedAt, teacher.Version);

    public static TeacherOption ToOption(this TeacherOptionRow row) => new(row.Id, row.Code, row.Name, row.Status);

    public static void ApplyProfile(this Teacher teacher, TeacherProfileValues profile)
    {
        teacher.StaffCode = profile.StaffCode;
        teacher.User.FullName = profile.FullName;
        teacher.Department = profile.Department;
        teacher.MainSubjectId = profile.MainSubjectId;
        teacher.Specialization = profile.Specialization;
        teacher.Position = profile.Position;
        teacher.Gender = profile.Gender;
        teacher.Phone = profile.Phone;
        teacher.Email = profile.WorkEmail;
        teacher.DateOfBirth = profile.DateOfBirth;
        teacher.JoinedOn = profile.JoinedOn;
        teacher.EmploymentStatus = profile.EmploymentStatus;
    }
}

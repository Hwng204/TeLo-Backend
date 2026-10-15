using Application.DTOs;
using Domain.Entities.Identity;
using Infrastructure.Repositories.Interface;

namespace Application.Mappings;

public static class TeacherMappingExtensions
{
    public static TeacherListItem ToListItem(this TeacherListRow row) => new(
        row.Id, row.StaffCode, row.FullName, row.Gender, row.EmploymentStatus,
        row.AccountStatus, row.SchoolId, row.SchoolBranchId, row.SchoolBranchName);

    public static TeacherListItem ToListItem(this Teacher teacher) => new(
        teacher.Id, teacher.User.MoetIdentifier, teacher.User.FullName, teacher.Gender,
        EmploymentStatus(teacher.User.Status), teacher.User.Status,
        teacher.User.SchoolBranch!.SchoolId, teacher.User.SchoolBranchId!.Value,
        teacher.User.SchoolBranch.Name);

    public static TeacherDetailDto ToDetail(this Teacher teacher) => new(
        teacher.ToListItem(), teacher.Specialization, teacher.Phone,
        // No homeroom in DB v2: homeroomClassId/Name stay in the API as null.
        teacher.Email, teacher.DateOfBirth, null, null,
        teacher.User.UserRoles.Select(x => x.Role.Code).OrderBy(x => x).ToArray());

    public static TeacherAccountDto ToAccount(this Teacher teacher) => new(
        teacher.Id, teacher.UserId, teacher.User.Username, teacher.Email ?? "",
        teacher.User.Status, teacher.User.UserRoles.Select(x => x.Role.Code).OrderBy(x => x).ToArray(),
        teacher.User.CreatedAt);

    public static TeacherOption ToOption(this TeacherOptionRow row) => new(row.Id, row.Code, row.Name, row.Status);

    // Employment status has no column: it is derived from the account status.
    public static string EmploymentStatus(string accountStatus) => accountStatus == "ACTIVE" ? "WORKING" : "INACTIVE";

    public static void ApplyProfile(this Teacher teacher, TeacherProfileValues profile)
    {
        teacher.User.MoetIdentifier = profile.StaffCode;
        teacher.User.FullName = profile.FullName;
        teacher.Specialization = profile.Specialization;
        teacher.Gender = profile.Gender;
        teacher.Phone = profile.Phone;
        // workEmail and the account email share teacher.email; an omitted workEmail keeps the stored address.
        teacher.Email = profile.WorkEmail ?? teacher.Email;
        teacher.DateOfBirth = profile.DateOfBirth;
    }
}

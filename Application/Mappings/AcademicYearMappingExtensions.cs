using Application.DTOs;
using Domain.Entities.Academic;
using Infrastructure.Repositories.Interface;

namespace Application.Mappings;

public static class AcademicYearMappingExtensions
{
    public static AcademicYearListFilter ToFilter(this AcademicYearListQuery query) =>
        new(
            query.Status,
            query.Search,
            query.Page,
            query.PageSize);

    public static AcademicYearDetailDto ToDetailDto(this AcademicYear academicYear) =>
        new(
            academicYear.Id,
            academicYear.Code ?? string.Empty,
            academicYear.Name,
            academicYear.StartDate,
            academicYear.EndDate,
            academicYear.Status,
            academicYear.Version,
            academicYear.Semesters
                .OrderBy(semester => semester.Order)
                .Select(semester => semester.ToDto())
                .ToArray());

    public static SemesterDto ToDto(this Semester semester) =>
        new(
            semester.Id,
            semester.Order,
            semester.Name,
            semester.StartDate,
            semester.EndDate,
            semester.Status,
            semester.Version);

    public static AcademicYearListItem ToListItem(this AcademicYear academicYear) =>
        new(
            academicYear.Id,
            academicYear.Code ?? string.Empty,
            academicYear.Name,
            academicYear.StartDate,
            academicYear.EndDate,
            academicYear.Status,
            academicYear.Version,
            academicYear.Semesters.Count);
}

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

    // `code` has no column in v2; it was always assigned from the name.
    public static AcademicYearDetailDto ToDetailDto(this AcademicYear academicYear) =>
        new(
            academicYear.Id,
            academicYear.Name,
            academicYear.Name,
            academicYear.Status,
            academicYear.Semesters
                .OrderBy(semester => semester.Order)
                .Select(semester => semester.ToDto())
                .ToArray());

    public static SemesterDto ToDto(this Semester semester) =>
        new(
            semester.Id,
            semester.Order,
            semester.Name);

    public static AcademicYearListItem ToListItem(this AcademicYear academicYear) =>
        new(
            academicYear.Id,
            academicYear.Name,
            academicYear.Name,
            academicYear.Status,
            academicYear.Semesters.Count);
}

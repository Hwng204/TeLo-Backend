using Infrastructure.Repositories.Interface;

namespace Application.Tests.SchoolDirectory;

internal sealed class FakeSchoolDirectoryRepository : ISchoolDirectoryRepository
{
    public DirectoryReadStatus Status { get; init; } = DirectoryReadStatus.Success;

    public IReadOnlyList<StudentEnrollmentRow> History { get; init; } = [];

    public IReadOnlyList<StudentScoreRow> Scores { get; init; } = [];

    public object? LastFilter { get; private set; }

    public (ulong StudentId, ulong ClassId, int Page, int PageSize)? LastScoreQuery { get; private set; }

    public Task<DirectoryReadResult<DirectoryRowsPage<StudentDirectoryRow>>> ListStudentsAsync(
        StudentDirectoryFilter filter, CancellationToken cancellationToken)
    {
        LastFilter = filter;
        return Task.FromResult(Status is DirectoryReadStatus.Success
            ? DirectoryReadResult<DirectoryRowsPage<StudentDirectoryRow>>.Ok(
                new DirectoryRowsPage<StudentDirectoryRow>([], 0))
            : DirectoryReadResult<DirectoryRowsPage<StudentDirectoryRow>>.Fail(Status));
    }

    public Task<DirectoryReadResult<StudentDetailRow>> GetStudentAsync(
        DirectoryScope scope, ulong studentId, CancellationToken cancellationToken) =>
        Task.FromResult(Status is DirectoryReadStatus.Success
            ? DirectoryReadResult<StudentDetailRow>.Ok(new StudentDetailRow(
                studentId, "HS1", "An", null, null, new DateOnly(2024, 9, 1), "ACTIVE", History))
            : DirectoryReadResult<StudentDetailRow>.Fail(Status));

    public Task<DirectoryReadResult<DirectoryRowsPage<StudentScoreRow>>> ListStudentScoresAsync(
        DirectoryScope scope, ulong studentId, ulong classId, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        LastScoreQuery = (studentId, classId, page, pageSize);
        return Task.FromResult(Status is DirectoryReadStatus.Success
            ? DirectoryReadResult<DirectoryRowsPage<StudentScoreRow>>.Ok(
                new DirectoryRowsPage<StudentScoreRow>(Scores, Scores.Count))
            : DirectoryReadResult<DirectoryRowsPage<StudentScoreRow>>.Fail(Status));
    }

    public Task<DirectoryReadResult<DirectoryRowsPage<ClassDirectoryRow>>> ListClassesAsync(
        ClassDirectoryFilter filter, CancellationToken cancellationToken)
    {
        LastFilter = filter;
        return Task.FromResult(Status is DirectoryReadStatus.Success
            ? DirectoryReadResult<DirectoryRowsPage<ClassDirectoryRow>>.Ok(
                new DirectoryRowsPage<ClassDirectoryRow>([], 0))
            : DirectoryReadResult<DirectoryRowsPage<ClassDirectoryRow>>.Fail(Status));
    }

    public Task<DirectoryReadResult<ClassDetailRow>> GetClassAsync(
        DirectoryScope scope, ulong classId, int rosterPage, int rosterPageSize,
        CancellationToken cancellationToken) =>
        Task.FromResult(Status is DirectoryReadStatus.Success
            ? DirectoryReadResult<ClassDetailRow>.Ok(new ClassDetailRow(
                new ClassDirectoryRow(
                    classId, "6A", "6A", 1, "Khối 6", 1, "2025-2026", 1, "Cơ sở 1",
                    null, null, 0, "ACTIVE"),
                new DirectoryRowsPage<ClassStudentRow>([], 0)))
            : DirectoryReadResult<ClassDetailRow>.Fail(Status));

    public Task<DirectoryReadResult<DirectoryReferenceRows>> GetReferenceDataAsync(
        DirectoryScope scope, ulong? academicYearId, CancellationToken cancellationToken) =>
        Task.FromResult(Status is DirectoryReadStatus.Success
            ? DirectoryReadResult<DirectoryReferenceRows>.Ok(
                new DirectoryReferenceRows([], [], [], []))
            : DirectoryReadResult<DirectoryReferenceRows>.Fail(Status));
}

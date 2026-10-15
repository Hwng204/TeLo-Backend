namespace Infrastructure.Repositories.Interface;

public interface IStudentRepository
{
    // Read methods
    Task<DirectoryReadResult<DirectoryRowsPage<StudentDirectoryRow>>> ListStudentsAsync(
        StudentDirectoryFilter filter,
        CancellationToken cancellationToken);

    Task<DirectoryReadResult<StudentDetailRow>> GetStudentAsync(
        DirectoryScope scope,
        ulong studentId,
        CancellationToken cancellationToken);

    Task<DirectoryReadResult<DirectoryRowsPage<StudentScoreRow>>> ListStudentScoresAsync(
        DirectoryScope scope,
        ulong studentId,
        ulong classId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    // Write methods
    Task<DirectoryWriteResult<ulong>> CreateStudentAsync(
        CreateStudentCommand command,
        CancellationToken cancellationToken);

    Task<string> SuggestStudentCodeAsync(
        ulong schoolId,
        ulong classId,
        CancellationToken cancellationToken);

    Task<DirectoryWriteResult<ulong>> UpdateStudentAsync(
        UpdateStudentCommand command,
        CancellationToken cancellationToken);

    Task<DirectoryWriteResult<ulong>> TransferStudentClassAsync(
        TransferStudentClassCommand command,
        CancellationToken cancellationToken);

    Task<DirectoryWriteResult<ulong>> DeactivateStudentAsync(
        ulong schoolId,
        ulong studentId,
        CancellationToken cancellationToken);
}

namespace Infrastructure.Repositories.Interface;

public interface IClassRepository
{
    // Read methods
    Task<DirectoryReadResult<DirectoryRowsPage<ClassDirectoryRow>>> ListClassesAsync(
        ClassDirectoryFilter filter,
        CancellationToken cancellationToken);

    Task<DirectoryReadResult<ClassDetailRow>> GetClassAsync(
        DirectoryScope scope,
        ulong classId,
        int rosterPage,
        int rosterPageSize,
        CancellationToken cancellationToken);

    // Write methods
    Task<DirectoryWriteResult<ulong>> CreateClassAsync(
        CreateClassCommand command,
        CancellationToken cancellationToken);

    Task<DirectoryWriteResult<ulong>> UpdateClassAsync(
        UpdateClassCommand command,
        CancellationToken cancellationToken);

    Task<DirectoryWriteResult<ulong>> DeactivateClassAsync(
        ulong schoolId,
        ulong classId,
        CancellationToken cancellationToken);
}

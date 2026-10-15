namespace Infrastructure.Repositories.Interface;

public interface ISchoolRepository
{
    Task<DirectoryReadResult<DirectoryReferenceRows>> GetReferenceDataAsync(
        DirectoryScope scope,
        ulong? academicYearId,
        CancellationToken cancellationToken);
}

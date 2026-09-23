using Application.Common;
using Application.DTOs;

namespace Application.Services.Interface;

// Who is calling. School-side callers have no school of their own choosing: it is resolved from
// their branch. Admin callers either name a school (admin routes) or, for the cross-school inbox,
// none.
public sealed record ImportCaller(ulong ActorUserId, ulong? SchoolId, bool IsAdmin)
{
    public static ImportCaller School(ulong actorUserId) => new(actorUserId, null, false);

    public static ImportCaller Admin(ulong actorUserId, ulong? schoolId) =>
        new(actorUserId, schoolId, true);
}

// Bulk student import from Excel. Two steps: Preview stores the file with a per-row verdict, and
// nothing reaches the student tables until an admin applies the batch (directly, or after a school
// submitted it).
public interface IStudentImportService
{
    Task<ServiceResult<StudentImportTemplateInfo>> GetTemplateInfoAsync(
        ImportCaller caller,
        ulong? academicYearId,
        CancellationToken cancellationToken);

    Task<ServiceResult<StudentImportFile>> BuildTemplateAsync(
        ImportCaller caller,
        ulong? academicYearId,
        CancellationToken cancellationToken);

    Task<ServiceResult<StudentImportBatchDetailDto>> PreviewAsync(
        ImportCaller caller,
        ulong? academicYearId,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken);

    Task<ServiceResult<DirectoryPage<StudentImportBatchDto>>> ListAsync(
        ImportCaller caller,
        StudentImportListQuery query,
        CancellationToken cancellationToken);

    Task<ServiceResult<StudentImportBatchDetailDto>> GetAsync(
        ImportCaller caller,
        ulong batchId,
        StudentImportRowsQuery rows,
        CancellationToken cancellationToken);

    Task<ServiceResult<StudentImportFile>> GetFileAsync(
        ImportCaller caller,
        ulong batchId,
        CancellationToken cancellationToken);

    Task<ServiceResult<StudentImportBatchDetailDto>> SubmitAsync(
        ImportCaller caller,
        ulong batchId,
        CancellationToken cancellationToken);

    Task<ServiceResult<StudentImportBatchDetailDto>> CancelAsync(
        ImportCaller caller,
        ulong batchId,
        CancellationToken cancellationToken);

    Task<ServiceResult<StudentImportBatchDetailDto>> ApplyAsync(
        ImportCaller caller,
        ulong batchId,
        CancellationToken cancellationToken);

    Task<ServiceResult<StudentImportBatchDetailDto>> RejectAsync(
        ImportCaller caller,
        ulong batchId,
        RejectStudentImportRequest request,
        CancellationToken cancellationToken);
}

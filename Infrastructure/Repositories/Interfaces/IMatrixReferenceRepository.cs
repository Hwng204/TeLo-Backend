using Domain.Entities.QuestionBank;
using Infrastructure.Models;

namespace Infrastructure.Repositories.Interface;

// Reads the reference data (scopes, semesters, lessons, team leads) and validates matrix scope.
public interface IMatrixReferenceRepository
{
    Task EnsureValidAsync(
        MatrixScope scope,
        IReadOnlyCollection<ulong> lessonIds,
        CancellationToken cancellationToken,
        ulong? requiredBranchId = null);

    Task<MatrixExportInfo> GetExportInfoAsync(
        MatrixScope scope,
        IReadOnlyCollection<ulong> lessonIds,
        CancellationToken cancellationToken);

    Task EnsureAssignmentValidAsync(
        MatrixActor actor,
        ulong assignedToUserId,
        MatrixScope scope,
        CancellationToken cancellationToken);

    // academicContextId is a MatrixScope.Key.
    Task<MatrixReferenceModel> GetReferenceDataAsync(
        MatrixActor actor,
        ulong? academicContextId,
        CancellationToken cancellationToken);
}

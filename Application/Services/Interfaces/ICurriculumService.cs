using Application.Common;
using Application.DTOs;
using Infrastructure.Repositories.Interface;

namespace Application.Services.Interface;

// Chapters and lessons of the caller's own school branch. Reading is open to the vice principal,
// team leads and teachers; every change is limited to the vice principal by the controller policy.
public interface ICurriculumService
{
    Task<ServiceResult<CurriculumDto>> GetAsync(CurriculumActor actor, CancellationToken cancellationToken);

    Task<ServiceResult<CurriculumChapterRow>> CreateChapterAsync(
        CurriculumActor actor,
        SaveChapterRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<CurriculumChapterRow>> UpdateChapterAsync(
        CurriculumActor actor,
        ulong chapterId,
        SaveChapterRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<bool>> DeleteChapterAsync(
        CurriculumActor actor,
        ulong chapterId,
        CancellationToken cancellationToken);

    Task<ServiceResult<CurriculumLessonRow>> CreateLessonAsync(
        CurriculumActor actor,
        ulong chapterId,
        SaveLessonRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<CurriculumLessonRow>> UpdateLessonAsync(
        CurriculumActor actor,
        ulong lessonId,
        SaveLessonRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<bool>> DeleteLessonAsync(
        CurriculumActor actor,
        ulong lessonId,
        CancellationToken cancellationToken);

    Task<ServiceResult<CurriculumFile>> BuildTemplateAsync(CancellationToken cancellationToken);

    // Checks the file and reports what would be created; never writes.
    Task<ServiceResult<CurriculumImportPreviewDto>> PreviewImportAsync(
        CurriculumActor actor,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken);

    // Checks the same file again and, only if every row is valid, creates everything in one save.
    Task<ServiceResult<CurriculumImportResultDto>> ImportAsync(
        CurriculumActor actor,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken);
}

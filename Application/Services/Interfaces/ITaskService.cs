using Application.Common;
using Application.DTOs;

namespace Application.Services.Interface;

// Màn Nhiệm vụ chung cho ma trận, đề thi và câu hỏi. Tạo nhiệm vụ ma trận vẫn ở IMatrixTaskApplicationService.
public interface ITaskService
{
    Task<ServiceResult<DirectoryPage<TaskListItem>>> ListAsync(TaskActor actor, TaskListQuery query, CancellationToken cancellationToken);
    Task<ServiceResult<TaskDetail>> GetAsync(TaskActor actor, ulong taskId, CancellationToken cancellationToken);
    Task<ServiceResult<TaskOptions>> GetOptionsAsync(TaskActor actor, CancellationToken cancellationToken);
    Task<ServiceResult<TaskDetail>> UpdateMatrixAsync(TaskActor actor, ulong taskId, CreateMatrixTaskRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<TaskDetail>> CreateExamAsync(TaskActor actor, SaveExamTaskRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<TaskDetail>> UpdateExamAsync(TaskActor actor, ulong taskId, SaveExamTaskRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<TaskDetail>> CreateQuestionAsync(TaskActor actor, SaveQuestionTaskRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<TaskDetail>> UpdateQuestionAsync(TaskActor actor, ulong taskId, SaveQuestionTaskRequest request, CancellationToken cancellationToken);
    // Trả tên nhiệm vụ đã xoá để báo lại cho người dùng.
    Task<ServiceResult<string>> DeleteAsync(TaskActor actor, ulong taskId, CancellationToken cancellationToken);
}

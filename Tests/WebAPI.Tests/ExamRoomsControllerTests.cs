using Application.Common;
using Application.DTOs;
using Application.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Controllers;
using Xunit;

namespace WebAPI.Tests;

public sealed class ExamRoomsControllerTests
{
    [Fact]
    public void Controller_RequiresExamManagerPolicy()
    {
        var authorize = Assert.Single(
            typeof(ExamRoomsController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
                .Cast<AuthorizeAttribute>());

        Assert.Equal("ExamManager", authorize.Policy);
    }

    [Fact]
    public async Task Create_Returns201AndApiEnvelope()
    {
        var detail = Detail();
        var service = new FakeExamRoomService
        {
            CreateResult = ServiceResult<ExamRoomDto>.Success(detail)
        };
        var controller = new ExamRoomsController(service);

        var action = await controller.Create(
            7,
            new CreateExamRoomRequest("k5p01", 3, 30),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(action);
        var envelope = Assert.IsType<ApiResponse<ExamRoomDto>>(created.Value);
        Assert.True(envelope.Success);
        Assert.Equal("k5p01", envelope.Data?.Code);
    }

    [Fact]
    public async Task Delete_Returns409WhenRoomHasSessions()
    {
        var service = new FakeExamRoomService
        {
            DeleteResult = ServiceResult<bool>.Failure(
                "EXAM_ROOM_HAS_DEPENDENCIES",
                "Room has sessions")
        };
        var controller = new ExamRoomsController(service);

        var action = await controller.Delete(7, 9, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(action);
    }

    private static ExamRoomDto Detail() =>
        new(
            9,
            7,
            "k5p01",
            new ExamRoomPhysicalRoomSummary(3, "P101", "Phòng 101", "CLASSROOM", "ACTIVE"),
            30,
            0);

    private sealed class FakeExamRoomService : IExamRoomService
    {
        public ServiceResult<ExamRoomDto> CreateResult { get; init; } =
            ServiceResult<ExamRoomDto>.Failure("NOT_CONFIGURED", "Not configured");
        public ServiceResult<bool> DeleteResult { get; init; } =
            ServiceResult<bool>.Failure("NOT_CONFIGURED", "Not configured");

        public Task<ServiceResult<IReadOnlyList<ExamRoomDto>>> ListAsync(
            ulong examId,
            CancellationToken cancellationToken) =>
            Task.FromResult(ServiceResult<IReadOnlyList<ExamRoomDto>>.Success([]));

        public Task<ServiceResult<IReadOnlyList<ExamRoomOptionDto>>> ListOptionsAsync(
            ulong examId,
            CancellationToken cancellationToken) =>
            Task.FromResult(ServiceResult<IReadOnlyList<ExamRoomOptionDto>>.Success([]));

        public Task<ServiceResult<ExamRoomDto>> GetByIdAsync(
            ulong examId,
            ulong id,
            CancellationToken cancellationToken) =>
            Task.FromResult(ServiceResult<ExamRoomDto>.Failure("EXAM_ROOM_NOT_FOUND", "Not found"));

        public Task<ServiceResult<ExamRoomDto>> CreateAsync(
            ulong examId,
            CreateExamRoomRequest request,
            CancellationToken cancellationToken) => Task.FromResult(CreateResult);

        public Task<ServiceResult<ExamRoomDto>> UpdateAsync(
            ulong examId,
            ulong id,
            UpdateExamRoomRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(ServiceResult<ExamRoomDto>.Failure("EXAM_ROOM_NOT_FOUND", "Not found"));

        public Task<ServiceResult<bool>> DeleteAsync(
            ulong examId,
            ulong id,
            CancellationToken cancellationToken) => Task.FromResult(DeleteResult);
    }
}

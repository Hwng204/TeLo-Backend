using System.Net;
using Application.Common;
using Application.DTOs;
using Application.Services.Interface;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using WebAPI.Controllers;
using Xunit;

namespace WebAPI.Tests;

public sealed class ExamsControllerTests
{
    [Fact]
    public async Task Create_Returns201AndApiEnvelope()
    {
        var detail = Detail();
        var service = new FakeExamService
        {
            CreateResult = ServiceResult<ExamDetailDto>.Success(detail)
        };
        var controller = new ExamsController(service);

        var action = await controller.Create(
            new CreateExamRequest(1, 2, 3, 4, "Kiểm tra", detail.StartDate, detail.StartTime, detail.EndDate, detail.EndTime),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(action);
        var envelope = Assert.IsType<ApiResponse<ExamDetailDto>>(created.Value);
        Assert.True(envelope.Success);
        Assert.Equal(detail.Id, envelope.Data?.Id);
    }

    [Fact]
    public async Task Delete_Returns409_WhenExamHasDependencies()
    {
        var service = new FakeExamService
        {
            DeleteResult = ServiceResult<bool>.Failure(
                "EXAM_HAS_DEPENDENCIES",
                "Exam has dependencies")
        };
        var controller = new ExamsController(service);

        var action = await controller.Delete(7, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(action);
    }

    [Fact]
    public async Task Endpoint_Returns401_WhenJwtIsMissing()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting(
                    "ConnectionStrings:DefaultConnection",
                    "Server=127.0.0.1;Database=unused;User=unused;Password=unused;");
                builder.UseSetting(
                    "Jwt:SigningKey",
                    "exam-test-signing-key-at-least-32-characters");
            });
        using var client = factory.CreateClient();
        client.BaseAddress = new Uri("https://localhost");

        var response = await client.GetAsync("/api/exams");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static ExamDetailDto Detail() =>
        new(
            7,
            "Kiểm tra",
            new ExamSemesterSummary(1, "Học kỳ 1"),
            new ExamAcademicYearSummary(5, "2026-2027"),
            new ExamCatalogSummary(3, "Khối 5"),
            new ExamCatalogSummary(4, "Toán"),
            new ExamSchoolBranchSummary(2, "CS1", "Cơ sở 1"),
            new DateOnly(2026, 11, 1),
            new TimeOnly(8, 0),
            new DateOnly(2026, 11, 2),
            new TimeOnly(10, 0),
            ExamStatusCodes.Draft,
            0,
            0,
            0,
            0,
            0);

    private sealed class FakeExamService : IExamService
    {
        public Task<ServiceResult<ExamFormOptionsDto>> GetFormOptionsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ServiceResult<ExamFormOptionsDto>.Success(new ExamFormOptionsDto([], [], [])));

        public Task<ServiceResult<ExamCandidateCountDto>> GetCandidateCountAsync(
            ulong academicYearId,
            ulong schoolBranchId,
            ulong gradeLevelId,
            CancellationToken cancellationToken) =>
            Task.FromResult(ServiceResult<ExamCandidateCountDto>.Success(
                new ExamCandidateCountDto(academicYearId, schoolBranchId, gradeLevelId, 0)));

        public ServiceResult<ExamDetailDto> CreateResult { get; init; } =
            ServiceResult<ExamDetailDto>.Failure("NOT_CONFIGURED", "Not configured");
        public ServiceResult<bool> DeleteResult { get; init; } =
            ServiceResult<bool>.Failure("NOT_CONFIGURED", "Not configured");

        public Task<ServiceResult<ExamDetailDto>> CreateAsync(
            CreateExamRequest request,
            CancellationToken cancellationToken) => Task.FromResult(CreateResult);

        public Task<ServiceResult<ExamPage>> ListAsync(
            ExamListQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult(ServiceResult<ExamPage>.Success(new ExamPage([], 1, 20, 0)));

        public Task<ServiceResult<ExamDetailDto>> GetByIdAsync(
            ulong id,
            CancellationToken cancellationToken) =>
            Task.FromResult(ServiceResult<ExamDetailDto>.Failure("EXAM_NOT_FOUND", "Not found"));

        public Task<ServiceResult<ExamDetailDto>> UpdateAsync(
            ulong id,
            UpdateExamRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(ServiceResult<ExamDetailDto>.Failure("EXAM_NOT_FOUND", "Not found"));

        public Task<ServiceResult<bool>> DeleteAsync(
            ulong id,
            CancellationToken cancellationToken) => Task.FromResult(DeleteResult);
    }
}

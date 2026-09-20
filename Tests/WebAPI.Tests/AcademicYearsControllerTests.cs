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

public sealed class AcademicYearsControllerTests
{
    [Fact]
    public async Task List_Returns422ForInvalidPaginationWithoutCallingTheService()
    {
        var service = new FakeAcademicYearService();
        var controller = new AcademicYearsController(service);

        var result = await controller.List("1", null, null, 0, 101, CancellationToken.None);

        var response = Assert.IsType<UnprocessableEntityObjectResult>(result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, response.StatusCode);
        Assert.Equal(0, service.ListCallCount);
    }

    [Fact]
    public async Task List_Returns422ForInvalidStatusAndOversizedSearch()
    {
        var service = new FakeAcademicYearService();
        var controller = new AcademicYearsController(service);

        var result = await controller.List(
            "01",
            "DELETED",
            new string('a', 101),
            1,
            20,
            CancellationToken.None);

        var response = Assert.IsType<UnprocessableEntityObjectResult>(result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, response.StatusCode);
        Assert.Equal(0, service.ListCallCount);
    }

    [Fact]
    public async Task Create_Returns409ForAcademicYearConflict()
    {
        var service = new FakeAcademicYearService
        {
            CreateResult = ServiceResult<AcademicYearListItem>.Failure(
                "ACADEMIC_YEAR_CONFLICT",
                "Conflict")
        };
        var controller = new AcademicYearsController(service);
        var request = new CreateAcademicYearRequest(
            "01",
            "2026-2027",
            new DateOnly(2026, 8, 15),
            new DateOnly(2027, 5, 31));

        var result = await controller.Create(request, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Endpoint_Returns401WhenNoJwtIsProvided()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(
                    "ConnectionStrings:DefaultConnection",
                    "Server=127.0.0.1;Database=unused;User=unused;Password=unused;");
                builder.UseSetting(
                    "Jwt:SigningKey",
                    "academic-year-test-signing-key-at-least-32-chars");
            });
        using var client = factory.CreateClient();
        client.BaseAddress = new Uri("https://localhost");

        var response = await client.GetAsync("/api/academic-years?provinceCode=01");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StudentsEndpoint_RequiresAuthenticationAndResolvesDirectoryService()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(
                    "ConnectionStrings:DefaultConnection",
                    "Server=127.0.0.1;Database=unused;User=unused;Password=unused;");
                builder.UseSetting(
                    "Jwt:SigningKey",
                    "academic-year-test-signing-key-at-least-32-chars");
            });
        using var client = factory.CreateClient();
        client.BaseAddress = new Uri("https://localhost");

        var response = await client.GetAsync("/api/students");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed class FakeAcademicYearService : IAcademicYearService
    {
        public int ListCallCount { get; private set; }
        public ServiceResult<AcademicYearListItem> CreateResult { get; init; } =
            ServiceResult<AcademicYearListItem>.Failure("UNEXPECTED", "Not configured");

        public Task<ServiceResult<AcademicYearListItem>> CreateAsync(
            CreateAcademicYearRequest request,
            CancellationToken cancellationToken) => Task.FromResult(CreateResult);

        public Task<AcademicYearPage> ListAsync(
            AcademicYearListQuery query,
            CancellationToken cancellationToken)
        {
            ListCallCount++;
            return Task.FromResult(new AcademicYearPage([], query.Page, query.PageSize, 0));
        }

        public Task<ServiceResult<AcademicYearDetailDto>> GetByIdAsync(
            ulong id,
            CancellationToken cancellationToken) =>
            Task.FromResult(ServiceResult<AcademicYearDetailDto>.Failure("ACADEMIC_YEAR_NOT_FOUND", "Not found"));

        public Task<ServiceResult<AcademicYearDetailDto>> UpdateAsync(
            ulong id,
            UpdateAcademicYearRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(ServiceResult<AcademicYearDetailDto>.Failure("ACADEMIC_YEAR_NOT_FOUND", "Not found"));

        public Task<ServiceResult<AcademicYearDetailDto>> ActivateAsync(
            ulong id,
            CancellationToken cancellationToken) =>
            Task.FromResult(ServiceResult<AcademicYearDetailDto>.Failure("ACADEMIC_YEAR_NOT_FOUND", "Not found"));

        public Task<ServiceResult<AcademicYearDetailDto>> CloseAsync(
            ulong id,
            CancellationToken cancellationToken) =>
            Task.FromResult(ServiceResult<AcademicYearDetailDto>.Failure("ACADEMIC_YEAR_NOT_FOUND", "Not found"));

        public Task<ServiceResult<AcademicYearDetailDto>> ConfigureTermsAsync(
            ulong id,
            ConfigureTermsRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(ServiceResult<AcademicYearDetailDto>.Failure("ACADEMIC_YEAR_NOT_FOUND", "Not found"));

        public Task<ServiceResult<SemesterDto>> CloseTermAsync(
            ulong yearId,
            ulong termId,
            CancellationToken cancellationToken) =>
            Task.FromResult(ServiceResult<SemesterDto>.Failure("TERM_NOT_FOUND", "Not found"));
    }
}

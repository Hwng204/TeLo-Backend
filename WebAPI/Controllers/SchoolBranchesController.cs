using Application.DTOs;
using Application.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[ApiController]
[Route("api")]
public class SchoolBranchesController(ISchoolBranchService branchService) : ControllerBase
{
    [HttpPost("schools/{schoolId}/branches")]
    public async Task<IActionResult> CreateBranch(
        [FromRoute] ulong schoolId,
        [FromBody] CreateSchoolBranchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await branchService.CreateAsync(schoolId, request, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new { success = false, message = result.Error!.Message });

        return Ok(new { success = true, data = result.Value });
    }

    [HttpPatch("branches/{id}")]
    public async Task<IActionResult> UpdateBranch(
        [FromRoute] ulong id,
        [FromBody] UpdateSchoolBranchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await branchService.UpdateAsync(id, request, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new { success = false, message = result.Error!.Message });

        return Ok(new { success = true, data = result.Value });
    }

    [HttpDelete("branches/{id}")]
    public async Task<IActionResult> DeleteBranch(
        [FromRoute] ulong id,
        CancellationToken cancellationToken)
    {
        var result = await branchService.DeleteAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new { success = false, message = result.Error!.Message });

        return Ok(new { success = true, data = result.Value });
    }
}

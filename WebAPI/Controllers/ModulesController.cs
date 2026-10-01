using Application.DTOs;
using Application.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[Route("api/modules")]
public sealed class ModulesController(IModuleService service) : IdentityControllerBase
{
    [HttpGet]
    public Task<IActionResult> List([FromQuery] IdentityListQuery query, CancellationToken ct) => Respond(() => service.ListAsync(query, ct));
    [HttpGet("{id:long}")]
    public Task<IActionResult> Get(ulong id, CancellationToken ct) => Respond(() => service.GetAsync(id, ct));
    [HttpPost]
    public Task<IActionResult> Create(SaveModuleRequest request, CancellationToken ct) => Respond(() => service.SaveAsync(null, request, ActorId, ct), true);
    [HttpPut("{id:long}")]
    public Task<IActionResult> Update(ulong id, SaveModuleRequest request, CancellationToken ct) => Respond(() => service.SaveAsync(id, request, ActorId, ct));
    [HttpPatch("{id:long}/status")]
    public Task<IActionResult> Status(ulong id, IdentityStatusRequest request, CancellationToken ct) => Respond(() => service.StatusAsync(id, request, ActorId, ct));
    [HttpDelete("{id:long}")]
    public Task<IActionResult> Delete(ulong id, [FromQuery] uint version, CancellationToken ct) => Respond(() => service.DeleteAsync(id, version, ActorId, ct));
}

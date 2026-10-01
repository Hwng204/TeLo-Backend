using Application.DTOs;
using Application.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[Route("api/users")]
public sealed class UsersController(IRoleService service) : IdentityControllerBase
{
    [HttpGet]
    public Task<IActionResult> List([FromQuery] IdentityListQuery query, CancellationToken ct) => Respond(() => service.UsersAsync(query, ct));
    [HttpGet("{id:long}/roles")]
    public Task<IActionResult> Roles(ulong id, CancellationToken ct) => Respond(() => service.UserRolesAsync(id, ct));
    [HttpPut("{id:long}/roles")]
    public Task<IActionResult> AssignRoles(ulong id, AssignUserRolesRequest request, CancellationToken ct) => Respond(() => service.AssignUserRolesAsync(id, request, ActorId, ct));
    [HttpGet("/api/identity/scopes")]
    public Task<IActionResult> Scopes([FromQuery] string kind, [FromQuery] IdentityListQuery query, CancellationToken ct) => Respond(() => service.ScopesAsync(kind, query, ct));
}

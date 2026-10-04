using Application.DTOs;
using Application.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[Route("api/users")]
public sealed class UsersController(IUserService userService, IRoleService roleService) : IdentityControllerBase
{
    [HttpGet]
    public Task<IActionResult> List([FromQuery] IdentityListQuery query, CancellationToken ct) =>
        Respond(() => userService.ListAsync(query, ct));

    [HttpGet("{id:long}")]
    public Task<IActionResult> Get(ulong id, CancellationToken ct) =>
        Respond(() => userService.GetAsync(id, ct));

    [HttpPost]
    public Task<IActionResult> Create(CreateUserRequest request, CancellationToken ct) =>
        Respond(() => userService.CreateAsync(request, ActorId, ct), true);

    [HttpPut("{id:long}")]
    public Task<IActionResult> Update(ulong id, UpdateUserRequest request, CancellationToken ct) =>
        Respond(() => userService.UpdateAsync(id, request, ActorId, ct));

    [HttpPatch("{id:long}/status")]
    public Task<IActionResult> Status(ulong id, IdentityStatusRequest request, CancellationToken ct) =>
        Respond(() => userService.StatusAsync(id, request, ActorId, ct));

    [HttpPatch("{id:long}/password")]
    public Task<IActionResult> ResetPassword(
        ulong id,
        ResetUserPasswordRequest request,
        CancellationToken ct) =>
        Respond(() => userService.ResetPasswordAsync(id, request, ActorId, ct));

    [HttpDelete("{id:long}")]
    public Task<IActionResult> Delete(ulong id, [FromQuery] uint version, CancellationToken ct) =>
        Respond(() => userService.DeleteAsync(id, version, ActorId, ct));

    [HttpGet("{id:long}/roles")]
    public Task<IActionResult> Roles(ulong id, CancellationToken ct) =>
        Respond(() => roleService.UserRolesAsync(id, ct));

    [HttpPut("{id:long}/roles")]
    public Task<IActionResult> AssignRoles(
        ulong id,
        AssignUserRolesRequest request,
        CancellationToken ct) =>
        Respond(() => roleService.AssignUserRolesAsync(id, request, ActorId, ct));

    [HttpGet("/api/identity/scopes")]
    public Task<IActionResult> Scopes(
        [FromQuery] string kind,
        [FromQuery] IdentityListQuery query,
        CancellationToken ct) =>
        Respond(() => roleService.ScopesAsync(kind, query, ct));
}

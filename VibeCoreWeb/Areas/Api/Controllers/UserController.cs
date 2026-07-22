using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VibeCore.Areas.Api.Controllers;

[Area("Api")]
[Route("api/[controller]")]
[ApiController]
[Authorize]
public sealed class UserController : ControllerBase
{
    [HttpGet("current")]
    public ActionResult<UserInfoDto> GetCurrentUser()
    {
        return Ok(new UserInfoDto
        {
            IsAuthenticated = true,
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            UserName = User.Identity?.Name,
            Email = User.FindFirstValue(ClaimTypes.Email),
            TenantId = User.FindFirstValue("flex:tenant_id"),
            TenantRole = User.FindFirstValue("flex:tenant_role"),
            Roles = User.FindAll(ClaimTypes.Role)
                .Select(claim => claim.Value)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
        });
    }
}

public sealed class UserInfoDto
{
    public bool IsAuthenticated { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? TenantId { get; set; }
    public string? TenantRole { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = [];
}

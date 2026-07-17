using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;

namespace VibeCore.Areas.Api.Controllers;

[Area("Api")]
[Route("api/[controller]")]
[ApiController]
public class UserController : ControllerBase
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;

    public UserController(SignInManager<IdentityUser> signInManager, UserManager<IdentityUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [HttpGet("current")]
    public async Task<ActionResult<UserInfoDto>> GetCurrentUser()
    {
        if (!_signInManager.IsSignedIn(User))
        {
            return Ok(new UserInfoDto { IsAuthenticated = false });
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Ok(new UserInfoDto { IsAuthenticated = false });
        }

        return Ok(new UserInfoDto
        {
            IsAuthenticated = true,
            UserName = user.UserName,
            Email = user.Email
        });
    }
}

public class UserInfoDto
{
    public bool IsAuthenticated { get; set; }
    public string? UserName { get; set; }
    public string? Email { get; set; }
}

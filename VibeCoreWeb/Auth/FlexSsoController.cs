using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace VibeCore.Auth;

[ApiExplorerSettings(IgnoreApi = true)]
public sealed class FlexSsoController(
    IOptions<FlexSsoOptions> options,
    IHttpClientFactory httpClientFactory,
    FlexSsoTransactionProtector transactionProtector,
    ILogger<FlexSsoController> logger) : Controller
{
    private const string TransactionCookie = "__Host-VibeCore.FlexSso.Transaction";

    [AllowAnonymous]
    [HttpGet("/flex-auth/login")]
    public IActionResult Login([FromQuery] string? returnUrl = null)
    {
        var config = GetValidatedOptions();
        var safeReturnUrl = IsSafeLocalReturnUrl(returnUrl) ? returnUrl! : "/app/";
        var state = CreateRandomValue();
        var verifier = CreateRandomValue();
        var challenge = WebEncoders.Base64UrlEncode(
            SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var callbackUri = BuildCallbackUri();

        Response.Cookies.Append(
            TransactionCookie,
            transactionProtector.Protect(new FlexSsoTransaction(state, verifier, safeReturnUrl)),
            BuildTransactionCookieOptions());

        var authorizeUrl = QueryHelpers.AddQueryString(
            $"{config.Authority.TrimEnd('/')}{config.AuthorizePath}",
            new Dictionary<string, string?>
            {
                ["redirect_uri"] = callbackUri,
                ["state"] = state,
                ["code_challenge"] = challenge,
                ["code_challenge_method"] = "S256",
            });

        return Redirect(authorizeUrl);
    }

    [AllowAnonymous]
    [HttpGet("/flex-auth/callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string code,
        [FromQuery] string state,
        [FromQuery] string iss,
        CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue(TransactionCookie, out var protectedTransaction))
            return BadRequest("The Flex SSO transaction cookie is missing or expired.");

        FlexSsoTransaction transaction;
        try
        {
            transaction = transactionProtector.Unprotect(protectedTransaction);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Rejected an invalid Flex SSO transaction cookie.");
            return BadRequest("The Flex SSO transaction is invalid or expired.");
        }

        Response.Cookies.Delete(TransactionCookie, BuildTransactionCookieOptions());
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(transaction.State),
                Encoding.UTF8.GetBytes(state)))
        {
            return BadRequest("The Flex SSO state did not match.");
        }

        var config = GetValidatedOptions();
        if (!string.Equals(
                iss.TrimEnd('/'),
                config.Authority.TrimEnd('/'),
                StringComparison.Ordinal))
        {
            return BadRequest("The Flex SSO issuer did not match.");
        }

        var client = httpClientFactory.CreateClient(FlexSsoDefaults.HttpClientName);
        var backchannelAuthority = string.IsNullOrWhiteSpace(config.BackchannelAuthority)
            ? config.Authority
            : config.BackchannelAuthority;
        using var response = await client.PostAsJsonAsync(
            $"{backchannelAuthority.TrimEnd('/')}{config.TokenPath}",
            new FlexSsoTokenRequest(code, transaction.CodeVerifier, BuildCallbackUri()),
            ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Flex SSO code exchange failed with status {StatusCode}.",
                response.StatusCode);
            return StatusCode(StatusCodes.Status502BadGateway, "Flex SSO sign-in failed.");
        }

        var token = await response.Content.ReadFromJsonAsync<FlexSsoTokenResponse>(cancellationToken: ct);
        if (token is null || string.IsNullOrWhiteSpace(token.UserId))
            return StatusCode(StatusCodes.Status502BadGateway, "Flex SSO returned an invalid identity.");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, token.UserId),
            new(ClaimTypes.Name, token.Name ?? token.Email ?? token.UserId),
            new("flex:tenant_id", token.TenantId),
            new("flex:tenant_role", token.TenantRole),
        };
        if (!string.IsNullOrWhiteSpace(token.Email))
            claims.Add(new Claim(ClaimTypes.Email, token.Email));
        foreach (var role in token.Roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, FlexSsoDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(
            FlexSsoDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
            });

        return LocalRedirect(transaction.ReturnUrl);
    }

    [Authorize(AuthenticationSchemes = FlexSsoDefaults.AuthenticationScheme)]
    [IgnoreAntiforgeryToken]
    [HttpPost("/flex-auth/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(FlexSsoDefaults.AuthenticationScheme);
        return LocalRedirect("/app/");
    }

    [AllowAnonymous]
    [HttpGet("/flex-auth/denied")]
    public IActionResult Denied() => StatusCode(StatusCodes.Status403Forbidden);

    private FlexSsoOptions GetValidatedOptions()
    {
        var config = options.Value;
        if (!config.Enabled)
            throw new InvalidOperationException("Flex SSO is not enabled.");
        if (!Uri.TryCreate(config.Authority, UriKind.Absolute, out var authority) ||
            authority.Scheme is not ("https" or "http"))
        {
            throw new InvalidOperationException("FlexSso:Authority must be an absolute HTTP(S) URL.");
        }
        if (!string.IsNullOrWhiteSpace(config.BackchannelAuthority) &&
            (!Uri.TryCreate(config.BackchannelAuthority, UriKind.Absolute, out var backchannelAuthority) ||
             backchannelAuthority.Scheme is not ("https" or "http")))
        {
            throw new InvalidOperationException(
                "FlexSso:BackchannelAuthority must be an absolute HTTP(S) URL.");
        }

        return config;
    }

    private string BuildCallbackUri() =>
        $"{Request.Scheme}://{Request.Host}{Request.PathBase}/flex-auth/callback";

    private CookieOptions BuildTransactionCookieOptions() => new()
    {
        HttpOnly = true,
        Secure = Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        MaxAge = TimeSpan.FromMinutes(5),
        IsEssential = true,
    };

    private static bool IsSafeLocalReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) &&
        returnUrl.StartsWith("/", StringComparison.Ordinal) &&
        !returnUrl.StartsWith("//", StringComparison.Ordinal) &&
        !returnUrl.StartsWith("/\\", StringComparison.Ordinal);

    private static string CreateRandomValue() =>
        WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
}

public sealed record FlexSsoTokenRequest(string Code, string CodeVerifier, string RedirectUri);

public sealed record FlexSsoTokenResponse(
    string UserId,
    string? Email,
    string? Name,
    string TenantId,
    string TenantRole,
    string[] Roles);

using Microsoft.AspNetCore.Identity;
using StandServer.Configuration;
using StandServer.Services;

namespace StandServer.Controllers;

/// <summary> A controller containing authorization-related actions. </summary>
[ApiController]
public class AuthController : ControllerBase
{
    /// <summary> Instance of <see cref="PasswordHasher"/>. </summary>
    public static readonly PasswordHasher<User> PasswordHasher = new();

    private readonly ILogger<AuthController> logger;
    private readonly IStringLocalizer<AuthController> localizer;
    private readonly AuthConfig authConfig;

    public AuthController(ILogger<AuthController> logger, IStringLocalizer<AuthController> localizer, IOptions<AuthConfig> authConfig)
    {
        this.logger = logger;
        this.localizer = localizer;
        this.authConfig = authConfig.Value;
    }

    /// <summary> Creating a token by combining refresh token uid and device uid. </summary>
    static string CreateRefreshToken(Guid token, Guid deviceUid) => $"{token:N}{deviceUid:N}";

    /// <summary> Get the refresh token uid and device uid from the merged string. </summary>
    /// <param name="refreshToken">Refresh token string.</param>
    /// <param name="token">When this method returns, contains the parsed value.
    /// If the method returns true, result contains a valid Guid. If the method returns false, result equals Empty.</param>
    /// <param name="deviceUid">When this method returns, contains the parsed value.
    /// If the method returns true, result contains a valid Guid. If the method returns false, result equals Empty.</param>
    /// <returns>True if the parse operation was successful; otherwise, false.</returns>
    static bool TryParseRefreshToken(string? refreshToken, out Guid token, out Guid deviceUid)
    {
        token = deviceUid = Guid.Empty;
        if (refreshToken is not { Length: 64 }) return false;
        if (!Guid.TryParseExact(refreshToken[..32], "N", out token)) return false;
        if (!Guid.TryParseExact(refreshToken[32..], "N", out deviceUid)) return false;
        return true;
    }

    /// <summary> The authorization method for obtaining access to the site resources. </summary>
    /// <returns>If authorization is successful, a bundle of access and refresh tokens, if not, an error.</returns>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginModel model,
        [FromServices] ApplicationContext context)
    {
        User? user = await context.Users.FirstOrDefaultAsync(x => x.Login == model.Login);
        if (user == null)
            return Problem(title: localizer["Login.InvalidLogin"], statusCode: StatusCodes.Status401Unauthorized);

        var passwordVerificationResult = PasswordHasher.VerifyHashedPassword(null!, user.Password, model.Password!);
        if (passwordVerificationResult == PasswordVerificationResult.Failed)
            return Problem(title: localizer["Login.InvalidPassword"], statusCode: StatusCodes.Status401Unauthorized);

        Guid refreshTokenId = Guid.NewGuid();
        Guid deviceUid = Guid.NewGuid();
        string jwt = CreateJwtToken(user, deviceUid);

        RefreshToken refreshToken = new()
        {
            Id = refreshTokenId,
            UserId = user.Id,
            Expires = DateTime.UtcNow + TimeSpan.FromSeconds(authConfig.TokenTTL.Refresh),
            DeviceUid = deviceUid
        };
        context.RefreshTokens.Add(refreshToken);
        await context.SaveChangesAsync();

        string fullRefreshToken = CreateRefreshToken(refreshTokenId, deviceUid);
        var response = new
        {
            AccessToken = jwt, RefreshToken = fullRefreshToken,
            User = new { user.Id, user.Login, user.IsAdmin }
        };

        Response.Cookies.Append("RefreshToken", fullRefreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                // Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.Now.Add(TimeSpan.FromSeconds(authConfig.TokenTTL.Refresh))
            });

        return Ok(response);
    }

    /// <summary> Getting the password hash. </summary>
    /// <param name="password">Raw password.</param>
    /// <returns>Password hash.</returns>
    public static string GetHashPassword(string password) => PasswordHasher.HashPassword(null!, password);

    /// <summary> A method for obtaining a new bundle of access and refresh tokens. </summary>
    /// <param name="rawRefreshToken">If not passed, the token is taken from the cookie.</param>
    /// <returns>If updating token is successful, a bundle of access and refresh tokens, if not, an error.</returns>
    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken(
        [FromBody] string rawRefreshToken,
        [FromServices] ApplicationContext context)
    {
        if (String.IsNullOrWhiteSpace(rawRefreshToken) && Request.Cookies.TryGetValue("RefreshToken", out string? sToken))
            rawRefreshToken = sToken;
        if (!TryParseRefreshToken(rawRefreshToken, out Guid token, out Guid deviceUid))
            return Problem(title: localizer["RefreshToken.MissingRefreshToken"], statusCode: StatusCodes.Status400BadRequest);

        List<RefreshToken> refreshTokens = await context.RefreshTokens
            .Where(t => t.Id == token || t.DeviceUid == deviceUid).ToListAsync();

        var refreshToken = refreshTokens.FirstOrDefault(t => t.Id == token);
        var newDeviceRelatedToken = refreshTokens.FirstOrDefault(t => t.DeviceUid == deviceUid);

        if (refreshToken == null || refreshToken.Expires <= DateTime.UtcNow) // Expired tokens are automatically deleted
        {
            if (newDeviceRelatedToken == null)
                return Problem(title: localizer["RefreshToken.InvalidOrExpiredToken"], statusCode: StatusCodes.Status400BadRequest);

            await context.RefreshTokens.Where(t => t.DeviceUid == deviceUid).ExecuteDeleteAsync();
            return Problem(title: localizer["RefreshToken.TokenAlreadyUsed"],
                statusCode: StatusCodes.Status400BadRequest);
        }

        // This is only possible if the client changes the second part of the refresh token, which doesn't make sense, but let it be
        if (refreshToken.DeviceUid != deviceUid)
            return Problem(title: localizer["RefreshToken.TokenFromAnotherClient"],
                statusCode: StatusCodes.Status400BadRequest);

        User user = await context.Users.FirstAsync(u => u.Id == refreshToken.UserId);
        string jwt = CreateJwtToken(user, deviceUid);

        Guid newRefreshTokenId = Guid.NewGuid();
        string fullRefreshToken = CreateRefreshToken(newRefreshTokenId, deviceUid);

        RefreshToken newRefreshToken = new()
        {
            Id = newRefreshTokenId,
            UserId = user.Id,
            Expires = DateTime.UtcNow + TimeSpan.FromSeconds(authConfig.TokenTTL.Refresh),
            DeviceUid = deviceUid
        };
        context.RefreshTokens.Remove(refreshToken);
        context.RefreshTokens.Add(newRefreshToken);
        await context.SaveChangesAsync();

        var response = new { AccessToken = jwt, RefreshToken = fullRefreshToken };

        Response.Cookies.Append("RefreshToken", fullRefreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                // Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.Now.Add(TimeSpan.FromSeconds(authConfig.TokenTTL.Refresh))
            });

        return Ok(response);
    }

    /// <summary> The method of logging out of the account, which removes the token from the database and from cookies. </summary>
    [HttpPost("logout"), Authorize]
    public async Task<IActionResult> Logout(
        [FromServices] ApplicationContext context,
        [FromServices] RequestData requestData)
    {
        await context.RefreshTokens
            .Where(t => t.DeviceUid == requestData.DeviceUid)
            .ExecuteDeleteAsync();

        Response.Cookies.Delete("RefreshToken");

        return Ok();
    }

    /// <summary> Generating a JWT access token. </summary>
    /// <returns>JWT access token</returns>
    string CreateJwtToken(User user, Guid deviceUid)
    {
        var now = DateTime.UtcNow;

        var jwt = new JwtSecurityToken(
            issuer: authConfig.Jwt.Issuer,
            notBefore: now,
            claims: new Claim[]
            {
                new(JwtRegisteredClaimNames.Name, user.Login),
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new("IsAdmin", user.IsAdmin.ToString()),
                new("DeviceUid", deviceUid.ToString("N"))
            },
            expires: now.Add(TimeSpan.FromSeconds(authConfig.TokenTTL.Access)),
            signingCredentials: new SigningCredentials(authConfig.Jwt.SecretKey,
                SecurityAlgorithms.HmacSha256));

        var encodedJwt = new JwtSecurityTokenHandler().WriteToken(jwt);
        return encodedJwt;
    }

    /// <summary> The POST method available to the user to change the account password.
    /// Cancels all sessions except the current one. </summary>
    [HttpPost("change-password"), Authorize]
    public async Task<IActionResult> ChangePassword(
        [FromServices] ApplicationContext context,
        [FromServices] RequestData requestData,
        [FromBody] ChangePasswordModel model)
    {
        string? currentPassword = await context.Users
            .Where(u => u.Id == requestData.UserId)
            .Select(u => u.Password)
            .FirstOrDefaultAsync();

        if (currentPassword is null)
            return Problem(title: localizer["ChangePassword.UserDoesNotExist"], statusCode: StatusCodes.Status404NotFound);

        var verificationResult = PasswordHasher.VerifyHashedPassword(null!, currentPassword, model.OldPassword!);
        if (verificationResult == PasswordVerificationResult.Failed)
            return Problem(title: localizer["ChangePassword.InvalidOldPassword"], statusCode: StatusCodes.Status400BadRequest);

        await using var transaction = await context.Database.BeginTransactionAsync();

        int c = await context.Users
            .Where(u => u.Id == requestData.UserId)
            .ExecuteUpdateAsync(p =>
                p.SetProperty(u => u.Password, _ => GetHashPassword(model.NewPassword!)));

        await context.RefreshTokens
            .Where(t => t.UserId == requestData.UserId && t.DeviceUid != requestData.DeviceUid)
            .ExecuteDeleteAsync();

        await transaction.CommitAsync();

        return c > 0 ? Ok() : NotFound();
    }
}
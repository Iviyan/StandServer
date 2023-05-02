using Microsoft.AspNetCore.Identity;
using StandServer.Configuration;

namespace StandServer.Controllers;

[ApiController]
public class AuthController : ControllerBase
{
    public static readonly PasswordHasher<User> PasswordHasher = new();

    private readonly ILogger<AuthController> logger;
    private readonly AuthConfig authConfig;

    public AuthController(ILogger<AuthController> logger, IOptions<AuthConfig> authConfig)
    {
        this.logger = logger;
        this.authConfig = authConfig.Value;
    }

    static string CreateRefreshToken(Guid token, Guid deviceUid) => $"{token:N}{deviceUid:N}";

    static bool TryParseRefreshToken(string? refreshToken, out Guid token, out Guid deviceUid)
    {
        token = deviceUid = Guid.Empty;
        if (refreshToken is not { Length: 64 }) return false;
        if (!Guid.TryParseExact(refreshToken[..32], "N", out token)) return false;
        if (!Guid.TryParseExact(refreshToken[32..], "N", out deviceUid)) return false;
        return true;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginModel model,
        [FromServices] ApplicationContext context)
    {
        User? user = await context.Users.FirstOrDefaultAsync(x => x.Login == model.Login);
        if (user == null)
            return Problem(title: "Invalid login", statusCode: StatusCodes.Status401Unauthorized);

        var passwordVerificationResult = PasswordHasher.VerifyHashedPassword(null!, user.Password, model.Password);
        if (passwordVerificationResult == PasswordVerificationResult.Failed)
            return Problem(title: "Invalid password", statusCode: StatusCodes.Status401Unauthorized);

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
            User = new { user.Id, user.Login }
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

    public static string GetHashPassword(string password) => PasswordHasher.HashPassword(null!, password);

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken(
        [FromBody] string rawRefreshToken,
        [FromServices] ApplicationContext context)
    {
        if (String.IsNullOrWhiteSpace(rawRefreshToken) 
            && Request.Cookies.TryGetValue("RefreshToken", out string? sToken) && sToken != null)
            rawRefreshToken = sToken;
        if (!TryParseRefreshToken(rawRefreshToken, out Guid token, out Guid deviceUid))
            return Problem(title: "There is no RefreshToken in body or cookie", statusCode: StatusCodes.Status400BadRequest);

        List<RefreshToken> refreshTokens = await context.RefreshTokens
            .Where(t => t.Id == token || t.DeviceUid == deviceUid).ToListAsync();

        var refreshToken = refreshTokens.FirstOrDefault(t => t.Id == token);
        var newDeviceRelatedToken = refreshTokens.FirstOrDefault(t => t.DeviceUid == deviceUid);

        if (refreshToken == null || refreshToken.Expires <= DateTime.UtcNow) // Expired tokens are automatically deleted
        {
            if (newDeviceRelatedToken == null)
                return Problem(title: "Invalid or expired token", statusCode: StatusCodes.Status400BadRequest);

            await context.RefreshTokens.Where(t => t.DeviceUid == deviceUid).BatchDeleteAsync();
            return Problem(title: "The token has already been used. The token may have been stolen.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // This is only possible if the client changes the second part of the refresh token, which doesn't make sense, but let it be
        if (refreshToken.DeviceUid != deviceUid)
            return Problem(title: "Invalid token", detail: "The token was created by another client",
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

    [HttpPost("logout"), Authorize]
    public async Task<IActionResult> Logout(
        [FromServices] ApplicationContext context,
        [FromServices] RequestData requestData)
    {
        await context.RefreshTokens
            .Where(t => t.DeviceUid == requestData.DeviceUid)
            .BatchDeleteAsync();

        Response.Cookies.Delete("RefreshToken");

        return Ok();
    }

    string CreateJwtToken(User user, Guid deviceUid)
    {
        var now = DateTime.UtcNow;

        var jwt = new JwtSecurityToken(
            issuer: authConfig.Jwt.Issuer,
            notBefore: now,
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Name, user.Login),
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim("IsAdmin", user.IsAdmin.ToString()),
                new Claim("DeviceUid", deviceUid.ToString("N"))
            },
            expires: now.Add(TimeSpan.FromSeconds(authConfig.TokenTTL.Access)),
            signingCredentials: new SigningCredentials(authConfig.Jwt.SecretKey,
                SecurityAlgorithms.HmacSha256));

        var encodedJwt = new JwtSecurityTokenHandler().WriteToken(jwt);
        return encodedJwt;
    }

    [HttpPost("change-password"), Authorize]
    public async Task<IActionResult> ChangePassword(
        [FromServices] ApplicationContext context,
        [FromServices] RequestData requestData,
        [FromBody] ChangePasswordRequest model)
    {
        string? password = await context.Users
            .Where(u => u.Id == requestData.UserId)
            .Select(u => u.Password)
            .FirstOrDefaultAsync();

        if (password is null)
            return Problem(title: "This user seems to have been deleted", statusCode: StatusCodes.Status404NotFound);

        var verificationResult = PasswordHasher.VerifyHashedPassword(null!, password, model.OldPassword);
        if (verificationResult == PasswordVerificationResult.Failed)
            return Problem(title: "The old password does not match the current one",
                statusCode: StatusCodes.Status400BadRequest);

        await using var transaction = await context.Database.BeginTransactionAsync();
        
        int c = await context.Users
            .Where(u => u.Id == requestData.UserId)
            .BatchUpdateAsync(u => new User { Password = GetHashPassword(model.NewPassword!) });

        await context.RefreshTokens
            .Where(t => t.UserId == requestData.UserId && t.DeviceUid != requestData.DeviceUid)
            .BatchDeleteAsync();

        await transaction.CommitAsync();

        return c > 0
            ? StatusCode(StatusCodes.Status204NoContent)
            : Problem(title: "Unknown error", statusCode: StatusCodes.Status404NotFound);
    }
}
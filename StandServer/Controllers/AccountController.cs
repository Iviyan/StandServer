using Microsoft.AspNetCore.Identity;
using StandServer.Configuration;
using StandServer.Services;
using Telegram.Bot;
using Telegram.Bot.Exceptions;

namespace StandServer.Controllers;

[ApiController]
public class AccountController : ControllerBase
{
    public static readonly PasswordHasher<User> PasswordHasher = new();

    private readonly ILogger<AccountController> logger;
    private readonly JwtConfig jwtConfig;

    public AccountController(ILogger<AccountController> logger, IOptions<JwtConfig> jwtConfig)
    {
        this.logger = logger;
        this.jwtConfig = jwtConfig.Value;
    }

    [HttpPost("/login")]
    public async Task<IActionResult> Login([FromBody] LoginModel model,
        [FromServices] ApplicationContext context,
        [FromServices] RequestData requestData)
    {
        User? user = await context.Users.FirstOrDefaultAsync(x => x.Login == model.Login);
        if (user == null)
            return Problem(title: "Invalid login", statusCode: StatusCodes.Status401Unauthorized);

        var passwordVerificationResult = PasswordHasher.VerifyHashedPassword(null!, user.Password, model.Password);
        if (passwordVerificationResult == PasswordVerificationResult.Failed)
            return Problem(title: "Invalid password", statusCode: StatusCodes.Status401Unauthorized);

        string jwt = CreateJwtToken(user);

        RefreshToken? oldRefreshToken =
            context.RefreshTokens.FirstOrDefault(t => t.DeviceUid == requestData.DeviceUid);
        if (oldRefreshToken is { }) context.RefreshTokens.Remove(oldRefreshToken);

        RefreshToken refreshToken = new()
        {
            UserId = user.Id,
            Expires = DateTime.UtcNow + TimeSpan.FromDays(30),
            DeviceUid = requestData.DeviceUid
        };
        context.RefreshTokens.Add(refreshToken);
        await context.SaveChangesAsync();

        var response = new
        {
            AccessToken = jwt,
            User = new { user.Id, user.Login }
        };

        Response.Cookies.Append("RefreshToken", refreshToken.Id.ToString(),
            new CookieOptions
            {
                HttpOnly = true,
                // Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.Now.Add(TimeSpan.FromDays(30))
            });

        return Ok(response);
    }

    [HttpPost("/register"), Authorize]
    public async Task<IActionResult> Register([FromBody] RegisterModel model,
        [FromServices] ApplicationContext context,
        [FromServices] RequestData requestData)
    {
        if (requestData.IsAdmin is not true)
            return Problem(title: "You must be an admin to create a user", statusCode: StatusCodes.Status403Forbidden);

        if (await context.Users.AnyAsync(x => x.Login == model.Login))
            return Problem(title: "The user with this login already exists",
                statusCode: StatusCodes.Status400BadRequest);

        User user = new()
        {
            Login = model.Login!,
            Password = GetHashPassword(model.Password!),
            IsAdmin = model.IsAdmin
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        return Ok();
    }

    public static string GetHashPassword(string password) => PasswordHasher.HashPassword(null!, password);

    [HttpPost("/refresh-token")]
    public async Task<IActionResult> RefreshToken(
        [FromServices] ApplicationContext context,
        [FromServices] RequestData requestData)
    {
        if (!Request.Cookies.TryGetValue("RefreshToken", out string? sToken)
            || !Guid.TryParse(sToken, out Guid token))
            return Problem(title: "There is no RefreshToken cookie", statusCode: StatusCodes.Status400BadRequest);

        RefreshToken? refreshToken = await context.RefreshTokens.FirstOrDefaultAsync(t => t.Id == token);

        if (refreshToken == null || refreshToken.Expires <= DateTime.UtcNow)
            return Problem(title: "Invalid or expired token", statusCode: StatusCodes.Status400BadRequest);

        if (refreshToken.DeviceUid != requestData.DeviceUid)
            return Problem(title: "Invalid token", detail: "The token was created by another client",
                statusCode: StatusCodes.Status400BadRequest);

        User user = await context.Users.FirstAsync(u => u.Id == refreshToken.UserId);
        string jwt = CreateJwtToken(user);
        
        RefreshToken newRefreshToken = new()
        {
            UserId = user.Id,
            Expires = DateTime.UtcNow + TimeSpan.FromDays(30),
            DeviceUid = requestData.DeviceUid
        };
        context.RefreshTokens.Remove(refreshToken);
        context.RefreshTokens.Add(newRefreshToken);
        await context.SaveChangesAsync();

        var response = new { AccessToken = jwt };

        Response.Cookies.Append("RefreshToken", newRefreshToken.Id.ToString(),
            new CookieOptions
            {
                HttpOnly = true,
                // Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.Now.Add(TimeSpan.FromDays(30))
            });

        return Ok(response);
    }

    [HttpPost("/logout"), Authorize]
    public async Task<IActionResult> Logout(
        [FromServices] ApplicationContext context,
        [FromServices] RequestData requestData)
    {
        if (!Request.Cookies.TryGetValue("RefreshToken", out string? sToken)
            || !Guid.TryParse(sToken, out Guid token))
            return Problem(title: "There is no RefreshToken cookie", statusCode: StatusCodes.Status400BadRequest);

        RefreshToken? refreshToken = await context.RefreshTokens.FirstOrDefaultAsync(t => t.Id == token);
        if (refreshToken == null)
            return Problem(title: "Invalid or expired token", statusCode: StatusCodes.Status400BadRequest);
        if (refreshToken.DeviceUid != requestData.DeviceUid)
            return Problem(title: "Invalid token", detail: "The token was created on another client",
                statusCode: StatusCodes.Status400BadRequest);

        context.RefreshTokens.Remove(refreshToken);
        await context.SaveChangesAsync();

        Response.Cookies.Delete("RefreshToken");

        return Ok();
    }

    string CreateJwtToken(User user)
    {
        var now = DateTime.UtcNow;

        var jwt = new JwtSecurityToken(
            issuer: jwtConfig.Issuer,
            notBefore: now,
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Name, user.Login),
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim("IsAdmin", user.IsAdmin.ToString()),
                //new Claim("roles", user.Role)
            },
            expires: now.Add(TimeSpan.FromHours(2)),
            signingCredentials: new SigningCredentials(jwtConfig.SecretKey,
                SecurityAlgorithms.HmacSha256));

        var encodedJwt = new JwtSecurityTokenHandler().WriteToken(jwt);
        return encodedJwt;
    }

    [HttpPost("/change-password"), Authorize]
    public async Task<IActionResult> ChangePassword(
        [FromServices] ApplicationContext context,
        [FromServices] RequestData requestData,
        [FromBody] ChangePasswordRequest model)
    {
        if (!Request.Cookies.TryGetValue("RefreshToken", out string? sRefreshToken)
            || !Guid.TryParse(sRefreshToken, out Guid refreshToken))
            return Problem(title: "There is no RefreshToken cookie", statusCode: StatusCodes.Status400BadRequest);

        string? password = await context.Users
            .Where(u => u.Id == requestData.UserId)
            .Select(u => u.Password)
            .FirstOrDefaultAsync();

        if (password is null)
            return Problem(title: "User not found", statusCode: StatusCodes.Status404NotFound);

        if (PasswordHasher.VerifyHashedPassword(null!, password, model.NewPassword)
            != PasswordVerificationResult.Failed)
            return Problem(title: "The old password does not match the current one",
                statusCode: StatusCodes.Status400BadRequest);

        await using var transaction = await context.Database.BeginTransactionAsync();
        
        int c = await context.Users
            .Where(u => u.Id == requestData.UserId)
            .BatchUpdateAsync(u => new User { Password = GetHashPassword(model.NewPassword!) });

        await context.RefreshTokens
            .Where(t => t.UserId == requestData.UserId && t.Id != refreshToken)
            .BatchDeleteAsync();

        await transaction.CommitAsync();

        return c > 0
            ? StatusCode(StatusCodes.Status204NoContent)
            : Problem(title: "Unknown error", statusCode: StatusCodes.Status404NotFound);
    }

    [HttpGet("/api/users"), Authorize]
    public async Task<IActionResult> GetUsers(
        [FromServices] ApplicationContext context,
        [FromServices] RequestData requestData)
    {
        if (requestData.IsAdmin is not true)
            return Problem(title: "You must be an admin to create a user", statusCode: StatusCodes.Status403Forbidden);

        var users = await context.Users
            .Include(u => u.TelegramBotUsers)
            .AsSplitQuery()
            .Select(u => new
            {
                u.Id, u.Login, u.IsAdmin,
                TelegramBotUsers = u.TelegramBotUsers
                    .Select(tu => new { tu.TelegramUserId, tu.Username })
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpPatch("/api/users/{id:int}"), Authorize]
    public async Task<IActionResult> EditUser(int id,
        [FromServices] ApplicationContext context,
        [FromServices] RequestData requestData,
        [FromBody] EditUserRequest model)
    {
        if (requestData.IsAdmin is not true)
            return Problem(title: "You must be an admin to edit user", statusCode: StatusCodes.Status403Forbidden);

        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
            return Problem(title: "User not found", statusCode: StatusCodes.Status404NotFound);

        if (model.IsFieldPresent(nameof(model.NewPassword))) 
            user.Password = GetHashPassword(model.NewPassword!);

        if (model.IsFieldPresent(nameof(model.IsAdmin)))
        {
            if (user.IsAdmin && model.IsAdmin is false)
            {
                bool isLastAdmin = await context.Users.AnyAsync(u => u.Id != id && u.IsAdmin == true);
                if (!isLastAdmin)
                    return Problem(title: "It is impossible to deprive the rights of a single admin",
                        statusCode: StatusCodes.Status400BadRequest);
            }

            user.IsAdmin = model.IsAdmin!.Value;
        }

        await context.SaveChangesAsync();

        if (model.IsFieldPresent(nameof(model.NewPassword)))
        {
            await context.RefreshTokens
                .Where(t => t.UserId == id)
                .BatchDeleteAsync();
        }

        return StatusCode(StatusCodes.Status204NoContent);
    }

    [HttpDelete("/api/users/{id:int}"), Authorize]
    public async Task<IActionResult> DeleteUser(int id,
        [FromServices] ApplicationContext context,
        [FromServices] ITelegramService telegramService,
        [FromServices] RequestData requestData)
    {
        if (requestData.IsAdmin is not true)
            return Problem(title: "You must be an admin to delete user", statusCode: StatusCodes.Status403Forbidden);

        if (requestData.UserId == id)
        {
            bool isLastAdmin = !await context.Users.AnyAsync(u => u.Id != id && u.IsAdmin == true);
            if (isLastAdmin)
                return Problem(title: "It is forbidden to delete a single administrator",
                    statusCode: StatusCodes.Status400BadRequest);
        }

        var telegramBotUsers = await context.TelegramBotUsers
            .Where(u => u.UserId == id)
            .Select(u => u.TelegramUserId).ToListAsync();

        int c = await context.Users.Where(u => u.Id == id).BatchDeleteAsync();
        
        if (c <= 0) return Problem(title: "User not found", statusCode: 404);
        
        if (!telegramService.IsOk) return StatusCode(204);

        foreach (var telegramBotUsersChunk in telegramBotUsers.Chunk(30 - 1))
        {
            foreach (var telegramBotUser in telegramBotUsersChunk)
            {
                await telegramService.BotClient!.SendTextMessageAsync(telegramBotUser,
                    "Был произведён выход из аккаунта ввиду удаления пользователя.");
            }

            await Task.Delay(1000);
        }

        return StatusCode(204);

    }

    [HttpDelete("/api/telegram/users/{id:int}"), Authorize]
    public async Task<IActionResult> LogoutTelegramBotUser(int id,
        [FromServices] ApplicationContext context,
        [FromServices] ITelegramService telegramService,
        [FromServices] RequestData requestData)
    {
        if (requestData.IsAdmin is not true)
            return Problem(title: "You must be an admin to edit user", statusCode: StatusCodes.Status403Forbidden);

        int c = await context.TelegramBotUsers.Where(u => u.TelegramUserId == id).BatchDeleteAsync();
        
        if (c <= 0) return Problem(title: "Telegram user not found", statusCode: 404);

        if (!telegramService.IsOk) return StatusCode(204);
        
        try
        {
            await telegramService.BotClient!.SendTextMessageAsync(id,
                "Был произведён выход из аккаунта.");
        }
        catch (ApiRequestException ex)
        {
            logger.LogError(ex, "Error sending notification to telegram user about logout");
        }

        return StatusCode(204);

    }
}
using StandServer.Services;
using Telegram.Bot;
using Telegram.Bot.Exceptions;

namespace StandServer.Controllers;

[ApiController]
public class UsersController : ControllerBase
{
    private readonly ILogger<UsersController> logger;

    public UsersController(ILogger<UsersController> logger) => this.logger = logger;
    
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
            Password = AuthController.GetHashPassword(model.Password!),
            IsAdmin = model.IsAdmin
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        return Ok(new
        {
            user.Id, user.Login, user.IsAdmin,
            TelegramBotUsers = Array.Empty<TelegramBotUser>()
        });
    }

    [HttpGet("/api/users"), Authorize]
    public async Task<IActionResult> GetUsers(
        [FromServices] ApplicationContext context,
        [FromServices] RequestData requestData)
    {
        if (requestData.IsAdmin is not true)
            return Problem(title: "You must be an admin to get users", statusCode: StatusCodes.Status403Forbidden);

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
            user.Password = AuthController.GetHashPassword(model.NewPassword!);

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
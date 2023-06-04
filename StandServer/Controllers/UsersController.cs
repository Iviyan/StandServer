using StandServer.Services;
using Telegram.Bot;
using Telegram.Bot.Exceptions;

namespace StandServer.Controllers;

/// <summary> A controller containing actions for managing users. </summary>
[ApiController]
public class UsersController : ControllerBase
{
    private readonly ILogger<UsersController> logger;
    private readonly IStringLocalizer<UsersController> localizer;

    public UsersController(ILogger<UsersController> logger, IStringLocalizer<UsersController> localizer)
    {
        this.logger = logger;
        this.localizer = localizer;
    }

    /// <summary> An administrator-accessible POST method, which creates a new user. </summary>
    [HttpPost("users"), Authorize(AuthPolicy.Admin)]
    public async Task<IActionResult> AddUser(
        [FromBody] RegisterModel model,
        [FromServices] ApplicationContext context)
    {
        if (await context.Users.AnyAsync(x => x.Login == model.Login))
            return Problem(title: localizer["AddUser.LoginAlreadyInUse"],
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

    /// <summary> An administrator-accessible GET method, which returns a list of all users. </summary>
    [HttpGet("users"), Authorize(AuthPolicy.Admin)]
    public async Task<IActionResult> GetUsers(
        [FromServices] ApplicationContext context)
    {
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

    /// <summary> An administrator-accessible PATCH method that edits the user with the given id. </summary>
    [HttpPatch("users/{id:int}"), Authorize(AuthPolicy.Admin)]
    public async Task<IActionResult> EditUser(int id,
        [FromServices] ApplicationContext context,
        [FromBody] EditUserModel model)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        if (model.IsFieldPresent(nameof(model.NewPassword))) 
            user.Password = AuthController.GetHashPassword(model.NewPassword!);

        if (model.IsFieldPresent(nameof(model.IsAdmin)))
        {
            if (user.IsAdmin && model.IsAdmin is false)
            {
                bool isLastAdmin = await context.Users.AnyAsync(u => u.Id != id && u.IsAdmin == true);
                if (!isLastAdmin)
                    return Problem(title: localizer["EditUser.SingleAdministrator"],
                        statusCode: StatusCodes.Status400BadRequest);
            }

            user.IsAdmin = model.IsAdmin!.Value;
        }

        await context.SaveChangesAsync();

        if (model.IsFieldPresent(nameof(model.NewPassword)))
        {
            await context.RefreshTokens.Where(t => t.UserId == id).ExecuteDeleteAsync();
        }

        return Ok();
    }

    /// <summary> An administrator-accessible DELETE method that removes the user with the given id. </summary>
    [HttpDelete("users/{id:int}"), Authorize(AuthPolicy.Admin)]
    public async Task<IActionResult> DeleteUser(int id,
        [FromServices] ApplicationContext context,
        [FromServices] ITelegramService telegramService,
        [FromServices] RequestData requestData)
    {
        if (requestData.UserId == id)
        {
            bool isLastAdmin = !await context.Users.AnyAsync(u => u.Id != id && u.IsAdmin == true);
            if (isLastAdmin)
                return Problem(title: localizer["DeleteUser.SingleAdministrator"],
                    statusCode: StatusCodes.Status400BadRequest);
        }

        var linkedTelegramAccounts = await context.TelegramBotUsers
            .Where(u => u.UserId == id)
            .Select(u => u.TelegramUserId).ToListAsync();

        int c = await context.Users.Where(u => u.Id == id).ExecuteDeleteAsync();

        if (c <= 0) return NotFound();
        
        if (!telegramService.IsOk) return Ok();

        foreach (var chunk in linkedTelegramAccounts.Chunk(30 - 1))
        {
            foreach (long telegramBotUserId in chunk)
            {
                await telegramService.BotClient!.SendTextMessageAsync(telegramBotUserId,
                    "Был произведён выход из аккаунта ввиду удаления пользователя.");
            }

            await Task.Delay(1000);
        }

        return Ok();

    }

    /// <summary> An administrator-accessible DELETE method that removes the telegram bot user with the given id (not user id). </summary>
    [HttpDelete("telegram-users/{id:int}"), Authorize(AuthPolicy.Admin)]
    public async Task<IActionResult> LogoutTelegramBotUser(int id,
        [FromServices] ApplicationContext context,
        [FromServices] ITelegramService telegramService)
    {
        int c = await context.TelegramBotUsers.Where(u => u.TelegramUserId == id).ExecuteDeleteAsync();

        if (c <= 0) return NotFound();
        if (!telegramService.IsOk) return Ok();
        
        try
        {
            await telegramService.BotClient!.SendTextMessageAsync(id,
                "Был произведён выход из аккаунта.");
        }
        catch (ApiRequestException ex)
        {
            logger.LogError(ex, "Error sending notification to telegram user about logout");
        }

        return Ok();

    }
}
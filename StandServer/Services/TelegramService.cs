﻿using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;
using StandServer.Configuration;
using StandServer.Controllers;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramUser = Telegram.Bot.Types.User;
using User = StandServer.Models.User;

namespace StandServer.Services;

/// <summary> Basic functionality of telegram service. </summary>
public interface ITelegramService
{
    /// <summary> Is the telegram service available. (Is the configuration set) </summary>
    bool IsOk => BotClient is { };

    /// <summary> TelegramBotClient instance. </summary>
    TelegramBotClient? BotClient { get; }

    /// <summary> Send message to telegram chanel with state of <paramref name="measurements"/>. </summary>
    Task SendAlarm(params Measurement[] measurements);
}

/// <summary> Extension methods for setting up telegram service in an <see cref="IServiceCollection" />. </summary>
public static class TelegramServiceExtension
{
    /// <summary> Adds services required to work with telegram. </summary>
    public static IServiceCollection AddTelegramService(this IServiceCollection services)
    {
        services.AddSingleton<ITelegramService, TelegramService>();
        services.AddHostedService(p => (TelegramService)p.GetRequiredService<ITelegramService>());
        return services;
    }
}

/// <summary> A background task that supports the functioning of the bot
/// and provides functionality for sending notifications in a telegram. </summary>
[SuppressMessage("ReSharper", "ConvertTypeCheckPatternToNullCheck")]
public class TelegramService : BackgroundService, ITelegramService
{
    private readonly ILogger<TelegramService> logger;
    private readonly NotificationsConfig notificationsConfig;
    private readonly IServiceProvider serviceProvider;
    private readonly IServiceScope scope;
    private readonly CachedData cachedData;

    private const string DateTimeFormat = "dd.MM.yyyy HH:mm:ss";

    public TelegramBotClient? BotClient { get; private set; }
    public bool IsOk => BotClient is { };

    public TelegramService(
        ILogger<TelegramService> logger,
        IOptions<NotificationsConfig> notificationsOptions,
        IServiceProvider serviceProvider,
        CachedData cachedData)
    {
        this.logger = logger;
        this.notificationsConfig = notificationsOptions.Value;
        this.serviceProvider = serviceProvider;
        this.cachedData = cachedData;
        scope = serviceProvider.CreateScope();
    }

    private ApplicationContext context = null!;

    /// <summary> Verifying telegram configuration, the validity of the token and start receiving telegram bot events. </summary>
    /// <exception cref="ConfigurationException">An exception that is called if there are errors in the configuration file.</exception>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("TelegramService started");

        try
        {
            if (notificationsConfig.Telegram is not { } config || String.IsNullOrWhiteSpace(config.Token))
            {
                logger.LogError("Telegram configuration is not defined, notifications will not be sent");
                return;
            }

            BotClient = new(new TelegramBotClientOptions(
                config.Token,
                String.IsNullOrWhiteSpace(config.ApiUrl) ? null : config.ApiUrl
            ));

            try
            {
                using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(10));
                var requestCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, timeoutCts.Token);
                _ = await BotClient.GetChatAsync(config.ChannelId, requestCts.Token);
            }
            catch (ApiRequestException e)
                when (e.ErrorCode is 400 or 401 or 404)
            {
                logger.LogError("Telegram token is invalid");
                throw new ConfigurationException("Telegram token is invalid");
            }

            context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            BotClient!.StartReceiving(
                updateHandler: HandleUpdateAsync,
                pollingErrorHandler: HandlePollingErrorAsync,
                receiverOptions: new()
                {
                    AllowedUpdates = new[]
                    {
                        UpdateType.Message,
                        UpdateType.CallbackQuery,
                    }
                },
                cancellationToken: stoppingToken);
        }
        finally
        {
            logger.LogInformation("TelegramService initialization completed");
        }
    }

    /// <summary> Logging of pooling errors. </summary>
    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception,
        CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        logger.LogError(exception, "Telegram polling error:\n{ErrorMessage}", errorMessage);
        return Task.CompletedTask;
    }

    /// <summary> Calling the appropriate event handling method and handling exceptions. </summary>
    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (update)
            {
                case { Message: { Chat.Type: ChatType.Private } message }:
                {
                    try
                    {
                        await HandleNewMessageAsync(update, message, cancellationToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogError(ex, "Error handling telegram bot command\n{ErrorMessage}", ex.Message);
                        await BotClient!.SendTextMessageAsync(message.Chat, "Ошибка выполнения команды",
                            cancellationToken: cancellationToken);
                    }

                    break;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Error handling telegram bot event");
        }
    }

    /// <summary> Handling the message received by the bot </summary>
    private async Task HandleNewMessageAsync(Update update, Message message,
        CancellationToken cancellationToken)
    {
        if (message.Text is not string text) return;
        if (message.From is not TelegramUser sender) return;

        (string command, string? arg) = ParseCommand(text);

        (User? user, bool init) senderUser = (null, false);

        async Task<User?> GetUser()
        {
            if (senderUser.init) return senderUser.user;
            senderUser = (await context.TelegramBotUsers.AsNoTracking()
                .Where(u => u.TelegramUserId == sender.Id)
                .Include(u => u.User)
                .Select(u => new User
                {
                    Id = u.User!.Id, Login = u.User!.Login,
                    IsAdmin = u.User!.IsAdmin
                })
                .FirstOrDefaultAsync(cancellationToken), true);
            return senderUser.user;
        }

        async Task<User?> CheckAuth()
        {
            User? currentUser = await GetUser();
            if (currentUser == null)
                await ReplyByTextMessage($"Для выполнения команды необходимо авторизоваться.");
            return currentUser;
        }

        Task<Message> ReplyByTextMessage(string msg) => BotClient!.SendTextMessageAsync(message.Chat, msg,
            cancellationToken: cancellationToken);

        Task<Message> ReplyByTextMessageV2(string msg) => BotClient!.SendTextMessageAsync(message.Chat, msg,
            parseMode: ParseMode.MarkdownV2, cancellationToken: cancellationToken);

        if (command == "/start")
        {
            await BotClient!.SetMyCommandsAsync(BotCommandsEn, cancellationToken: cancellationToken);
            await BotClient!.SetMyCommandsAsync(BotCommandsRu, languageCode: "ru",
                cancellationToken: cancellationToken);

            await ReplyByTextMessage(StartCommandText);
        }
        else if (command == "/getuserid")
        {
            await ReplyByTextMessage(sender.Id.ToString());
        }
        else if (command == "/login")
        {
            int spacePos = (arg ?? "").IndexOf(' ');
            if (spacePos == -1)
            {
                await ReplyByTextMessage("Неверный формат команды");
                return;
            }

            var (login, password) = (arg![..spacePos], arg[(spacePos + 1)..]);

            User? currentUser = await GetUser();

            User? authUser = await context.Users.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Login == login, cancellationToken);
            if (authUser == null)
            {
                await ReplyByTextMessage("Неверный логин");
                return;
            }

            var passwordVerificationResult = AuthController.PasswordHasher.VerifyHashedPassword(null!,
                authUser.Password, password);
            if (passwordVerificationResult == PasswordVerificationResult.Failed)
            {
                await ReplyByTextMessage("Неверный пароль");
                return;
            }

            if (currentUser != null)
            {
                await context.TelegramBotUsers.Where(u => u.TelegramUserId == sender.Id)
                    .ExecuteUpdateAsync(p => p.SetProperty(
                            u => u.UserId, _ => authUser.Id),
                        cancellationToken: cancellationToken);
            }
            else
            {
                context.TelegramBotUsers.Add(new()
                {
                    TelegramUserId = sender.Id,
                    Username = sender.Username,
                    UserId = authUser.Id
                });
                await context.SaveChangesAsync(cancellationToken);
                context.ChangeTracker.Clear();
            }

            await ReplyByTextMessageV2($"Успешная авторизация\\.\n*{login}* \\- _{sender.Id}_\\.");
        }
        else if (command == "/logout")
        {
            if (await CheckAuth() is not User currentUser) return;

            await context.TelegramBotUsers.Where(u => u.TelegramUserId == sender.Id)
                .ExecuteDeleteAsync(cancellationToken);

            await ReplyByTextMessageV2("*Выход*");
        }
        else if (command == "/getlink")
        {
            if (await CheckAuth() is not User currentUser) return;

            ChatInviteLink link = await BotClient!.CreateChatInviteLinkAsync(notificationsConfig.Telegram!.ChannelId,
                name: sender.Username is { } username
                    ? $"{username.Truncate(32 - 13)}'s invitation"
                    : $"User {sender.Id} invitation",
                expireDate: DateTime.Now.AddHours(1),
                memberLimit: 1,
                cancellationToken: cancellationToken);

            await BotClient!.SendTextMessageAsync(message.Chat, link.InviteLink,
                cancellationToken: cancellationToken);
        }
        else if (command == "/state")
        {
            if (await CheckAuth() is not User currentUser) return;

            var connection = context.Database.GetDbConnection();
            
            List<int> sampleIds = new();
            foreach (var lastStandTime in cachedData.LastStandMeasurementTime)
            {
                sampleIds.AddRange(
                    cachedData.LastSampleMeasurements.Values
                        .Where(m => m.Time == lastStandTime.Value && m.StandId == lastStandTime.Key)
                        .Select(m => m.SampleId)
                );
            }

            var measurements = (await connection.QueryAsync<Measurement>(
                $"select * from get_last_measurements(@count, @sampleIds);",
                new { count = 1, sampleIds })).ToArray();

            string MeasurementToString(Measurement m) =>
                $"*{m.SampleId}* \\(_{m.State} \\| {SecondsToInterval(m.SecondsFromStart)}_\\) \\~ I: {m.I}, t: {m.T}";

            string samplesStateText = measurements.Length == 0
                ? "Данные отсутствуют"
                : string.Join("\n", measurements.GroupBy(m => m.StandId).Select(standGroup =>
                    $"*Стенд {standGroup.Key}*\n"
                    + standGroup.First().Time.ToLocalTime().ToString(DateTimeFormat).Replace(".", @"\.")
                    + "\n" + String.Join('\n', standGroup.Select(MeasurementToString))
                ));

            await BotClient!.SendTextMessageAsync(message.Chat, samplesStateText,
                parseMode: ParseMode.MarkdownV2, cancellationToken: cancellationToken);
        }
        else if (command == "/test")
        {
            await ReplyByTextMessage(message.Text["/test".Length..].Trim());
        }
    }

    private const string StartCommandText = """
                                            Для работы с ботом необходимо авторизоваться.

                                            /login [username] [password] - авторизация
                                            /logout - выход
                                            /getlink - канал с уведомлениями стенда
                                            /state - состояние образцов
                                            """;

    private static readonly BotCommand[] BotCommandsRu =
    {
        new() { Command = "/start", Description = "Описание бота и список команд" },
        new() { Command = "/login", Description = "Вход в аккаунт" },
        new() { Command = "/state", Description = "Состояние образцов" },
        new() { Command = "/logout", Description = "Выход" },
        new() { Command = "/getlink", Description = "Канал с уведомлениями стенда" },
        new() { Command = "/getuserid", Description = "Получить id пользователя" },
    };

    private static readonly BotCommand[] BotCommandsEn =
    {
        new() { Command = "/start", Description = "Bot description and list of commands" },
        new() { Command = "/login", Description = "Login" },
        new() { Command = "/state", Description = "Get samples state" },
        new() { Command = "/logout", Description = "Logout" },
        new() { Command = "/getlink", Description = "Channel with stand notifications" },
        new() { Command = "/getuserid", Description = "Get user id" },
    };

    public async Task SendAlarm(params Measurement[] measurements)
    {
        string GetAlarmMsg(Measurement measurement) =>
            $"*{measurement.SampleId}* \\(_{measurement.State}_\\) \\~ I: {measurement.I}, t: {measurement.T}\n"
            + $"{measurement.Time.ToLocalTime().ToString(DateTimeFormat).Replace(".", @"\.")} \\| {SecondsToInterval(measurement.SecondsFromStart)}";

        await BotClient!.SendTextMessageAsync(notificationsConfig.Telegram!.ChannelId,
            String.Join('\n', measurements.Select(GetAlarmMsg)),
            parseMode: ParseMode.MarkdownV2);
    }

    private static string SecondsToInterval(int s) => $"{(s / 3600 | 0).ToString().PadLeft(2, '0')}"
                                                      + $":{(s % 3600 / 60 | 0).ToString().PadLeft(2, '0')}"
                                                      + $":{(s % 60).ToString().PadLeft(2, '0')}";

    /// <summary> Splitting a line with a first space. </summary>
    private static (string command, string? arg) ParseCommand(string s)
    {
        int spacePos = s.IndexOf(' ');
        if (spacePos == -1) return (s, null);
        return (s[..spacePos], s[(spacePos + 1)..]);
    }

    /// <summary> Dispose base and IServiceScope. </summary>
    public override void Dispose()
    {
        base.Dispose();
        scope.Dispose();
    }
}
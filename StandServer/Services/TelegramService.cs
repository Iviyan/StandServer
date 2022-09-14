using StandServer.Configuration;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace StandServer.Services;

public interface ITelegramService
{
    bool IsOk => BotClient is { };
    TelegramBotClient? BotClient { get; }
    Task SendAlarm(Measurement measurement);

    Task ExecuteIfOk(Func<TelegramBotClient, Task> action) 
        => BotClient is { } ? action(BotClient) : Task.CompletedTask;
}

public static class MqttServiceExtension
{
    public static IServiceCollection AddTelegramService(this IServiceCollection services)
    {
        services.AddSingleton<ITelegramService, TelegramService>();
        services.AddHostedService(p => (TelegramService)p.GetRequiredService<ITelegramService>());
        return services;
    }
}

public class TelegramService : BackgroundService, ITelegramService
{
    private readonly ILogger<TelegramService> logger;
    private readonly NotificationsConfig notificationsConfig;
    private readonly IServiceProvider serviceProvider;

    public TelegramBotClient? BotClient { get; private set; }
    public bool IsOk => BotClient is { };

    public TelegramService(
        ILogger<TelegramService> logger,
        IOptions<NotificationsConfig> notificationsOptions,
        IServiceProvider serviceProvider)
    {
        this.logger = logger;
        this.notificationsConfig = notificationsOptions.Value;
        this.serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("TelegramService started");

        try
        {
            if (notificationsConfig.Telegram is not { } config || String.IsNullOrWhiteSpace(config.Token))
            {
                logger.LogError("Telegram configuration is not defined, notifications will not be sent.");
                return;
                //throw new ConfigurationException("Telegram configuration is not defined");
            }

            BotClient = new(config.Token);

            try
            {
                _ = await BotClient.GetChatAsync(config.ChannelId, stoppingToken);
            }
            catch (ApiRequestException e)
                when (e.ErrorCode is 400 or 401 or 404)
            {
                logger.LogError("Telegram token is invalid.");
                throw new ConfigurationException("Telegram token is invalid");
            }

            ExecuteCoreAsync(stoppingToken);
        }
        finally
        {
            logger.LogInformation("TelegramService stopped");
        }
    }

    private void ExecuteCoreAsync(CancellationToken stoppingToken)
    {
        BotClient!.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandlePollingErrorAsync,
            receiverOptions: new(),
            cancellationToken: stoppingToken);
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception,
        CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        logger.LogError(exception, errorMessage);
        return Task.CompletedTask;
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        switch (update)
        {
            case { Message: { } message } when message.Text is { } text:
            {
                if (text.StartsWith("/test"))
                {
                    await BotClient!.SendTextMessageAsync(message.Chat, message.Text["/test".Length..].Trim(),
                        cancellationToken: cancellationToken);
                }
            } break;
        }
    }

    public async Task SendAlarm(Measurement measurement)
    {
        await BotClient!.SendTextMessageAsync(notificationsConfig.Telegram!.ChannelId,
            $"*{measurement.SampleId}* \\(_{measurement.State}_\\) \\~ I: {measurement.I}, t: {measurement.T}\n" +
            $"{measurement.Time.ToString(CultureInfo.CurrentCulture).Replace(".", @"\.")} \\| {SecondsToInterval(measurement.SecondsFromStart)}\n",
            parseMode: ParseMode.MarkdownV2);
    }
    
    static string SecondsToInterval(int s) => $"{(s / 3600 | 0).ToString().PadLeft(2, '0') }" +
                                              $":{(s % 3600 / 60 | 0).ToString().PadLeft(2, '0')}" +
                                              $":{(s % 60).ToString().PadLeft(2, '0')}";
}
namespace StandServer.Configuration;

/// <summary> Model for notification configuration. </summary>
public class NotificationsConfig
{
    /// <summary> Section name in JSON. </summary>
    public const string SectionName = "Notifications";

    /// <summary> Telegram configuration. </summary>
    public TelegramConfig? Telegram { get; set; }
}

/// <summary> Model for telegram bot and notifications configuration. </summary>
public class TelegramConfig
{
    /// <summary> Telegram bot token. </summary>
    public string Token { get; set; } = null!;

    /// <summary> Telegram bot api url. </summary>
    public string? ApiUrl { get; set; }

    /// <summary> Telegram channel id for sending notifications. </summary>
    public string ChannelId { get; set; } = null!;
}
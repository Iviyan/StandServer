namespace StandServer.Configuration;

public class NotificationsConfig
{
    public const string SectionName = "Notifications";
    
    public TelegramConfig? Telegram { get; set; }
}

public class TelegramConfig
{
    public string Token { get; set; } = null!;
    public string ChannelId { get; set; } = null!;
}
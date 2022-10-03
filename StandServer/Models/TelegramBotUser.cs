namespace StandServer.Models;

[Table("telegram_bot_users")]
public class TelegramBotUser
{
    [Key, Column("telegram_user_id")] public long TelegramUserId { get; set; }
    [Column("user_id")] public int UserId { get; set; }
    public User? User { get; set; }
    [Column("username")] public string? Username { get; set; }
}
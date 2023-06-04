namespace StandServer.Models;

/// <summary> Model for the "telegram_bot_users" table. </summary>
[Table("telegram_bot_users")]
public class TelegramBotUser
{
    /// <summary> Id of the telegram user. (PK) </summary>
    [Key, Column("telegram_user_id")] public long TelegramUserId { get; set; }

    /// <summary> Id of the user under which the telegram user logged in. </summary>
    [Column("user_id")] public int UserId { get; set; }

    /// <summary> The user under which the telegram user logged in. </summary>
    public User? User { get; set; }

    /// <summary> Telegram username at the time of authorization. </summary>
    [Column("username")] public string? Username { get; set; }
}
namespace StandServer.Models;

[Table("refresh_tokens")]
public class RefreshToken
{
    [Column("id")] public Guid Id { get; set; }
    [Column("user_id")] public int UserId { get; set; }
    public User? User { get; set; }
    [Column("device_uid")] public Guid DeviceUid { get; set; }
    [Column("expires")] public DateTime Expires { get; set; }
    public bool IsExpired => DateTime.UtcNow >= Expires;
}
namespace StandServer.Models;

/// <summary> Model for the "refresh_tokens" table. </summary>
[Table("refresh_tokens")]
public class RefreshToken
{
    /// <summary> Refresh token uid. (PK) </summary>
    [Column("id")] public Guid Id { get; set; }

    /// <summary> Id of the user who owns the token. </summary>
    [Column("user_id")] public int UserId { get; set; }

    /// <summary> User who owns the token. (Navigation property) </summary>
    public User? User { get; set; }

    /// <summary> Random uid that is generated during authorization. </summary>
    [Column("device_uid")] public Guid DeviceUid { get; set; }

    /// <summary> Token expiration date. </summary>
    [Column("expires")] public DateTime Expires { get; set; }

    /// <summary> Has the token expired. </summary>
    public bool IsExpired => DateTime.UtcNow >= Expires;
}
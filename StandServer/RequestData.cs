namespace StandServer;

/// <summary> Scoped service that stores request data. </summary>
public class RequestData
{
    /// <summary> User id in case of an authorized request. </summary>
    public int? UserId { get; set; }

    /// <summary> User login in case of an authorized request. </summary>
    public string? UserLogin { get; set; }

    /// <summary> Device Uid (authorization id) in case of an authorized request. </summary>
    public Guid? DeviceUid { get; set; }

    /// <summary> Whether the user is an administrator in case of an authorized request. </summary>
    public bool? IsAdmin { get; set; }
}
namespace StandServer;

public class RequestData
{
    public int? UserId { get; set; }
    public string? UserLogin { get; set; }
    public Guid DeviceUid { get; set; }
    public bool IsAdmin { get; set; }
}
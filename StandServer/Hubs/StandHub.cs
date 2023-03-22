namespace StandServer.Hubs;

public class StandHub : Hub<IStandHubClient>
{
    public const string MeasurementsGroup = "Measurements";
    
    private readonly CachedData cachedData;

    public StandHub(CachedData cachedData)
    {
        this.cachedData = cachedData;
    }
    
    public async Task Msg(string message)
    {
        await Clients.All.Msg(message);
    }
    
    public async Task SubscribeToMeasurements() => await Groups.AddToGroupAsync(Context.ConnectionId, MeasurementsGroup);
    public async Task UnsubscribeFromMeasurements() => await Groups.RemoveFromGroupAsync(Context.ConnectionId, MeasurementsGroup);

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.ActiveInfo(cachedData.LastMeasurementTime);
        await base.OnConnectedAsync();
    }
}

public interface IStandHubClient
{
    Task Msg(string msg, CancellationToken cancellationToken = default);
    Task ActiveInfo(DateTime? lastMeasurementTime, CancellationToken cancellationToken = default);
    Task NewMeasurements(IEnumerable<Measurement> measurements, CancellationToken cancellationToken = default);
}
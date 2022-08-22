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
        await Clients.Caller.StateChange(cachedData.State?.State ?? false);
        await base.OnConnectedAsync();
    }
}

public interface IStandHubClient
{
    Task Msg(string msg, CancellationToken cancellationToken = default);
    Task StateChange(bool newState, CancellationToken cancellationToken = default);
    Task NewMeasurement(int sampleId, Measurement measurement, CancellationToken cancellationToken = default);
}
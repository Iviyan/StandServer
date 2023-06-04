namespace StandServer.Hubs;

/// <summary> SignalR hub for mass distribution of measurements in real time. </summary>
public class StandHub : Hub<IStandHubClient>
{
    /// <summary> Name of the group that will receive measurements. </summary>
    public const string MeasurementsGroup = "Measurements";

    private readonly CachedData cachedData;

    public StandHub(CachedData cachedData)
    {
        this.cachedData = cachedData;
    }

    /// <summary> Test method called by the client to send a text message to all clients. </summary>
    public Task Msg(string message) => Clients.All.Msg(message);

    /// <summary> The method called by the client to subscribe to receive measurements. </summary>
    public Task SubscribeToMeasurements() => Groups.AddToGroupAsync(Context.ConnectionId, MeasurementsGroup);

    /// <summary> The method called by the client to unsubscribe to receive measurements. </summary>
    public Task UnsubscribeFromMeasurements() => Groups.RemoveFromGroupAsync(Context.ConnectionId, MeasurementsGroup);

    /// <summary> Method called when a new client connects. The date of the last measurement is sent automatically. </summary>
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.ActiveInfo(cachedData.LastMeasurementTime);
        await base.OnConnectedAsync();
    }
}

/// <summary> An interface that defines which methods the client handles. </summary>
public interface IStandHubClient
{
    /// <summary> Test method that prints the received text to the console. </summary>
    Task Msg(string msg, CancellationToken cancellationToken = default);

    /// <summary> Method that updates information about the last measurement date. </summary>
    Task ActiveInfo(DateTime? lastMeasurementTime, CancellationToken cancellationToken = default);

    /// <summary> Method that handles new measurements. </summary>
    Task NewMeasurements(IEnumerable<Measurement> measurements, CancellationToken cancellationToken = default);
}
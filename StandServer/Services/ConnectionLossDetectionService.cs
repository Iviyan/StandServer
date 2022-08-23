using StandServer.Configuration;

namespace StandServer.Services;

public class ConnectionLossDetectionService : BackgroundService
{
    private readonly ILogger<ConnectionLossDetectionService> logger;
    private readonly IServiceProvider serviceProvider;
    private readonly CachedData cachedData;
    private readonly IHubContext<StandHub, IStandHubClient> hubContext;
    private readonly StandInfo standInfo;

    private readonly PeriodicTimer timer;

    private const int MeasurementIntervalError = 10; // seconds

    public ConnectionLossDetectionService(
        ILogger<ConnectionLossDetectionService> logger,
        IServiceProvider serviceProvider,
        CachedData cachedData,
        IHubContext<StandHub, IStandHubClient> hubContext,
        IOptions<StandInfo> standInfo)
    {
        this.logger = logger;
        this.serviceProvider = serviceProvider;
        this.cachedData = cachedData;
        this.hubContext = hubContext;
        this.standInfo = standInfo.Value;
        
        timer = new(TimeSpan.FromSeconds(10));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ConnectionLossDetectionService started");
        
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        
        while (await timer.WaitForNextTickAsync(stoppingToken)
               && !stoppingToken.IsCancellationRequested)
        {
            if (cachedData.State is null or { State: false} || !cachedData.LastActiveTime.HasValue ) continue;
            
            logger.LogDebug($"Connection loss checking... ({DateTime.Now:O})");

            if (DateTime.Now > cachedData.LastActiveTime.Value
                    .AddSeconds(standInfo.MeasurementInterval + MeasurementIntervalError))
            {
                logger.LogInformation($"Connection loss detected... ({DateTime.Now:O})");
                
                await cachedData.StateChangeLock.WaitAsync(stoppingToken);
                try
                {
                    StateHistory state = new()
                    {
                        State = false,
                        Time = cachedData.LastActiveTime.Value.RoundToSeconds().AddSeconds(1).GetKindUtc()
                    };
                    context.StateHistory.Add(state);
                    await context.SaveChangesAsync(stoppingToken);
                    await hubContext.Clients.All.StateChange(false, stoppingToken);
                    cachedData.State = state;
                }
                catch (Exception ex) { logger.LogError(ex, "Connection loss handling error"); }
                finally { cachedData.StateChangeLock.Release(); }
            }
        }
        
        logger.LogInformation("ConnectionLossDetectionService stopped");
    }
}
namespace StandServer.Services;

public class LoadCacheService : BackgroundService
{
    private readonly IServiceProvider serviceProvider;
    private readonly CachedData cachedData;
    private readonly ILogger<LoadCacheService> logger;

    public LoadCacheService(IServiceProvider serviceProvider, CachedData cachedData, ILogger<LoadCacheService> logger)
    {
        this.serviceProvider = serviceProvider;
        this.cachedData = cachedData;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation($"Load cache start...");

        using var scope = serviceProvider.CreateScope();
        var efContext = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        await using var con = efContext.Database.GetDbConnection();

        cachedData.SampleIds = new(
            await con.QueryAsync<int>(new  CommandDefinition(
                @"select * from get_unique_sample_ids()", cancellationToken: stoppingToken)));

        logger.LogInformation($"> Sample ids loaded");

        cachedData.State = await efContext.StateHistory.AsNoTracking()
            .OrderByDescending(e => e.Time)
            .Take(1).SingleOrDefaultAsync(stoppingToken);

        if (cachedData.State is { State: true })
        {
            logger.LogWarning("The last state of the stand is enabled, i.e. the server has shut down incorrectly.");
            
            var lastMeasurement = await efContext.Measurements.AsNoTracking()
                .Where(m => m.Time >= cachedData.State.Time)
                .OrderByDescending(m => m.Time)
                .FirstOrDefaultAsync(stoppingToken);

            StateHistory state = new()
            {
                State = false,
                Time = lastMeasurement is { }
                    ? lastMeasurement.Time.RoundToSeconds().AddSeconds(1)
                    : cachedData.State.Time.RoundToSeconds().AddSeconds(1)
            };
            efContext.StateHistory.Add(state);
            await efContext.SaveChangesAsync(stoppingToken);
            cachedData.State = state;  
            
            logger.LogInformation($"A record of the state of the stand has been created based on the last" +
                                  $" {(lastMeasurement is { } ? "measurement" : "state")}");
        }
        logger.LogInformation($"> Last state loaded");

        logger.LogInformation($"Load cache end...");
    }
}

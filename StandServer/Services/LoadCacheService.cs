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

        cachedData.LastMeasurementTime = await efContext.Measurements.AsNoTracking()
            .OrderByDescending(e => e.Time)
            .Take(1).Select(m => (DateTime?)m.Time).SingleOrDefaultAsync(stoppingToken);
        
        logger.LogInformation($"> Last measurement time loaded");

        logger.LogInformation($"Load cache end...");
    }
}

namespace StandServer.Services;

/// <summary> Loading frequently used data into the <see cref="CachedData">cache</see> </summary>
public class CacheLoader
{
    private readonly IServiceProvider serviceProvider;
    private readonly CachedData cachedData;
    private readonly ILogger<CacheLoader> logger;

    public CacheLoader(IServiceProvider serviceProvider, CachedData cachedData, ILogger<CacheLoader> logger)
    {
        this.serviceProvider = serviceProvider;
        this.cachedData = cachedData;
        this.logger = logger;
    }

    /// <inheritdoc cref="CacheLoader"/>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation($"Load cache start...");

        using var scope = serviceProvider.CreateScope();
        var efContext = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

        var lastMeasurements = await efContext.Measurements
            .FromSqlRaw("select * from get_last_measurements(1);")
            .AsNoTracking()
            .ToListAsync(cancellationToken: cancellationToken);

        foreach (var measurement in lastMeasurements)
            cachedData.LastSampleMeasurements[measurement.SampleId] = measurement;

        logger.LogInformation($"> Last sample measurements loaded");

        cachedData.LastStandMeasurementTime = cachedData.LastSampleMeasurements.Values.GroupBy(m => m.StandId)
            .ToDictionary(g => g.Key, g => g.Max(m => m.Time));
        
        logger.LogInformation($"Load cache end...");
    }
}
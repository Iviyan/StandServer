namespace StandServer.Services;

public class ClearOldRefreshTokensService : BackgroundService
{
    private readonly ILogger<ClearOldRefreshTokensService> logger;
    private readonly IServiceProvider serviceProvider;
    private readonly IServiceScope serviceScope;
    private readonly ApplicationContext context;

    private readonly PeriodicTimer timer = new(TimeSpan.FromHours(2));
    
    public ClearOldRefreshTokensService(ILogger<ClearOldRefreshTokensService> logger, IServiceProvider serviceProvider)
    {
        this.logger = logger;
        this.serviceProvider = serviceProvider;
        
        serviceScope = serviceProvider.CreateScope();
        context = serviceScope.ServiceProvider.GetRequiredService<ApplicationContext>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ClearOldRefreshTokensService started");

        do
        {
            logger.LogDebug("Old refresh tokens deleting... ({Now})", DateTime.Now);

            var now = DateTime.UtcNow;
            await context.RefreshTokens.Where(t => now >= t.Expires).ExecuteDeleteAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken)
                 && !stoppingToken.IsCancellationRequested);
        
        logger.LogInformation("ClearOldRefreshTokensService stopped");
    }

    public override void Dispose()
    {
        base.Dispose();
        serviceScope.Dispose();
    }
}
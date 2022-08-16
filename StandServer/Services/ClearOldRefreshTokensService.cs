namespace StandServer.Services;

public class ClearOldRefreshTokensService : BackgroundService
{
    private readonly ILogger<ClearOldRefreshTokensService> logger;
    private readonly IServiceProvider serviceProvider;

    private readonly PeriodicTimer timer = new(TimeSpan.FromHours(2));


    public ClearOldRefreshTokensService(ILogger<ClearOldRefreshTokensService> logger, IServiceProvider serviceProvider)
    {
        this.logger = logger;
        this.serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ClearOldRefreshTokensService started");
        
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        
        while (await timer.WaitForNextTickAsync(stoppingToken)
               && !stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation($"Old refresh tokens deleting... ({DateTime.Now:O})");

            var now = DateTime.Now.GetKindUtc();
            await context.RefreshTokens
                .Where(t => now >= t.Expires)
                .BatchDeleteAsync(stoppingToken);
        }
        
        logger.LogInformation("ClearOldRefreshTokensService stopped");
    }
}
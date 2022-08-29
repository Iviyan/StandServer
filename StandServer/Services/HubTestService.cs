namespace StandServer.Services;

public class HubTestService : BackgroundService
{
    private readonly ILogger<HubTestService> logger;
    private readonly IHubContext<StandHub, IStandHubClient> hubContext;

    private readonly PeriodicTimer timer = new(TimeSpan.FromSeconds(10));


    public HubTestService(ILogger<HubTestService> logger, IHubContext<StandHub, IStandHubClient> hubContext)
    {
        this.logger = logger;
        this.hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("HubTestService started");

        while (await timer.WaitForNextTickAsync(stoppingToken)
               && !stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation($"Hub send data... ({DateTime.Now:O})");

            //await hubContext.Clients.All.SendAsync("msg", $"{DateTime.Now:O}", stoppingToken);
            await hubContext.Clients.Group(StandHub.MeasurementsGroup).NewMeasurement(1,
                new Measurement
                {
                    SampleId = 1,
                    Time = DateTime.UtcNow,
                    SecondsFromStart = 60,
                    DutyCycle = 20,
                    T = (short)Random.Shared.Next(10, 50),
                    Tu = 50,
                    I = (short)Random.Shared.Next(6900, 7500),
                    Period = 1000,
                    Work = 50,
                    Relax = 10,
                    Frequency = 10000,
                    State = true
                },
                stoppingToken);
        }

        logger.LogInformation("HubTestService stopped");
    }
}
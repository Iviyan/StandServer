//#define SERVER

using System.Net.Mime;
using System.Text;

#if SERVER
const string url = "http://192.168.1.23/api/samples";
#else
const string url = "http://localhost:5161/api/samples";
#endif

const int samplesCount = 4;
const int interval = 5;

HttpClient client = new();
PeriodicTimer timer = new(TimeSpan.FromSeconds(interval));
CancellationTokenSource cts = new(TimeSpan.FromMinutes(60 * 16));
CancellationToken ct = cts.Token;

const int secondsFromStart = 0;

Measurement[] samples = Enumerable.Range(1, samplesCount)
    .Select(i => new Measurement { SampleId = 100 + i, SecondsFromStart = secondsFromStart - interval})
    .ToArray();

Console.CancelKeyPress += (_, _) => cts.Cancel();
try
{
    do
    {
        DateTime now = DateTime.Now;
        foreach (var sample in samples)
        {
            sample.Time = now;
            sample.SecondsFromStart += interval;
            int cycleTime = sample.Work * 60 + sample.Relax * 60;
            sample.State = sample.SecondsFromStart % cycleTime <= sample.Work * 60 ? SampleState.Work : SampleState.Relax;
            sample.T = (short)(sample.State == SampleState.Work
                ? Random.Shared.Next(45, 50)
                : Random.Shared.Next(20, 35));
            sample.I = (short)(sample.State == SampleState.Work
                ? Random.Shared.Next(7000, 8000)
                : Random.Shared.Next(0, 10) != 0
                    ? Random.Shared.Next(8, 15)
                    : Random.Shared.Next(100, 8000));
        }

        string measurements = String.Join('\n', samples.Select(GetMeasurementString));
        var result = await client.PostAsync(url,
            new StringContent(measurements, Encoding.Default, "text/plain"));

        Console.WriteLine(measurements);
        Console.WriteLine($"> {result.StatusCode:D} - {await result.Content.ReadAsStringAsync()}");
    } while (await timer.WaitForNextTickAsync(ct));
}
catch (OperationCanceledException) { }

// Ctrl+C
await Task.Delay(TimeSpan.FromSeconds(2));

{
    DateTime now = DateTime.Now;
    foreach (var sample in samples)
    {
        sample.Time = now;
        sample.State = SampleState.Off;
        sample.T = (short)Random.Shared.Next(20, 35);
        sample.I = (short)(Random.Shared.Next(0, 10) != 0
                ? Random.Shared.Next(8, 15)
                : Random.Shared.Next(100, 8000));
    }

    string measurements = String.Join('\n', samples.Select(GetMeasurementString));
    var result = await client.PostAsync(url,
        new StringContent(measurements, Encoding.Default,  MediaTypeNames.Text.Plain));
    
    Console.WriteLine(measurements);
    Console.WriteLine($"> {result.StatusCode:D} - {await result.Content.ReadAsStringAsync()}");
}



static string GetMeasurementString(Measurement m)
    => $"{m.SampleId:D8} {m.Time:HH:mm:ss dd.MM.yyyy}|{SecondsToInterval(m.SecondsFromStart)}|{m.DutyCycle}|" +
       $"{m.T}|{m.Tu}|{m.I}|{m.Period}|{m.Work}|{m.Relax}|{m.Frequency}|{m.State.ToString("G")[0]}";

static string SecondsToInterval(int s) => $"{(s / 60 / 60).ToString().PadLeft(2, '0')}" +
                                          $":{(s / 60).ToString().PadLeft(2, '0')}" +
                                          $":{(s % 60).ToString().PadLeft(2, '0')}";

public enum SampleState { Off, Work, Relax }

public class Measurement
{
    public int SampleId { get; set; }
    public DateTime Time { get; set; }
    public int SecondsFromStart { get; set; } = 0;
    public short DutyCycle { get; set; } = 10;
    public short T { get; set; }
    public short Tu { get; set; } = 50;
    public short I { get; set; }
    public short Period { get; set; } = 1000;
    public short Work { get; set; } = 5;
    public short Relax { get; set; } = 1;
    public short Frequency { get; set; } = 10000;
    public SampleState State { get; set; } = SampleState.Work;
}
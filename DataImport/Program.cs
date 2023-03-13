using System.Globalization;
using System.Net.Mime;
using System.Text;

const string url = "http://192.168.1.23/api/samples?silent=true";

Console.ReadLine();

HttpClient client = new();

var files = Directory.EnumerateFiles("data");
foreach (string file in files)
{
    var lines = File.ReadAllLines(file);

    var validMeasurements = lines.Skip(1)
        .Select(ParseRawMeasurement)
        .Where(m => m is { })
        .Cast<Measurement>();

    string measurementsString = String.Join('\n', validMeasurements.Select(GetMeasurementString));

    Console.WriteLine(measurementsString);

    var response = await client.PostAsync(url,
        new StringContent(measurementsString, Encoding.Default, MediaTypeNames.Text.Plain));

    Console.WriteLine(response);
}


Measurement? ParseRawMeasurement(string str)
{
    if (str.Length < 49) return null;

    ReadOnlySpan<char> s = str.AsSpan();
    int nextSeparatorIndex, valueStartIndex;

    if (!int.TryParse(s[..8], out int sampleId)) return null;
    if (!DateTime.TryParseExact(s[9..25], "HH:mm dd.MM.yyyy",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time)) return null;

    nextSeparatorIndex = str.IndexOf('|', 26);
    var timeFromStartParts = str[26..nextSeparatorIndex].Split(':');
    if (timeFromStartParts.Length != 2) return null;
    if (!int.TryParse(timeFromStartParts[0], out int hoursFromStart)) return null;
    if (!int.TryParse(timeFromStartParts[1], out int hourMinutesFromStart)) return null;
    int minutesFromStart = hoursFromStart * 60 + hourMinutesFromStart;

    bool ParseNext(ReadOnlySpan<char> s, out short val)
    {
        val = 0;
        valueStartIndex = nextSeparatorIndex + 1;
        nextSeparatorIndex = str.IndexOf('|', valueStartIndex);
        if (nextSeparatorIndex == -1) return false;
        return short.TryParse(s[valueStartIndex..nextSeparatorIndex].Trim(' '), out val);
    }

    if (!(ParseNext(s, out short dutyCycle)
          && ParseNext(s, out short t)
          && ParseNext(s, out short tu)
          && ParseNext(s, out short i)
          && ParseNext(s, out short period)
          && ParseNext(s, out short work)
          && ParseNext(s, out short relax)
          && ParseNext(s, out short frequency)
        )) return null;

    if ((valueStartIndex = nextSeparatorIndex + 1) >= str.Length) return null;
    SampleState? state = null;
    var stateRaw = s[valueStartIndex..].Trim(' ');
    if (stateRaw.SequenceEqual("OFF")) state = SampleState.Off;
    if (stateRaw.SequenceEqual("ON")) 
        state = i < 100 ? SampleState.Relax : SampleState.Work;
    if (state is null) return null;

    /*Console.WriteLine($"Number: {number:d8}");
    Console.WriteLine($"DateTime: {dateTime}");
    Console.WriteLine($"Seconds from start: {secondsFromStart}");
    Console.WriteLine($"DutyCycle (%): {dutyCycle}");
    Console.WriteLine($"t (*C): {t}");
    Console.WriteLine($"tu (*C): {tu}");
    Console.WriteLine($"I (mA): {i}");
    Console.WriteLine($"Period (us): {period}");
    Console.WriteLine($"Work (min): {work}");
    Console.WriteLine($"Relax (min): {relax}");
    Console.WriteLine($"Frequency (GHz): {frequency}");
    Console.WriteLine($"Status: {state}");*/

    Measurement measurement = new()
    {
        SampleId = sampleId,
        Time = time.ToUniversalTime(),
        MinutesFromStart = minutesFromStart,
        DutyCycle = dutyCycle,
        T = t,
        Tu = tu,
        I = i,
        Period = period,
        Work = work,
        Relax = relax,
        Frequency = frequency,
        State = state.Value
    };

    return measurement;
}

static string GetMeasurementString(Measurement m)
    => $"{m.SampleId:D8} {m.Time:HH:mm dd.MM.yyyy}|{MinutesToInterval(m.MinutesFromStart)}|{m.DutyCycle}|" +
       $"{m.T}|{m.Tu}|{m.I}|{m.Period}|{m.Work}|{m.Relax}|{m.Frequency}|{m.State.ToString("G")[0]}";

static string MinutesToInterval(int m) => $"{(m / 60 | 0).ToString().PadLeft(2, '0')}" +
                                          $":{(m % 60 | 0).ToString().PadLeft(2, '0')}";

public enum SampleState { Off, Work, Relax }

public class Measurement
{
    public int SampleId { get; set; }
    public DateTime Time { get; set; }
    public int MinutesFromStart { get; set; } = 0;
    public short DutyCycle { get; set; } = 10;
    public short T { get; set; }
    public short Tu { get; set; } = 50;
    public short I { get; set; }
    public short Period { get; set; } = 1000;
    public short Work { get; set; } = 50;
    public short Relax { get; set; } = 10;
    public short Frequency { get; set; } = 10000;
    public SampleState State { get; set; } = SampleState.Work;
}
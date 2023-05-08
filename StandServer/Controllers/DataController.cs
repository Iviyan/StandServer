using CsvHelper.Configuration;
using StandServer.Services;

namespace StandServer.Controllers;

[ApiController, Authorize]
public class DataController : Controller
{
    private readonly ILogger<DataController> logger;

    public DataController(ILogger<DataController> logger)
    {
        this.logger = logger;
    }

    [HttpGet("samples")]
    public IActionResult GetSamplesList([FromServices] CachedData data)
    {
        return Ok(data.SampleIds /*.Select(id => $"{id:D8}")*/);
    }

    [HttpPost("samples"), Consumes(MediaTypeNames.Text.Plain), AllowAnonymous]
    public async Task<IActionResult> AddMeasurements(
        [FromServices] ApplicationContext context, [FromServices] CachedData data,
        [FromServices] IHubContext<StandHub, IStandHubClient> standHub,
        [FromServices] ITelegramService telegramService,
        [FromBody] string raw, [FromQuery] bool silent = false)
    {
        data.LastActiveTime = DateTime.UtcNow;
        
        List<Measurement> measurements = new();
        foreach (string measurementRaw in raw.GetLines(removeEmptyLines: true))
        {
            Measurement? measurement = ParseRawMeasurement(measurementRaw);

            if (measurement is null)
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid format");

            measurements.Add(measurement);
        }

        if (measurements.Count == 0)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Empty input");

        if (silent)
        {
            context.AddRange(measurements);
            await context.SaveChangesAsync();
            foreach (int sampleId in measurements.Select(m => m.SampleId).Distinct())
                data.SampleIds.Add(sampleId);
            return Ok();
        }

        data.LastMeasurementTime = measurements.MaxBy(m => m.Time)!.Time;

        if (!measurements.Select(m => m.SampleId).All(new HashSet<int>().Add)) // check duplicates
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "To add multiple measurements at once for one sample, use the silent parameter");
        
        context.AddRange(measurements);
        await context.SaveChangesAsync();

        foreach (var measurement in measurements)
            data.SampleIds.Add(measurement.SampleId);

        if (telegramService.IsOk)
        {
            var badMeasurements = measurements
                .Where(m => m is { State: not SampleState.Work, I: >= 200 })
                .ToArray();
            
            if (badMeasurements.Any())
                await telegramService.SendAlarm(badMeasurements);
        }

        await standHub.Clients.All.NewMeasurements(measurements);

        return Ok();
    }

    [HttpGet("samples/{id:int}")]
    public async Task<IActionResult> GetMeasurements(int id,
        [FromServices] ApplicationContext context, [FromServices] CachedData data,
        DateTime? from = null, DateTime? to = null)
    {
        from.SetKindUtc() ??= DateTime.MinValue;
        to.SetKindUtc() ??= DateTime.MaxValue;

        if (!data.SampleIds.Contains(id))
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "There are no measurements with this id");
        
        var measurements = await context.Measurements.AsNoTracking()
            .Where(m => m.SampleId == id && m.Time >= from && m.Time <= to)
            .OrderBy(m => m.Time).Cast<IIndependentMeasurement>().ToListAsync();

        return Ok(measurements);
    }

    [HttpDelete("samples/{id:int}")]
    public async Task<IActionResult> DeleteSampleMeasurements(int id,
        [FromServices] ApplicationContext context, [FromServices] CachedData data)
    {
        if (!data.SampleIds.Contains(id))
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "There are no measurements with this id");

        int delCount = await context.Measurements.Where(m => m.SampleId == id).ExecuteDeleteAsync();
        data.SampleIds.Remove(id);

        await context.Database.ExecuteSqlRawAsync("VACUUM ANALYZE measurements");

        return delCount > 0 ? Ok() : BadRequest();
    }

    [HttpGet("samples/last")]
    public async Task<IActionResult> GetLastMeasurements(
        [FromServices] DatabaseContext context,
        [FromQuery(Name = "sample_ids")] string sampleIdsRaw = "active", int count = 20)
    {
        if (count <= 0)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Count must be greater than 0");

        IEnumerable<Measurement> measurements;

        if (sampleIdsRaw == "active")
        {
            var sampleIds = await context.Connection.QueryAsync<int>(
                $"select sample_id from measurements " +
                $"where time = (select time from measurements order by time desc limit 1)");

            measurements = await context.Connection.QueryAsync<Measurement>(
                $"select * from get_last_measurements(@count, @sampleIds);",
                new { count, sampleIds });
        }
        else if (String.IsNullOrWhiteSpace(sampleIdsRaw) || sampleIdsRaw == "*")
        {
            measurements = await context.Connection.QueryAsync<Measurement>(
                $"select * from get_last_measurements(@count);",
                new { count });
        }
        else
        {
            List<int>? sampleIds = new();
            foreach (var sampleIdRaw in sampleIdsRaw.Split(','))
            {
                if (int.TryParse(sampleIdRaw, out int sampleId))
                    sampleIds.Add(sampleId);
                else
                {
                    sampleIds = null;
                    break;
                }
            }

            if (sampleIds is null)
                return Problem(statusCode: StatusCodes.Status400BadRequest,
                    title: """Invalid sample_ids format, valid format: comma-separated enumeration, "*", "active" (default).""");

            measurements = await context.Connection.QueryAsync<Measurement>(
                $"select * from get_last_measurements(@count, @sampleIds);",
                new { count, sampleIds });
        }

        return Ok(measurements
            .GroupBy(m => m.SampleId)
            .ToDictionary(g => g.Key, g => g.AsEnumerable()));
    }

    class SamplePeriod
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
    }

    [HttpGet("samples/{id:int}/period")]
    public async Task<IActionResult> GetSamplePeriod(int id,
        [FromServices] DatabaseContext context, [FromServices] CachedData data)
    {
        if (!data.SampleIds.Contains(id))
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "There are no measurements with this id");

        SamplePeriod period = (await context.Connection.QueryAsync<SamplePeriod>(
            @"select * from get_sample_period(@id);",
            new { id })).Single();

        return Ok(period);
    }

    [HttpGet("samples/{id:int}/csv")]
    public async Task<IActionResult> GetMeasurementsCsv(int id,
        [FromServices] ApplicationContext context, [FromServices] CachedData data,
        DateTime? from = null, DateTime? to = null)
    {
        from ??= DateTime.MinValue;
        to ??= DateTime.MaxValue;

        if (!data.SampleIds.Contains(id))
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "There are no measurements with this id");

        var measurements = await context.Measurements.AsNoTracking()
            .Where(m => m.SampleId == id && m.Time >= from && m.Time <= to)
            .OrderBy(m => m.Time).ToListAsync();
        
        MemoryStream stream = new();
        using var writer = new StreamWriter(stream);
        using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
               {
                   Delimiter = ";" // excel
               }, leaveOpen: true))
        {
            csv.Context.RegisterClassMap<MeasurementMap>();
            csv.WriteRecords(measurements);
        }

        return File(stream.ToArray(), "text/csv",
            $"{id:D8} [{from:dd.MM.yyyy HH.mm.ss} - {to:dd.MM.yyyy HH.mm.ss}].csv");
    }

    Measurement? ParseRawMeasurement(string str)
    {
        //00000123 12:01 01.01.2023|0:01| 10|39|50|7213|1000| 50| 10|10000|W
        if (str.Length < 41) return null;
        
        ReadOnlySpan<char> s = str.AsSpan();
        
        int valueStartIndex = 0, nextSeparatorIndex = -1;
        bool TryNext(char separator = ' ')
        {
            valueStartIndex = nextSeparatorIndex + 1;
            nextSeparatorIndex = str.IndexOf(separator, valueStartIndex);
            return nextSeparatorIndex != -1;
        }

        if (!TryNext()) return null;
        if (!int.TryParse(s[..nextSeparatorIndex], out int sampleId)) return null;

        if (!TryNext()) return null;
        if (!TimeOnly.TryParse(s[valueStartIndex..nextSeparatorIndex],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly timePart)) return null;
        if (!TryNext('|')) return null;
        if (!DateOnly.TryParseExact(s[valueStartIndex..nextSeparatorIndex], "dd.MM.yyyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly datePart)) return null;
        DateTime dateTime = datePart.ToDateTime(timePart);

        nextSeparatorIndex = str.IndexOf('|', valueStartIndex);
        if (!TryNext('|')) return null;
        var timeFromStartParts = str[valueStartIndex..nextSeparatorIndex].Split(':');

        int secondsFromStart;
        if (timeFromStartParts.Length is >= 2 and <= 3)
        {
            if (!int.TryParse(timeFromStartParts[0], out int hours)) return null;
            if (!int.TryParse(timeFromStartParts[1], out int minutes)) return null;
            int seconds = 0;
            if (timeFromStartParts.Length == 3 && !int.TryParse(timeFromStartParts[2], out seconds)) return null;
            secondsFromStart = (hours * 60 + minutes) * 60 + seconds;
        }
        else return null;

        bool ParseNext(ReadOnlySpan<char> s, out short val)
        {
            val = 0;
            if (!TryNext('|')) return false;
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
        if (stateRaw.SequenceEqual("O")) state = SampleState.Off;
        if (stateRaw.SequenceEqual("W")) state = SampleState.Work;
        if (stateRaw.SequenceEqual("R")) state = SampleState.Relax;
        if (state is null) return null;

        /*Console.WriteLine($"Number: {sampleId}");
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
            Time = dateTime.ToUniversalTime(),
            SecondsFromStart = secondsFromStart,
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
}
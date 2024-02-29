using CsvHelper.Configuration;
using StandServer.Services;

// ReSharper disable SimplifyLinqExpressionUseAll

namespace StandServer.Controllers;

/// <summary> A controller containing actions for managing measurements data. </summary>
[ApiController, Authorize]
public class DataController : Controller
{
    private readonly ILogger<DataController> logger;
    private readonly IStringLocalizer<DataController> localizer;

    public DataController(ILogger<DataController> logger, IStringLocalizer<DataController> localizer)
    {
        this.logger = logger;
        this.localizer = localizer;
    }

    /// <summary> A user-accessible GET method for getting a list of all sample ids. </summary>
    [HttpGet("samples")]
    public IActionResult GetSamplesList([FromServices] CachedData cachedData)
    {
        return Ok(cachedData.LastSampleMeasurements.Keys);
    }

    /// <summary> A POST method for receiving the status of active samples or importing measurements. </summary>
    /// <param name="raw">Measurements in text format.</param>
    /// <param name="silent">If this parameter is set, the measurements are imported without sending notifications
    /// and updating the data on the site in real time. Required for importing measurements.</param>
    /// <param name="standId">Overrides the stand id in measurements.</param>
    [HttpPost("samples"), Consumes(MediaTypeNames.Text.Plain), AllowAnonymous]
    public async Task<IActionResult> AddMeasurements(
        [FromServices] ApplicationContext context, [FromServices] CachedData cachedData,
        [FromServices] IHubContext<StandHub, IStandHubClient> standHub,
        [FromServices] ITelegramService telegramService,
        [FromServices] IApplicationConfiguration applicationConfiguration,
        [FromBody] string raw, [FromQuery] bool silent = false, [FromQuery] short? standId = null)
    {
        cachedData.LastActiveTime = DateTime.UtcNow;

        List<Measurement> measurements = new();
        foreach (string measurementRaw in raw.GetLines(removeEmptyLines: true))
        {
            Measurement? measurement = ParseRawMeasurement(measurementRaw);

            if (measurement is null)
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: localizer["AddMeasurements.InvalidFormat"]);

            if (standId.HasValue) measurement.StandId = standId.Value;

            measurements.Add(measurement);
        }

        if (measurements.Count == 0)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: localizer["AddMeasurements.EmptyInput"]);

        for (int i = 0; i < measurements.Count; i++)
        {
            int sampleId = measurements[i].SampleId;
            DateTime time = measurements[i].Time;

            bool checkDuplicatesFail = false;
            for (int j = 0; j < measurements.Count && j != i; j++)
            {
                var cMeasurement = measurements[j];
                checkDuplicatesFail = cMeasurement.SampleId == sampleId && cMeasurement.Time == time;
                if (checkDuplicatesFail) break;
            }

            if (checkDuplicatesFail)
            {
                string error = localizer.GetString("AddMeasurements.Duplicates", sampleId, time.ToLocalTime());
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: error);
            }
        }

        if (silent)
        {
            context.AddRange(measurements);
            await context.SaveChangesAsync();
            var lastSampleMeasurements = measurements.GroupBy(m => m.SampleId)
                .Select(g => (sampleId: g.Key, measurement: g.MaxBy(m => m.Time)!));

            foreach (var lastSampleMeasurement in lastSampleMeasurements)
            {
                if (cachedData.LastSampleMeasurements.TryAdd(lastSampleMeasurement.sampleId, lastSampleMeasurement.measurement)) continue;

                var measurement = cachedData.LastSampleMeasurements[lastSampleMeasurement.sampleId];
                if (lastSampleMeasurement.measurement.Time > measurement.Time)
                    cachedData.LastSampleMeasurements[lastSampleMeasurement.sampleId] = lastSampleMeasurement.measurement;
            }

            await standHub.Clients.All.ActiveInfo(cachedData.LastActiveTime, cachedData.LastStandMeasurementTime); // ? 
            return Ok();
        }

        if (!measurements.Select(m => m.SampleId).All(new HashSet<int>().Add)) // check duplicates
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: localizer["AddMeasurements.UseSilent"]);

        var measurementsTime = measurements[0].Time;
        if (measurements.Any(m => m.Time != measurementsTime)) // time of all measurements must be same
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: localizer["AddMeasurements.MeasurementsTimeIsDifferent"]);

        var measurementsStandId = measurements[0].StandId;
        if (measurements.Any(m => m.StandId != measurementsStandId)) // stand id of all measurements must be same
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: localizer["AddMeasurements.MeasurementsStandIdIsDifferent"]);

        cachedData.LastStandMeasurementTime[measurementsStandId] = measurementsTime;

        context.AddRange(measurements);
        await context.SaveChangesAsync();

        foreach (var measurement in measurements)
            cachedData.LastSampleMeasurements[measurement.SampleId] = measurement;

        if (telegramService.IsOk)
        {
            var badMeasurements = measurements
                .Where(m => m.State != SampleState.Work && m.I >= applicationConfiguration.OffSampleMaxI)
                .ToArray();

            if (badMeasurements.Any())
                await telegramService.SendAlarm(badMeasurements);
        }

        await standHub.Clients.All.NewMeasurements(measurements);
        await standHub.Clients.All.ActiveInfo(cachedData.LastActiveTime, cachedData.LastStandMeasurementTime);

        return Ok();
    }

    /// <summary> A user-accessible GET method for getting measurements for the specified period. </summary>
    /// <param name="sampleId">Sample id</param>
    /// <param name="from">IIf not set, then unlimited.</param>
    /// <param name="to">If not set, then unlimited.</param>
    [HttpGet("samples/{sampleId:int}")]
    public async Task<IActionResult> GetMeasurements(int sampleId,
        [FromServices] ApplicationContext context, [FromServices] CachedData cachedData,
        DateTime? from = null, DateTime? to = null)
    {
        from.SetKindUtc() ??= DateTime.MinValue;
        to.SetKindUtc() ??= DateTime.MaxValue;

        if (!cachedData.LastSampleMeasurements.ContainsKey(sampleId))
            return Problem(statusCode: StatusCodes.Status404NotFound, title: localizer["NoMeasurements"]);

        var measurements = await context.Measurements.AsNoTracking()
            .Where(m => m.SampleId == sampleId && m.Time >= from && m.Time <= to)
            .OrderBy(m => m.Time).Cast<IIndependentMeasurement>().ToListAsync();

        return Ok(measurements);
    }

    /// <summary> An admin-accessible DELETE method that removes sample with all its measurements. </summary>
    /// <param name="sampleId">Sample id</param>
    [HttpDelete("samples/{sampleId:int}"), Authorize(AuthPolicy.Admin)]
    public async Task<IActionResult> DeleteSampleMeasurements(int sampleId,
        [FromServices] ApplicationContext context,
        [FromServices] IHubContext<StandHub, IStandHubClient> standHub,
        [FromServices] CachedData cachedData)
    {
        if (!cachedData.LastSampleMeasurements.ContainsKey(sampleId))
            return Problem(statusCode: StatusCodes.Status404NotFound, title: localizer["NoMeasurements"]);

        int delCount = await context.Measurements.Where(m => m.SampleId == sampleId).ExecuteDeleteAsync();
        cachedData.LastSampleMeasurements.Remove(sampleId);

        cachedData.LastStandMeasurementTime = cachedData.LastSampleMeasurements.Values.GroupBy(m => m.StandId)
            .ToDictionary(g => g.Key, g => g.Max(m => m.Time));

        await standHub.Clients.All.ActiveInfo(cachedData.LastActiveTime, cachedData.LastStandMeasurementTime);

        await context.Database.ExecuteSqlRawAsync("VACUUM ANALYZE measurements");

        return delCount > 0 ? Ok() : BadRequest();
    }

    /// <summary> A user-accessible GET method for getting a list of last sample measurements. </summary>
    /// <param name="sampleIdsRaw">Either "active" or "*", or comma-separated sample numbers.</param>
    /// <param name="count">The number of recent measurements of each sample.</param>
    [HttpGet("samples/last")]
    public async Task<IActionResult> GetLastMeasurements(
        [FromServices] DatabaseContext context,
        [FromServices] CachedData cachedData,
        [FromQuery(Name = "sample_ids")] string sampleIdsRaw = "active", int count = 20)
    {
        if (count <= 0)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: localizer["GetLastMeasurements.IncorrectCount"]);

        IEnumerable<Measurement> measurements;

        if (sampleIdsRaw == "active")
        {
            List<int> sampleIds = new();
            foreach (var lastStandTime in cachedData.LastStandMeasurementTime)
            {
                sampleIds.AddRange(
                    cachedData.LastSampleMeasurements.Values
                        .Where(m => m.Time == lastStandTime.Value && m.StandId == lastStandTime.Key)
                        .Select(m => m.SampleId)
                );
            }

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
                    title: localizer["GetLastMeasurements.InvalidSampleIdsFormat"]);

            measurements = await context.Connection.QueryAsync<Measurement>(
                $"select * from get_last_measurements(@count, @sampleIds);",
                new { count, sampleIds });
        }

        /*
         * If the sample's stand id changes, then until enough new measurements appear,
         * the sample measurements will be partially included in both stands.
         */

        return Ok(measurements
            .GroupBy(m => m.StandId)
            .ToDictionary(
                standGroup => standGroup.Key,
                standGroup => standGroup
                    .GroupBy(m => m.SampleId)
                    .ToDictionary(
                        sampleGroup => sampleGroup.Key,
                        sampleGroup => sampleGroup.AsEnumerable())
            ));
    }

    /// <summary> An admin-accessible GET method that reload cache from database. </summary>
    [HttpGet("reload-cache"), Authorize(AuthPolicy.Admin)]
    public async Task<IActionResult> ReloadCache([FromServices] CacheLoader cacheLoader)
    {
        await cacheLoader.LoadAsync();
        return Ok();
    }

    /// <summary> A model for obtaining and displaying the measurement period of a sample. </summary>
    class SamplePeriod
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
    }

    /// <summary> A user-accessible GET method for getting the measurement period of the sample. </summary>
    /// <param name="id">Sample id</param>
    [HttpGet("samples/{sampleId:int}/period")]
    public async Task<IActionResult> GetSamplePeriod(int sampleId,
        [FromServices] DatabaseContext context, [FromServices] CachedData cachedData)
    {
        if (!cachedData.LastSampleMeasurements.ContainsKey(sampleId))
            return Problem(statusCode: StatusCodes.Status404NotFound, title: localizer["NoMeasurements"]);

        SamplePeriod period = (await context.Connection.QueryAsync<SamplePeriod>(
            @"select * from get_sample_period(@id);",
            new { id = sampleId })).Single();

        return Ok(period);
    }

    /// <summary> A user-accessible GET method to download sample measurements for a given period in csv format. </summary>
    /// <param name="id">Sample id</param>
    /// <param name="from">IIf not set, then unlimited.</param>
    /// <param name="to">If not set, then unlimited.</param>
    [HttpGet("samples/{sampleId:int}/csv")]
    public async Task<IActionResult> GetMeasurementsCsv(int sampleId,
        [FromServices] ApplicationContext context, [FromServices] CachedData cachedData,
        DateTime? from = null, DateTime? to = null)
    {
        from ??= DateTime.MinValue;
        to ??= DateTime.MaxValue;

        if (!cachedData.LastSampleMeasurements.ContainsKey(sampleId))
            return Problem(statusCode: StatusCodes.Status404NotFound, title: localizer["NoMeasurements"]);

        var measurements = await context.Measurements.AsNoTracking()
            .Where(m => m.SampleId == sampleId && m.Time >= from && m.Time <= to)
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
            $"{sampleId:D8} [{from:dd.MM.yyyy HH.mm.ss} - {to:dd.MM.yyyy HH.mm.ss}].csv");
    }

    /// <summary> A user-accessible GET method to download all measurements in csv format. </summary>
    [HttpGet("samples/csv")]
    public async Task<IActionResult> GetMeasurementsCsv(
        [FromServices] ApplicationContext context)
    {
        // TODO: use streams
        var measurements = await context.Measurements.AsNoTracking()
            .OrderBy(m => m.Time).ToListAsync();

        var from = measurements.First().Time;
        var to = measurements.Last().Time;

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
            $"[{from:dd.MM.yyyy HH.mm.ss} - {to:dd.MM.yyyy HH.mm.ss}].csv");
    }

    /// <summary> Parse measurement from text format to <see cref="Measurement"/> class. </summary>
    /// <param name="str">One measurement in text format</param>
    /// <returns><see cref="Measurement"/> on success, otherwise null.</returns>
    public static Measurement? ParseRawMeasurement(string str)
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
        if (!short.TryParse(s[..nextSeparatorIndex], out short standId)) return null;

        if (!TryNext()) return null;
        if (int.TryParse(s[valueStartIndex..nextSeparatorIndex], out int sampleId))
        {
            if (!TryNext()) return null;
        }
        else
        {
            sampleId = standId;
            standId = 1;
        }

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

        Measurement measurement = new()
        {
            StandId = standId,
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
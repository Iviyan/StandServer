namespace StandServer.Controllers;

[ApiController, Route("/api"), Authorize]
public class DataController : Controller
{
    [HttpGet("samples")]
    public IActionResult GetSamplesList([FromServices] CachedData data)
    {
        return Ok(data.SampleIds /*.Select(id => $"{id:D8}")*/);
    }

    [HttpPost("state-history/{newStateRaw:regex(^on$|^off$)}")]
    public async Task<IActionResult> SetStateOn(string newStateRaw,
        [FromServices] ApplicationContext context, [FromServices] CachedData data,
        [FromServices] IHubContext<StandHub, IStandHubClient> standHub)
    {
        await data.StateChangeLock.WaitAsync();

        bool newState = newStateRaw.ToLowerInvariant() == "on";

        try
        {
            var currentState = data.State;

            if (currentState is { } && currentState.State == newState)
                return Problem(statusCode: StatusCodes.Status404NotFound, title: $"Stand is already {(currentState.State ? "on" : "off")}");

            StateHistory stateRecord = new() { State = newState, Time = DateTime.Now.GetKindUtc().RoundToSeconds() };

            context.StateHistory.Add(stateRecord);
            await context.SaveChangesAsync();

            data.State = stateRecord;
            data.LastActiveTime = DateTime.Now.GetKindUtc().RoundToSeconds();

            await standHub.Clients.Group(StandHub.MeasurementsGroup).StateChange(newState);

            return Ok();
        }
        finally { data.StateChangeLock.Release(); }
    }

    record WorkPeriod(DateTime From, DateTime? To);

    [HttpGet("state-history")]
    public IActionResult GetStateHistory(
        [FromServices] ApplicationContext context)
    {
        var stateHistoryRaw = context.StateHistory.AsNoTracking().AsEnumerable();

        List<WorkPeriod> workPeriods = new();
        using (var iterator = stateHistoryRaw.GetEnumerator())
        {
            while (iterator.MoveNext())
            {
                var from = iterator.Current;
                var to = iterator.MoveNext() ? iterator.Current : null;
                workPeriods.Add(new(from.Time, to?.Time));
            }
        }

        return Ok(workPeriods);
    }

    [HttpPost("samples"), Consumes("application/json"), AllowAnonymous]
    public async Task<IActionResult> AddMeasurement(
        [FromServices] ApplicationContext context, [FromServices] CachedData data,
        [FromServices] IHubContext<StandHub, IStandHubClient> standHub,
        [FromBody] Measurement measurement, [FromQuery] bool silent = false)
    {
        measurement.Time = measurement.Time.GetKindUtc();

        if (silent)
        {
            context.Measurements.Add(measurement);
            await context.SaveChangesAsync();
            data.SampleIds.Add(measurement.SampleId);
            return Ok();
        }

        await data.StateChangeLock.WaitAsync();
        bool lockReleased = false;

        try
        {
            StateHistory? newState = null;

            if (data.State is { State: false } or null)
            {
                newState = new() { State = true, Time = measurement.Time };
                context.StateHistory.Add(newState);
            }
            else
            {
                data.StateChangeLock.Release();
                lockReleased = true;
            }

            context.Measurements.Add(measurement);
            await context.SaveChangesAsync();

            data.SampleIds.Add(measurement.SampleId);

            if (newState is { })
            {
                data.State = newState;
                await standHub.Clients.All.StateChange(newState.State);
            }

            data.LastActiveTime = DateTime.Now;

            await standHub.Clients.Group(StandHub.MeasurementsGroup).NewMeasurement(measurement.SampleId, measurement);

            return Ok();
        }
        finally
        {
            if (!lockReleased) data.StateChangeLock.Release();
        }
    }

    [HttpPost("samples"), Consumes("text/plain"), AllowAnonymous]
    public async Task<IActionResult> AddMeasurement(
        [FromServices] ApplicationContext context, [FromServices] CachedData data,
        [FromServices] IHubContext<StandHub, IStandHubClient> standHub,
        [FromBody] string raw, [FromQuery] bool silent = false)
    {
        List<Measurement> measurements = new();
        foreach (string measurementRaw in raw.GetLines())
        {
            Measurement? measurement = ParseRawMeasurement(measurementRaw);

            if (measurement is null)
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid format");

            measurements.Add(measurement); //context.Measurements.Add(measurement);
        }

        if (measurements.Count == 0)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Empty input");

        if (silent)
        {
            await context.BulkInsertAsync(measurements);
            foreach (int sampleId in measurements.Select(m => m.SampleId).Distinct())
                data.SampleIds.Add(sampleId);
            return Ok();
        }

        await data.StateChangeLock.WaitAsync();
        bool lockReleased = false;

        try
        {
            StateHistory? newState = null;

            if (data.State is { State: false } or null)
            {
                newState = new() { State = true, Time = measurements[0].Time }; //DateTime.Now.GetKindUtc() };
                context.StateHistory.Add(newState);
            }
            else
            {
                data.StateChangeLock.Release();
                lockReleased = true;
            }

            await context.BulkInsertAsync(measurements);

            foreach (int sampleId in measurements.Select(m => m.SampleId).Distinct())
                data.SampleIds.Add(sampleId);

            if (newState is { })
            {
                await context.SaveChangesAsync();
                data.State = newState;
                await standHub.Clients.All.StateChange(newState.State);
            }

            foreach (var measurement in measurements)
                await standHub.Clients.All.NewMeasurement(measurement.SampleId, measurement);

            data.LastActiveTime = DateTime.Now;

            return Ok();
        }
        finally
        {
            if (!lockReleased) data.StateChangeLock.Release();
        }
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

        /*var measurements = await context.Connection.QueryAsync<Measurement>(
            $"select * from measurements where sample_id = @sampleId and time >= @from and time <= @to order by time",
            new { id, from, to });*/
        var measurements = await context.Measurements.AsNoTracking()
            .Where(m => m.SampleId == id && m.Time >= from && m.Time <= to)
            .OrderBy(m => m.Time).ToListAsync();

        return Ok(measurements);
    }
    
    [HttpDelete("samples/{id:int}")]
    public async Task<IActionResult> DeleteSampleMeasurements(int id,
        [FromServices] DatabaseContext context, [FromServices] CachedData data)
    {
        if (!data.SampleIds.Contains(id))
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "There are no measurements with this id");

        int delCount = await context.Connection.ExecuteAsync($"delete from measurements where sample_id = {id}");
        data.SampleIds.Remove(id);
        
        await context.Connection.ExecuteAsync($"VACUUM ANALYZE measurements");

        return delCount > 0 ? Ok() : BadRequest();
    }

    [HttpGet("samples/last")]
    public async Task<IActionResult> GetLastMeasurements(
        [FromServices] DatabaseContext context, [FromServices] CachedData data,
        int count = 20)
    {
        if (count <= 0)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Count must be greater than 0");

        var measurements = await context.Connection.QueryAsync<Measurement>(
            $"select * from get_last_measurements(@count);",
            new { count });

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
        from.SetKindUtc() ??= DateTime.MinValue;
        to.SetKindUtc() ??= DateTime.MaxValue;

        if (!data.SampleIds.Contains(id))
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "There are no measurements with this id");

        var measurements = await context.Measurements.AsNoTracking()
            .Where(m => m.SampleId == id && m.Time >= from && m.Time <= to)
            .OrderBy(m => m.Time).ToListAsync();

        MemoryStream stream = new();
        using var writer = new StreamWriter(stream);
        using (var csv = new CsvWriter(writer, new(CultureInfo.InvariantCulture)
               {
                   LeaveOpen = true,
                   Delimiter = ";" // excel
               }))
        {
            csv.Context.RegisterClassMap<MeasurementMap>();
            csv.WriteRecords(measurements);
        }

        return File(stream.ToArray(), "text/csv",
            $"{id:D8} [{from:dd.MM.yyyy HH.mm.ss} - {to:dd.MM.yyyy HH.mm.ss}].csv");
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
        if (!int.TryParse(timeFromStartParts[1], out int minutesFromStart)) return null;
        int secondsFromStart = (hoursFromStart * 60 + minutesFromStart) * 60;

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
        bool state = s[valueStartIndex..].Trim(' ').SequenceEqual("ON");

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
            Time = time.GetKindUtc(),
            SecondsFromStart = secondsFromStart,
            DutyCycle = dutyCycle,
            T = t,
            Tu = tu,
            I = i,
            Period = period,
            Work = work,
            Relax = relax,
            Frequency = frequency,
            State = state
        };

        return measurement;
    }
}
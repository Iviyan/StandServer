namespace StandServer.Utils;

/// <summary> Extension methods for DateTime. </summary>
public static class DateTimeExtensions
{
    /// <summary> Sets the Kind UTC for the parameter passed by ref. Example: now.SetKindUtc(); </summary>
    /// <param name="dateTime">Ref to DateTime? variable or field.</param>
    /// <returns>Ref to <paramref name="dateTime"/></returns>
    public static ref DateTime? SetKindUtc(this ref DateTime? dateTime)
    {
        if (dateTime.HasValue)
            dateTime = dateTime.Value.GetKindUtc();
        return ref dateTime;
    }

    /// <summary> Returns a new DateTime? with kind UTC and value of this instance. </summary>
    /// <returns>New DateTime? instance with specified kind</returns>
    public static DateTime? GetKindUtc(this DateTime? dateTime) => dateTime?.GetKindUtc();

    /// <summary> Sets the Kind UTC for the parameter passed by ref. Example: now.SetKindUtc(); </summary>
    /// <param name="dateTime">Ref to DateTime variable or field.</param>
    /// <returns>Ref to <paramref name="dateTime"/></returns>
    public static ref DateTime SetKindUtc(this ref DateTime dateTime)
    {
        if (dateTime.Kind != DateTimeKind.Utc)
            DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        return ref dateTime;
    }

    /// <summary> Returns a new DateTime with kind UTC and value of this instance. </summary>
    /// <returns>New DateTime instance with specified kind</returns>
    public static DateTime GetKindUtc(this DateTime dateTime)
        => dateTime.Kind == DateTimeKind.Utc ? dateTime : DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);

    /// <summary> Returns a new DateTime with a value rounded to seconds. </summary>
    /// <returns>New DateTime? instance with a value rounded to seconds.</returns>
    public static DateTime RoundToSeconds(this DateTime dateTime)
        => new(dateTime.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, dateTime.Kind);

    /// <summary> Converts a given DateTime into a Unix timestamp </summary>
    /// <param name="value">Any DateTime</param>
    /// <returns>The given DateTime in Unix timestamp format</returns>
    public static long ToUnix(this DateTime value) => (long)value.ToUniversalTime().Subtract(DateTime.UnixEpoch).TotalMilliseconds;
}

/// <summary> Converts a DateTime to UNIX and read from UNIX or ISO8601. </summary>
public class DateTimeJsonConverter : JsonConverter<DateTime>
{
    /// <summary> Read DateTime from UNIX or ISO8601. </summary>
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string str = reader.GetString()!;
        if (long.TryParse(str, out long unix))
            return DateTime.UnixEpoch.AddMilliseconds(unix);
        return DateTime.Parse(str, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }

    /// <summary> Write DateTime to UNIX format. </summary>
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.ToUnix());
    }
}
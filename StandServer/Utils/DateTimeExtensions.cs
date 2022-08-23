namespace StandServer.Utils;

public static class DateTimeExtensions
{
    public static ref DateTime? SetKindUtc(this ref DateTime? dateTime)
    {
        if (dateTime.HasValue)
            dateTime = dateTime.Value.GetKindUtc();
        return ref dateTime;
    }
    
    public static DateTime? GetKindUtc(this DateTime? dateTime) => dateTime?.GetKindUtc();

    public static ref DateTime SetKindUtc(this ref DateTime dateTime)
    {
        if (dateTime.Kind != DateTimeKind.Utc) 
            DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        return ref dateTime;
    }
    
    public static DateTime GetKindUtc(this DateTime dateTime) 
        => dateTime.Kind == DateTimeKind.Utc ? dateTime : DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);

    public static DateTime RoundToSeconds(this DateTime dateTime)
        => new(dateTime.Ticks / 1000_0000 * 1000_0000, dateTime.Kind);

    /// <summary>
    /// Converts a given DateTime into a Unix timestamp
    /// </summary>
    /// <param name="value">Any DateTime</param>
    /// <returns>The given DateTime in Unix timestamp format</returns>
    public static long ToUnix(this DateTime value) => (long)value.Subtract(DateTime.UnixEpoch.ToLocalTime()).TotalMilliseconds;
}

public class DateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string str = reader.GetString()!;
        if (long.TryParse(str, out long unix))
            return DateTime.UnixEpoch.ToLocalTime().AddMilliseconds(unix);
        return DateTime.Parse(str, new CultureInfo("ru"), DateTimeStyles.None);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.ToUnix());
    }
}

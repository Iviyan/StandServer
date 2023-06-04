namespace StandServer;

/// <summary> Singleton service, which contains frequently used data. </summary>
public class CachedData
{
    /// <summary> Unique sample ids in the database. </summary>
    public SortedSet<int> SampleIds { get; set; } = new();

    /// <summary> Last POST /api/samples request time. </summary>
    public DateTime? LastActiveTime { get; set; }

    /// <summary> The time of the latest measurement in the database
    /// or the time of the last measurement sent to /api/samples. </summary>
    public DateTime? LastMeasurementTime { get; set; }
}
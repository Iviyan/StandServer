namespace StandServer.Services;

/// <summary> Singleton service, which contains frequently used data. </summary>
public class CachedData
{
    /// <summary> Sample ids and their last measurements. </summary>
    public Dictionary<int, Measurement> LastSampleMeasurements { get; } = new();

    /// <summary> Last POST /api/samples request time. </summary>
    public DateTime? LastActiveTime { get; set; }

    /// <summary> The time of the latest measurement per stand in the database
    /// or the time of the last measurement sent to /api/samples. </summary>
    public Dictionary<short, DateTime> LastStandMeasurementTime { get; set; } = new();
}
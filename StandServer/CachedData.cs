namespace StandServer;

public class CachedData
{
    public SortedSet<int> SampleIds { get; set; } = new();
    
    public DateTime? LastActiveTime { get; set; }
    public DateTime? LastMeasurementTime { get; set; }
}
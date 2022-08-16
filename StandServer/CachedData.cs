namespace StandServer;

public class CachedData
{
    public SortedSet<int> SampleIds { get; set; } = new();
    
    public StateHistory? State { get; set; }
    public readonly SemaphoreSlim StateChangeLock = new(1, 1);
    
    public DateTime? LastActiveTime { get; set; }
}
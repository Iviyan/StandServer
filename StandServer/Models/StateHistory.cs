namespace StandServer.Models;

[Table("state_history")/*, Keyless*/]
public class StateHistory
{
    [Column("time"), Key] public DateTime Time { get; set; }
    [Column("state")] public bool State { get; set; }
}
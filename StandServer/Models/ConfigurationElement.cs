namespace StandServer.Models;

/// <summary> Model for the "configuration" table. </summary>
[Table("configuration")]
public class ConfigurationElement
{
    /// <summary> Property key. (PK) </summary>
    [Column("key"), Key] public string Key { get; set; } = null!;

    /// <summary> Property value. </summary>
    [Column("value")] public string? Value { get; set; }
}
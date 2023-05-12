namespace StandServer.Models;

[Table("configuration")]
public class ConfigurationElement
{
    [Column("key"), Key] public string Key { get; set; } = null!;
    [Column("value")] public string? Value { get; set; }
}
using CsvHelper.Configuration;

namespace StandServer.Models;

[Table("measurements")/*, Keyless*/]
public class Measurement
{
    [Column("sample_id"), JsonIgnore] public int SampleId { get; set; }
    [Column("time")] public DateTime Time { get; set; }
    [Column("seconds_from_start")] public int SecondsFromStart { get; set; }
    [Column("duty_cycle")] public short DutyCycle { get; set; }
    [Column("t")] public short T { get; set; }
    [Column("tu")] public short Tu { get; set; }
    [Column("i")] public short I { get; set; }
    [Column("period")] public short Period { get; set; }
    [Column("work")] public short Work { get; set; }
    [Column("relax")] public short Relax { get; set; }
    [Column("frequency")] public short Frequency { get; set; }
    
    [Column("state")]
    public bool State { get; set; }
}

public sealed class MeasurementMap : ClassMap<Measurement>
{
    public MeasurementMap()
    {
        AutoMap(CultureInfo.InvariantCulture);
        Map(m => m.SampleId).Convert(m => $"{m.Value.SampleId:D8}");
        Map(m => m.State).Convert(m => m.Value.State ? "yes" : "no");
    }
}

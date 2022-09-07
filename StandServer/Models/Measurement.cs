using CsvHelper.Configuration;

namespace StandServer.Models;

public enum SampleState { Off, Work, Relax }

[Table("measurements") /*, Keyless*/]
public class Measurement : IIndependentMeasurement
{
    [Column("sample_id")] public int SampleId { get; set; }
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
    [Column("state")] public SampleState State { get; set; }
}

public interface IIndependentMeasurement
{
    public DateTime Time { get; set; }
    public int SecondsFromStart { get; set; }
    public short DutyCycle { get; set; }
    public short T { get; set; }
    public short Tu { get; set; }
    public short I { get; set; }
    public short Period { get; set; }
    public short Work { get; set; }
    public short Relax { get; set; }
    public short Frequency { get; set; }
    public SampleState State { get; set; }
}

public sealed class MeasurementMap : ClassMap<Measurement>
{
    public MeasurementMap()
    {
        AutoMap(CultureInfo.InvariantCulture);
        Map(m => m.SampleId).Convert(m => $"{m.Value.SampleId:D8}");
        Map(m => m.Time).Convert(m => 
            m.Value.Time.ToLocalTime().ToString(CultureInfo.CurrentCulture));
    }
}
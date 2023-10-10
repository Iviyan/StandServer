using CsvHelper.Configuration;

namespace StandServer.Models;

/// <summary> Sample states. </summary>
public enum SampleState { Off, Work, Relax }

/// <summary> Model for the "measurements" table. </summary>
[Table("measurements") /*, Keyless*/]
public class Measurement : IIndependentMeasurement
{
    /// <summary> Stand id </summary>
    [Column("stand_id")] public short StandId { get; set; }
    
    /// <summary> Sample id </summary>
    [Column("sample_id")] public int SampleId { get; set; }

    /// <summary> Measurement time </summary>
    [Column("time")] public DateTime Time { get; set; }

    /// <summary> Time since sample measurements started. </summary>
    [Column("seconds_from_start")] public int SecondsFromStart { get; set; }

    /// <summary> Duty cycle </summary>
    [Column("duty_cycle")] public short DutyCycle { get; set; }

    /// <summary> Sample temperature </summary>
    [Column("t")] public short T { get; set; }

    /// <summary> Setpoint temperature </summary>
    [Column("tu")] public short Tu { get; set; }

    /// <summary> Sample current </summary>
    [Column("i")] public short I { get; set; }

    /// <summary> Sample period </summary>
    [Column("period")] public short Period { get; set; }

    /// <summary> Work time in minutes </summary>
    [Column("work")] public short Work { get; set; }

    /// <summary> Relax time in minutes </summary>
    [Column("relax")] public short Relax { get; set; }

    /// <summary> Frequency </summary>
    [Column("frequency")] public short Frequency { get; set; }

    /// <summary> Sample state </summary>
    [Column("state")] public SampleState State { get; set; }
}

public interface IIndependentMeasurement
{
    /// <inheritdoc cref="Measurement.Time"/>
    public DateTime Time { get; set; }

    /// <inheritdoc cref="Measurement.SecondsFromStart"/>
    public int SecondsFromStart { get; set; }

    /// <inheritdoc cref="Measurement.DutyCycle"/>
    public short DutyCycle { get; set; }

    /// <inheritdoc cref="Measurement.T"/>
    public short T { get; set; }

    /// <inheritdoc cref="Measurement.Tu"/>
    public short Tu { get; set; }

    /// <inheritdoc cref="Measurement.I"/>
    public short I { get; set; }

    /// <inheritdoc cref="Measurement.Period"/>
    public short Period { get; set; }

    /// <inheritdoc cref="Measurement.Work"/>
    public short Work { get; set; }

    /// <inheritdoc cref="Measurement.Relax"/>
    public short Relax { get; set; }

    /// <inheritdoc cref="Measurement.Frequency"/>
    public short Frequency { get; set; }

    /// <inheritdoc cref="Measurement.State"/>
    public SampleState State { get; set; }
}

/// <summary> CsvHelper map for <see cref="Measurement"/> class. </summary>
public sealed class MeasurementMap : ClassMap<Measurement>
{
    public MeasurementMap()
    {
        AutoMap(CultureInfo.InvariantCulture);
        Map(m => m.SampleId).Convert(m => $"{m.Value.SampleId:D8}");
        Map(m => m.Time).Convert(m => m.Value.Time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
    }
}
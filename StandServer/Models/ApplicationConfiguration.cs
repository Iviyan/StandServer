using System.Reflection;
using StandServer.Services;

namespace StandServer.Models;

/// <summary> Application configuration properties </summary>
public interface IApplicationConfiguration
{
    /// <summary> Maximum current in the non-working state. </summary>
    public short OffSampleMaxI { get; }
}

/// <summary> <see cref="IApplicationConfiguration"/> implementation with setters and default values. </summary>
public class ApplicationConfiguration : IApplicationConfiguration
{
    public short OffSampleMaxI { get; set; } = 200;
}

/// <summary> The model for the PATCH method for changing the application configuration. </summary>
public class ApplicationConfigurationPatch : PatchDtoBase, IApplicationConfiguration
{
    private short offSampleMaxI;

    public short OffSampleMaxI
    {
        get => offSampleMaxI;
        set => SetField(ref offSampleMaxI, value);
    }

    /// <summary> Apply the changed properties to <paramref name="target"/> using reflection. </summary>
    /// <param name="target"><see cref="ApplicationConfiguration"/> instance.</param>
    public void Apply(ApplicationConfiguration target)
    {
        foreach (string changedProperty in ChangedProperties)
        {
            var patchProperty = PatchProperties.First(p => p.Name == changedProperty);
            var targetProperty = TargetProperties.First(p => p.Name == changedProperty);

            var value = patchProperty.GetValue(this);
            targetProperty.SetValue(target, value);
        }
    }

    private static readonly PropertyInfo[] TargetProperties;
    private static readonly PropertyInfo[] PatchProperties;

    static ApplicationConfigurationPatch()
    {
        var targetType = typeof(ApplicationConfiguration);
        var patchType = typeof(ApplicationConfigurationPatch);

        TargetProperties = targetType.GetProperties().Where(DbStoredConfigurationService.PropertyPredicate).ToArray();
        PatchProperties = patchType.GetProperties().Where(DbStoredConfigurationService.PropertyPredicate).ToArray();
    }
}
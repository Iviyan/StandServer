using System.Reflection;
using StandServer.Services;

namespace StandServer.Models;

public interface IApplicationConfiguration
{
    public short OffSampleMaxI { get; }
}

public class ApplicationConfiguration : IApplicationConfiguration
{
    public short OffSampleMaxI { get; set; } = 200;
}

public class ApplicationConfigurationPatch : PatchDtoBase, IApplicationConfiguration
{
    private short offSampleMaxI;

    public short OffSampleMaxI { get => offSampleMaxI; set => SetField(ref offSampleMaxI, value); }

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
using System.Runtime.CompilerServices;

namespace StandServer.Models;

public abstract class PatchDtoBase
{
    public HashSet<string> ChangedProperties { get; } = new();
    
    public bool IsFieldPresent(string propertyName) 
        => ChangedProperties.Contains(propertyName);

    protected void SetHasProperty([CallerMemberName] string propertyName = null!)
        => ChangedProperties.Add(propertyName);
}
namespace StandServer.Utils;

/// <summary> Determines the naming policy used to convert a string-based name from camel case to pascal case and vice versa. </summary>
public class CamelCaseNamingPolicy : JsonNamingPolicy
{
    /// <summary> Instance of <see cref="CamelCaseNamingPolicy"/>. </summary>
    public static CamelCaseNamingPolicy Instance { get; } = new();

    /// <summary> Convert name from pascal to camel case. </summary>
    public override string ConvertName(string name) => FromPascalToCamelCase(name);

    /// <summary> Convert name from pascal to camel case. </summary>
    public static string FromPascalToCamelCase(string name)
    {
        if (String.IsNullOrWhiteSpace(name)) return name;

        if (name.Length == 1) return name.ToLowerInvariant();

        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    /// <summary> Convert name from camel to pascal case. </summary>
    public static string FromCamelToPascalCase(string name)
    {
        if (String.IsNullOrWhiteSpace(name)) return name;

        if (name.Length == 1) return name.ToUpperInvariant();

        return char.ToUpperInvariant(name[0]) + name[1..];
    }
}
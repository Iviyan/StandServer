namespace StandServer.Utils;

/// <summary> Determines the naming policy used to convert a string-based name to snake case format. </summary>
public class SnakeCaseNamingPolicy : JsonNamingPolicy
{
    /// <summary> Instance of <see cref="SnakeCaseNamingPolicy"/>. </summary>
    public static SnakeCaseNamingPolicy Instance { get; } = new();

    /// <inheritdoc />
    public override string ConvertName(string name) => FromPascalToSnakeCase(name);

    /// <summary> Convert property name from pascal or camel case to snake case. </summary>
    /// <param name="name">Property name in pascal or camel case.</param>
    /// <returns>Property name in snake case.</returns>
    public static string FromPascalToSnakeCase(string name)
    {
        if (String.IsNullOrWhiteSpace(name))
            return name;

        int upperCount = 1;
        for (int i = 1; i < name.Length; i++)
            if (Char.IsUpper(name[i]))
                upperCount++;

        var buffer = new char[name.Length + upperCount - 1];
        buffer[0] = Char.ToLowerInvariant(name[0]);

        int bufferPos = 0;

        for (int namePos = 1; namePos < name.Length; namePos++)
        {
            char c = name[namePos];
            if (Char.IsUpper(c))
            {
                buffer[++bufferPos] = '_';
                buffer[++bufferPos] = Char.ToLowerInvariant(c);
            }
            else
                buffer[++bufferPos] = c;
        }

        return new string(buffer);
    }
}
namespace StandServer.Utils;

public class SnakeCaseNamingPolicy : JsonNamingPolicy
{
    public static SnakeCaseNamingPolicy Instance { get; } = new();

    public override string ConvertName(string name) => FromPascalToSnakeCase(name);

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
    /*public static string FromSnakeToPascalCase(string name)
    {
        if (String.IsNullOrWhiteSpace(name))
            return name;
        
        int underscoreCount = 1;
        for (int i = 1; i < name.Length; i++)
            if (name[i] == '_')
                underscoreCount++;

        var buffer = new char[name.Length + underscoreCount - 1];
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
    }*/
}
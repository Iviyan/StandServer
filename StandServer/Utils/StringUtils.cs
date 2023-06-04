namespace StandServer.Utils;

/// <summary> Extension methods for string. </summary>
public static class StringUtils
{
    /// <summary> An effective string splitting method using yield. </summary>
    public static IEnumerable<string> GetLines(this string str, bool removeEmptyLines = false)
    {
        using var sr = new StringReader(str);
        while (sr.ReadLine() is { } line)
        {
            if (removeEmptyLines && String.IsNullOrWhiteSpace(line)) continue;
            yield return line;
        }
    }

    /// <summary> Cut the string if it is longer than the maxLength number. </summary>
    public static string? Truncate(this string? value, int maxLength)
        => value?.Length > maxLength
            ? value.Substring(0, maxLength)
            : value;
}
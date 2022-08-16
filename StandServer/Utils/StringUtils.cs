namespace StandServer.Utils;

public static class StringUtils
{
    public static IEnumerable<string> GetLines(this string str, bool removeEmptyLines = false)
    {
        using var sr = new StringReader(str);
        while (sr.ReadLine() is { } line)
        {
            if (removeEmptyLines && String.IsNullOrWhiteSpace(line)) continue;
            yield return line;
        }
    }
}
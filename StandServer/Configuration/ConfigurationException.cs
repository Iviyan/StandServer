namespace StandServer.Configuration;

/// <summary> The exception that is thrown when a configuration error occurs. </summary>
[Serializable]
public class ConfigurationException : Exception
{
    public ConfigurationException() { }
    public ConfigurationException(string? message) : base(message) { }
    public ConfigurationException(string? message, Exception? innerException) : base(message, innerException) { }
}
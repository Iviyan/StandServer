namespace StandServer.Database;

public class DatabaseContext : IDisposable
{
    private readonly DatabaseSource source;

    private NpgsqlConnection? connection;
    public NpgsqlConnection Connection => connection ??= source.CreateConnection();

    public DatabaseContext(DatabaseSource source)
    {
        this.source = source;
    }

    public void Dispose() => connection?.Dispose();
}
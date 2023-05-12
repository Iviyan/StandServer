namespace StandServer.Database;

public class DatabaseContext : IDisposable
{
    private readonly NpgsqlDataSource source;

    private NpgsqlConnection? connection;
    public NpgsqlConnection Connection => connection ??= source.CreateConnection();

    public DatabaseContext(NpgsqlDataSource source) => this.source = source;

    public void Dispose() => connection?.Dispose();
}
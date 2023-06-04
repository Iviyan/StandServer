namespace StandServer.Database;

/// <summary> Scoped service containing a lazy initialized <see cref="NpgsqlConnection"/>. </summary>
public class DatabaseContext : IDisposable
{
    private readonly NpgsqlDataSource source;

    private NpgsqlConnection? connection;

    /// <summary> Get <see cref="NpgsqlConnection"/> instance. <br/>
    /// The connection is created the first time the property is accessed. </summary>
    public NpgsqlConnection Connection => connection ??= source.CreateConnection();

    public DatabaseContext(NpgsqlDataSource source) => this.source = source;

    /// <summary> Dispose <see cref="NpgsqlConnection"/> instance. </summary>
    public void Dispose() => connection?.Dispose();
}
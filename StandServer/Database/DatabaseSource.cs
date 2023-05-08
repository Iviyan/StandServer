namespace StandServer.Database;

public class DatabaseSource
{
    private readonly string connectionString;
    
    public DatabaseSource(IConfiguration configuration)
    {
        connectionString = configuration.GetConnectionString("PgsqlConnection")!;
    }
    
    public NpgsqlConnection CreateConnection() => new(connectionString);
}

namespace StandServer.Database;

/// <summary> EF context for the application database. </summary>
public class ApplicationContext : DbContext
{
    /// <summary> DbSet of "users" table. </summary>
    public DbSet<User> Users { get; set; } = null!;

    /// <summary> DbSet of "refresh_tokens" table. </summary>
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;

    /// <summary> DbSet of "telegram_bot_users" table. </summary>
    public DbSet<TelegramBotUser> TelegramBotUsers { get; set; } = null!;

    /// <summary> DbSet of "measurements" table. </summary>
    public DbSet<Measurement> Measurements { get; set; } = null!;

    /// <summary> DbSet of "configuration" table. </summary>
    public DbSet<ConfigurationElement> Configuration { get; set; } = null!;

    public ApplicationContext(DbContextOptions<ApplicationContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Measurement>().HasKey(m => new { m.SampleId, m.Time });
        // Keyless are never tracked for changes in the DbContext and therefore are never inserted,
        // updated or deleted on the database.

        modelBuilder.HasPostgresEnum<SampleState>();
    }

    /// <summary> Method that configures the <see cref="NpgsqlDataSourceBuilder"/>. </summary>
    public static void ConfigureNpgsqlBuilder(NpgsqlDataSourceBuilder builder)
    {
        builder.MapEnum<SampleState>();
    }
}
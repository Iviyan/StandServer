using System.Diagnostics;

namespace StandServer.Database;

public class ApplicationContext : DbContext
{
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
    public DbSet<TelegramBotUser> TelegramBotUsers { get; set; } = null!;
    public DbSet<Measurement> Measurements { get; set; } = null!;
    
    static ApplicationContext()
        => NpgsqlConnection.GlobalTypeMapper.MapEnum<SampleState>();

    public ApplicationContext(DbContextOptions<ApplicationContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Measurement>().HasKey(m=> new { m.SampleId, m.Time });
        // Keyless are never tracked for changes in the DbContext and therefore are never inserted,
        // updated or deleted on the database.
        
        modelBuilder.HasPostgresEnum<SampleState>();
    }
}
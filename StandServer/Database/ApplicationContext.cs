using System.Diagnostics;

namespace StandServer.Database;

public class ApplicationContext : DbContext
{
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
    public DbSet<Measurement> Measurements { get; set; } = null!;
    public DbSet<StateHistory> StateHistory { get; set; } = null!;

    public ApplicationContext() { }

    public ApplicationContext(DbContextOptions<ApplicationContext> options)
        : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
#if DEBUG
        //Console.WriteLine($"\nApplicationContext configuring {ContextId.InstanceId}\n");
        optionsBuilder.LogTo(m => Debug.WriteLine(m), LogLevel.Trace)
            .EnableSensitiveDataLogging();
#endif
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Measurement>().HasKey(m=> new { m.SampleId, m.Time });
        // Keyless are never tracked for changes in the DbContext and therefore are never inserted,
        // updated or deleted on the database.
    }
}
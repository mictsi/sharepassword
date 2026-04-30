using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SharePassword.Models;

namespace SharePassword.Data;

public abstract class SharePasswordDbContext : DbContext
{
    private static readonly ValueConverter<DateTime, DateTime> UtcDateTimeConverter = new(
        value => EnsureUtc(value),
        value => EnsureUtc(value));

    private static readonly ValueConverter<DateTime?, DateTime?> NullableUtcDateTimeConverter = new(
        value => EnsureUtc(value),
        value => EnsureUtc(value));

    protected SharePasswordDbContext(DbContextOptions options)
        : base(options)
    {
    }

    public DbSet<PasswordShare> PasswordShares => Set<PasswordShare>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var shares = modelBuilder.Entity<PasswordShare>();
        shares.ToTable("PasswordShares");
        shares.HasKey(x => x.Id);
        shares.Property(x => x.RecipientEmail).IsRequired().HasMaxLength(256);
        shares.Property(x => x.SharedUsername).IsRequired().HasMaxLength(256);
        shares.Property(x => x.EncryptedPassword).IsRequired();
        shares.Property(x => x.Instructions).HasMaxLength(1000);
        shares.Property(x => x.AccessCodeHash).IsRequired().HasMaxLength(128);
        shares.Property(x => x.AccessToken).IsRequired().HasMaxLength(64);
        shares.Property(x => x.CreatedAtUtc).IsRequired().HasConversion(UtcDateTimeConverter);
        shares.Property(x => x.ExpiresAtUtc).IsRequired().HasConversion(UtcDateTimeConverter);
        shares.Property(x => x.LastAccessedAtUtc).HasConversion(NullableUtcDateTimeConverter);
        shares.Property(x => x.CreatedBy).IsRequired().HasMaxLength(256);
        shares.Property(x => x.RequireOidcLogin).IsRequired();
        shares.HasIndex(x => x.AccessToken).IsUnique();
        shares.HasIndex(x => x.CreatedBy);
        shares.HasIndex(x => x.ExpiresAtUtc);

        var auditLogs = modelBuilder.Entity<AuditLog>();
        auditLogs.ToTable("AuditLogs");
        auditLogs.HasKey(x => x.Id);
        auditLogs.Property(x => x.Id).ValueGeneratedOnAdd();
        auditLogs.Property(x => x.TimestampUtc).IsRequired().HasConversion(UtcDateTimeConverter);
        auditLogs.Property(x => x.ActorType).IsRequired().HasMaxLength(32);
        auditLogs.Property(x => x.ActorIdentifier).IsRequired().HasMaxLength(256);
        auditLogs.Property(x => x.Operation).IsRequired().HasMaxLength(128);
        auditLogs.Property(x => x.TargetType).HasMaxLength(128);
        auditLogs.Property(x => x.TargetId).HasMaxLength(128);
        auditLogs.Property(x => x.IpAddress).HasMaxLength(64);
        auditLogs.Property(x => x.UserAgent).HasMaxLength(1024);
        auditLogs.Property(x => x.CorrelationId).HasMaxLength(128);
        auditLogs.Property(x => x.Details).HasMaxLength(2048);
        auditLogs.HasIndex(x => x.Operation);
        auditLogs.HasIndex(x => x.TimestampUtc);
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        if (value == default)
        {
            return value;
        }

        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static DateTime? EnsureUtc(DateTime? value)
    {
        return value is null ? null : EnsureUtc(value.Value);
    }
}

public sealed class SqliteSharePasswordDbContext : SharePasswordDbContext
{
    public SqliteSharePasswordDbContext(DbContextOptions<SqliteSharePasswordDbContext> options)
        : base(options)
    {
    }
}

public sealed class SqlServerSharePasswordDbContext : SharePasswordDbContext
{
    public SqlServerSharePasswordDbContext(DbContextOptions<SqlServerSharePasswordDbContext> options)
        : base(options)
    {
    }
}

public sealed class PostgresqlSharePasswordDbContext : SharePasswordDbContext
{
    public PostgresqlSharePasswordDbContext(DbContextOptions<PostgresqlSharePasswordDbContext> options)
        : base(options)
    {
    }
}
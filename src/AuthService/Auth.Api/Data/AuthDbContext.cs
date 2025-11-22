using Microsoft.EntityFrameworkCore;

namespace Auth.Api.Data;

public sealed class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.UserId)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(e => e.Token)
                .IsRequired()
                .HasMaxLength(512);

            entity.Property(e => e.ExpiresAt)
                .IsRequired();

            entity.Property(e => e.RevokedAt);

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            entity.Property(e => e.DeviceId)
                .HasMaxLength(256);

            entity.HasIndex(e => e.Token)
                .IsUnique()
                .HasDatabaseName("IX_RefreshTokens_Token");

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_RefreshTokens_UserId");

            entity.HasIndex(e => e.ExpiresAt)
                .HasDatabaseName("IX_RefreshTokens_ExpiresAt");
        });
    }
}

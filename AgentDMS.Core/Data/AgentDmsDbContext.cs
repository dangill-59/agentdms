using Microsoft.EntityFrameworkCore;
using AgentDMS.Core.Models;

namespace AgentDMS.Core.Data;

public class AgentDmsDbContext : DbContext
{
    public AgentDmsDbContext(DbContextOptions<AgentDmsDbContext> options) : base(options)
    {
    }

    public DbSet<DocumentEntity> Documents { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DocumentEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(500);
            entity.Property(e => e.FilePath).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.ProcessedPngPath).HasMaxLength(1000);
            entity.Property(e => e.ThumbnailPath).HasMaxLength(1000);
            entity.Property(e => e.FileFormat).HasMaxLength(50);
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.IsArchived).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => e.FileName);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.IsArchived);
            entity.HasIndex(e => e.CreatedAt);
        });
    }
}
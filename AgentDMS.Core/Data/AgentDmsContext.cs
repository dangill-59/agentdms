using Microsoft.EntityFrameworkCore;
using AgentDMS.Core.Models;

namespace AgentDMS.Core.Data;

/// <summary>
/// Database context for AgentDMS document management
/// </summary>
public class AgentDmsContext : DbContext
{
    public AgentDmsContext(DbContextOptions<AgentDmsContext> options) : base(options)
    {
    }

    /// <summary>
    /// Documents table
    /// </summary>
    public DbSet<Document> Documents { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Document entity
        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            
            entity.HasIndex(e => e.FileName);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Status);
            
            // Configure full-text search index on ExtractedText if supported
            // SQLite FTS will be added separately
        });
    }
}
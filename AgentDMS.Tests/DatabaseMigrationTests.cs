using Microsoft.EntityFrameworkCore;
using AgentDMS.Core.Data;
using AgentDMS.Core.Models;
using Xunit;

namespace AgentDMS.Tests;

public class DatabaseMigrationTests : IDisposable
{
    private readonly AgentDmsDbContext _context;
    private readonly string _testDbPath;

    public DatabaseMigrationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_agentdms_{Guid.NewGuid()}.db");
        var options = new DbContextOptionsBuilder<AgentDmsDbContext>()
            .UseSqlite($"Data Source={_testDbPath}")
            .Options;

        _context = new AgentDmsDbContext(options);
        _context.Database.EnsureCreated();
    }

    [Fact]
    public void Database_ShouldHaveDocumentsTableWithRequiredColumns()
    {
        // Simple test - verify we can create and read documents
        // This implicitly tests that the table exists and has the right schema
        var document = new DocumentEntity
        {
            FileName = "schema_test.pdf",
            FilePath = "/path/to/schema_test.pdf",
            IsActive = true,
            IsArchived = false
        };

        _context.Documents.Add(document);
        _context.SaveChanges();

        var savedDocument = _context.Documents.First(d => d.FileName == "schema_test.pdf");
        
        // Verify that IsActive and IsArchived columns exist and work
        Assert.True(savedDocument.IsActive, "IsActive column should exist and be accessible");
        Assert.False(savedDocument.IsArchived, "IsArchived column should exist and be accessible");
        Assert.NotEqual(0, savedDocument.Id); // Document should have been saved with an ID
    }

    [Fact]
    public void DocumentEntity_ShouldHaveCorrectDefaultValues()
    {
        // Test that new documents have correct default values
        var document = new DocumentEntity
        {
            FileName = "test.pdf",
            FilePath = "/path/to/test.pdf"
        };

        _context.Documents.Add(document);
        _context.SaveChanges();

        var savedDocument = _context.Documents.First(d => d.FileName == "test.pdf");
        
        Assert.True(savedDocument.IsActive, "IsActive should default to true");
        Assert.False(savedDocument.IsArchived, "IsArchived should default to false");
        Assert.True(savedDocument.CreatedAt > DateTime.MinValue, "CreatedAt should be set");
        Assert.True(savedDocument.UpdatedAt > DateTime.MinValue, "UpdatedAt should be set");
    }

    [Fact]
    public void DocumentEntity_ShouldAllowUpdatingActiveAndArchivedStatus()
    {
        // Test that we can update IsActive and IsArchived columns
        var document = new DocumentEntity
        {
            FileName = "test2.pdf",
            FilePath = "/path/to/test2.pdf"
        };

        _context.Documents.Add(document);
        _context.SaveChanges();

        // Update the document
        document.IsActive = false;
        document.IsArchived = true;
        document.UpdatedAt = DateTime.UtcNow;
        _context.SaveChanges();

        // Verify changes were saved
        var updatedDocument = _context.Documents.First(d => d.FileName == "test2.pdf");
        Assert.False(updatedDocument.IsActive, "IsActive should be updated to false");
        Assert.True(updatedDocument.IsArchived, "IsArchived should be updated to true");
    }

    public void Dispose()
    {
        _context.Dispose();
        if (File.Exists(_testDbPath))
        {
            File.Delete(_testDbPath);
        }
    }
}
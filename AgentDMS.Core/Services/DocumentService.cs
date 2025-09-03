using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AgentDMS.Core.Data;
using AgentDMS.Core.Models;

namespace AgentDMS.Core.Services;

/// <summary>
/// Service for managing documents and their metadata
/// </summary>
public interface IDocumentService
{
    Task<Document> CreateDocumentAsync(Document document);
    Task<Document?> GetDocumentAsync(int id);
    Task<Document?> UpdateDocumentAsync(Document document);
    Task<bool> DeleteDocumentAsync(int id);
    Task<IEnumerable<Document>> GetDocumentsAsync(int skip = 0, int take = 50);
    Task<IEnumerable<Document>> SearchDocumentsAsync(string searchTerm, int skip = 0, int take = 50);
    Task<int> GetDocumentCountAsync();
    Task<IEnumerable<Document>> GetRecentDocumentsAsync(int count = 10);
}

/// <summary>
/// Implementation of document management service
/// </summary>
public class DocumentService : IDocumentService
{
    private readonly AgentDmsContext _context;
    private readonly ILogger<DocumentService>? _logger;

    public DocumentService(AgentDmsContext context, ILogger<DocumentService>? logger = null)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Document> CreateDocumentAsync(Document document)
    {
        try
        {
            document.CreatedAt = DateTime.UtcNow;
            document.UpdatedAt = DateTime.UtcNow;

            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            _logger?.LogInformation("Created document {DocumentId} with filename {FileName}", 
                document.Id, document.FileName);

            return document;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error creating document with filename {FileName}", document.FileName);
            throw;
        }
    }

    public async Task<Document?> GetDocumentAsync(int id)
    {
        try
        {
            return await _context.Documents.FindAsync(id);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error retrieving document {DocumentId}", id);
            throw;
        }
    }

    public async Task<Document?> UpdateDocumentAsync(Document document)
    {
        try
        {
            var existingDocument = await _context.Documents.FindAsync(document.Id);
            if (existingDocument == null)
            {
                return null;
            }

            document.UpdatedAt = DateTime.UtcNow;
            _context.Entry(existingDocument).CurrentValues.SetValues(document);
            
            await _context.SaveChangesAsync();

            _logger?.LogInformation("Updated document {DocumentId}", document.Id);
            return document;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating document {DocumentId}", document.Id);
            throw;
        }
    }

    public async Task<bool> DeleteDocumentAsync(int id)
    {
        try
        {
            var document = await _context.Documents.FindAsync(id);
            if (document == null)
            {
                return false;
            }

            _context.Documents.Remove(document);
            await _context.SaveChangesAsync();

            _logger?.LogInformation("Deleted document {DocumentId}", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting document {DocumentId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<Document>> GetDocumentsAsync(int skip = 0, int take = 50)
    {
        try
        {
            return await _context.Documents
                .OrderByDescending(d => d.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error retrieving documents");
            throw;
        }
    }

    public async Task<IEnumerable<Document>> SearchDocumentsAsync(string searchTerm, int skip = 0, int take = 50)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return await GetDocumentsAsync(skip, take);
            }

            // Simple text search - can be enhanced with full-text search later
            var searchTermLower = searchTerm.ToLower();
            
            return await _context.Documents
                .Where(d => 
                    d.FileName.ToLower().Contains(searchTermLower) ||
                    (d.ExtractedText != null && d.ExtractedText.ToLower().Contains(searchTermLower)) ||
                    (d.Tags != null && d.Tags.ToLower().Contains(searchTermLower)))
                .OrderByDescending(d => d.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error searching documents with term {SearchTerm}", searchTerm);
            throw;
        }
    }

    public async Task<int> GetDocumentCountAsync()
    {
        try
        {
            return await _context.Documents.CountAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting document count");
            throw;
        }
    }

    public async Task<IEnumerable<Document>> GetRecentDocumentsAsync(int count = 10)
    {
        try
        {
            return await _context.Documents
                .OrderByDescending(d => d.CreatedAt)
                .Take(count)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error retrieving recent documents");
            throw;
        }
    }
}
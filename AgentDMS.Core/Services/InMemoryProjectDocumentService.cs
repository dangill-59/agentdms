using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AgentDMS.Core.Models;

namespace AgentDMS.Core.Services;

/// <summary>
/// In-memory implementation of project document service
/// This is a temporary implementation until a proper database is configured
/// </summary>
public class InMemoryProjectDocumentService : IProjectDocumentService
{
    private readonly List<ProjectDocument> _documents;
    private readonly ILogger<InMemoryProjectDocumentService>? _logger;
    private int _nextId = 1;

    public InMemoryProjectDocumentService(ILogger<InMemoryProjectDocumentService>? logger = null)
    {
        _logger = logger;
        _documents = new List<ProjectDocument>();
        
        // Initialize with sample data for project ID 3 (as mentioned in the problem statement)
        SeedSampleData();
    }

    private void SeedSampleData()
    {
        // Add sample document for project ID 3 with proper business data
        _documents.Add(new ProjectDocument
        {
            Id = _nextId++,
            ProjectId = 3,
            CustomerName = "ABC Corporation",
            InvoiceNumber = "INV-2024-0123",
            InvoiceDate = new DateTime(2024, 3, 15),
            DocumentType = "Invoice",
            Status = "Pending Review",
            Notes = "Quarterly service invoice requiring approval",
            CreatedDate = DateTime.UtcNow.AddDays(-5),
            UpdatedDate = DateTime.UtcNow.AddDays(-1)
        });

        // Add additional sample documents for other projects
        _documents.Add(new ProjectDocument
        {
            Id = _nextId++,
            ProjectId = 1,
            CustomerName = "XYZ Ltd",
            InvoiceNumber = "INV-2024-0100",
            InvoiceDate = new DateTime(2024, 2, 28),
            DocumentType = "Receipt",
            Status = "Processed",
            Notes = "Equipment purchase receipt",
            CreatedDate = DateTime.UtcNow.AddDays(-10),
            UpdatedDate = DateTime.UtcNow.AddDays(-8)
        });

        _logger?.LogInformation("Seeded {Count} sample project documents", _documents.Count);
    }

    public async Task<ProjectDocument?> GetProjectDocumentAsync(int projectId)
    {
        await Task.CompletedTask; // Simulate async operation
        
        var document = _documents.FirstOrDefault(d => d.ProjectId == projectId);
        _logger?.LogInformation("Retrieved document for project {ProjectId}: {Found}", 
            projectId, document != null ? "Found" : "Not Found");
        
        return document;
    }

    public async Task<List<ProjectDocument>> GetProjectDocumentsAsync(int projectId)
    {
        await Task.CompletedTask; // Simulate async operation
        
        var documents = _documents.Where(d => d.ProjectId == projectId).ToList();
        _logger?.LogInformation("Retrieved {Count} documents for project {ProjectId}", 
            documents.Count, projectId);
        
        return documents;
    }

    public async Task<ProjectDocument> CreateProjectDocumentAsync(ProjectDocument document)
    {
        await Task.CompletedTask; // Simulate async operation
        
        document.Id = _nextId++;
        document.CreatedDate = DateTime.UtcNow;
        document.UpdatedDate = DateTime.UtcNow;
        
        _documents.Add(document);
        _logger?.LogInformation("Created new document with ID {Id} for project {ProjectId}", 
            document.Id, document.ProjectId);
        
        return document;
    }

    public async Task<ProjectDocument> UpdateProjectDocumentAsync(ProjectDocument document)
    {
        await Task.CompletedTask; // Simulate async operation
        
        var existingDocument = _documents.FirstOrDefault(d => d.Id == document.Id);
        if (existingDocument == null)
        {
            throw new InvalidOperationException($"Document with ID {document.Id} not found");
        }

        // Update fields
        existingDocument.CustomerName = document.CustomerName;
        existingDocument.InvoiceNumber = document.InvoiceNumber;
        existingDocument.InvoiceDate = document.InvoiceDate;
        existingDocument.DocumentType = document.DocumentType;
        existingDocument.Status = document.Status;
        existingDocument.Notes = document.Notes;
        existingDocument.UpdatedDate = DateTime.UtcNow;

        _logger?.LogInformation("Updated document with ID {Id} for project {ProjectId}", 
            document.Id, document.ProjectId);
        
        return existingDocument;
    }

    public async Task<bool> DeleteProjectDocumentAsync(int id)
    {
        await Task.CompletedTask; // Simulate async operation
        
        var document = _documents.FirstOrDefault(d => d.Id == id);
        if (document == null)
        {
            return false;
        }

        _documents.Remove(document);
        _logger?.LogInformation("Deleted document with ID {Id}", id);
        
        return true;
    }
}
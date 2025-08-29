using System;

namespace AgentDMS.Core.Models;

/// <summary>
/// Represents a document associated with a specific project
/// </summary>
public class ProjectDocument
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime? InvoiceDate { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Service for managing project documents
/// </summary>
public interface IProjectDocumentService
{
    Task<ProjectDocument?> GetProjectDocumentAsync(int projectId);
    Task<List<ProjectDocument>> GetProjectDocumentsAsync(int projectId);
    Task<ProjectDocument> CreateProjectDocumentAsync(ProjectDocument document);
    Task<ProjectDocument> UpdateProjectDocumentAsync(ProjectDocument document);
    Task<bool> DeleteProjectDocumentAsync(int id);
}
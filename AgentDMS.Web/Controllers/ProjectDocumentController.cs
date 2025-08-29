using Microsoft.AspNetCore.Mvc;
using AgentDMS.Core.Models;
using AgentDMS.Core.Services;
using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;

namespace AgentDMS.Web.Controllers;

/// <summary>
/// Controller for managing project documents and their metadata
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[SwaggerTag("Project document management and metadata operations")]
public class ProjectDocumentController : ControllerBase
{
    private readonly IProjectDocumentService _projectDocumentService;
    private readonly ILogger<ProjectDocumentController> _logger;

    /// <summary>
    /// Initializes a new instance of the ProjectDocumentController
    /// </summary>
    /// <param name="projectDocumentService">Project document service</param>
    /// <param name="logger">Logger instance</param>
    public ProjectDocumentController(
        IProjectDocumentService projectDocumentService,
        ILogger<ProjectDocumentController> logger)
    {
        _projectDocumentService = projectDocumentService;
        _logger = logger;
    }

    /// <summary>
    /// Get document details for a specific project
    /// </summary>
    /// <param name="projectId">The project ID to retrieve document details for</param>
    /// <returns>Project document details if found</returns>
    /// <response code="200">Document found and returned</response>
    /// <response code="404">No document found for the specified project ID</response>
    [HttpGet("project/{projectId}")]
    [SwaggerOperation(Summary = "Get project document", Description = "Retrieves document details for a specific project ID")]
    [SwaggerResponse(200, "Document details", typeof(ProjectDocument))]
    [SwaggerResponse(404, "Document not found")]
    [ProducesResponseType(typeof(ProjectDocument), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDocument>> GetProjectDocument(
        [FromRoute, Range(1, int.MaxValue)] int projectId)
    {
        _logger.LogInformation("Getting document details for project {ProjectId}", projectId);

        var document = await _projectDocumentService.GetProjectDocumentAsync(projectId);
        
        if (document == null)
        {
            _logger.LogWarning("No document found for project {ProjectId}", projectId);
            return NotFound(new { message = $"No document found for project {projectId}" });
        }

        return Ok(document);
    }

    /// <summary>
    /// Get all documents for a specific project
    /// </summary>
    /// <param name="projectId">The project ID to retrieve documents for</param>
    /// <returns>List of project documents</returns>
    /// <response code="200">Documents found and returned</response>
    [HttpGet("project/{projectId}/all")]
    [SwaggerOperation(Summary = "Get all project documents", Description = "Retrieves all document details for a specific project ID")]
    [SwaggerResponse(200, "List of document details", typeof(List<ProjectDocument>))]
    [ProducesResponseType(typeof(List<ProjectDocument>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ProjectDocument>>> GetProjectDocuments(
        [FromRoute, Range(1, int.MaxValue)] int projectId)
    {
        _logger.LogInformation("Getting all documents for project {ProjectId}", projectId);

        var documents = await _projectDocumentService.GetProjectDocumentsAsync(projectId);
        
        return Ok(documents);
    }

    /// <summary>
    /// Create a new project document
    /// </summary>
    /// <param name="document">The document details to create</param>
    /// <returns>Created document with assigned ID</returns>
    /// <response code="201">Document created successfully</response>
    /// <response code="400">Invalid document data</response>
    [HttpPost]
    [SwaggerOperation(Summary = "Create project document", Description = "Creates a new document with the specified details")]
    [SwaggerResponse(201, "Document created", typeof(ProjectDocument))]
    [SwaggerResponse(400, "Invalid document data")]
    [ProducesResponseType(typeof(ProjectDocument), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProjectDocument>> CreateProjectDocument(
        [FromBody] ProjectDocument document)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        _logger.LogInformation("Creating new document for project {ProjectId}", document.ProjectId);

        var createdDocument = await _projectDocumentService.CreateProjectDocumentAsync(document);
        
        return CreatedAtAction(
            nameof(GetProjectDocument), 
            new { projectId = createdDocument.ProjectId }, 
            createdDocument);
    }

    /// <summary>
    /// Update an existing project document
    /// </summary>
    /// <param name="id">The document ID to update</param>
    /// <param name="document">The updated document details</param>
    /// <returns>Updated document</returns>
    /// <response code="200">Document updated successfully</response>
    /// <response code="400">Invalid document data</response>
    /// <response code="404">Document not found</response>
    [HttpPut("{id}")]
    [SwaggerOperation(Summary = "Update project document", Description = "Updates an existing document with new details")]
    [SwaggerResponse(200, "Document updated", typeof(ProjectDocument))]
    [SwaggerResponse(400, "Invalid document data")]
    [SwaggerResponse(404, "Document not found")]
    [ProducesResponseType(typeof(ProjectDocument), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDocument>> UpdateProjectDocument(
        [FromRoute, Range(1, int.MaxValue)] int id,
        [FromBody] ProjectDocument document)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        document.Id = id;
        
        try
        {
            _logger.LogInformation("Updating document {Id}", id);
            var updatedDocument = await _projectDocumentService.UpdateProjectDocumentAsync(document);
            return Ok(updatedDocument);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Document {Id} not found for update", id);
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Delete a project document
    /// </summary>
    /// <param name="id">The document ID to delete</param>
    /// <returns>No content if successful</returns>
    /// <response code="204">Document deleted successfully</response>
    /// <response code="404">Document not found</response>
    [HttpDelete("{id}")]
    [SwaggerOperation(Summary = "Delete project document", Description = "Deletes an existing document")]
    [SwaggerResponse(204, "Document deleted")]
    [SwaggerResponse(404, "Document not found")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProjectDocument(
        [FromRoute, Range(1, int.MaxValue)] int id)
    {
        _logger.LogInformation("Deleting document {Id}", id);

        var deleted = await _projectDocumentService.DeleteProjectDocumentAsync(id);
        
        if (!deleted)
        {
            _logger.LogWarning("Document {Id} not found for deletion", id);
            return NotFound(new { message = $"Document with ID {id} not found" });
        }

        return NoContent();
    }
}
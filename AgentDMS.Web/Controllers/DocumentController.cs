using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using AgentDMS.Core.Services;
using AgentDMS.Core.Models;
using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;

namespace AgentDMS.Web.Controllers;

/// <summary>
/// Controller for document management operations including search and metadata access
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[SwaggerTag("Document management operations including search, retrieval, and metadata access")]
public class DocumentController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly ILogger<DocumentController> _logger;

    public DocumentController(IDocumentService documentService, ILogger<DocumentController> logger)
    {
        _documentService = documentService;
        _logger = logger;
    }

    /// <summary>
    /// Get a list of documents with pagination
    /// </summary>
    /// <param name="skip">Number of documents to skip</param>
    /// <param name="take">Number of documents to take (max 100)</param>
    /// <returns>List of documents</returns>
    /// <response code="200">Documents retrieved successfully</response>
    /// <response code="400">Invalid pagination parameters</response>
    /// <response code="500">Internal server error</response>
    [HttpGet]
    [SwaggerOperation(Summary = "Get documents", Description = "Retrieve a paginated list of documents")]
    [SwaggerResponse(200, "Documents retrieved successfully", typeof(DocumentListResponse))]
    [SwaggerResponse(400, "Invalid pagination parameters")]
    [SwaggerResponse(500, "Internal server error")]
    [ProducesResponseType(typeof(DocumentListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DocumentListResponse>> GetDocuments(
        [FromQuery] int skip = 0, 
        [FromQuery] int take = 20)
    {
        try
        {
            if (skip < 0 || take <= 0 || take > 100)
            {
                return BadRequest("Invalid pagination parameters. skip must be >= 0, take must be > 0 and <= 100");
            }

            var documents = await _documentService.GetDocumentsAsync(skip, take);
            var totalCount = await _documentService.GetDocumentCountAsync();

            return Ok(new DocumentListResponse
            {
                Documents = documents.ToList(),
                TotalCount = totalCount,
                Skip = skip,
                Take = take
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving documents");
            return StatusCode(500, "Internal server error while retrieving documents");
        }
    }

    /// <summary>
    /// Search documents by content, filename, or tags
    /// </summary>
    /// <param name="q">Search query term</param>
    /// <param name="skip">Number of documents to skip</param>
    /// <param name="take">Number of documents to take (max 100)</param>
    /// <returns>List of matching documents</returns>
    /// <response code="200">Documents found successfully</response>
    /// <response code="400">Invalid search parameters</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("search")]
    [SwaggerOperation(Summary = "Search documents", Description = "Search documents by content, filename, or tags")]
    [SwaggerResponse(200, "Documents found successfully", typeof(DocumentListResponse))]
    [SwaggerResponse(400, "Invalid search parameters")]
    [SwaggerResponse(500, "Internal server error")]
    [ProducesResponseType(typeof(DocumentListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DocumentListResponse>> SearchDocuments(
        [FromQuery][Required] string q,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return BadRequest("Search query 'q' is required");
            }

            if (skip < 0 || take <= 0 || take > 100)
            {
                return BadRequest("Invalid pagination parameters. skip must be >= 0, take must be > 0 and <= 100");
            }

            var documents = await _documentService.SearchDocumentsAsync(q, skip, take);

            return Ok(new DocumentListResponse
            {
                Documents = documents.ToList(),
                TotalCount = documents.Count(), // Note: This is not accurate for pagination, but search doesn't have total count
                Skip = skip,
                Take = take,
                SearchQuery = q
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching documents with query {Query}", q);
            return StatusCode(500, "Internal server error while searching documents");
        }
    }

    /// <summary>
    /// Get a specific document by ID
    /// </summary>
    /// <param name="id">Document ID</param>
    /// <returns>Document details</returns>
    /// <response code="200">Document found</response>
    /// <response code="404">Document not found</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("{id:int}")]
    [SwaggerOperation(Summary = "Get document by ID", Description = "Retrieve a specific document by its ID")]
    [SwaggerResponse(200, "Document found", typeof(Document))]
    [SwaggerResponse(404, "Document not found")]
    [SwaggerResponse(500, "Internal server error")]
    [ProducesResponseType(typeof(Document), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Document>> GetDocument(int id)
    {
        try
        {
            var document = await _documentService.GetDocumentAsync(id);
            if (document == null)
            {
                return NotFound($"Document with ID {id} not found");
            }

            return Ok(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document {DocumentId}", id);
            return StatusCode(500, "Internal server error while retrieving document");
        }
    }

    /// <summary>
    /// Delete a document by ID
    /// </summary>
    /// <param name="id">Document ID</param>
    /// <returns>Success status</returns>
    /// <response code="200">Document deleted successfully</response>
    /// <response code="404">Document not found</response>
    /// <response code="500">Internal server error</response>
    [HttpDelete("{id:int}")]
    [SwaggerOperation(Summary = "Delete document", Description = "Delete a document by its ID")]
    [SwaggerResponse(200, "Document deleted successfully")]
    [SwaggerResponse(404, "Document not found")]
    [SwaggerResponse(500, "Internal server error")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> DeleteDocument(int id)
    {
        try
        {
            var success = await _documentService.DeleteDocumentAsync(id);
            if (!success)
            {
                return NotFound($"Document with ID {id} not found");
            }

            _logger.LogInformation("Document {DocumentId} deleted successfully", id);
            return Ok(new { message = "Document deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document {DocumentId}", id);
            return StatusCode(500, "Internal server error while deleting document");
        }
    }

    /// <summary>
    /// Get recent documents
    /// </summary>
    /// <param name="count">Number of recent documents to retrieve (max 50)</param>
    /// <returns>List of recent documents</returns>
    /// <response code="200">Recent documents retrieved successfully</response>
    /// <response code="400">Invalid count parameter</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("recent")]
    [SwaggerOperation(Summary = "Get recent documents", Description = "Retrieve the most recently uploaded documents")]
    [SwaggerResponse(200, "Recent documents retrieved successfully", typeof(IEnumerable<Document>))]
    [SwaggerResponse(400, "Invalid count parameter")]
    [SwaggerResponse(500, "Internal server error")]
    [ProducesResponseType(typeof(IEnumerable<Document>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<Document>>> GetRecentDocuments([FromQuery] int count = 10)
    {
        try
        {
            if (count <= 0 || count > 50)
            {
                return BadRequest("Count must be > 0 and <= 50");
            }

            var documents = await _documentService.GetRecentDocumentsAsync(count);
            return Ok(documents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent documents");
            return StatusCode(500, "Internal server error while retrieving recent documents");
        }
    }

    /// <summary>
    /// Get document statistics
    /// </summary>
    /// <returns>Document statistics</returns>
    /// <response code="200">Statistics retrieved successfully</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("stats")]
    [SwaggerOperation(Summary = "Get document statistics", Description = "Retrieve statistics about stored documents")]
    [SwaggerResponse(200, "Statistics retrieved successfully", typeof(DocumentStatsResponse))]
    [SwaggerResponse(500, "Internal server error")]
    [ProducesResponseType(typeof(DocumentStatsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DocumentStatsResponse>> GetDocumentStats()
    {
        try
        {
            var totalCount = await _documentService.GetDocumentCountAsync();
            var recentDocuments = await _documentService.GetRecentDocumentsAsync(5);

            return Ok(new DocumentStatsResponse
            {
                TotalDocuments = totalCount,
                RecentDocuments = recentDocuments.ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document statistics");
            return StatusCode(500, "Internal server error while retrieving statistics");
        }
    }
}

/// <summary>
/// Response model for document list operations
/// </summary>
public class DocumentListResponse
{
    public List<Document> Documents { get; set; } = new();
    public int TotalCount { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
    public string? SearchQuery { get; set; }
}

/// <summary>
/// Response model for document statistics
/// </summary>
public class DocumentStatsResponse
{
    public int TotalDocuments { get; set; }
    public List<Document> RecentDocuments { get; set; } = new();
}
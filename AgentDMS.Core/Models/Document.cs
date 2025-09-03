using System;
using System.ComponentModel.DataAnnotations;

namespace AgentDMS.Core.Models;

/// <summary>
/// Represents a document in the system with metadata and extracted content
/// </summary>
public class Document
{
    /// <summary>
    /// Unique identifier for the document
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Original filename of the uploaded document
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// File path in the storage system (local path or cloud URL)
    /// </summary>
    [Required]
    [MaxLength(1000)]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Content type of the file (e.g., "application/pdf", "image/jpeg")
    /// </summary>
    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// When the document was uploaded/created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the document was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Text extracted from the document via OCR
    /// </summary>
    public string? ExtractedText { get; set; }

    /// <summary>
    /// OCR method used for text extraction (e.g., "Tesseract", "Mistral")
    /// </summary>
    [MaxLength(50)]
    public string? OcrMethod { get; set; }

    /// <summary>
    /// OCR confidence score (0.0 to 1.0)
    /// </summary>
    public double? OcrConfidence { get; set; }

    /// <summary>
    /// Time taken for OCR processing
    /// </summary>
    public TimeSpan? OcrProcessingTime { get; set; }

    /// <summary>
    /// Number of pages in the document (for multi-page documents)
    /// </summary>
    public int PageCount { get; set; } = 1;

    /// <summary>
    /// Additional metadata as JSON
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Path to thumbnail image if generated
    /// </summary>
    [MaxLength(1000)]
    public string? ThumbnailPath { get; set; }

    /// <summary>
    /// Document tags for categorization
    /// </summary>
    [MaxLength(500)]
    public string? Tags { get; set; }

    /// <summary>
    /// Processing status of the document
    /// </summary>
    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;
}

/// <summary>
/// Status of document processing
/// </summary>
public enum DocumentStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}
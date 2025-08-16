using System.ComponentModel.DataAnnotations;

namespace AgentDMS.Core.Models;

public class DocumentEntity
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(500)]
    public string FileName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(1000)]
    public string FilePath { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? ProcessedPngPath { get; set; }
    
    [MaxLength(1000)]
    public string? ThumbnailPath { get; set; }
    
    [MaxLength(50)]
    public string? FileFormat { get; set; }
    
    public long? FileSizeBytes { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public bool IsArchived { get; set; } = false;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? ProcessedAt { get; set; }
}
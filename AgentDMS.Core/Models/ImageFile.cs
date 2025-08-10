using System;

namespace AgentDMS.Core.Models;

/// <summary>
/// Represents an image file with metadata and processing information
/// </summary>
public class ImageFile
{
    public string OriginalFilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string OriginalFormat { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime CreatedDate { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsMultiPage { get; set; }
    public int PageCount { get; set; } = 1;
    public string ConvertedPngPath { get; set; } = string.Empty;
    public string ThumbnailPath { get; set; } = string.Empty;
    public List<string> SplitPagePaths { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}
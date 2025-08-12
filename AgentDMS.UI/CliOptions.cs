using System;

namespace AgentDMS.UI;

/// <summary>
/// Configuration options for the CLI application
/// </summary>
public class CliOptions
{
    public int MaxConcurrency { get; set; } = Environment.ProcessorCount;
    public long MaxFileSizeMB { get; set; } = 100; // Default 100MB limit
    public string? ProcessFile { get; set; }
    public string? ProcessDirectory { get; set; }
    public bool ShowHelp { get; set; }
    public bool ShowFormats { get; set; }
    public string? OutputDirectory { get; set; }
    public int ThumbnailSize { get; set; } = 200;
    public bool EnableMetricsLogging { get; set; } = true;
    public string? BenchmarkFile { get; set; }

    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();
        
        // Check for environment variable
        var envConcurrency = Environment.GetEnvironmentVariable("AGENTDMS_MAX_CONCURRENCY");
        if (!string.IsNullOrEmpty(envConcurrency) && int.TryParse(envConcurrency, out var concurrency))
        {
            options.MaxConcurrency = Math.Max(1, concurrency);
        }
        
        var envMaxSize = Environment.GetEnvironmentVariable("AGENTDMS_MAX_FILE_SIZE_MB");
        if (!string.IsNullOrEmpty(envMaxSize) && long.TryParse(envMaxSize, out var maxSize))
        {
            options.MaxFileSizeMB = Math.Max(1, maxSize);
        }

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "--help":
                case "-h":
                    options.ShowHelp = true;
                    break;
                    
                case "--process":
                case "-p":
                    if (i + 1 < args.Length)
                    {
                        options.ProcessFile = args[++i];
                    }
                    break;
                    
                case "--directory":
                case "-d":
                    if (i + 1 < args.Length)
                    {
                        options.ProcessDirectory = args[++i];
                    }
                    break;
                    
                case "--max-concurrency":
                case "-c":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var maxConc))
                    {
                        options.MaxConcurrency = Math.Max(1, maxConc);
                    }
                    break;
                    
                case "--max-file-size":
                case "-s":
                    if (i + 1 < args.Length && long.TryParse(args[++i], out var maxFileSize))
                    {
                        options.MaxFileSizeMB = Math.Max(1, maxFileSize);
                    }
                    break;
                    
                case "--output":
                case "-o":
                    if (i + 1 < args.Length)
                    {
                        options.OutputDirectory = args[++i];
                    }
                    break;
                    
                case "--thumbnail-size":
                case "-t":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var thumbSize))
                    {
                        options.ThumbnailSize = Math.Max(50, Math.Min(1000, thumbSize));
                    }
                    break;
                    
                case "--formats":
                case "-f":
                    options.ShowFormats = true;
                    break;
                    
                case "--no-metrics":
                    options.EnableMetricsLogging = false;
                    break;
                    
                case "--benchmark":
                case "-b":
                    if (i + 1 < args.Length)
                    {
                        options.BenchmarkFile = args[++i];
                    }
                    break;
            }
        }

        return options;
    }
}
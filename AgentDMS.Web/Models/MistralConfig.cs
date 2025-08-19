using System.ComponentModel.DataAnnotations;

namespace AgentDMS.Web.Models;

/// <summary>
/// Configuration model for Mistral LLM integration settings
/// </summary>
public class MistralConfig
{
    [Display(Name = "API Key")]
    public string ApiKey { get; set; } = string.Empty;
    
    [Required]
    [Display(Name = "Endpoint")]
    [Url]
    public string Endpoint { get; set; } = "https://api.mistral.ai/v1/chat/completions";
    
    [Required]
    [Display(Name = "Model")]
    public string Model { get; set; } = "mistral-small";
    
    [Range(0.0, 2.0)]
    [Display(Name = "Temperature")]
    public double Temperature { get; set; } = 0.1;
}
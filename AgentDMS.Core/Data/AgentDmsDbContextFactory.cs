using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace AgentDMS.Core.Data;

public class AgentDmsDbContextFactory : IDesignTimeDbContextFactory<AgentDmsDbContext>
{
    public AgentDmsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AgentDmsDbContext>();
        
        // Try to find configuration in multiple locations
        var currentDirectory = Directory.GetCurrentDirectory();
        var webProjectPath = Path.Combine(currentDirectory, "..", "AgentDMS.Web");
        var rootProjectPath = currentDirectory;
        
        var configurationBuilder = new ConfigurationBuilder();
        
        // Try to load from Web project first (primary location)
        if (Directory.Exists(webProjectPath))
        {
            configurationBuilder
                .SetBasePath(webProjectPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
        }
        else
        {
            // Fallback to current directory
            configurationBuilder
                .SetBasePath(rootProjectPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
        }
        
        var configuration = configurationBuilder.Build();

        // Get connection string from configuration
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        
        // If no connection string in config, use the default targeting agentdms.db
        if (string.IsNullOrEmpty(connectionString))
        {
            connectionString = "Data Source=./agentdms.db";
        }

        optionsBuilder.UseSqlite(connectionString);

        return new AgentDmsDbContext(optionsBuilder.Options);
    }
}
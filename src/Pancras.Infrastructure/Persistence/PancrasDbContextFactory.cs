using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Pancras.Infrastructure.Persistence;

public sealed class PancrasDbContextFactory : IDesignTimeDbContextFactory<PancrasDbContext>
{
    public const string UserSecretsId = "pancras-web-7c1a9e2b";

    public PancrasDbContext CreateDbContext(string[] args)
    {
        var connectionString = ResolveConnectionString();
        var options = new DbContextOptionsBuilder<PancrasDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new PancrasDbContext(options);
    }

    internal static string ResolveConnectionString()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment;
        }

        var secretsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "UserSecrets",
            UserSecretsId,
            "secrets.json");
        if (File.Exists(secretsPath))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(secretsPath));
            if (document.RootElement.TryGetProperty("ConnectionStrings:DefaultConnection", out var flat)
                && flat.ValueKind == JsonValueKind.String)
            {
                var flatText = flat.GetString();
                if (!string.IsNullOrWhiteSpace(flatText))
                {
                    return flatText;
                }
            }

            if (document.RootElement.TryGetProperty("ConnectionStrings", out var connections)
                && connections.TryGetProperty("DefaultConnection", out var value))
            {
                var text = value.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        throw new InvalidOperationException(
            "Thiếu ConnectionStrings:DefaultConnection trong user-secrets.");
    }
}

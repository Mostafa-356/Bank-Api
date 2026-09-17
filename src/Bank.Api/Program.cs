using Bank.Api.Extensions;
using Bank.Api.Extensions.Configuration;
using Bank.Api.Extensions.Data;
using Bank.Api.Extensions.DependencyInjection;
using Bank.Api.Extensions.Infrastructure;
using Bank.Api.Extensions.Middleware;
using Bank.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

// Load environment variables from .env file
var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envPath))
{
    foreach (var line in File.ReadAllLines(envPath))
    {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            continue;

        var parts = line.Split('=', 2);
        if (parts.Length == 2)
        {
            var key = parts[0].Trim();
            var value = parts[1].Trim();
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on all interfaces on port 5000
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000);
});

// Resolve database URL: prefer NEON_DATABASE_URL override, then fall back to DATABASE_URL
var databaseUrl = Environment.GetEnvironmentVariable("NEON_DATABASE_URL")
    ?? Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(databaseUrl))
{
    // Convert PostgreSQL URI format to Npgsql key-value connection string
    // e.g. postgresql://user:pass@host/db?sslmode=require -> Host=host;Database=db;Username=user;Password=pass;SSL Mode=Require
    var npgsqlConnStr = ConvertPostgresUrlToNpgsql(databaseUrl);
    builder.Configuration["ConnectionStrings:DefaultConnection"] = npgsqlConnStr;
}

// Process appsettings.json to replace placeholders with environment variables
ProcessConfigurationPlaceholders(builder.Configuration);

// Add controllers with JSON configuration
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// === SERVICE REGISTRATIONS (Organized by Concern) ===

// 1. Database & Data Access Layer
builder.Services.AddDatabaseServices(builder.Configuration);

// 2. Caching & Session Management
builder.Services.AddCachingServices(builder.Configuration);

// 3. Authentication & Authorization
builder.Services.AddAuthenticationServices(builder.Configuration);

// 4. Repository Layer (Data Access)
builder.Services.AddRepositoryServices();

// 5. Application Services (Business Logic)
builder.Services.AddApplicationServices(builder.Configuration);

// 6. Infrastructure Services (External Integrations)
builder.Services.AddInfrastructureServices();

// 7. CQRS & Validation
builder.Services.AddCqrsServices();

// 8. AutoMapper (Object Mapping)
builder.Services.AddAutoMapperServices();

// 9. Background Jobs
builder.Services.AddBackgroundJobServices(builder.Configuration);

// 10. API Documentation
builder.Services.AddApiDocumentationServices();

// 11. CORS Policies
builder.Services.AddCorsServices(builder.Configuration);

var app = builder.Build();

// === DATABASE MIGRATION ===
var skipMigrations = builder.Configuration.GetValue<bool>("DatabaseSettings:SkipMigrations", false);
var skipSeeding = builder.Configuration.GetValue<bool>("DatabaseSettings:SkipSeeding", false);

if (!skipMigrations)
{
    await app.ApplyDatabaseMigrationsAsync();
}
else
{
    app.Logger.LogWarning("⚠️ Database migrations skipped (development mode)");
}

// === DATA SEEDING ===
if (!skipSeeding)
{
    await app.SeedInitialDataAsync();
}
else
{
    app.Logger.LogWarning("⚠️ Data seeding skipped (development mode)");
}

// === MIDDLEWARE PIPELINE (Order Matters) ===

// 1. Development-specific middleware
app.ConfigureDevelopmentMiddleware();

// 2. Security & audit middleware
app.ConfigureSecurityMiddleware();

app.UseRouting();

app.MapControllers();

app.Run();

// Make Program class accessible for testing
public partial class Program
{
    /// <summary>
    /// Private constructor to prevent direct instantiation of Program class.
    /// This class is primarily used as an entry point and for static configuration methods.
    /// </summary>
    private Program() { }

    /// <summary>
    /// Process configuration to replace {PLACEHOLDER} values with environment variables
    /// </summary>
    private static void ProcessConfigurationPlaceholders(IConfiguration config)
    {
        var connectionString = config.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(connectionString))
        {
            var processedConnection = ReplacePlaceholders(connectionString);
            // Force set the processed connection string
            ((IConfigurationBuilder)config).AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = processedConnection
            });
        }

        var jwtKey = config["Jwt:Key"];
        if (!string.IsNullOrEmpty(jwtKey) && jwtKey.StartsWith('{') && jwtKey.EndsWith('}'))
        {
            var processedKey = ReplacePlaceholders(jwtKey);
            ((IConfigurationBuilder)config).AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = processedKey
            });
        }

        // Process all Email config placeholders
        var emailKeys = new[] { "Email:SmtpHost", "Email:SmtpPort", "Email:Username", "Email:Password" };
        var emailOverrides = new Dictionary<string, string?>();
        foreach (var key in emailKeys)
        {
            var val = config[key];
            if (!string.IsNullOrEmpty(val))
            {
                var processed = ReplacePlaceholders(val);
                if (processed != val)
                    emailOverrides[key] = processed;
            }
        }
        if (emailOverrides.Count > 0)
            ((IConfigurationBuilder)config).AddInMemoryCollection(emailOverrides);
    }

    /// <summary>
    /// Convert a PostgreSQL URI (postgresql://user:pass@host/db?sslmode=disable)
    /// to an Npgsql key-value connection string that Npgsql can parse correctly.
    /// </summary>
    internal static string ConvertPostgresUrlToNpgsql(string url)
    {
        try
        {
            // Handle both postgresql:// and postgres:// schemes
            var normalized = url.Replace("postgresql://", "http://").Replace("postgres://", "http://");
            var uri = new Uri(normalized);

            var host = uri.Host;
            // uri.Port returns 80 when scheme is http:// and no port specified; default to 5432 for PostgreSQL
            var port = (uri.Port > 0 && uri.Port != 80) ? uri.Port : 5432;
            var database = uri.AbsolutePath.TrimStart('/');
            var username = Uri.UnescapeDataString(uri.UserInfo.Split(':')[0]);
            var password = uri.UserInfo.Contains(':') ? Uri.UnescapeDataString(uri.UserInfo.Split(':', 2)[1]) : "";

            var connStr = $"Host={host};Port={port};Database={database};Username={username};Password={password}";

            // Parse query string parameters (e.g. sslmode=disable)
            var query = uri.Query.TrimStart('?');
            if (!string.IsNullOrEmpty(query))
            {
                foreach (var param in query.Split('&'))
                {
                    var kv = param.Split('=', 2);
                    if (kv.Length == 2)
                    {
                        var key = kv[0].ToLowerInvariant();
                        var value = kv[1];
                        // Map common URL params to Npgsql connection string keys
                        var npgsqlKey = key switch
                        {
                            "sslmode" => "SSL Mode",
                            "ssl" => "SSL Mode",
                            "connect_timeout" => "Timeout",
                            "application_name" => "Application Name",
                            "search_path" => "Search Path",
                            "channel_binding" => "Channel Binding",
                            _ => null
                        };
                        if (npgsqlKey != null)
                        {
                            var npgsqlValue = (key == "sslmode" || key == "ssl") ? CapitalizeSslMode(value) : value;
                            connStr += $";{npgsqlKey}={npgsqlValue}";
                        }
                    }
                }
            }

            return connStr;
        }
        catch
        {
            // If parsing fails, return the original and let Npgsql try
            return url;
        }
    }

    private static string CapitalizeSslMode(string mode) => mode.ToLowerInvariant() switch
    {
        "disable" => "Disable",
        "allow" => "Allow",
        "prefer" => "Prefer",
        "require" => "Require",
        "verify-ca" => "VerifyCA",
        "verify-full" => "VerifyFull",
        _ => mode
    };

    private static string ReplacePlaceholders(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var result = value;
        var pattern = @"\{([^}]+)\}";
        
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(value, pattern))
        {
            var placeholder = match.Groups[1].Value;
            var envValue = Environment.GetEnvironmentVariable(placeholder);
            if (!string.IsNullOrEmpty(envValue))
            {
                result = result.Replace(match.Value, envValue);
            }
        }

        return result;
    }
}

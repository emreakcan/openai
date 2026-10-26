using Betalgo.Ranul.OpenAI.Extensions;
using Betalgo.Ranul.OpenAI.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OpenAI.IntegrationTests.Fixtures;

/// <summary>
/// Shared test fixture that provides a configured OpenAI service for integration tests.
/// This fixture is shared across all tests in a collection to avoid creating multiple HTTP clients.
/// </summary>
/// <remarks>
/// Configuration is loaded from (in order of precedence):
/// 1. Environment variables (OPENAI__APIKEY)
/// 2. User secrets (for local development)
/// 3. appsettings.json
/// 
/// To run tests locally, either:
/// - Set the OPENAI_API_KEY environment variable, or
/// - Use 'dotnet user-secrets set "OpenAI:ApiKey" "your-key"'
/// </remarks>
public class OpenAITestFixture : IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    public OpenAITestFixture()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<OpenAITestFixture>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        // Configure OpenAI service with beta features enabled for Responses API
        services.AddOpenAIService(options =>
        {
            options.UseBeta = true;
        });

        _serviceProvider = services.BuildServiceProvider();

        // Validate API key is configured
        var apiKey = configuration["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "OpenAI API key is not configured. " +
                "Set the 'OpenAI:ApiKey' in user secrets or 'OPENAI__APIKEY' environment variable.");
        }

        OpenAI = _serviceProvider.GetRequiredService<IOpenAIService>();
        DefaultModel = configuration["OpenAI:DefaultModel"] ?? "gpt-4o-mini";
        ReasoningModel = configuration["OpenAI:ReasoningModel"] ?? "o3-mini";
    }

    /// <summary>
    /// The configured OpenAI service instance.
    /// </summary>
    public IOpenAIService OpenAI { get; }

    /// <summary>
    /// The default model to use for tests (cost-effective).
    /// </summary>
    public string DefaultModel { get; }

    /// <summary>
    /// The reasoning model to use for reasoning tests.
    /// </summary>
    public string ReasoningModel { get; }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Collection definition for sharing the OpenAI fixture across test classes.
/// </summary>
[CollectionDefinition("OpenAI")]
public class OpenAICollection : ICollectionFixture<OpenAITestFixture>
{
    // This class has no code, it's just used to define the collection.
}


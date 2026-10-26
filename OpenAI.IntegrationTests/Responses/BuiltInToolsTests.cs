using Betalgo.Ranul.OpenAI.Contracts.Requests.Responses;
using Betalgo.Ranul.OpenAI.Contracts.Types.Content;
using Betalgo.Ranul.OpenAI.Contracts.Types.Items;
using Betalgo.Ranul.OpenAI.Contracts.Types.Tools;
using OpenAI.IntegrationTests.Fixtures;
using OpenAI.IntegrationTests.Helpers;

namespace OpenAI.IntegrationTests.Responses;

/// <summary>
/// Integration tests for OpenAI's built-in tools (web search, code interpreter, etc.).
/// These tests demonstrate how to use tools that OpenAI provides out of the box.
/// </summary>
/// <remarks>
/// Learn more: https://platform.openai.com/docs/guides/tools
/// </remarks>
[Collection("OpenAI")]
[Trait("Category", "Integration")]
public class BuiltInToolsTests
{
    private readonly OpenAITestFixture _fixture;

    public BuiltInToolsTests(OpenAITestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Test 4.1: Web Search Tool

    /// <summary>
    /// Demonstrates using the web search tool to get real-time information.
    /// The web search tool allows the model to search the internet for current data.
    /// </summary>
    /// <example>
    /// <code>
    /// var response = await openAI.Responses.CreateResponse(new CreateResponse
    /// {
    ///     Model = "gpt-4o-mini",
    ///     Input = "What are the latest news headlines today?",
    ///     Tools = new List&lt;ITool&gt;
    ///     {
    ///         new WebSearchTool
    ///         {
    ///             Type = WebSearchTool.TypeWebSearch,
    ///             SearchContextSize = "medium"  // low, medium, or high
    ///         }
    ///     }
    /// });
    /// 
    /// // Check for web search results in output
    /// var searchCalls = response.Output?.OfType&lt;WebSearchToolCallItem&gt;();
    /// </code>
    /// </example>
    [Fact]
    public async Task CreateResponse_WithWebSearchTool_ReturnsSearchResults()
    {
        // Arrange
        var webSearchTool = new WebSearchTool
        {
            Type = WebSearchTool.TypeWebSearch,
            SearchContextSize = "low" // Use low to minimize token usage
        };

        var request = new CreateResponse
        {
            Model = _fixture.DefaultModel,
            Input = "What is the current population of Tokyo? Search the web for the latest data.",
            Tools = new List<ITool> { webSearchTool },
            MaxOutputTokens = 500 // Web search responses can be longer
        };

        // Act
        var response = await _fixture.OpenAI.Responses.CreateResponse(request);

        // Assert
        AssertExtensions.AssertSuccessfulResponse(response);
        Assert.NotNull(response.Output);
        Assert.NotEmpty(response.Output);

        // The output should contain either web search tool calls or a message with search results
        var hasWebSearchCall = response.Output.OfType<WebSearchToolCallItem>().Any();
        var hasTextOutput = response.Output.OfType<OutputMessageItem>().Any();

        Assert.True(hasWebSearchCall || hasTextOutput,
            "Expected either web search tool calls or text output with search results");
    }

    #endregion

    #region Test 4.2: Code Interpreter Tool

    /// <summary>
    /// Demonstrates using the code interpreter tool to execute Python code.
    /// This is useful for calculations, data analysis, and generating visualizations.
    /// </summary>
    /// <example>
    /// <code>
    /// var response = await openAI.Responses.CreateResponse(new CreateResponse
    /// {
    ///     Model = "gpt-4o-mini",
    ///     Input = "Calculate the first 10 Fibonacci numbers using Python.",
    ///     Tools = new List&lt;ITool&gt;
    ///     {
    ///         new CodeInterpreterTool()
    ///     }
    /// });
    /// 
    /// // Check for code interpreter results
    /// var codeResults = response.Output?.OfType&lt;CodeInterpreterToolCall&gt;();
    /// foreach (var result in codeResults)
    /// {
    ///     Console.WriteLine($"Code: {result.Code}");
    ///     // result.Outputs contains the execution results
    /// }
    /// </code>
    /// </example>
    [Fact]
    public async Task CreateResponse_WithCodeInterpreterTool_ExecutesPython()
    {
        // Arrange
        var codeInterpreterTool = new CodeInterpreterTool();

        var request = new CreateResponse
        {
            Model = _fixture.DefaultModel,
            Input = "Use Python to calculate what 15 factorial (15!) equals. Show the code and result.",
            Tools = new List<ITool> { codeInterpreterTool },
            MaxOutputTokens = 500
        };

        // Act
        var response = await _fixture.OpenAI.Responses.CreateResponse(request);

        // Assert
        AssertExtensions.AssertSuccessfulResponse(response);
        Assert.NotNull(response.Output);
        Assert.NotEmpty(response.Output);

        // The output should contain code interpreter results or a final message
        var hasCodeCall = response.Output.OfType<CodeInterpreterToolCall>().Any();
        var hasTextOutput = response.Output.OfType<OutputMessageItem>().Any();

        Assert.True(hasCodeCall || hasTextOutput,
            "Expected either code interpreter calls or text output with results");

        // If there's code interpreter output, verify it has code
        var codeCall = response.Output.OfType<CodeInterpreterToolCall>().FirstOrDefault();
        if (codeCall != null)
        {
            Assert.False(string.IsNullOrWhiteSpace(codeCall.Code),
                "Expected code interpreter to have executed some code");
        }

        // The response should mention the factorial result (1307674368000)
        var outputText = GetOutputText(response);
        // 15! = 1307674368000
        Assert.True(
            outputText.Contains("1307674368000") || outputText.Contains("factorial", StringComparison.OrdinalIgnoreCase),
            "Expected response to contain factorial calculation");
    }

    #endregion

    /// <summary>
    /// Helper method to extract text from response output items.
    /// </summary>
    private static string GetOutputText(Betalgo.Ranul.OpenAI.Contracts.Responses.Responses.Response response)
    {
        if (response.Output == null) return string.Empty;

        var textContents = response.Output
            .OfType<OutputMessageItem>()
            .SelectMany(m => m.Content ?? Enumerable.Empty<IContent>())
            .OfType<OutputTextContent>()
            .Select(c => c.Text);

        return string.Join(" ", textContents);
    }
}


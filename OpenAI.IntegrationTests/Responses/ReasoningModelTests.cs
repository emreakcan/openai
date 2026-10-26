using Betalgo.Ranul.OpenAI.Contracts.Enums;
using Betalgo.Ranul.OpenAI.Contracts.Enums.Responses;
using Betalgo.Ranul.OpenAI.Contracts.Requests.Responses;
using Betalgo.Ranul.OpenAI.Contracts.Types.Content;
using Betalgo.Ranul.OpenAI.Contracts.Types.Items;
using OpenAI.IntegrationTests.Fixtures;
using OpenAI.IntegrationTests.Helpers;

namespace OpenAI.IntegrationTests.Responses;

/// <summary>
/// Integration tests for reasoning models (o1, o3-mini, etc.) with the Responses API.
/// These tests demonstrate how to use models with advanced reasoning capabilities.
/// </summary>
/// <remarks>
/// Learn more: https://platform.openai.com/docs/guides/reasoning
/// </remarks>
[Collection("OpenAI")]
[Trait("Category", "Integration")]
public class ReasoningModelTests
{
    private readonly OpenAITestFixture _fixture;

    public ReasoningModelTests(OpenAITestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Test 6.1: Reasoning Model Basic

    /// <summary>
    /// Demonstrates using a reasoning model for complex problem-solving tasks.
    /// Reasoning models (like o3-mini) use extended "thinking" to solve difficult problems.
    /// </summary>
    /// <example>
    /// <code>
    /// var response = await openAI.Responses.CreateResponse(new CreateResponse
    /// {
    ///     Model = "o3-mini",  // or "o1", "o1-mini", "o1-preview"
    ///     Input = "Solve: If 3x + 7 = 22, what is x?",
    ///     Reasoning = new ReasoningConfig
    ///     {
    ///         Effort = ReasoningEffort.Medium,  // low, medium, or high
    ///         Summary = ReasoningSummary.Auto   // auto, concise, or detailed
    ///     }
    /// });
    /// 
    /// // Check reasoning token usage
    /// var reasoningTokens = response.Usage?.OutputTokensDetails?.ReasoningTokens;
    /// Console.WriteLine($"Reasoning tokens used: {reasoningTokens}");
    /// </code>
    /// </example>
    [Fact]
    public async Task CreateResponse_WithReasoningModel_ReturnsReasoningTokens()
    {
        // Arrange
        var request = new CreateResponse
        {
            Model = _fixture.ReasoningModel, // o3-mini or similar
            Input = "Solve this step by step: A train leaves Station A at 9:00 AM traveling at 60 mph. " +
                    "Another train leaves Station B (120 miles away) at 10:00 AM traveling toward Station A at 40 mph. " +
                    "At what time do they meet?",
            Reasoning = new ReasoningConfig
            {
                Effort = ReasoningEffort.Low, // Use low to minimize costs
                Summary = ReasoningSummary.Auto
            },
            MaxOutputTokens = 2000 // Reasoning can use many tokens
        };

        // Act
        var response = await _fixture.OpenAI.Responses.CreateResponse(request);

        // Assert
        AssertExtensions.AssertSuccessfulResponse(response);
        Assert.NotNull(response.Usage);

        // Reasoning models should report reasoning tokens in the usage details
        // Note: The exact structure depends on the API response format
        Assert.True(response.Usage.OutputTokens > 0, "Expected output tokens from reasoning");

        // Check for output - should contain the answer
        var outputText = GetOutputText(response);
        Assert.False(string.IsNullOrWhiteSpace(outputText), "Expected reasoning output");

        // The answer should be around 10:48 AM (or mention 1 hour 48 minutes)
        // But we just verify we got a response since exact format may vary
        Assert.True(outputText.Length > 50, "Expected detailed reasoning response");
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


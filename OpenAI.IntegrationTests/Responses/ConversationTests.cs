using Betalgo.Ranul.OpenAI.Contracts.Requests.Responses;
using Betalgo.Ranul.OpenAI.Contracts.Types;
using Betalgo.Ranul.OpenAI.Contracts.Types.Content;
using Betalgo.Ranul.OpenAI.Contracts.Types.Items;
using Betalgo.Ranul.OpenAI.Contracts.Enums.Responses;
using OpenAI.IntegrationTests.Fixtures;
using OpenAI.IntegrationTests.Helpers;

namespace OpenAI.IntegrationTests.Responses;

/// <summary>
/// Integration tests for multi-turn conversations using the Responses API.
/// These tests demonstrate how to maintain conversation context across multiple requests.
/// </summary>
/// <remarks>
/// Learn more: https://platform.openai.com/docs/guides/conversation-state
/// </remarks>
[Collection("OpenAI")]
[Trait("Category", "Integration")]
public class ConversationTests
{
    private readonly OpenAITestFixture _fixture;

    public ConversationTests(OpenAITestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Test 2.1: Previous Response ID

    /// <summary>
    /// Demonstrates maintaining conversation context using PreviousResponseId.
    /// This is the recommended way to build multi-turn conversations.
    /// </summary>
    /// <example>
    /// <code>
    /// // First turn
    /// var turn1 = await openAI.Responses.CreateResponse(new CreateResponse
    /// {
    ///     Model = "gpt-4o-mini",
    ///     Input = "My name is Alice."
    /// });
    /// 
    /// // Second turn - references the first
    /// var turn2 = await openAI.Responses.CreateResponse(new CreateResponse
    /// {
    ///     Model = "gpt-4o-mini",
    ///     Input = "What is my name?",
    ///     PreviousResponseId = turn1.Id  // Links to previous conversation
    /// });
    /// // turn2 will know the user's name is Alice
    /// </code>
    /// </example>
    [Fact]
    public async Task CreateResponse_WithPreviousResponseId_MaintainsContext()
    {
        // Arrange - First turn: introduce a fact
        var turn1Request = new CreateResponse
        {
            Model = _fixture.DefaultModel,
            Input = "Remember this number: 42. Just acknowledge that you've noted it.",
            MaxOutputTokens = 50,
            Store = true
        };

        var turn1 = await _fixture.OpenAI.Responses.CreateResponse(turn1Request);
        AssertExtensions.AssertCompletedStatus(turn1);

        // Act - Second turn: ask about the fact
        var turn2Request = new CreateResponse
        {
            Model = _fixture.DefaultModel,
            Input = "What number did I ask you to remember? Reply with just the number.",
            PreviousResponseId = turn1.Id,
            MaxOutputTokens = 50
        };

        var turn2 = await _fixture.OpenAI.Responses.CreateResponse(turn2Request);

        // Assert
        AssertExtensions.AssertCompletedStatus(turn2);
        Assert.Equal(turn1.Id, turn2.PreviousResponseId);

        // The response should contain "42"
        var outputText = GetOutputText(turn2);
        Assert.Contains("42", outputText);

        // Clean up
        await _fixture.OpenAI.Responses.DeleteResponse(turn1.Id);
        await _fixture.OpenAI.Responses.DeleteResponse(turn2.Id);
    }

    #endregion

    #region Test 2.2: Multi-turn Conversation

    /// <summary>
    /// Demonstrates a longer multi-turn conversation with context maintained throughout.
    /// This shows how to build a chatbot-style interaction.
    /// </summary>
    /// <example>
    /// <code>
    /// // Build a conversation chain
    /// var turn1 = await openAI.Responses.CreateResponse(new CreateResponse
    /// {
    ///     Model = "gpt-4o-mini",
    ///     Input = "I'm planning a trip to Paris."
    /// });
    /// 
    /// var turn2 = await openAI.Responses.CreateResponse(new CreateResponse
    /// {
    ///     Model = "gpt-4o-mini",
    ///     Input = "What's a good time to visit?",
    ///     PreviousResponseId = turn1.Id
    /// });
    /// 
    /// var turn3 = await openAI.Responses.CreateResponse(new CreateResponse
    /// {
    ///     Model = "gpt-4o-mini",
    ///     Input = "What should I pack?",
    ///     PreviousResponseId = turn2.Id  // Chain continues
    /// });
    /// </code>
    /// </example>
    [Fact]
    public async Task CreateResponse_MultiTurnConversation_RemembersPriorContext()
    {
        var responseIds = new List<string>();

        try
        {
            // Turn 1: Establish context
            var turn1 = await _fixture.OpenAI.Responses.CreateResponse(new CreateResponse
            {
                Model = _fixture.DefaultModel,
                Input = "I have a pet cat named Whiskers. Just acknowledge this.",
                MaxOutputTokens = 50,
                Store = true
            });
            AssertExtensions.AssertCompletedStatus(turn1);
            responseIds.Add(turn1.Id);

            // Turn 2: Add more context
            var turn2 = await _fixture.OpenAI.Responses.CreateResponse(new CreateResponse
            {
                Model = _fixture.DefaultModel,
                Input = "Whiskers is 3 years old. Just acknowledge this.",
                PreviousResponseId = turn1.Id,
                MaxOutputTokens = 50,
                Store = true
            });
            AssertExtensions.AssertCompletedStatus(turn2);
            responseIds.Add(turn2.Id);

            // Turn 3: Query the accumulated context
            var turn3 = await _fixture.OpenAI.Responses.CreateResponse(new CreateResponse
            {
                Model = _fixture.DefaultModel,
                Input = "What is the name and age of my pet? Answer in format: 'Name: X, Age: Y'",
                PreviousResponseId = turn2.Id,
                MaxOutputTokens = 50,
                Store = true
            });
            AssertExtensions.AssertCompletedStatus(turn3);
            responseIds.Add(turn3.Id);

            // Assert - Turn 3 should know both facts from earlier turns
            var outputText = GetOutputText(turn3);
            Assert.Contains("Whiskers", outputText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("3", outputText);
        }
        finally
        {
            // Clean up all responses
            foreach (var id in responseIds)
            {
                try { await _fixture.OpenAI.Responses.DeleteResponse(id); }
                catch { /* Ignore cleanup errors */ }
            }
        }
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


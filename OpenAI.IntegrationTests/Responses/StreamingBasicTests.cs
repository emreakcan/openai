using Betalgo.Ranul.OpenAI.Contracts.Requests.Responses;
using Betalgo.Ranul.OpenAI.Contracts.Types.Streaming;
using Betalgo.Ranul.OpenAI.Contracts.Types.Streaming.Events.Lifecycle;
using Betalgo.Ranul.OpenAI.Contracts.Types.Streaming.Events.Text;
using Betalgo.Ranul.OpenAI.Extensions.Streaming;
using OpenAI.IntegrationTests.Fixtures;
using OpenAI.IntegrationTests.Helpers;

namespace OpenAI.IntegrationTests.Responses;

/// <summary>
///     Integration tests for Responses API streaming functionality.
///     These tests demonstrate streaming patterns using IAsyncEnumerable.
/// </summary>
/// <remarks>
///     Learn more: https://platform.openai.com/docs/api-reference/responses-streaming
/// </remarks>
[Collection("OpenAI")]
[Trait("Category", "Integration")]
public class StreamingBasicTests
{
    private readonly OpenAITestFixture _fixture;

    public StreamingBasicTests(OpenAITestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Test: Basic Streaming

    /// <summary>
    ///     Demonstrates basic streaming using await foreach.
    /// </summary>
    [Fact]
    public async Task CreateAsStreamAsync_WithSimpleInput_StreamsEvents()
    {
        // Arrange
        var request = new CreateResponse
        {
            Model = _fixture.DefaultModel,
            Input = "Say hello in exactly 3 words."
        };

        var events = new List<IResponseStreamEvent>();

        // Act
        await foreach (var evt in _fixture.OpenAI.Responses.CreateAsStreamAsync(request))
        {
            events.Add(evt);
        }

        // Assert
        Assert.NotEmpty(events);
        Assert.Contains(events, e => e is ResponseCreatedEvent);
        Assert.Contains(events, e => e is ResponseCompletedEvent);
    }

    /// <summary>
    ///     Demonstrates pattern matching on streaming events.
    /// </summary>
    [Fact]
    public async Task CreateAsStreamAsync_WithPatternMatching_HandlesEvents()
    {
        // Arrange
        var request = new CreateResponse
        {
            Model = _fixture.DefaultModel,
            Input = "Count from 1 to 3."
        };

        var textDeltas = new List<string>();
        var hasCreated = false;
        var hasCompleted = false;

        // Act
        await foreach (var evt in _fixture.OpenAI.Responses.CreateAsStreamAsync(request))
        {
            switch (evt)
            {
                case ResponseCreatedEvent:
                    hasCreated = true;
                    break;
                case ResponseTextDeltaEvent textDelta:
                    textDeltas.Add(textDelta.Delta);
                    break;
                case ResponseCompletedEvent:
                    hasCompleted = true;
                    break;
            }
        }

        // Assert
        Assert.True(hasCreated, "should receive ResponseCreatedEvent");
        Assert.True(hasCompleted, "should receive ResponseCompletedEvent");
        Assert.NotEmpty(textDeltas);
    }

    #endregion

    #region Test: Accumulators

    /// <summary>
    ///     Demonstrates using GetTextAsync() accumulator for simple text extraction.
    /// </summary>
    [Fact]
    public async Task GetTextAsync_WithSimpleInput_ReturnsAccumulatedText()
    {
        // Arrange
        var request = new CreateResponse
        {
            Model = _fixture.DefaultModel,
            Input = "Say 'Hello, World!' exactly."
        };

        // Act
        var text = await _fixture.OpenAI.Responses.CreateAsStreamAsync(request).GetTextAsync();

        // Assert
        Assert.NotNull(text);
        Assert.NotEmpty(text);
        Assert.Contains("Hello", text);
    }

    /// <summary>
    ///     Demonstrates using GetFinalResponseAsync() to get the completed response.
    /// </summary>
    [Fact]
    public async Task GetFinalResponseAsync_WithSimpleInput_ReturnsResponse()
    {
        // Arrange
        var request = new CreateResponse
        {
            Model = _fixture.DefaultModel,
            Input = "Say 'Test' exactly."
        };

        // Act
        var response = await _fixture.OpenAI.Responses.CreateAsStreamAsync(request).GetFinalResponseAsync();

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.Id);
        Assert.NotEmpty(response.Id);
        Assert.NotNull(response.Usage);
        Assert.True(response.Usage.TotalTokens > 0);
    }

    /// <summary>
    ///     Demonstrates using WithTextAccumulation() for streaming with running total.
    /// </summary>
    [Fact]
    public async Task WithTextAccumulation_WithInput_TracksAccumulatedText()
    {
        // Arrange
        var request = new CreateResponse
        {
            Model = _fixture.DefaultModel,
            Input = "Count: 1, 2, 3."
        };

        var lastAccumulatedText = string.Empty;
        var textLengths = new List<int>();

        // Act
        await foreach (var (evt, accumulatedText) in _fixture.OpenAI.Responses.CreateAsStreamAsync(request).WithTextAccumulation())
        {
            textLengths.Add(accumulatedText.Length);
            lastAccumulatedText = accumulatedText;
        }

        // Assert
        Assert.NotNull(lastAccumulatedText);
        Assert.NotEmpty(lastAccumulatedText);
    }

    #endregion

    #region Test: Fluent Callback API

    /// <summary>
    ///     Demonstrates using the fluent callback API.
    /// </summary>
    [Fact]
    public async Task FluentCallbackApi_WithTextDeltaHandler_ReceivesDeltas()
    {
        // Arrange
        var request = new CreateResponse
        {
            Model = _fixture.DefaultModel,
            Input = "Say 'Streaming works!' exactly."
        };

        var textDeltas = new List<string>();
        var wasCompleted = false;

        // Act
        await _fixture.OpenAI.Responses.CreateAsStreamAsync(request)
            .OnTextDelta(delta => textDeltas.Add(delta))
            .OnCompleted(_ => wasCompleted = true)
            .ExecuteAsync();

        // Assert
        Assert.NotEmpty(textDeltas);
        Assert.True(wasCompleted);
    }

    /// <summary>
    ///     Demonstrates using ExecuteAndGetResponseAsync().
    /// </summary>
    [Fact]
    public async Task FluentCallbackApi_ExecuteAndGetResponse_ReturnsResponse()
    {
        // Arrange
        var request = new CreateResponse
        {
            Model = _fixture.DefaultModel,
            Input = "Say 'Done' exactly."
        };

        var receivedDeltas = false;

        // Act
        var response = await _fixture.OpenAI.Responses.CreateAsStreamAsync(request)
            .OnTextDelta(_ => receivedDeltas = true)
            .ExecuteAndGetResponseAsync();

        // Assert
        Assert.True(receivedDeltas);
        Assert.NotNull(response);
        Assert.NotNull(response.Id);
        Assert.NotEmpty(response.Id);
    }

    #endregion

    #region Test: Event Filters

    /// <summary>
    ///     Demonstrates using TextEventsOnly() filter.
    /// </summary>
    [Fact]
    public async Task TextEventsOnly_WithInput_FiltersToTextEvents()
    {
        // Arrange
        var request = new CreateResponse
        {
            Model = _fixture.DefaultModel,
            Input = "Say 'Filtered!' exactly."
        };

        var events = new List<IResponseStreamEvent>();

        // Act
        await foreach (var evt in _fixture.OpenAI.Responses.CreateAsStreamAsync(request).TextEventsOnly())
        {
            events.Add(evt);
        }

        // Assert
        Assert.NotEmpty(events);
        Assert.All(events, e => Assert.True(
            e is ResponseTextDeltaEvent or ResponseTextDoneEvent or ResponseOutputTextAnnotationAddedEvent,
            "All events should be text events"));
    }

    /// <summary>
    ///     Demonstrates using OfEventType() for strongly-typed filtering.
    /// </summary>
    [Fact]
    public async Task OfEventType_WithInput_ReturnsStronglyTypedEvents()
    {
        // Arrange
        var request = new CreateResponse
        {
            Model = _fixture.DefaultModel,
            Input = "Say 'Typed!' exactly."
        };

        var deltas = new List<ResponseTextDeltaEvent>();

        // Act
        await foreach (var delta in _fixture.OpenAI.Responses.CreateAsStreamAsync(request).OfEventType<ResponseTextDeltaEvent>())
        {
            deltas.Add(delta);
        }

        // Assert
        Assert.NotEmpty(deltas);
        Assert.All(deltas, d =>
        {
            Assert.NotNull(d.Delta);
            Assert.True(d.SequenceNumber >= 0);
        });
    }

    /// <summary>
    ///     Demonstrates using LifecycleEventsOnly() filter.
    /// </summary>
    [Fact]
    public async Task LifecycleEventsOnly_WithInput_FiltersToLifecycleEvents()
    {
        // Arrange
        var request = new CreateResponse
        {
            Model = _fixture.DefaultModel,
            Input = "Say 'Lifecycle!' exactly."
        };

        var events = new List<IResponseStreamEvent>();

        // Act
        await foreach (var evt in _fixture.OpenAI.Responses.CreateAsStreamAsync(request).LifecycleEventsOnly())
        {
            events.Add(evt);
        }

        // Assert
        Assert.NotEmpty(events);
        Assert.Contains(events, e => e is ResponseCreatedEvent);
        Assert.Contains(events, e => e is ResponseInProgressEvent or ResponseCompletedEvent);
    }

    #endregion
}

using System.Net;
using System.Text;
using Betalgo.Ranul.OpenAI.Contracts.Types.Streaming;
using Betalgo.Ranul.OpenAI.Contracts.Types.Streaming.Events.Text;
using Betalgo.Ranul.OpenAI.Extensions.Streaming;
using Shouldly;

namespace OpenAI.SDK.Tests;

public class StreamParserTests
{
    [Fact]
    public async Task ParseSseStreamAsyncParsesMultilineEventsAndStopsOnDone()
    {
        using var response = CreateEventStreamResponse("""
: comment
event: response.output_text.delta
data: {"type":"response.output_text.delta","sequence_number":1,
data: "item_id":"item_1","output_index":0,"content_index":0,"delta":"Hello"}

data: [DONE]

data: {"type":"response.output_text.delta","sequence_number":2,"item_id":"item_2","output_index":0,"content_index":0,"delta":"Ignored"}

""");

        var events = await ToListAsync(response.ParseSseStreamAsync<IResponseStreamEvent>());

        events.Count.ShouldBe(1);
        var delta = events[0].ShouldBeOfType<ResponseTextDeltaEvent>();
        delta.Delta.ShouldBe("Hello");
    }

    [Fact]
    public async Task ParseSseStreamAsyncSkipsMalformedEventsWithoutConsumingSubsequentBlocks()
    {
        using var response = CreateEventStreamResponse("""
data: {"type":"response.output_text.delta","sequence_number":1,
data: bad-json}

data: {"type":"response.output_text.delta","sequence_number":2,"item_id":"item_2","output_index":0,"content_index":0,"delta":"Recovered"}

data: [DONE]

""");

        var events = await ToListAsync(response.ParseSseStreamAsync<IResponseStreamEvent>());

        events.Count.ShouldBe(1);
        var delta = events[0].ShouldBeOfType<ResponseTextDeltaEvent>();
        delta.Delta.ShouldBe("Recovered");
        delta.SequenceNumber.ShouldBe(2);
    }

    private static HttpResponseMessage CreateEventStreamResponse(string payload)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "text/event-stream")
        };
    }

    private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> values)
    {
        var results = new List<T>();
        await foreach (var value in values)
        {
            results.Add(value);
        }

        return results;
    }
}

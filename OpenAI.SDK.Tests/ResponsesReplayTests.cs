using System.Text.Json;
using System.Text.Json.Serialization;
using Betalgo.Ranul.OpenAI;
using Betalgo.Ranul.OpenAI.Contracts.Requests.Responses;
using Betalgo.Ranul.OpenAI.Contracts.Responses.Responses;
using Betalgo.Ranul.OpenAI.Contracts.Types;
using Betalgo.Ranul.OpenAI.Contracts.Types.Content;
using Betalgo.Ranul.OpenAI.Contracts.Types.Items;

namespace OpenAI.SDK.Tests;

public class ResponsesReplayTests
{
    [Fact]
    public void ReplayRetainsAllSerializedOutputFieldsAndOrder()
    {
        var response = JsonSerializer.Deserialize<Response>("""
            {"output":[
             {"type":"message","id":"msg_1","role":"assistant","status":"completed","content":[{"type":"output_text","text":"hello","annotations":[]}]},
             {"type":"function_call","id":"fc_1","call_id":"call_1","name":"lookup","arguments":"{\"x\":1}","status":"completed"},
             {"type":"computer_call","id":"cu_1","call_id":"call_2","action":{"type":"click","x":1,"y":2,"button":"left"},"pending_safety_checks":[],"status":"completed"}
            ]}
            """)!;
        var expected = JsonSerializer.Serialize(response.Output);
        var replay = response.ToInputItems();
        Assert.Equal(expected, JsonSerializer.Serialize(replay));
        Assert.Equal(new[] { "message", "function_call", "computer_call" }, replay.Select(item => item.Type));
        var request = JsonSerializer.Serialize(new CreateResponse { Model = "fixture", Input = replay });
        using var parsed = JsonDocument.Parse(request);
        Assert.Equal("msg_1", parsed.RootElement.GetProperty("input")[0].GetProperty("id").GetString());
        Assert.Equal("call_1", parsed.RootElement.GetProperty("input")[1].GetProperty("call_id").GetString());
    }

    [Fact]
    public void ReplayIsDetachedFromMutableSourceContentAndCollections()
    {
        var text = new OutputTextContent("original");
        var message = new OutputMessageItem { Id = "msg_1", Content = new List<IContent> { text } };
        var response = new Response { Output = new List<IOutputItem> { message } };
        var replay = response.ToInputItems();
        var snapshot = JsonSerializer.Serialize(replay);
        text.Text = "changed";
        message.Id = "changed";
        message.Content.Clear();
        response.Output.Clear();
        GC.Collect();
        Assert.Equal(snapshot, JsonSerializer.Serialize(replay));
        Assert.Contains("original", snapshot);
    }

    [Fact]
    public void ReplayRetainsOpaqueItemPayloadWithoutInterpretingItsType()
    {
        using var document = JsonDocument.Parse("""
            {"type":"future_tool_result","id":"opaque_1","payload":{"nested":[1,"two",null]},"encrypted_content":"fixture_ciphertext"}
            """);
        var item = new OpaqueFixture
        {
            Data = document.RootElement.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.Clone())
        };
        var response = new Response { Output = new List<IOutputItem> { item } };
        var replay = response.ToInputItems();
        Assert.Equal(JsonSerializer.Serialize(response.Output), JsonSerializer.Serialize(replay));
        item.Data.Clear();
        Assert.Contains("fixture_ciphertext", JsonSerializer.Serialize(replay));
    }

    private sealed class OpaqueFixture : IOutputItem
    {
        [JsonIgnore]
        public string Type => "future_tool_result";
        [JsonExtensionData]
        public Dictionary<string, JsonElement> Data { get; set; } = new();
    }

    [Fact]
    public void EmptyReplayIsMutableAndNullReceiverIsRejected()
    {
        Assert.Empty(new Response().ToInputItems());
        Assert.Empty(new Response { Output = new List<IOutputItem>() }.ToInputItems());
        Assert.Throws<ArgumentNullException>(() => ((Response)null!).ToInputItems());
    }
}

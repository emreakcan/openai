using System.Net;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Betalgo.Ranul.OpenAI;
using Betalgo.Ranul.OpenAI.Contracts.Requests.Responses;
using Betalgo.Ranul.OpenAI.Contracts.Types;
using Betalgo.Ranul.OpenAI.Contracts.Types.Tools;
using Betalgo.Ranul.OpenAI.Contracts.Types.TextFormat;
using Betalgo.Ranul.OpenAI.Contracts.Types.Content;
using Betalgo.Ranul.OpenAI.Contracts.Types.Items;
using Betalgo.Ranul.OpenAI.Contracts.Types.Streaming.Events.Lifecycle;
using Betalgo.Ranul.OpenAI.Contracts.Types.Streaming.Events.Text;
using Betalgo.Ranul.OpenAI.Managers;

var packageVersion = typeof(OpenAIService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
var candidateCommit = packageVersion?.Split('+').LastOrDefault();
var mode = args.Length == 0 ? "--preflight" : args[0];
var scenario = args.Length < 2 ? "basic" : args[1];
var key = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var model = Environment.GetEnvironmentVariable("OPENAI_TEST_MODEL");
var report = new Dictionary<string, object?>
{
    ["timestamp_utc"] = DateTimeOffset.UtcNow,
    ["candidate_commit"] = candidateCommit,
    ["candidate_package"] = packageVersion,
    ["runtime"] = RuntimeInformation.FrameworkDescription,
    ["os"] = RuntimeInformation.OSDescription,
    ["architecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
    ["mode"] = mode,
    ["scenario"] = scenario
};

void WriteReport() => Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
void Require([DoesNotReturnIf(false)] bool condition, string message)
{
    if (!condition)
    {
        report["failed_check"] = message;
        throw new InvalidOperationException(message);
    }
}

if (mode == "--preflight")
{
    report["result"] = "PREFLIGHT_ONLY";
    report["api_key_configured"] = !string.IsNullOrWhiteSpace(key);
    report["test_model_configured"] = !string.IsNullOrWhiteSpace(model);
    report["sdk_assembly_version"] = typeof(OpenAIService).Assembly.GetName().Version?.ToString();
    report["sdk_informational_version"] = typeof(OpenAIService).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    report["live_requests"] = 0;
    WriteReport();
    return 0;
}

if ((mode != "--offline" && mode != "--live") || !new[] { "basic", "stream", "structured", "function", "context" }.Contains(scenario))
{
    report["result"] = "INVALID_ARGUMENTS";
    report["usage"] = "ResponsesSmoke [--preflight | --offline basic|stream|structured|function|context | --live basic|stream|structured|function|context]";
    WriteReport();
    return 2;
}

if (mode == "--live" && (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(model)))
{
    report["result"] = "NOT_RUN_MISSING_CONFIGURATION";
    report["requires"] = "OPENAI_API_KEY and OPENAI_TEST_MODEL configured locally";
    report["live_requests"] = 0;
    WriteReport();
    return 2;
}

var offline = mode == "--offline";
var metadata = new MetadataHandler(offline ? new FixtureHandler(scenario) : new HttpClientHandler(), scenario is "function" or "context" ? 4 : 1);
using var http = new HttpClient(metadata) { Timeout = TimeSpan.FromSeconds(60) };
using var service = new OpenAIService(new OpenAIOptions { ApiKey = offline ? "fixture-only" : key! }, http);
using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
var request = new CreateResponse
{
    Model = offline ? "fixture-model" : model!,
    Input = "Reply with the single word READY.",
    Store = false,
    MaxOutputTokens = 256,
    Reasoning = new() { Effort = "none" },
    ServiceTier = "default"
};
report["requested_model"] = request.Model;
report["max_output_tokens"] = request.MaxOutputTokens;

var storedIds = new List<string>();
var usages = new List<object>();
void CheckResponse(Betalgo.Ranul.OpenAI.Contracts.Responses.Responses.Response response)
{
    Require(response.Successful, "API returned an error response");
    Require(response.Status?.ToString() == "completed", "Response did not complete");
    Require(response.Usage is { TotalTokens: > 0 }, "Missing token usage");
    usages.Add(new { response.Id, response.Model, response.Usage });
}
try
{
    if (scenario == "basic")
    {
        var response = await service.CreateResponse(request, deadline.Token);
        report["http_status"] = metadata.StatusCode;
        report["response_id"] = response.Id;
        report["response_status"] = response.Status?.ToString();
        report["returned_model"] = response.Model;
        report["usage"] = response.Usage;
        report["error_code"] = response.Error?.Code;
        report["error_type"] = response.Error?.Type;
        report["output_text_property_present"] = !string.IsNullOrWhiteSpace(response.OutputText);
        Require(response.Successful, "API returned an error response");
        Require(response.Status?.ToString() == "completed", "Response did not complete");
        var text = string.Concat((response.Output ?? []).OfType<OutputMessageItem>()
            .SelectMany(message => message.Content).OfType<OutputTextContent>().Select(content => content.Text));
        Require(!string.IsNullOrWhiteSpace(text), "No parsed text output");
        Require(response.Usage is { TotalTokens: > 0 }, "No parsed token usage");
        report["parsed_text_characters"] = text.Length;
    }
    else if (scenario == "stream")
    {
        var count = 0;
        var text = new StringBuilder();
        ResponseCompletedEvent? completed = null;
        await foreach (var evt in service.CreateAsStreamAsync(request, deadline.Token))
        {
            count++;
            if (evt is ResponseTextDeltaEvent delta) text.Append(delta.Delta);
            if (evt is ResponseCompletedEvent terminal) completed = terminal;
        }
        Require(completed is not null, "No parsed response.completed event");
        Require(text.Length > 0, "No parsed text deltas");
        Require(completed.Response.Status?.ToString() == "completed", "Streamed response did not complete");
        Require(completed.Response.Usage is { TotalTokens: > 0 }, "No parsed streaming token usage");
        report["response_id"] = completed.Response.Id;
        report["returned_model"] = completed.Response.Model;
        report["usage"] = completed.Response.Usage;
        report["event_count"] = count;
        report["parsed_text_characters"] = text.Length;
    }
    else if (scenario == "structured")
    {
        request.Input = "Return the integer 7 in the answer field.";
        request.Text = new TextConfig { Format = TextFormat.JsonSchema("answer", new()
        {
            ["type"] = "object", ["additionalProperties"] = false,
            ["properties"] = new Dictionary<string, object>
            {
                ["answer"] = new Dictionary<string, object> { ["type"] = "integer" }
            },
            ["required"] = new[] { "answer" }
        }, strict: true) };
        var response = await service.CreateResponse(request, deadline.Token);
        CheckResponse(response);
        using var json = JsonDocument.Parse(response.OutputText!);
        Require(json.RootElement.GetProperty("answer").GetInt32() == 7, "Structured value mismatch");
        Require(json.RootElement.EnumerateObject().Count() == 1, "Unexpected structured properties");
        report["validated_schema"] = "one required integer answer; no additional properties";
    }
    else if (scenario is "function" or "context")
    {
        request.Store = true;
        if (scenario == "function")
        {
            request.Input = "Call get_test_value with key alpha. After receiving its result, reply with exactly that result.";
            request.Tools = [new FunctionTool
            {
                Name = "get_test_value", Strict = true,
                Parameters = """{"type":"object","properties":{"key":{"type":"string","enum":["alpha"]}},"required":["key"],"additionalProperties":false}"""
            }];
            request.ToolChoice = "required";
        }
        else request.Input = "Remember this code for my next question: BLUE_7319. Reply OK.";
        var first = await service.CreateResponse(request, deadline.Token);
        if (!string.IsNullOrEmpty(first.Id)) storedIds.Add(first.Id);
        CheckResponse(first);
        request.PreviousResponseId = first.Id;
        request.Store = true;
        if (scenario == "function")
        {
            var call = first.Output!.OfType<FunctionToolCallItem>().Single();
            Require(call.Name == "get_test_value", "Unexpected function name");
            using var arguments = JsonDocument.Parse(call.Arguments);
            Require(arguments.RootElement.GetProperty("key").GetString() == "alpha", "Unexpected tool arguments");
            Require(!string.IsNullOrEmpty(call.CallId), "Missing call correlation ID");
            request.Input = new InputParam([new FunctionCallOutputItemParam(call.CallId, "VALUE_7841")]);
            request.ToolChoice = "none";
            report["tool_arguments_validated"] = true;
        }
        else request.Input = "What code did I ask you to remember? Reply with only the code.";
        var second = await service.CreateResponse(request, deadline.Token);
        if (!string.IsNullOrEmpty(second.Id)) storedIds.Add(second.Id);
        CheckResponse(second);
        Require(second.PreviousResponseId == first.Id, "Missing response lineage");
        Require(second.OutputText?.Trim() == (scenario == "function" ? "VALUE_7841" : "BLUE_7319"), "Follow-up result mismatch");
        report["follow_up_validated"] = true;
    }
    Require(metadata.RequestCount == (scenario is "function" or "context" ? 2 : 1), "Unexpected number of requests");
    report["result"] = offline ? "OFFLINE_PASS" : "LIVE_PASS";
}
catch (Exception error)
{
    report["result"] = offline ? "OFFLINE_FAIL" : "LIVE_FAIL";
    report["exception_type"] = error.GetType().Name;
    // Provider exception messages may contain response bodies; do not persist them here.
}
finally
{
    // Delete every stored synthetic response, including when an assertion failed.
    using var cleanupDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    var deleted = 0;
    foreach (var id in storedIds)
    {
        try
        {
            var result = await service.DeleteResponse(id, cleanupDeadline.Token);
            if (result.Deleted) deleted++;
        }
        catch { /* Record failure without persisting provider payloads. */ }
    }
    report["stored_responses"] = storedIds.Count;
    report["deleted_responses"] = deleted;
    if (deleted != storedIds.Count)
    {
        report["result"] = offline ? "OFFLINE_FAIL" : "LIVE_FAIL";
        report["cleanup_incomplete"] = true;
    }
}
report["workflow_usage"] = usages;
report["http_status"] = metadata.StatusCode;
report["request_ids"] = metadata.RequestIds;
report["request_count"] = metadata.RequestCount;
report["live_requests"] = offline ? 0 : metadata.RequestCount;
WriteReport();
return report["result"]?.ToString()?.EndsWith("_PASS", StringComparison.Ordinal) is true ? 0 : 1;

sealed class MetadataHandler(HttpMessageHandler inner, int maxRequests) : DelegatingHandler(inner)
{
    public int RequestCount { get; private set; }
    public int? StatusCode { get; private set; }
    public List<string> RequestIds { get; } = [];
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (RequestCount >= maxRequests) throw new InvalidOperationException("Smoke request limit exceeded");
        RequestCount++;
        var response = await base.SendAsync(request, cancellationToken);
        StatusCode = (int)response.StatusCode;
        if (response.Headers.TryGetValues("x-request-id", out var ids)) RequestIds.AddRange(ids);
        return response;
    }
}

sealed class FixtureHandler(string scenario) : HttpMessageHandler
{
    const string completed = """{"id":"resp_fixture","object":"response","status":"completed","model":"fixture-model","output":[{"type":"message","id":"msg_fixture","status":"completed","role":"assistant","content":[{"type":"output_text","text":"READY","annotations":[]}]}],"usage":{"input_tokens":5,"output_tokens":1,"total_tokens":6}}""";
    private int posts;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Delete)
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"resp_fixture\",\"deleted\":true}", Encoding.UTF8, "application/json") };
        posts++;
        if (request.Method != HttpMethod.Post || request.RequestUri?.AbsolutePath != "/v1/responses")
            throw new InvalidOperationException("Unexpected endpoint");
        using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
        if (body.RootElement.GetProperty("store").GetBoolean() != (scenario is "function" or "context")
            || body.RootElement.GetProperty("max_output_tokens").GetInt32() != 256
            || body.RootElement.GetProperty("service_tier").GetString() != "default"
            || body.RootElement.GetProperty("reasoning").GetProperty("effort").GetString() != "none")
            throw new InvalidOperationException("Request limits were not serialized");
        var streaming = body.RootElement.TryGetProperty("stream", out var flag) && flag.GetBoolean();
        var fixture = completed;
        if (scenario == "structured")
        {
            var format = body.RootElement.GetProperty("text").GetProperty("format");
            if (format.GetProperty("name").GetString() != "answer" || !format.GetProperty("strict").GetBoolean()
                || format.TryGetProperty("json_schema", out _)) throw new InvalidOperationException("Invalid schema wire format");
            fixture = completed.Replace("READY", JsonSerializer.Serialize("{\"answer\":7}")[1..^1]);
        }
        if (scenario == "function" && posts == 1)
            fixture = """{"id":"resp_fixture","status":"completed","model":"fixture-model","output":[{"type":"function_call","name":"get_test_value","call_id":"call_fixture","arguments":"{\"key\":\"alpha\"}"}],"usage":{"input_tokens":5,"output_tokens":1,"total_tokens":6}}""";
        if (scenario is "function" or "context" && posts == 2)
        {
            if (body.RootElement.GetProperty("previous_response_id").GetString() != "resp_fixture") throw new InvalidOperationException("Missing lineage");
            if (scenario == "function" && body.RootElement.GetProperty("input")[0].GetProperty("call_id").GetString() != "call_fixture") throw new InvalidOperationException("Missing call correlation");
            fixture = completed.Replace("READY", scenario == "function" ? "VALUE_7841" : "BLUE_7319").Replace("\"status\":", "\"previous_response_id\":\"resp_fixture\",\"status\":");
        }
        var payload = streaming
            ? "data: {\"type\":\"response.output_text.delta\",\"sequence_number\":1,\"item_id\":\"msg_fixture\",\"output_index\":0,\"content_index\":0,\"delta\":\"READY\"}\n\n"
                + "data: {\"type\":\"response.completed\",\"sequence_number\":2,\"response\":" + completed + "}\n\n"
            : fixture;
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, streaming ? "text/event-stream" : "application/json")
        };
        response.Headers.Add("x-request-id", "fixture-request");
        return response;
    }
}

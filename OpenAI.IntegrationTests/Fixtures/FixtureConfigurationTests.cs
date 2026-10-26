using System.Net;
using System.Text;
using Betalgo.Ranul.OpenAI.Contracts.Requests.Responses;
using Microsoft.Extensions.Configuration;

namespace OpenAI.IntegrationTests.Fixtures;

[Trait("Category", "Offline")]
public class FixtureConfigurationTests
{
    [Theory]
    [InlineData("OPENAI_API_KEY")]
    [InlineData("OpenAIServiceOptions:ApiKey")]
    [InlineData("OpenAI:ApiKey")]
    public async Task SupportedCredentialSettingsReachTheActualHttpClient(string setting)
    {
        var handler = new CapturingHandler();
        using var fixture = new OpenAITestFixture(Configuration((setting, "offline-test-key")), handler);

        var response = await fixture.OpenAI.Responses.CreateResponse(new CreateResponse
        {
            Model = "offline-model",
            Input = "hello"
        });

        Assert.Equal("Bearer offline-test-key", handler.Authorization);
        Assert.Equal("resp_offline", response.Id);
    }

    [Fact]
    public async Task StandardEnvironmentKeyOverridesCanonicalAndLegacyKeys()
    {
        var handler = new CapturingHandler();
        using var fixture = new OpenAITestFixture(Configuration(
            ("OPENAI_API_KEY", "standard-key"),
            ("OpenAIServiceOptions:ApiKey", "canonical-key"),
            ("OpenAI:ApiKey", "legacy-key")), handler);
        await fixture.OpenAI.Responses.CreateResponse(new CreateResponse { Model = "offline-model", Input = "hello" });
        Assert.Equal("Bearer standard-key", handler.Authorization);
    }

    [Fact]
    public async Task BlankStandardKeyFallsBackToCanonicalSetting()
    {
        var handler = new CapturingHandler();
        using var fixture = new OpenAITestFixture(Configuration(
            ("OPENAI_API_KEY", " "),
            ("OpenAIServiceOptions:ApiKey", "canonical-key"),
            ("OpenAI:ApiKey", "legacy-key")), handler);
        await fixture.OpenAI.Responses.CreateResponse(new CreateResponse { Model = "offline-model", Input = "hello" });
        Assert.Equal("Bearer canonical-key", handler.Authorization);
    }

    [Fact]
    public void MissingCredentialsFailBeforeHttpClientConstruction()
    {
        var error = Assert.Throws<InvalidOperationException>(() => new OpenAITestFixture(Configuration()));
        Assert.Contains("OPENAI_API_KEY", error.Message);
    }

    [Fact]
    public void ModelAndStreamingLimitOverridesAreExplicit()
    {
        using var fixture = new OpenAITestFixture(Configuration(
            ("OPENAI_API_KEY", "offline-key"),
            ("OpenAI:DefaultModel", "configured-default"),
            ("OpenAI:ReasoningModel", "configured-reasoning"),
            ("OPENAI_TEST_MODEL", "override-default"),
            ("OPENAI_TEST_REASONING_MODEL", "override-reasoning"),
            ("OPENAI_TEST_MAX_OUTPUT_TOKENS", "512")), new CapturingHandler());
        Assert.Equal("override-default", fixture.DefaultModel);
        Assert.Equal("override-reasoning", fixture.ReasoningModel);
        Assert.Equal(512, fixture.DefaultMaxOutputTokens);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("4097")]
    [InlineData("invalid")]
    public void InvalidOutputLimitsFailBeforeMakingRequests(string limit)
    {
        Assert.Throws<InvalidOperationException>(() => new OpenAITestFixture(Configuration(
            ("OPENAI_API_KEY", "offline-key"), ("OPENAI_TEST_MAX_OUTPUT_TOKENS", limit))));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("0", false)]
    [InlineData("true", false)]
    [InlineData("1", true)]
    public void LiveTestsRequireAnExplicitOptIn(string? value, bool enabled)
    {
        Assert.Equal(enabled, LiveFactAttribute.IsOptedIn(value));
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(
            settings.Select(setting => new KeyValuePair<string, string?>(setting.Key, setting.Value))).Build();

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"resp_offline","status":"completed","output":[]}""", Encoding.UTF8, "application/json")
            });
        }
    }
}

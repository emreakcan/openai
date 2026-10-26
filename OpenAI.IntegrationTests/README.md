# Responses integration tests

A normal `dotnet test OpenAI.IntegrationTests` runs offline configuration tests and
skips paid API tests. Merely configuring credentials never enables live tests.
The offline tests inject an HTTP handler and verify the authorization header from
the actual DI-created SDK client; no OpenAI requests are made.

## Live tests

Configure credentials locally using an environment variable or user secrets.
Never put an API key into committed appsettings files.

```sh
dotnet user-secrets set OpenAIServiceOptions:ApiKey '<key>' --project OpenAI.IntegrationTests
export OPENAI_RUN_LIVE_TESTS=1
dotnet test OpenAI.IntegrationTests --filter 'FullyQualifiedName~BasicResponseTests'
```

The fixture reads providers in this order: appsettings.json,
appsettings.Development.json, user secrets, environment variables (last wins).
It then selects the first nonblank key from:

1. `OPENAI_API_KEY`
2. `OpenAIServiceOptions:ApiKey` (`OpenAIServiceOptions__ApiKey` in the environment)
3. Legacy `OpenAI:ApiKey` (`OpenAI__ApiKey` in the environment)

The chosen key is both validated and assigned to the SDK options. Other SDK
options retain normal `OpenAIServiceOptions` configuration binding.

Model selection is independent of credentials:

| Setting | Fallback |
| --- | --- |
| `OPENAI_TEST_MODEL` | `OpenAI:DefaultModel`, then `gpt-4o-mini` |
| `OPENAI_TEST_REASONING_MODEL` | `OpenAI:ReasoningModel`, then `o3-mini` |
| `OPENAI_TEST_MAX_OUTPUT_TOKENS` | 256; accepts 1–4096 |

The configurable output cap applies to streaming tests that previously had no
limit. Existing tests with explicit output limits keep them. HTTP requests have a
60-second timeout; streaming tests also pass a 60-second cancellation token that
covers reading the stream after headers arrive. There are no automatic retries.
These controls bound individual requests, not total monetary spend.

Run focused filters first and track usage externally. The full suite makes many
requests, includes built-in tools and stored responses, and may incur additional
charges. Existing prompts and assertions include model-dependent assumptions
(e.g. short exact answers and reasoning-token expectations). Choosing a different
model does not establish that those assertions are valid for it. Skipped tests and
offline passes are not evidence that live API features work.

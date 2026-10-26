# Verify a candidate through its actual NuGet packages

This separate consumer references a package, never the SDK project. It supports
five bounded scenarios: `basic`, `stream`, `structured`, `function`, `context`.
Each has a local HTTP fixture mode and an explicit paid live mode. The default
`--preflight` reports configuration and package identity without sending requests.

Build the candidate SDK and Contracts with the **same unique prerelease version**,
then pack both with `--no-build`. `GeneratePackageOnBuild` means a pack operation
alone may reuse old binaries: explicitly build first and verify the informational
version in the consumer's report. Restore into a new package directory to avoid
silently reusing an earlier package with the same version.

Example from the repository root (replace VERSION and FEED with your values):

```sh
dotnet build OpenAI.SDK/Betalgo.Ranul.OpenAI.csproj -c Release -p:Version=VERSION
dotnet pack OpenAI.SDK/Betalgo.Ranul.OpenAI.csproj -c Release --no-build -p:Version=VERSION -o FEED
dotnet pack Betalgo.Ranul.OpenAI.Contracts/Betalgo.Ranul.OpenAI.Contracts.csproj -c Release --no-build -p:Version=VERSION -o FEED
dotnet build tools/LiveSmoke/ResponsesSmoke.csproj -c Release -p:CandidateVersion=VERSION --packages FRESH_PACKAGE_DIRECTORY --source FEED --source https://api.nuget.org/v3/index.json
dotnet tools/LiveSmoke/bin/Release/net9.0/ResponsesSmoke.dll --offline structured
```

Run all five offline scenarios before making live calls. Configure
`OPENAI_API_KEY` and `OPENAI_TEST_MODEL` privately, then replace `--offline` with
`--live` for one selected scenario. Do not put the key into a command argument,
source file, report or shell history. Use a dedicated test project and a local
budget ledger. Choose a model supporting Responses, function tools, strict JSON,
and `reasoning.effort=none`; pricing and model compatibility must be reviewed
before the live run. The harness does not enforce an account-wide dollar cap.

Each generation uses short fixed synthetic prompts, Standard service tier,
256 maximum output tokens and a 60-second workflow deadline. There are no retries
or billed built-in tools. Basic, streaming and structured use one request each.
Function and context use two generation requests and at most two cleanup DELETE
requests. Stored response cleanup runs even on assertion failure and has its own
20-second deadline. Cleanup failures make the scenario fail.

Reports contain package commit/version, runtime, model, request IDs, usage and
assertion status. Provider error bodies and credential headers are not persisted.
A `LIVE_PASS` proves only that selected workflow on the recorded model/environment;
it does not establish support for every model or platform. Realtime and media
require their own live evidence.

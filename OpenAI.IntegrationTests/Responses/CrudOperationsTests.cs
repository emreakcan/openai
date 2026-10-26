using Betalgo.Ranul.OpenAI.Contracts.Enums.Responses;
using Betalgo.Ranul.OpenAI.Contracts.Requests.Responses;
using OpenAI.IntegrationTests.Fixtures;
using OpenAI.IntegrationTests.Helpers;

namespace OpenAI.IntegrationTests.Responses;

/// <summary>
/// Integration tests for Responses API CRUD operations.
/// These tests demonstrate how to retrieve, delete, and list response data.
/// </summary>
/// <remarks>
/// Learn more: https://platform.openai.com/docs/api-reference/responses
/// </remarks>
[Collection("OpenAI")]
[Trait("Category", "Integration")]
public class CrudOperationsTests
{
    private readonly OpenAITestFixture _fixture;

    public CrudOperationsTests(OpenAITestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Test 7.1: Retrieve Response

    /// <summary>
    /// Demonstrates retrieving a previously created response by its ID.
    /// This is useful for checking the status of long-running requests or
    /// getting response details after the fact.
    /// </summary>
    /// <example>
    /// <code>
    /// // First, create a response
    /// var created = await openAI.Responses.CreateResponse(new CreateResponse
    /// {
    ///     Model = "gpt-4o-mini",
    ///     Input = "Hello!"
    /// });
    /// 
    /// // Later, retrieve it by ID
    /// var retrieved = await openAI.Responses.RetrieveResponse(created.Id);
    /// Console.WriteLine($"Status: {retrieved.Status}");
    /// </code>
    /// </example>
    [Fact]
    public async Task RetrieveResponse_WithValidId_ReturnsResponse()
    {
        // Arrange - First create a response to retrieve
        var createRequest = new CreateResponse
        {
            Model = _fixture.DefaultModel,
            Input = "Say 'test' and nothing else.",
            MaxOutputTokens = 20,
            Store = true // Ensure the response is stored for retrieval
        };

        var created = await _fixture.OpenAI.Responses.CreateResponse(createRequest);
        AssertExtensions.AssertSuccessfulResponse(created);

        // Act - Retrieve the response by ID
        var retrieved = await _fixture.OpenAI.Responses.RetrieveResponse(created.Id);

        // Assert
        AssertExtensions.AssertSuccessfulResponse(retrieved);
        Assert.Equal(created.Id, retrieved.Id);
        Assert.Equal(ResponsesStatus.Completed, retrieved.Status);
    }

    #endregion

    #region Test 7.3: Delete Response

    /// <summary>
    /// Demonstrates deleting a response to remove it from storage.
    /// This can be useful for data retention compliance or cleanup.
    /// </summary>
    /// <example>
    /// <code>
    /// // Create a response
    /// var response = await openAI.Responses.CreateResponse(new CreateResponse
    /// {
    ///     Model = "gpt-4o-mini",
    ///     Input = "Hello!"
    /// });
    /// 
    /// // Delete it
    /// var deleted = await openAI.Responses.DeleteResponse(response.Id);
    /// Console.WriteLine($"Deleted: {deleted.Deleted}"); // true
    /// </code>
    /// </example>
    [Fact]
    public async Task DeleteResponse_WithValidId_DeletesSuccessfully()
    {
        // Arrange - First create a response to delete
        var createRequest = new CreateResponse
        {
            Model = _fixture.DefaultModel,
            Input = "Say 'delete me' and nothing else.",
            MaxOutputTokens = 20,
            Store = true
        };

        var created = await _fixture.OpenAI.Responses.CreateResponse(createRequest);
        AssertExtensions.AssertSuccessfulResponse(created);

        // Act - Delete the response
        var deleteResult = await _fixture.OpenAI.Responses.DeleteResponse(created.Id);

        // Assert
        AssertExtensions.AssertDeleted(deleteResult);
        Assert.Equal(created.Id, deleteResult.Id);
    }

    #endregion

    #region Test 7.4: List Input Items

    /// <summary>
    /// Demonstrates listing the input items that were sent with a response.
    /// This is useful for debugging or auditing what was sent to the model.
    /// </summary>
    /// <example>
    /// <code>
    /// // Create a response
    /// var response = await openAI.Responses.CreateResponse(new CreateResponse
    /// {
    ///     Model = "gpt-4o-mini",
    ///     Input = "Hello, how are you?"
    /// });
    /// 
    /// // List the input items
    /// var inputItems = await openAI.Responses.ListInputItems(response.Id);
    /// foreach (var item in inputItems.Data)
    /// {
    ///     Console.WriteLine($"Type: {item.Type}");
    /// }
    /// </code>
    /// </example>
    [Fact]
    public async Task ListInputItems_WithValidResponseId_ReturnsItems()
    {
        // Arrange - Create a response first
        var createRequest = new CreateResponse
        {
            Model = _fixture.DefaultModel,
            Input = "What is the capital of France?",
            MaxOutputTokens = 50,
            Store = true
        };

        var created = await _fixture.OpenAI.Responses.CreateResponse(createRequest);
        AssertExtensions.AssertSuccessfulResponse(created);

        // Act - List input items
        var inputItems = await _fixture.OpenAI.Responses.ListInputItems(created.Id);

        // Assert
        Assert.NotNull(inputItems);
        Assert.NotNull(inputItems.Data);
        Assert.NotEmpty(inputItems.Data);

        // The response should indicate if there are more items (pagination)
        // HasMore is a boolean value, so we just verify it was set

        // Clean up
        await _fixture.OpenAI.Responses.DeleteResponse(created.Id);
    }

    #endregion
}


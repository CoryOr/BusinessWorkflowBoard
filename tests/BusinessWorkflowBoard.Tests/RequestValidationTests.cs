using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace BusinessWorkflowBoard.Tests;

public class RequestValidationTests
{
    // Run the same test for each invalid input combination.
    [Theory]

    // Reject empty, whitespace-only, and too-short titles.
    [InlineData("", "Operations", "PurchaseRequest")]
    [InlineData("   ", "Operations", "PurchaseRequest")]
    [InlineData("Bad", "Operations", "PurchaseRequest")]

    // Reject missing owner or workflow type.
    [InlineData("Valid request title", null, "PurchaseRequest")]
    [InlineData("Valid request title", "Operations", null)]

    // Reject enum names that the API cannot recognize.
    [InlineData("Valid request title", "UnknownDepartment", "PurchaseRequest")]
    [InlineData("Valid request title", "Operations", "UnknownWorkflow")]
    public async Task CreateTask_InvalidInput_DoesNotSaveTask(
        string title,
        string? owningDepartment,
        string? kind)
    {
        // Each case gets a test host with its own isolated database.
        using var factory = new WorkflowApplicationFactory();
        using var client = factory.CreateClient();

        // Capture the starting count, including any startup seed data.
        var countBefore = await GetTaskCountAsync(client);

        // Use the current test case's values while keeping other fields valid.
        using var response = await client.PostAsJsonAsync("/api/tasks", new
        {
            title,
            description = "Check request validation.",
            requestingDepartment = "Sales",
            owningDepartment,
            kind
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Verify rejection did not create a task, rather than checking only HTTP status.
        var countAfter = await GetTaskCountAsync(client);
        Assert.Equal(countBefore, countAfter);
    }

    // Read the current task count through the API.
    private static async Task<int> GetTaskCountAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/tasks");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        // The endpoint returns a JSON array with one element per task.
        return json.RootElement.GetArrayLength();
    }
}
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace BusinessWorkflowBoard.Tests;

public class RequestValidationTests
{
    [Theory]
    [InlineData("", "Operations", "PurchaseRequest")]
    [InlineData("   ", "Operations", "PurchaseRequest")]
    [InlineData("Bad", "Operations", "PurchaseRequest")]
    [InlineData("Valid request title", null, "PurchaseRequest")]
    [InlineData("Valid request title", "Operations", null)]
    [InlineData("Valid request title", "UnknownDepartment", "PurchaseRequest")]
    [InlineData("Valid request title", "Operations", "UnknownWorkflow")]
    public async Task CreateTask_InvalidInput_DoesNotSaveTask(
        string title,
        string? owningDepartment,
        string? kind)
    {
        using var factory = new WorkflowApplicationFactory();
        using var client = factory.CreateClient();

        var countBefore = await GetTaskCountAsync(client);

        using var response = await client.PostAsJsonAsync("/api/tasks", new
        {
            title,
            description = "Check request validation.",
            requestingDepartment = "Sales",
            owningDepartment,
            kind
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var countAfter = await GetTaskCountAsync(client);
        Assert.Equal(countBefore, countAfter);
    }

    private static async Task<int> GetTaskCountAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/tasks");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        return json.RootElement.GetArrayLength();
    }
}
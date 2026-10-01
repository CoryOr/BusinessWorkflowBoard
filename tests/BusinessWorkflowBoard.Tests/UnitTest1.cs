using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace BusinessWorkflowBoard.Tests;

public class PurchaseWorkflowTests
{
    // Verify the purchase lifecycle and enforce the required reviewer department.
    [Fact]
    public async Task PurchaseRequest_RequiresFinanceApproval()
    {
        // Run API requests against the test host and its isolated database.
        using var factory = new WorkflowApplicationFactory();
        using var client = factory.CreateClient();

        // Create a purchase request owned by Operations and requiring Finance review.
        using var created = await client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Purchase monitor for analyst",
            description = "Prepare a quote for Finance review.",
            requestingDepartment = "Sales",
            owningDepartment = "Operations",
            kind = "PurchaseRequest"
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // Use the returned ID to target this task in subsequent requests.
        using var createdJson = JsonDocument.Parse(
            await created.Content.ReadAsStringAsync());

        var id = createdJson.RootElement.GetProperty("id").GetGuid();
        var taskUrl = $"/api/tasks/{id}";

        Assert.Equal("Queued", await GetStatusAsync(client, taskUrl));

        // Start the work and verify the saved transition to InProgress.
        using var started = await client.PostAsync(
            $"{taskUrl}/start", null);

        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        Assert.Equal("InProgress", await GetStatusAsync(client, taskUrl));

        // Submit active work for approval and verify it enters the review state.
        using var submitted = await client.PostAsync(
            $"{taskUrl}/submit-for-approval", null);

        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        Assert.Equal(
            "AwaitingApproval", await GetStatusAsync(client, taskUrl));

        // Reject the wrong reviewer department and preserve the pending review.
        using var denied = await client.PostAsJsonAsync(
            $"{taskUrl}/approve",
            new { reviewerDepartment = "IT" });

        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(
            "AwaitingApproval", await GetStatusAsync(client, taskUrl));

        // Accept Finance approval and verify that the request is completed.
        using var approved = await client.PostAsJsonAsync(
            $"{taskUrl}/approve",
            new { reviewerDepartment = "Finance" });

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal("Completed", await GetStatusAsync(client, taskUrl));

        // Reject another approval attempt after completion.
        using var repeated = await client.PostAsJsonAsync(
            $"{taskUrl}/approve",
            new { reviewerDepartment = "Finance" });

        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
    }

    // Read persisted status through a separate API request,
    // rather than relying only on each action's response.
    private static async Task<string?> GetStatusAsync(
        HttpClient client, string taskUrl)
    {
        using var response = await client.GetAsync(taskUrl);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        return json.RootElement.GetProperty("status").GetString();
    }
}
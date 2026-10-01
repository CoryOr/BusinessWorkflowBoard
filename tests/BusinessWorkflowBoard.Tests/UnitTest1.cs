using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace BusinessWorkflowBoard.Tests;

public class PurchaseWorkflowTests
{
    [Fact]
    public async Task PurchaseRequest_RequiresFinanceApproval()
    {
        using var factory = new WorkflowApplicationFactory();
        using var client = factory.CreateClient();

        // Create a purchase request.
        using var created = await client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Purchase monitor for analyst",
            description = "Prepare a quote for Finance review.",
            requestingDepartment = "Sales",
            owningDepartment = "Operations",
            kind = "PurchaseRequest"
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var createdJson = JsonDocument.Parse(
            await created.Content.ReadAsStringAsync());

        var id = createdJson.RootElement.GetProperty("id").GetGuid();
        var taskUrl = $"/api/tasks/{id}";

        Assert.Equal("Queued", await GetStatusAsync(client, taskUrl));

        // Start the work.
        using var started = await client.PostAsync(
            $"{taskUrl}/start", null);

        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        Assert.Equal("InProgress", await GetStatusAsync(client, taskUrl));

        // Submit the request for review.
        using var submitted = await client.PostAsync(
            $"{taskUrl}/submit-for-approval", null);

        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        Assert.Equal(
            "AwaitingApproval", await GetStatusAsync(client, taskUrl));

        // IT cannot approve a purchase request.
        using var denied = await client.PostAsJsonAsync(
            $"{taskUrl}/approve",
            new { reviewerDepartment = "IT" });

        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(
            "AwaitingApproval", await GetStatusAsync(client, taskUrl));

        // Finance can approve it.
        using var approved = await client.PostAsJsonAsync(
            $"{taskUrl}/approve",
            new { reviewerDepartment = "Finance" });

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal("Completed", await GetStatusAsync(client, taskUrl));

        // A completed request cannot be approved again.
        using var repeated = await client.PostAsJsonAsync(
            $"{taskUrl}/approve",
            new { reviewerDepartment = "Finance" });

        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
    }

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
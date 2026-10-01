using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace BusinessWorkflowBoard.Tests;

public class ServiceWorkflowTests
{
    [Fact]
    public async Task ServiceRequest_CompletesWithoutApproval()
    {
        using var factory = new WorkflowApplicationFactory();
        using var client = factory.CreateClient();

        using var created = await client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Update customer support guide",
            description = "Add the billing escalation steps.",
            requestingDepartment = "Operations",
            owningDepartment = "CustomerSupport",
            kind = "ServiceRequest"
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var createdJson = JsonDocument.Parse(
            await created.Content.ReadAsStringAsync());

        var id = createdJson.RootElement.GetProperty("id").GetGuid();
        var taskUrl = $"/api/tasks/{id}";

        // Work must start before it can be completed.
        using var premature = await client.PostAsync(
            $"{taskUrl}/complete", null);

        Assert.Equal(HttpStatusCode.Conflict, premature.StatusCode);

        using var started = await client.PostAsync(
            $"{taskUrl}/start", null);

        Assert.Equal(HttpStatusCode.OK, started.StatusCode);

        // Service requests do not go through approval.
        using var submitted = await client.PostAsync(
            $"{taskUrl}/submit-for-approval", null);

        Assert.Equal(HttpStatusCode.Conflict, submitted.StatusCode);

        // The failed submission must leave the task in progress.
        using var beforeCompletion = await client.GetAsync(taskUrl);
        Assert.Equal(HttpStatusCode.OK, beforeCompletion.StatusCode);

        using var beforeJson = JsonDocument.Parse(
            await beforeCompletion.Content.ReadAsStringAsync());

        Assert.Equal(
            "InProgress",
            beforeJson.RootElement.GetProperty("status").GetString());

        using var completed = await client.PostAsync(
            $"{taskUrl}/complete", null);

        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);

        // Read the saved result through a separate request.
        using var saved = await client.GetAsync(taskUrl);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        using var savedJson = JsonDocument.Parse(
            await saved.Content.ReadAsStringAsync());

        Assert.Equal(
            "Completed",
            savedJson.RootElement.GetProperty("status").GetString());

        Assert.Equal(
            JsonValueKind.Null,
            savedJson.RootElement.GetProperty("approvalDepartment").ValueKind);

        // A completed task cannot be completed again.
        using var repeated = await client.PostAsync(
            $"{taskUrl}/complete", null);

        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
    }
}
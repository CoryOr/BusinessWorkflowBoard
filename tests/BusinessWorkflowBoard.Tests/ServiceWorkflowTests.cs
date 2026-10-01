using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace BusinessWorkflowBoard.Tests;

public class ServiceWorkflowTests
{
    // Verify that service requests finish without review,
    // while still enforcing the required workflow order.
    [Fact]
    public async Task ServiceRequest_CompletesWithoutApproval()
    {
        // Run API requests against the test host and its isolated database.
        using var factory = new WorkflowApplicationFactory();
        using var client = factory.CreateClient();

        // Create a service request, which starts in the Queued state.
        using var created = await client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Update customer support guide",
            description = "Add the billing escalation steps.",
            requestingDepartment = "Operations",
            owningDepartment = "CustomerSupport",
            kind = "ServiceRequest"
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // Use the returned ID to target this task in subsequent requests.
        using var createdJson = JsonDocument.Parse(
            await created.Content.ReadAsStringAsync());

        var id = createdJson.RootElement.GetProperty("id").GetGuid();
        var taskUrl = $"/api/tasks/{id}";

        // Reject completion before work has started.
        using var premature = await client.PostAsync(
            $"{taskUrl}/complete", null);

        Assert.Equal(HttpStatusCode.Conflict, premature.StatusCode);

        // Start the work so the task becomes eligible for direct completion.
        using var started = await client.PostAsync(
            $"{taskUrl}/start", null);

        Assert.Equal(HttpStatusCode.OK, started.StatusCode);

        // Reject review submission because service requests need no approval.
        using var submitted = await client.PostAsync(
            $"{taskUrl}/submit-for-approval", null);

        Assert.Equal(HttpStatusCode.Conflict, submitted.StatusCode);

        // Read saved state to verify the rejected submission changed nothing.
        using var beforeCompletion = await client.GetAsync(taskUrl);
        Assert.Equal(HttpStatusCode.OK, beforeCompletion.StatusCode);

        using var beforeJson = JsonDocument.Parse(
            await beforeCompletion.Content.ReadAsStringAsync());

        Assert.Equal(
            "InProgress",
            beforeJson.RootElement.GetProperty("status").GetString());

        // Complete the active service request directly.
        using var completed = await client.PostAsync(
            $"{taskUrl}/complete", null);

        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);

        // Verify persisted data through a separate API request.
        using var saved = await client.GetAsync(taskUrl);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        using var savedJson = JsonDocument.Parse(
            await saved.Content.ReadAsStringAsync());

        Assert.Equal(
            "Completed",
            savedJson.RootElement.GetProperty("status").GetString());

        // A JSON null confirms this workflow has no required approval department.
        Assert.Equal(
            JsonValueKind.Null,
            savedJson.RootElement.GetProperty("approvalDepartment").ValueKind);

        // Reject another completion attempt after the task has finished.
        using var repeated = await client.PostAsync(
            $"{taskUrl}/complete", null);

        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
    }
}
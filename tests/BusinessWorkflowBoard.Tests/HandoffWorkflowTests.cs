using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace BusinessWorkflowBoard.Tests;

public class HandoffWorkflowTests
{
    // Verify handoff rules, saved ownership, history, and note trimming.
    [Fact]
    public async Task Handoff_SavesOwnerAndHistory()
    {
        // Run API requests against the test host and its isolated database.
        using var factory = new WorkflowApplicationFactory();
        using var client = factory.CreateClient();

        // Create a purchase request initially owned by Operations.
        using var created = await client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Prepare laptop for new hire",
            description = "Prepare a quote and confirm specifications.",
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

        // Reject a transfer before work has started.
        using var queuedHandoff = await client.PostAsJsonAsync(
            $"{taskUrl}/handoff",
            new
            {
                targetDepartment = "IT",
                note = "Please confirm specifications."
            });

        Assert.Equal(HttpStatusCode.Conflict, queuedHandoff.StatusCode);

        // Move the task into InProgress so handoffs are allowed.
        using var started = await client.PostAsync(
            $"{taskUrl}/start", null);

        Assert.Equal(HttpStatusCode.OK, started.StatusCode);

        // Reject a transfer to the department that already owns the task.
        using var sameOwner = await client.PostAsJsonAsync(
            $"{taskUrl}/handoff",
            new
            {
                targetDepartment = "Operations",
                note = "Please review the quote."
            });

        Assert.Equal(HttpStatusCode.BadRequest, sameOwner.StatusCode);

        // Reject a note containing only whitespace.
        using var missingNote = await client.PostAsJsonAsync(
            $"{taskUrl}/handoff",
            new
            {
                targetDepartment = "IT",
                note = "   "
            });

        Assert.Equal(HttpStatusCode.BadRequest, missingNote.StatusCode);

        const string note =
            "Quote prepared. Please confirm laptop specifications.";

        // Submit a valid transfer with surrounding spaces to check trimming.
        using var handedOff = await client.PostAsJsonAsync(
            $"{taskUrl}/handoff",
            new
            {
                targetDepartment = "IT",
                note = $"  {note}  "
            });

        Assert.Equal(HttpStatusCode.OK, handedOff.StatusCode);

        // Read through a separate API request to verify persisted data.
        using var saved = await client.GetAsync(taskUrl);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        using var savedJson = JsonDocument.Parse(
            await saved.Content.ReadAsStringAsync());

        var task = savedJson.RootElement;

        // Ownership changes, while the requester, reviewer, and status stay the same.
        Assert.Equal("IT", task.GetProperty("owningDepartment").GetString());
        Assert.Equal(
            "Sales", task.GetProperty("requestingDepartment").GetString());
        Assert.Equal(
            "Finance", task.GetProperty("approvalDepartment").GetString());
        Assert.Equal("InProgress", task.GetProperty("status").GetString());

        // Only the successful handoff should create a history entry.
        var history = task.GetProperty("handoffs");
        Assert.Equal(1, history.GetArrayLength());

        var entry = history[0];

        // Verify the original owner, receiving department, and trimmed note.
        Assert.Equal(
            "Operations", entry.GetProperty("fromDepartment").GetString());
        Assert.Equal("IT", entry.GetProperty("toDepartment").GetString());
        Assert.Equal(note, entry.GetProperty("note").GetString());
    }
}
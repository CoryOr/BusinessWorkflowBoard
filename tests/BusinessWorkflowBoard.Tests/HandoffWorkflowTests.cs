using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace BusinessWorkflowBoard.Tests;

public class HandoffWorkflowTests
{
    [Fact]
    public async Task Handoff_SavesOwnerAndHistory()
    {
        using var factory = new WorkflowApplicationFactory();
        using var client = factory.CreateClient();

        using var created = await client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Prepare laptop for new hire",
            description = "Prepare a quote and confirm specifications.",
            requestingDepartment = "Sales",
            owningDepartment = "Operations",
            kind = "PurchaseRequest"
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var createdJson = JsonDocument.Parse(
            await created.Content.ReadAsStringAsync());

        var id = createdJson.RootElement.GetProperty("id").GetGuid();
        var taskUrl = $"/api/tasks/{id}";

        // A queued task cannot be handed off.
        using var queuedHandoff = await client.PostAsJsonAsync(
            $"{taskUrl}/handoff",
            new
            {
                targetDepartment = "IT",
                note = "Please confirm specifications."
            });

        Assert.Equal(HttpStatusCode.Conflict, queuedHandoff.StatusCode);

        using var started = await client.PostAsync(
            $"{taskUrl}/start", null);

        Assert.Equal(HttpStatusCode.OK, started.StatusCode);

        // A handoff must go to a different department.
        using var sameOwner = await client.PostAsJsonAsync(
            $"{taskUrl}/handoff",
            new
            {
                targetDepartment = "Operations",
                note = "Please review the quote."
            });

        Assert.Equal(HttpStatusCode.BadRequest, sameOwner.StatusCode);

        // A handoff must include a meaningful note.
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

        using var handedOff = await client.PostAsJsonAsync(
            $"{taskUrl}/handoff",
            new
            {
                targetDepartment = "IT",
                note = $"  {note}  "
            });

        Assert.Equal(HttpStatusCode.OK, handedOff.StatusCode);

        // Read the saved task through a separate request.
        using var saved = await client.GetAsync(taskUrl);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        using var savedJson = JsonDocument.Parse(
            await saved.Content.ReadAsStringAsync());

        var task = savedJson.RootElement;

        Assert.Equal("IT", task.GetProperty("owningDepartment").GetString());
        Assert.Equal(
            "Sales", task.GetProperty("requestingDepartment").GetString());
        Assert.Equal(
            "Finance", task.GetProperty("approvalDepartment").GetString());
        Assert.Equal("InProgress", task.GetProperty("status").GetString());

        // Rejected handoffs must not create history entries.
        var history = task.GetProperty("handoffs");
        Assert.Equal(1, history.GetArrayLength());

        var entry = history[0];

        Assert.Equal(
            "Operations", entry.GetProperty("fromDepartment").GetString());
        Assert.Equal("IT", entry.GetProperty("toDepartment").GetString());
        Assert.Equal(note, entry.GetProperty("note").GetString());
    }
}
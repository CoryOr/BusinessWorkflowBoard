using BusinessWorkflowBoard.Data;
using BusinessWorkflowBoard.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BusinessWorkflowBoard.Tests;

public class ConcurrencyTests
{
    // Verify that an outdated handoff cannot overwrite a completed task
    // or leave behind a history entry from the rejected save.
    [Fact]
    public async Task StaleHandoff_CannotModifyCompletedTask()
    {
        // Start the test host with an isolated SQLite database.
        using var factory = new WorkflowApplicationFactory();

        // Create an active task that both contexts will later load.
        using var setupScope = factory.Services.CreateScope();
        var setupDb = setupScope.ServiceProvider
            .GetRequiredService<WorkflowDbContext>();

        var task = new BusinessTask
        {
            Title = "Update customer support guide",
            Description = "Check conflicting workflow updates.",
            RequestingDepartment = Department.Sales,
            OwningDepartment = Department.Operations,
            Kind = WorkflowKind.ServiceRequest,
            Status = WorkflowStatus.InProgress
        };

        setupDb.Tasks.Add(task);
        await setupDb.SaveChangesAsync();

        // Separate scopes provide independent contexts and change trackers.
        // This simulates two operations loading the same task before either saves.
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();

        var firstDb = firstScope.ServiceProvider
            .GetRequiredService<WorkflowDbContext>();

        var secondDb = secondScope.ServiceProvider
            .GetRequiredService<WorkflowDbContext>();

        var firstTask = await firstDb.Tasks.SingleAsync(
            t => t.Id == task.Id);

        var staleTask = await secondDb.Tasks.SingleAsync(
            t => t.Id == task.Id);

        // Confirm that both copies initially have the same concurrency token.
        var originalVersion = staleTask.Version;
        Assert.Equal(originalVersion, firstTask.Version);

        // The first operation completes the task and saves a new version.
        firstTask.Status = WorkflowStatus.Completed;
        await firstDb.SaveChangesAsync();

        Assert.NotEqual(originalVersion, firstTask.Version);

        // The second context still holds its original InProgress copy.
        // Attempt a transfer using that outdated version.
        staleTask.OwningDepartment = Department.IT;
        staleTask.Handoffs.Add(new HandoffEntry(
            Department.Operations,
            Department.IT,
            "This outdated handoff must not be saved.",
            DateTimeOffset.UtcNow));

        // EF must reject the save because the stored version has changed.
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => secondDb.SaveChangesAsync());

        // Use a fresh context to inspect persisted data rather than tracked copies.
        using var verificationScope = factory.Services.CreateScope();

        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<WorkflowDbContext>();

        var savedTask = await verificationDb.Tasks
            .AsNoTracking()
            .SingleAsync(t => t.Id == task.Id);

        // Preserve the successful completion and its version.
        // The failed transfer must change neither the owner nor the history.
        Assert.Equal(WorkflowStatus.Completed, savedTask.Status);
        Assert.Equal(Department.Operations, savedTask.OwningDepartment);
        Assert.Empty(savedTask.Handoffs);
        Assert.Equal(firstTask.Version, savedTask.Version);
    }
}
using BusinessWorkflowBoard.Data;
using BusinessWorkflowBoard.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BusinessWorkflowBoard.Tests;

public class ConcurrencyTests
{
    [Fact]
    public async Task StaleHandoff_CannotModifyCompletedTask()
    {
        using var factory = new WorkflowApplicationFactory();

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

        // Two separate contexts read the same task version.
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

        var originalVersion = staleTask.Version;
        Assert.Equal(originalVersion, firstTask.Version);

        // The first request completes the task.
        firstTask.Status = WorkflowStatus.Completed;
        await firstDb.SaveChangesAsync();

        Assert.NotEqual(originalVersion, firstTask.Version);

        // The second request still holds the old InProgress copy.
        staleTask.OwningDepartment = Department.IT;
        staleTask.Handoffs.Add(new HandoffEntry(
            Department.Operations,
            Department.IT,
            "This outdated handoff must not be saved.",
            DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => secondDb.SaveChangesAsync());

        // Verify that the failed save left no partial changes.
        using var verificationScope = factory.Services.CreateScope();

        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<WorkflowDbContext>();

        var savedTask = await verificationDb.Tasks
            .AsNoTracking()
            .SingleAsync(t => t.Id == task.Id);

        Assert.Equal(WorkflowStatus.Completed, savedTask.Status);
        Assert.Equal(Department.Operations, savedTask.OwningDepartment);
        Assert.Empty(savedTask.Handoffs);
        Assert.Equal(firstTask.Version, savedTask.Version);
    }
}
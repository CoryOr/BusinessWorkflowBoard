using BusinessWorkflowBoard.Data;
using BusinessWorkflowBoard.Hubs;
using BusinessWorkflowBoard.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BusinessWorkflowBoard.Endpoints;

// Keep task API routes together, separate from application startup.
public static class TaskEndpoints
{
    // Register the routes through app.MapTaskEndpoints() in Program.cs.
    public static void MapTaskEndpoints(this WebApplication app)
    {
        // Successful changes are saved before notifying connected browsers.
        // Database concurrency exceptions are handled by middleware in Program.cs.

        // Return all tasks using the shared API response format.
        app.MapGet("/api/tasks", async (WorkflowDbContext db) =>
        {
            // Read-only queries do not need EF change tracking.
            var storedTasks = await db.Tasks
                .AsNoTracking()
                .ToListAsync();

            return Results.Ok(
                storedTasks.Select(BusinessTaskResponse.From).ToArray());
        });

        // Return one task; the route requires a valid GUID identifier.
        app.MapGet("/api/tasks/{id:guid}",
            async (Guid id, WorkflowDbContext db) =>
        {
            var task = await db.Tasks
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(BusinessTaskResponse.From(task));
        });

        // Validate and create a new request in the Queued state.
        app.MapPost("/api/tasks",
            async (CreateTaskRequest request, WorkflowDbContext db,
                IHubContext<WorkflowHub> hub) =>
        {
            // Normalize text before validating or storing it.
            // Server validation also protects calls made outside the board form.
            var title = request.Title?.Trim() ?? string.Empty;
            var description = request.Description?.Trim() ?? string.Empty;

            if (title.Length < 5 || title.Length > 120)
            {
                return Results.BadRequest(new
                {
                    error = "Title must contain 5–120 characters."
                });
            }

            if (description.Length > 2000)
            {
                return Results.BadRequest(new
                {
                    error = "Description must be at most 2,000 characters."
                });
            }

            // Nullable request fields let us detect missing department choices.
            if (request.RequestingDepartment is null ||
                request.OwningDepartment is null)
            {
                return Results.BadRequest(new
                {
                    error = "Both departments are required."
                });
            }

            // Reject numeric enum values that do not represent defined departments.
            if (!Enum.IsDefined(request.RequestingDepartment.Value) ||
                !Enum.IsDefined(request.OwningDepartment.Value))
            {
                return Results.BadRequest(new
                {
                    error = "Choose valid departments."
                });
            }

            if (request.Kind is null ||
                !Enum.IsDefined(request.Kind.Value))
            {
                return Results.BadRequest(new
                {
                    error = "Choose a valid workflow kind."
                });
            }

            // BusinessTask supplies the initial ID, version, status, and timestamp.
            var task = new BusinessTask
            {
                Title = title,
                Description = description,
                RequestingDepartment = request.RequestingDepartment.Value,
                OwningDepartment = request.OwningDepartment.Value,
                Kind = request.Kind.Value
            };

            db.Tasks.Add(task);
            await db.SaveChangesAsync();

            var response = BusinessTaskResponse.From(task);

            // Publish the saved result so every connected board can refresh.
            await hub.Clients.All.SendAsync(
                "TaskChanged", response);

            // Return HTTP 201 with the new task's URL and response data.
            return Results.Created(
                $"/api/tasks/{task.Id}",
                response);
        });

        // Move a queued task into active work.
        app.MapPost("/api/tasks/{id:guid}/start",
            async (Guid id, WorkflowDbContext db,
                IHubContext<WorkflowHub> hub) =>
        {
            // Updates use tracked entities so EF detects changes and checks versions.
            var task = await db.Tasks
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task is null)
            {
                return Results.NotFound();
            }

            // Enforce the transition even when callers bypass the board buttons.
            if (task.Status != WorkflowStatus.Queued)
            {
                return Results.Conflict(new
                {
                    error = "Only queued tasks can be started."
                });
            }

            task.Status = WorkflowStatus.InProgress;

            await db.SaveChangesAsync();

            var response = BusinessTaskResponse.From(task);

            await hub.Clients.All.SendAsync(
                "TaskChanged", response);

            return Results.Ok(response);
        });

        // Move active work into review when its workflow requires approval.
        app.MapPost("/api/tasks/{id:guid}/submit-for-approval",
            async (Guid id, WorkflowDbContext db,
                IHubContext<WorkflowHub> hub) =>
        {
            var task = await db.Tasks
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task is null)
            {
                return Results.NotFound();
            }

            // Service requests skip approval and use the complete endpoint.
            if (task.ApprovalDepartment is null)
            {
                return Results.Conflict(new
                {
                    error = "This workflow does not require approval."
                });
            }

            if (task.Status != WorkflowStatus.InProgress)
            {
                return Results.Conflict(new
                {
                    error = "Only tasks in progress can be submitted for approval."
                });
            }

            task.Status = WorkflowStatus.AwaitingApproval;
            await db.SaveChangesAsync();

            var response = BusinessTaskResponse.From(task);

            await hub.Clients.All.SendAsync(
                "TaskChanged", response);

            return Results.Ok(response);
        });

        // Complete a request after approval by its required reviewing department.
        app.MapPost("/api/tasks/{id:guid}/approve",
            async (Guid id, ApproveTaskRequest request,
                WorkflowDbContext db, IHubContext<WorkflowHub> hub) =>
        {
            if (request.ReviewerDepartment is null ||
                !Enum.IsDefined(request.ReviewerDepartment.Value))
            {
                return Results.BadRequest(new
                {
                    error = "Choose a valid reviewer department."
                });
            }

            var task = await db.Tasks
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task is null)
            {
                return Results.NotFound();
            }

            if (task.ApprovalDepartment is null)
            {
                return Results.Conflict(new
                {
                    error = "This workflow does not require approval."
                });
            }

            // Prevent approval before submission or after completion.
            if (task.Status != WorkflowStatus.AwaitingApproval)
            {
                return Results.Conflict(new
                {
                    error = "Only tasks awaiting approval can be approved."
                });
            }

            // Required approval comes from the workflow type, not the current owner.
            // The supplied department is a demo selection, not an authenticated identity.
            if (request.ReviewerDepartment != task.ApprovalDepartment)
            {
                return Results.Json(new
                {
                    error = $"Only {task.ApprovalDepartment} can approve this request."
                }, statusCode: 403);
            }

            task.Status = WorkflowStatus.Completed;
            await db.SaveChangesAsync();

            var response = BusinessTaskResponse.From(task);

            await hub.Clients.All.SendAsync(
                "TaskChanged", response);

            return Results.Ok(response);
        });

        // Finish active work that does not require approval.
        app.MapPost("/api/tasks/{id:guid}/complete",
            async (Guid id, WorkflowDbContext db,
                IHubContext<WorkflowHub> hub) =>
        {
            var task = await db.Tasks
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task is null)
            {
                return Results.NotFound();
            }

            // Prevent purchase and access requests from bypassing review.
            if (task.ApprovalDepartment is not null)
            {
                return Results.Conflict(new
                {
                    error = "Requests requiring approval must be completed through review."
                });
            }

            if (task.Status != WorkflowStatus.InProgress)
            {
                return Results.Conflict(new
                {
                    error = "Only tasks in progress can be completed."
                });
            }

            task.Status = WorkflowStatus.Completed;

            await db.SaveChangesAsync();

            var response = BusinessTaskResponse.From(task);

            await hub.Clients.All.SendAsync(
                "TaskChanged", response);

            return Results.Ok(response);
        });

        // Transfer responsibility for active work and record the transfer history.
        app.MapPost("/api/tasks/{id:guid}/handoff",
            async (Guid id, HandoffTaskRequest request,
                WorkflowDbContext db, IHubContext<WorkflowHub> hub) =>
        {
            if (request.TargetDepartment is null ||
                !Enum.IsDefined(request.TargetDepartment.Value))
            {
                return Results.BadRequest(new
                {
                    error = "Choose a valid target department."
                });
            }

            // Trim the note so whitespace alone cannot satisfy the requirement.
            var note = request.Note?.Trim() ?? string.Empty;

            if (note.Length == 0 || note.Length > 1000)
            {
                return Results.BadRequest(new
                {
                    error = "A handoff note of 1–1,000 characters is required."
                });
            }

            var task = await db.Tasks
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task is null)
            {
                return Results.NotFound();
            }

            // Queued, awaiting-approval, and completed tasks cannot be handed off.
            if (task.Status != WorkflowStatus.InProgress)
            {
                return Results.Conflict(new
                {
                    error = "Only tasks in progress can be handed off."
                });
            }

            var targetDepartment = request.TargetDepartment.Value;

            if (targetDepartment == task.OwningDepartment)
            {
                return Results.BadRequest(new
                {
                    error = "Choose a department different from the current owner."
                });
            }

            // Capture the previous owner before assigning the receiving department.
            var handoff = new HandoffEntry(
                task.OwningDepartment,
                targetDepartment,
                note,
                DateTimeOffset.UtcNow);

            task.Handoffs.Add(handoff);
            task.OwningDepartment = targetDepartment;

            // Save the owner change and history together.
            // A concurrency conflict rolls back the save instead of leaving a partial transfer.
            // The requester, workflow type, and status remain unchanged.
            await db.SaveChangesAsync();

            var response = BusinessTaskResponse.From(task);

            await hub.Clients.All.SendAsync(
                "TaskChanged", response);

            return Results.Ok(response);
        });
    }
}
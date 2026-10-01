using BusinessWorkflowBoard.Data;
using BusinessWorkflowBoard.Hubs;
using BusinessWorkflowBoard.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BusinessWorkflowBoard.Endpoints;

public static class TaskEndpoints
{
    public static void MapTaskEndpoints(this WebApplication app)
    {
        app.MapGet("/api/tasks", async (WorkflowDbContext db) =>
        {
            var storedTasks = await db.Tasks
                .AsNoTracking()
                .ToListAsync();

            return Results.Ok(
                storedTasks.Select(BusinessTaskResponse.From).ToArray());
        });

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

        app.MapPost("/api/tasks",
            async (CreateTaskRequest request, WorkflowDbContext db,
                IHubContext<WorkflowHub> hub) =>
        {
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

            if (request.RequestingDepartment is null ||
                request.OwningDepartment is null)
            {
                return Results.BadRequest(new
                {
                    error = "Both departments are required."
                });
            }

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

                await hub.Clients.All.SendAsync(
                    "TaskChanged", response);

                return Results.Created(
                    $"/api/tasks/{task.Id}",
                    response);
            });

        app.MapPost("/api/tasks/{id:guid}/start",
            async (Guid id, WorkflowDbContext db,
                IHubContext<WorkflowHub> hub) =>
        {
            var task = await db.Tasks
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task is null)
            {
                return Results.NotFound();
            }

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

            if (task.Status != WorkflowStatus.AwaitingApproval)
            {
                return Results.Conflict(new
                {
                    error = "Only tasks awaiting approval can be approved."
                });
            }

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

            var handoff = new HandoffEntry(
                task.OwningDepartment,
                targetDepartment,
                note,
                DateTimeOffset.UtcNow);

            task.Handoffs.Add(handoff);
            task.OwningDepartment = targetDepartment;

            await db.SaveChangesAsync();

            var response = BusinessTaskResponse.From(task);

            await hub.Clients.All.SendAsync(
                "TaskChanged", response);

            return Results.Ok(response);
        });
    }
}
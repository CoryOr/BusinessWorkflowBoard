namespace BusinessWorkflowBoard.Models;

// Define the task data returned by the API and sent through SignalR.
// Keep database details, such as the concurrency token, out of the response.
public sealed record BusinessTaskResponse(
    Guid Id,
    string Title,
    string Description,
    Department RequestingDepartment,
    Department OwningDepartment,
    WorkflowStatus Status,
    DateTimeOffset CreatedAt,
    WorkflowKind Kind,
    Department? ApprovalDepartment,
    IReadOnlyList<HandoffEntry> Handoffs)
{
    // Convert a database entity into the shared response format.
    public static BusinessTaskResponse From(BusinessTask task)
    {
        return new BusinessTaskResponse(
            task.Id,
            task.Title,
            task.Description,
            task.RequestingDepartment,
            task.OwningDepartment,
            task.Status,
            task.CreatedAt,
            task.Kind,
            task.ApprovalDepartment,

            // Copy the history so the response does not share the entity's list.
            task.Handoffs.ToArray());
    }
}
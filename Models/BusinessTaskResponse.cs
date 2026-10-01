namespace BusinessWorkflowBoard.Models;

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
            task.Handoffs.ToArray());
    }
}
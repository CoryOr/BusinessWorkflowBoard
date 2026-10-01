namespace BusinessWorkflowBoard.Models;

// Represent a business request and its current workflow state.
public class BusinessTask
{
    // Assign a unique identifier when the task is created.
    public Guid Id { get; set; } = Guid.NewGuid();

    // EF uses this token to detect competing database writes.
    // WorkflowDbContext assigns a new version when saving a modified task.
    public Guid Version { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    // Record the department that originally requested the work.
    public Department RequestingDepartment { get; set; }

    // Track responsibility for the work; this changes during a handoff.
    public Department OwningDepartment { get; set; }

    // New requests enter the queue before work starts.
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Queued;

    // Store the creation time in UTC for consistent timestamps.
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public WorkflowKind Kind { get; set; } = WorkflowKind.ServiceRequest;

    // Determine the reviewer from the workflow type, regardless of the current owner.
    // A null value means approval is not required.
    // EF ignores this calculated property because it needs no database column.
    public Department? ApprovalDepartment => Kind switch
    {
        WorkflowKind.PurchaseRequest => Department.Finance,
        WorkflowKind.AccessRequest => Department.IT,
        _ => null
    };

    // Preserve the departments, notes, and timestamps of previous ownership transfers.
    public List<HandoffEntry> Handoffs { get; set; }
        = new List<HandoffEntry>();
}
namespace BusinessWorkflowBoard.Models;

public class BusinessTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid Version { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public Department RequestingDepartment { get; set; }

    public Department OwningDepartment { get; set; }

    public WorkflowStatus Status { get; set; } = WorkflowStatus.Queued;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public WorkflowKind Kind { get; set; } = WorkflowKind.ServiceRequest;

    public Department? ApprovalDepartment => Kind switch
    {
        WorkflowKind.PurchaseRequest => Department.Finance,
        WorkflowKind.AccessRequest => Department.IT,
        _ => null
    };

    public List<HandoffEntry> Handoffs { get; set; }
        = new List<HandoffEntry>();

    
}
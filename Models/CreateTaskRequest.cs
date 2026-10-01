namespace BusinessWorkflowBoard.Models;

public class CreateTaskRequest
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public Department? RequestingDepartment { get; set; }

    public Department? OwningDepartment { get; set; }

    public WorkflowKind? Kind { get; set; }
}
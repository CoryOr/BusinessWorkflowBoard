namespace BusinessWorkflowBoard.Models;

public class HandoffTaskRequest
{
    public Department? TargetDepartment { get; set; }

    public string Note { get; set; } = string.Empty;
}
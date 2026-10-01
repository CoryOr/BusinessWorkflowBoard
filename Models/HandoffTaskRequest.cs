namespace BusinessWorkflowBoard.Models;

// Define the JSON input for transferring a task to another department.
public class HandoffTaskRequest
{
    // Null identifies a missing selection.
    // The endpoint requires a valid department different from the current owner.
    public Department? TargetDepartment { get; set; }

    // Explain the transfer; the endpoint trims and validates this note
    // before storing it in the task's handoff history.
    public string Note { get; set; } = string.Empty;
}
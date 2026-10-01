namespace BusinessWorkflowBoard.Models;

// Define the task states displayed as board columns.
// The API enforces allowed transitions; service requests skip approval.
public enum WorkflowStatus
{
    // Created and waiting for work to start.
    Queued,

    // Work has started and can be handed off to another department.
    InProgress,

    // Submitted for review by the required approval department.
    AwaitingApproval,

    // Finished through direct completion or successful approval.
    Completed
}
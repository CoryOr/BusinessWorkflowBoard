namespace BusinessWorkflowBoard.Models;

// Define the workflow types used to determine each task's approval requirements.
// These names also match the JSON values sent by the board.
public enum WorkflowKind
{
    // Can be completed after work starts without approval.
    ServiceRequest,

    // Requires Finance approval before completion.
    PurchaseRequest,

    // Requires IT approval before completion.
    AccessRequest
}
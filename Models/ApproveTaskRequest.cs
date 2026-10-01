namespace BusinessWorkflowBoard.Models;

// Define the JSON input for approving a task awaiting review.
public class ApproveTaskRequest
{
    // Null identifies a missing reviewer selection.
    // The endpoint checks that this department matches the required approver.
    // This demo value comes from the dropdown, not an authenticated user identity.
    public Department? ReviewerDepartment { get; set; }
}
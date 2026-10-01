namespace BusinessWorkflowBoard.Models;

// Define the JSON input accepted when creating a business request.
// The endpoint validates these values before creating a BusinessTask.
public class CreateTaskRequest
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    // Nullable enums distinguish missing values from the first enum value.
    // The endpoint requires valid choices for both departments and the workflow type.

    // Identify the department requesting the work.
    public Department? RequestingDepartment { get; set; }

    // Identify the department initially responsible for the work.
    public Department? OwningDepartment { get; set; }

    // Choose the workflow type, which determines whether approval is required.
    public WorkflowKind? Kind { get; set; }
}
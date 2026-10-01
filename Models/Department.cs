namespace BusinessWorkflowBoard.Models;

// Define the departments that can request, own, or review business tasks.
// These names are serialized as JSON strings and match the board's dropdown values.
public enum Department
{
    Operations,
    Finance,
    Sales,
    IT,
    CustomerSupport
}
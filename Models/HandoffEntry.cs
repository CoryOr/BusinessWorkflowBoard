namespace BusinessWorkflowBoard.Models;

// Capture one ownership transfer in a task's handoff history.
public sealed record HandoffEntry(
    // The owner before the transfer.
    Department FromDepartment,

    // The department receiving responsibility for the work.
    Department ToDepartment,

    // Explain the transfer and any next steps.
    string Note,

    // Record when the transfer occurred; the endpoint supplies a UTC timestamp.
    DateTimeOffset OccurredAt);
namespace BusinessWorkflowBoard.Models;

public sealed record HandoffEntry(
    Department FromDepartment,
    Department ToDepartment,
    string Note,
    DateTimeOffset OccurredAt);
using Microsoft.AspNetCore.SignalR;

namespace BusinessWorkflowBoard.Hubs;

// Provide the SignalR hub that browsers connect to for live board updates.
// Task endpoints broadcast "TaskChanged" through IHubContext<WorkflowHub>.
public class WorkflowHub : Hub
{
    // No client-callable methods are needed because workflow actions use the HTTP API.
}
// Program.cs
using BusinessWorkflowBoard.Models;
using System.Text.Json.Serialization;
using BusinessWorkflowBoard.Data;
using Microsoft.EntityFrameworkCore;
using BusinessWorkflowBoard.Hubs;
using Microsoft.AspNetCore.SignalR;
using BusinessWorkflowBoard.Endpoints;

// Create the builder that configures application services and settings.
var builder = WebApplication.CreateBuilder(args);

// Register a scoped database context backed by the local SQLite database.
// Each HTTP request receives its own context for tracking and saving changes.
builder.Services.AddDbContext<WorkflowDbContext>(options =>
{
    options.UseSqlite("Data Source=workflow.db");
});

// Send enum names such as "Sales" and "InProgress" in HTTP JSON responses.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter());
});

// Register SignalR for live board updates.
// SignalR uses separate JSON settings, so configure enum names here too.
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    });

// Build the application after registering its services.
var app = builder.Build();

// Convert competing database writes into a readable HTTP 409 response.
// This happens when another context changes a task before this save completes.
app.Use(async (context, next) =>
{
    try
    {
        // Continue through the remaining middleware and endpoint.
        await next(context);
    }
    catch (DbUpdateConcurrencyException)
        when (!context.Response.HasStarted)
    {
        // Only replace the response if nothing has been sent to the client yet.
        context.Response.StatusCode = StatusCodes.Status409Conflict;

        await context.Response.WriteAsJsonAsync(new
        {
            error = "This request changed while your action was being saved. " +
                    "Check the latest task details and try again."
        });
    }
});

// Serve files from wwwroot, including /board.html.
app.UseStaticFiles();

// Startup runs outside an HTTP request, so create a scope for the database context.
// The database schema must already be prepared through EF Core migrations.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<WorkflowDbContext>();

    // Seed one example request only when the database is empty.
    // Existing tasks remain available across application restarts.
    if (!await db.Tasks.AnyAsync())
    {
        db.Tasks.Add(new BusinessTask
        {
            Kind = WorkflowKind.PurchaseRequest,
            Title = "Purchase laptop for new sales hire",
            Description = "Operations should prepare a vendor quote for Finance review.",
            RequestingDepartment = Department.Sales,
            OwningDepartment = Department.Operations
        });

        await db.SaveChangesAsync();
    }
}

// Provide a simple response at the root URL.
app.MapGet("/", () => "Business Workflow Task Board API");

// Register the task API routes defined in Endpoints/TaskEndpoints.cs.
app.MapTaskEndpoints();

// Provide the connection endpoint used by browsers to receive live updates.
app.MapHub<WorkflowHub>("/hubs/workflow");

// Start the application and listen for incoming requests.
app.Run();

// Let the test project's WebApplicationFactory access this entry point.
public partial class Program { }
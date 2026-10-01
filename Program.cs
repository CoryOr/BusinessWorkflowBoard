//Program.cs
using BusinessWorkflowBoard.Models;
using System.Text.Json.Serialization;
using BusinessWorkflowBoard.Data;
using Microsoft.EntityFrameworkCore;
using BusinessWorkflowBoard.Hubs;
using Microsoft.AspNetCore.SignalR;
using BusinessWorkflowBoard.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<WorkflowDbContext>(options =>
{
    options.UseSqlite("Data Source=workflow.db");
});

// Show enum names such as "Sales" in JSON responses.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter());
});

builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    });
var app = builder.Build();

app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (DbUpdateConcurrencyException)
        when (!context.Response.HasStarted)
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;

        await context.Response.WriteAsJsonAsync(new
        {
            error = "This request changed while your action was being saved. " +
                    "Check the latest task details and try again."
        });
    }
});

app.UseStaticFiles();

// Add a sample request only when the database is empty.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<WorkflowDbContext>();

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

app.MapGet("/", () => "Business Workflow Task Board API");

app.MapTaskEndpoints();

app.MapHub<WorkflowHub>("/hubs/workflow");

app.Run();

public partial class Program { }

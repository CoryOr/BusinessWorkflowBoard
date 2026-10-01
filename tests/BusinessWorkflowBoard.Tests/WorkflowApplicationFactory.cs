using BusinessWorkflowBoard.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BusinessWorkflowBoard.Tests;

// Start the application in a test host using its real Program entry point.
// Each factory instance gets its own database instead of using workflow.db.
public sealed class WorkflowApplicationFactory
    : WebApplicationFactory<Program>
{
    // Keep the connection open for the lifetime of the test database.
    private readonly SqliteConnection _connection;

    public WorkflowApplicationFactory()
    {
        // An in-memory SQLite database exists only while its connection stays open.
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        // Use this same connection for schema setup and application requests.
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new WorkflowDbContext(options);

        // Apply the real migrations before Program.cs queries the database.
        // This prepares the application schema inside the isolated test database.
        db.Database.Migrate();
    }

    // Customize the application's host before it starts.
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove the context, options, and configuration callbacks
            // registered in Program.cs before replacing its database connection.
            services.RemoveAll<WorkflowDbContext>();
            services.RemoveAll<DbContextOptions<WorkflowDbContext>>();
            services.RemoveAll<
                IDbContextOptionsConfiguration<WorkflowDbContext>>();

            // Give application requests scoped contexts backed by the test database.
            services.AddDbContext<WorkflowDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        // Shut down the test host before closing its database connection.
        base.Dispose(disposing);

        if (disposing)
        {
            // Closing the connection releases the in-memory database.
            _connection.Dispose();
        }
    }
}
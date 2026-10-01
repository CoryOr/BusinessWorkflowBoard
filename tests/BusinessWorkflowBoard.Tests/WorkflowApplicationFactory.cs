using BusinessWorkflowBoard.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BusinessWorkflowBoard.Tests;

public sealed class WorkflowApplicationFactory
    : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public WorkflowApplicationFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new WorkflowDbContext(options);

        // Create the test database before Program.cs queries it.
        db.Database.Migrate();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Replace the application's database configuration.
            services.RemoveAll<WorkflowDbContext>();
            services.RemoveAll<DbContextOptions<WorkflowDbContext>>();
            services.RemoveAll<
                IDbContextOptionsConfiguration<WorkflowDbContext>>();

            services.AddDbContext<WorkflowDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
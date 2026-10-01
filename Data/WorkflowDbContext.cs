using BusinessWorkflowBoard.Models;
using Microsoft.EntityFrameworkCore;

namespace BusinessWorkflowBoard.Data;

// Connect the application's task models to the database through EF Core.
public class WorkflowDbContext : DbContext
{
    // Receive the database configuration registered in Program.cs.
    public WorkflowDbContext(
        DbContextOptions<WorkflowDbContext> options)
        : base(options)
    {
    }

    // Provide access to tasks for querying, adding, and updating records.
    public DbSet<BusinessTask> Tasks => Set<BusinessTask>();

    // Update concurrency tokens before saving task changes asynchronously.
    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        // Detect property changes before checking which tasks were modified.
        ChangeTracker.DetectChanges();

        foreach (var entry in ChangeTracker.Entries<BusinessTask>())
        {
            if (entry.State == EntityState.Modified)
            {
                // Assign a new version for this update.
                // EF retains the original version to check against the database.
                entry.Entity.Version = Guid.NewGuid();
            }
        }

        // Let EF save the tracked changes and perform the concurrency check.
        return base.SaveChangesAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);
    }

    // Define how task properties and handoff history map to database tables.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var task = modelBuilder.Entity<BusinessTask>();

        task.ToTable("Tasks");
        task.HasKey(t => t.Id);

        // Include the original version in update and delete conditions.
        // If another save changed it, EF throws DbUpdateConcurrencyException.
        task.Property(t => t.Version)
            .IsConcurrencyToken();

        // Configure required fields and length metadata.
        // The API also validates lengths because SQLite does not enforce them.
        task.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(120);

        task.Property(t => t.Description)
            .HasMaxLength(2000);

        // ApprovalDepartment is calculated from Kind, so it needs no column.
        task.Ignore(t => t.ApprovalDepartment);

        // Each handoff belongs to a task and is stored in a separate table.
        // Saving the owner change and history together keeps the transfer consistent.
        task.OwnsMany(t => t.Handoffs, handoff =>
        {
            handoff.ToTable("TaskHandoffs");

            // Link each history entry to the task that owns it.
            handoff.WithOwner()
                .HasForeignKey("BusinessTaskId");

            // Create a shadow primary key managed by EF.
            // It exists in the database without being a HandoffEntry property.
            handoff.Property<int>("Id");
            handoff.HasKey("Id");

            handoff.Property(h => h.Note)
                .IsRequired()
                .HasMaxLength(1000);
        });
    }
}
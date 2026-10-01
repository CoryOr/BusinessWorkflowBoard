using BusinessWorkflowBoard.Models;
using Microsoft.EntityFrameworkCore;

namespace BusinessWorkflowBoard.Data;

public class WorkflowDbContext : DbContext
{
    public WorkflowDbContext(
        DbContextOptions<WorkflowDbContext> options)
        : base(options)
    {
    }

    public DbSet<BusinessTask> Tasks => Set<BusinessTask>();

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ChangeTracker.DetectChanges();

        foreach (var entry in ChangeTracker.Entries<BusinessTask>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.Version = Guid.NewGuid();
            }
        }

        return base.SaveChangesAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var task = modelBuilder.Entity<BusinessTask>();

        task.ToTable("Tasks");
        task.HasKey(t => t.Id);

        task.Property(t => t.Version)
            .IsConcurrencyToken();

        task.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(120);

        task.Property(t => t.Description)
            .HasMaxLength(2000);

        // Calculated from Kind rather than stored separately.
        task.Ignore(t => t.ApprovalDepartment);

        task.OwnsMany(t => t.Handoffs, handoff =>
        {
            handoff.ToTable("TaskHandoffs");

            handoff.WithOwner()
                .HasForeignKey("BusinessTaskId");

            // A database-only identifier for each history entry.
            handoff.Property<int>("Id");
            handoff.HasKey("Id");

            handoff.Property(h => h.Note)
                .IsRequired()
                .HasMaxLength(1000);
        });
    }
}
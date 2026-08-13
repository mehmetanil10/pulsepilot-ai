using Microsoft.EntityFrameworkCore;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.Common.Exceptions;
using PulsePilot.Domain.Actions;
using PulsePilot.Domain.Backlog;
using PulsePilot.Domain.CustomerResponses;
using PulsePilot.Domain.Feedback;
using PulsePilot.Domain.Users;
using PulsePilot.Domain.Workspaces;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext, IUnitOfWork
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Workspace> Workspaces => Set<Workspace>();

    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();

    public DbSet<FeedbackEntity> Feedback => Set<FeedbackEntity>();

    public DbSet<FeedbackAnalysis> FeedbackAnalyses => Set<FeedbackAnalysis>();

    public DbSet<FeedbackEmbedding> FeedbackEmbeddings => Set<FeedbackEmbedding>();

    public DbSet<FeedbackCluster> FeedbackClusters => Set<FeedbackCluster>();

    public DbSet<PendingAction> PendingActions => Set<PendingAction>();

    public DbSet<BacklogItem> BacklogItems => Set<BacklogItem>();

    public DbSet<CustomerResponseDraft> CustomerResponseDrafts => Set<CustomerResponseDraft>();

    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException(
                "The resource was changed by another request.",
                exception);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

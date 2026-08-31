namespace DocumentLifecycle.Application.Dashboard;

public interface IDashboardQuery
{
    Task<DashboardSnapshot> GetAsync(CancellationToken cancellationToken = default);
}

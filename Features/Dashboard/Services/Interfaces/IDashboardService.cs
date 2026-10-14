using Arlink28.Api.Features.Dashboard.ResponseModels;

namespace Arlink28.Api.Features.Dashboard.Services.Interfaces;

public interface IDashboardService
{
    /// <summary>The numbers behind the admin overview, for the last <paramref name="days"/> days (7 to 365).</summary>
    Task<DashboardResponse> GetAsync(int days, CancellationToken ct = default);
}

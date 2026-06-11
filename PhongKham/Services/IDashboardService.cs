using PhongKham.Models;

namespace PhongKham.Services;

public interface IDashboardService
{
    Task<ClinicDashboardViewModel> GetDashboardAsync();
}

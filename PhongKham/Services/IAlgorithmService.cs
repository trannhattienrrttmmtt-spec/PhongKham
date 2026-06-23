using PhongKham.Models;

namespace PhongKham.Services;

public interface IAlgorithmService
{
    List<ScheduleSuggestion> BuildScheduleSuggestions(
        IReadOnlyList<Appointment> appointments,
        IReadOnlyList<Doctor> doctors,
        DateTime fromDate,
        int days = 3,
        int take = 5);

    List<AppointmentPriority> BuildAppointmentPriorities(
        IReadOnlyList<Appointment> appointments,
        DateTime now,
        int take = 6);

    List<PatientCluster> ClusterPatients(
        IReadOnlyList<Patient> patients,
        IReadOnlyList<Appointment> appointments,
        IReadOnlyList<Invoice> invoices,
        int k = 3,
        int iterations = 8);

    List<FuzzyMatch<T>> FuzzyRank<T>(
        IEnumerable<T> items,
        string query,
        Func<T, IEnumerable<string>> fields,
        int take = 30);

    List<InventoryForecast> ForecastInventory(
        IReadOnlyList<Medicine> medicines,
        IReadOnlyList<InventoryTransaction> transactions,
        int lookbackDays = 30,
        int take = 8);
}

public record ScheduleSuggestion(
    Doctor Doctor,
    DateTime Slot,
    int Score,
    int DoctorLoad,
    string Reason);

public record AppointmentPriority(
    Appointment Appointment,
    int Score,
    string Reason);

public record PatientCluster(
    int ClusterId,
    string Label,
    int PatientCount,
    double AverageVisits,
    double AverageRevenue,
    double AverageAge,
    List<Patient> SamplePatients);

public record FuzzyMatch<T>(
    T Item,
    int Score,
    string MatchedText);

public record InventoryForecast(
    Medicine Medicine,
    double AverageDailyUsage,
    double DaysRemaining,
    DateTime? EstimatedRunoutDate,
    string RiskLevel);

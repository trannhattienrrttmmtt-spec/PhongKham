using PhongKham.Models;
using System.Globalization;
using System.Text;

namespace PhongKham.Services;

public class AlgorithmService : IAlgorithmService
{
    private static readonly TimeOnly[] Slots =
    [
        new(8, 0), new(8, 30), new(9, 0), new(9, 30),
        new(14, 0), new(14, 30), new(15, 0), new(15, 30)
    ];

    public List<ScheduleSuggestion> BuildScheduleSuggestions(
        IReadOnlyList<Appointment> appointments,
        IReadOnlyList<Doctor> doctors,
        DateTime fromDate,
        int days = 3,
        int take = 5)
    {
        var activeDoctors = doctors
            .Where(x => string.IsNullOrWhiteSpace(x.Status) || !x.Status.Contains("ngung", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var activeAppointments = appointments
            .Where(x => !IsCancelled(x.Status))
            .ToList();

        var suggestions = new List<ScheduleSuggestion>();
        foreach (var doctor in activeDoctors)
        {
            var doctorAppointments = activeAppointments.Where(x => x.DoctorId == doctor.Id).ToList();
            var doctorLoad = doctorAppointments.Count(x => x.AppointmentTime.Date >= DateTime.Today);

            for (var day = 0; day < days; day++)
            {
                var date = fromDate.Date.AddDays(day);
                var dailyLoad = doctorAppointments.Count(x => x.AppointmentTime.Date == date);
                foreach (var slot in Slots)
                {
                    var candidate = date.Add(slot.ToTimeSpan());
                    if (candidate <= DateTime.Now.AddMinutes(30))
                    {
                        continue;
                    }

                    var isBusy = doctorAppointments.Any(x => Math.Abs((x.AppointmentTime - candidate).TotalMinutes) < 30);
                    if (isBusy)
                    {
                        continue;
                    }

                    var score = 100 - (dailyLoad * 8) - (doctorLoad * 2) - (day * 5);
                    if (slot.Hour is >= 8 and <= 9)
                    {
                        score += 8;
                    }

                    suggestions.Add(new ScheduleSuggestion(
                        doctor,
                        candidate,
                        Math.Max(1, score),
                        dailyLoad,
                        dailyLoad == 0 ? "Bac si dang rong trong ngay" : $"Da co {dailyLoad} lich trong ngay"));
                }
            }
        }

        return suggestions
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Slot)
            .Take(take)
            .ToList();
    }

    public List<AppointmentPriority> BuildAppointmentPriorities(
        IReadOnlyList<Appointment> appointments,
        DateTime now,
        int take = 6)
    {
        return appointments
            .Where(x => !IsCancelled(x.Status) && !IsCompleted(x.Status))
            .Select(x =>
            {
                var score = 20;
                var reasons = new List<string>();
                var normalizedStatus = NormalizeVietnamese(x.Status ?? "");
                var minutesUntilVisit = (x.AppointmentTime - now).TotalMinutes;

                if (x.AppointmentTime.Date == now.Date)
                {
                    score += 30;
                    reasons.Add("trong ngày");
                }

                if (minutesUntilVisit < -15)
                {
                    score += 35;
                    reasons.Add("đã quá giờ");
                }
                else if (minutesUntilVisit is >= -15 and <= 45)
                {
                    score += 32;
                    reasons.Add("sắp đến lượt");
                }

                if (normalizedStatus.Contains("da dat lich") || normalizedStatus.Contains("da xac nhan") || normalizedStatus.Contains("dang cho"))
                {
                    score += 15;
                    reasons.Add("chờ xử lý");
                }
                if (normalizedStatus.Contains("dang kham"))
                {
                    score += 25;
                    reasons.Add("đang khám");
                }

                var reasonText = NormalizeVietnamese(x.Reason ?? "");
                if (new[] { "dau nguc", "kho tho", "sot cao", "choang", "ngat", "dau bung" }.Any(reasonText.Contains))
                {
                    score += 18;
                    reasons.Add("triệu chứng cần chú ý");
                }

                var patient = x.Patient;
                if (patient is not null && patient.DateOfBirth != default)
                {
                    var age = now.Year - patient.DateOfBirth.Year;
                    if (patient.DateOfBirth.Date > now.Date.AddYears(-age))
                    {
                        age--;
                    }
                    if (age is < 6 or >= 65)
                    {
                        score += 10;
                        reasons.Add("nhóm tuổi cần ưu tiên");
                    }
                }

                if (!reasons.Any())
                {
                    reasons.Add("theo thứ tự lịch hẹn");
                }

                return new AppointmentPriority(x, Math.Max(1, score), string.Join(", ", reasons.Distinct()));
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Appointment.AppointmentTime)
            .Take(take)
            .ToList();
    }

    public List<PatientCluster> ClusterPatients(
        IReadOnlyList<Patient> patients,
        IReadOnlyList<Appointment> appointments,
        IReadOnlyList<Invoice> invoices,
        int k = 3,
        int iterations = 8)
    {
        var points = patients.Select(patient =>
        {
            var visits = appointments.Count(x => x.PatientId == patient.Id && IsCompleted(x.Status));
            var revenue = invoices.Where(x => x.PatientId == patient.Id && x.PaymentStatus == "Paid").Sum(x => x.TotalAmount);
            var age = Math.Max(0, DateTime.Today.Year - patient.DateOfBirth.Year);
            return new PatientPoint(patient, visits, (double)revenue, age);
        }).ToList();

        if (!points.Any())
        {
            return [];
        }

        k = Math.Clamp(k, 1, Math.Min(3, points.Count));
        var normalized = Normalize(points);
        var centroids = normalized.Take(k).Select(x => x.Vector.ToArray()).ToList();
        var assignments = new int[normalized.Count];

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            for (var i = 0; i < normalized.Count; i++)
            {
                assignments[i] = NearestCentroid(normalized[i].Vector, centroids);
            }

            for (var cluster = 0; cluster < k; cluster++)
            {
                var members = normalized.Where((_, index) => assignments[index] == cluster).ToList();
                if (!members.Any())
                {
                    continue;
                }

                for (var dimension = 0; dimension < 3; dimension++)
                {
                    centroids[cluster][dimension] = members.Average(x => x.Vector[dimension]);
                }
            }
        }

        var result = new List<PatientCluster>();
        for (var cluster = 0; cluster < k; cluster++)
        {
            var members = points.Where((_, index) => assignments[index] == cluster).ToList();
            if (!members.Any())
            {
                continue;
            }

            var avgVisits = members.Average(x => x.Visits);
            var avgRevenue = members.Average(x => x.Revenue);
            var label = avgVisits >= 2 || avgRevenue >= 300000
                ? "Can cham soc dinh ky"
                : avgVisits <= 0
                    ? "Moi/it quay lai"
                    : "Kham thong thuong";

            result.Add(new PatientCluster(
                cluster + 1,
                label,
                members.Count,
                avgVisits,
                avgRevenue,
                members.Average(x => x.Age),
                members.Select(x => x.Patient).Take(4).ToList()));
        }

        return result.OrderByDescending(x => x.PatientCount).ToList();
    }

    public List<FuzzyMatch<T>> FuzzyRank<T>(
        IEnumerable<T> items,
        string query,
        Func<T, IEnumerable<string>> fields,
        int take = 30)
    {
        var normalizedQuery = NormalizeText(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return items.Take(take).Select(x => new FuzzyMatch<T>(x, 100, "")).ToList();
        }

        return items
            .Select(item =>
            {
                var best = fields(item)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(text => ScoreText(normalizedQuery, text))
                    .OrderByDescending(x => x.Score)
                    .FirstOrDefault();
                return new FuzzyMatch<T>(item, best.Score, best.Text);
            })
            .Where(x => x.Score >= 45)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.MatchedText)
            .Take(take)
            .ToList();
    }

    public List<InventoryForecast> ForecastInventory(
        IReadOnlyList<Medicine> medicines,
        IReadOnlyList<InventoryTransaction> transactions,
        int lookbackDays = 30,
        int take = 8)
    {
        var from = DateTime.Today.AddDays(-lookbackDays);
        return medicines
            .Where(x => x.IsActive)
            .Select(medicine =>
            {
                var used = transactions
                    .Where(x => x.MedicineId == medicine.Id && x.CreatedAt.Date >= from && IsExport(x.TransactionType))
                    .Sum(x => Math.Abs(x.Quantity));
                var averageDailyUsage = used <= 0 ? 0 : used / (double)Math.Max(1, lookbackDays);
                var daysRemaining = averageDailyUsage <= 0
                    ? double.PositiveInfinity
                    : medicine.QuantityInStock / averageDailyUsage;
                var runoutDate = double.IsInfinity(daysRemaining)
                    ? (DateTime?)null
                    : DateTime.Today.AddDays(Math.Ceiling(daysRemaining));
                var risk = medicine.QuantityInStock <= 0 || daysRemaining <= 7
                    ? "Critical"
                    : daysRemaining <= 21 || medicine.QuantityInStock <= medicine.MinimumStock
                        ? "Warning"
                        : "Stable";

                return new InventoryForecast(medicine, averageDailyUsage, daysRemaining, runoutDate, risk);
            })
            .OrderBy(x => x.RiskLevel == "Critical" ? 0 : x.RiskLevel == "Warning" ? 1 : 2)
            .ThenBy(x => double.IsInfinity(x.DaysRemaining) ? double.MaxValue : x.DaysRemaining)
            .Take(take)
            .ToList();
    }

    private static (int Score, string Text) ScoreText(string query, string text)
    {
        var normalized = NormalizeText(text);
        if (normalized.Contains(query))
        {
            return (100 - Math.Min(30, normalized.Length - query.Length), text);
        }

        var distance = LevenshteinDistance(query, normalized);
        var maxLength = Math.Max(query.Length, normalized.Length);
        var score = maxLength == 0 ? 100 : (int)Math.Round((1 - distance / (double)maxLength) * 100);
        return (Math.Max(0, score), text);
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var costs = new int[right.Length + 1];
        for (var j = 0; j <= right.Length; j++)
        {
            costs[j] = j;
        }

        for (var i = 1; i <= left.Length; i++)
        {
            var previous = costs[0];
            costs[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var current = costs[j];
                var substitution = previous + (left[i - 1] == right[j - 1] ? 0 : 1);
                costs[j] = Math.Min(Math.Min(costs[j] + 1, costs[j - 1] + 1), substitution);
                previous = current;
            }
        }

        return costs[right.Length];
    }

    private static string NormalizeText(string value)
        => new(value.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static string NormalizeVietnamese(string value)
    {
        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var chars = normalized
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Select(c => c == 'đ' ? 'd' : c);
        return new string(chars.ToArray()).Normalize(NormalizationForm.FormC);
    }

    private static bool IsCancelled(string status)
        => status.Contains("huy", StringComparison.OrdinalIgnoreCase)
            || status.Contains("hủy", StringComparison.OrdinalIgnoreCase);

    private static bool IsCompleted(string status)
        => status.Contains("hoan tat", StringComparison.OrdinalIgnoreCase)
            || status.Contains("hoàn tất", StringComparison.OrdinalIgnoreCase);

    private static bool IsExport(string type)
        => type.Contains("export", StringComparison.OrdinalIgnoreCase)
            || type.Contains("xuat", StringComparison.OrdinalIgnoreCase)
            || type.Contains("xuất", StringComparison.OrdinalIgnoreCase)
            || type.Contains("dispense", StringComparison.OrdinalIgnoreCase);

    private static int NearestCentroid(double[] vector, List<double[]> centroids)
    {
        var bestIndex = 0;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < centroids.Count; i++)
        {
            var distance = vector.Zip(centroids[i], (left, right) => Math.Pow(left - right, 2)).Sum();
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static List<NormalizedPatientPoint> Normalize(List<PatientPoint> points)
    {
        var maxVisits = Math.Max(1, points.Max(x => x.Visits));
        var maxRevenue = Math.Max(1, points.Max(x => x.Revenue));
        var maxAge = Math.Max(1, points.Max(x => x.Age));
        return points
            .Select(x => new NormalizedPatientPoint([x.Visits / maxVisits, x.Revenue / maxRevenue, x.Age / maxAge]))
            .ToList();
    }

    private record PatientPoint(Patient Patient, double Visits, double Revenue, double Age);
    private record NormalizedPatientPoint(double[] Vector);
}

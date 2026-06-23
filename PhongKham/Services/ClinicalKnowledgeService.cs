using System.Globalization;
using System.Text;

namespace PhongKham.Services;

public class ClinicalKnowledgeService : IClinicalKnowledgeService
{
    private static readonly List<SymptomNode> Symptoms =
    [
        new("Đau ngực", ["dau nguc", "tuc nguc", "nong nguc"], "Tim mạch", 35, true),
        new("Khó thở", ["kho tho", "hut hoi", "tho gap"], "Hô hấp", 35, true),
        new("Yếu liệt", ["yeu liet", "liet tay", "liet chan", "meo mieng"], "Thần kinh", 40, true),
        new("Co giật", ["co giat", "dong kinh"], "Thần kinh", 45, true),
        new("Chảy máu nhiều", ["chay mau nhieu", "mat mau", "xuat huyet"], "Cấp cứu", 45, true),
        new("Sốt cao", ["sot cao", "sot 39", "sot 40"], "Nội tổng quát", 25, true),
        new("Đau bụng dữ dội", ["dau bung du doi", "dau bung nhieu"], "Tiêu hóa", 30, true),
        new("Ho", ["ho", "ho khan", "ho dam"], "Tai Mũi Họng", 12, false),
        new("Đau họng", ["dau hong", "rat hong", "viem hong"], "Tai Mũi Họng", 10, false),
        new("Sổ mũi", ["so mui", "nghet mui", "chay mui"], "Tai Mũi Họng", 8, false),
        new("Đau đầu", ["dau dau", "nhuc dau"], "Thần kinh", 12, false),
        new("Chóng mặt", ["chong mat", "hoa mat"], "Nội tổng quát", 12, false),
        new("Đau khớp", ["dau khop", "moi khop", "sung khop"], "Cơ xương khớp", 12, false),
        new("Nổi mẩn", ["noi man", "phat ban", "ngua"], "Da liễu", 10, false),
        new("Đau răng", ["dau rang", "sung loi"], "Răng Hàm Mặt", 8, false)
    ];

    public ClinicalReasoningResult Analyze(string patientText)
    {
        var normalizedText = Normalize(patientText);
        var matched = Symptoms
            .Where(symptom => symptom.Keywords.Any(normalizedText.Contains))
            .ToList();

        if (!matched.Any())
        {
            return new ClinicalReasoningResult("Thấp", 0, "Nội tổng quát", [], [], []);
        }

        var specialtyScores = matched
            .GroupBy(x => x.Specialty)
            .Select(group => new
            {
                Specialty = group.Key,
                Score = group.Sum(x => x.Weight) + group.Count() * 5
            })
            .OrderByDescending(x => x.Score)
            .ToList();

        var riskScore = Math.Min(100, matched.Sum(x => x.Weight) + matched.Count(x => x.IsRedFlag) * 10);
        var riskLevel = riskScore >= 70 ? "Cao" : riskScore >= 35 ? "Trung bình" : "Thấp";
        var suggestedSpecialty = matched.Any(x => x.IsRedFlag) && matched.Count(x => x.IsRedFlag) >= 2
            ? "Cấp cứu"
            : specialtyScores.First().Specialty;

        var reasoningPaths = matched
            .Select(x => $"{x.Name} -> {x.Specialty} -> +{x.Weight} điểm")
            .ToList();
        var warnings = matched
            .Where(x => x.IsRedFlag)
            .Select(x => $"{x.Name} là dấu hiệu cần ưu tiên, nên liên hệ bác sĩ hoặc đi cấp cứu nếu triệu chứng nặng/đột ngột.")
            .Distinct()
            .ToList();

        return new ClinicalReasoningResult(
            riskLevel,
            riskScore,
            suggestedSpecialty,
            matched.Select(x => x.Name).Distinct().ToList(),
            reasoningPaths,
            warnings);
    }

    private static string Normalize(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString()
            .Replace('đ', 'd')
            .Replace("  ", " ")
            .Normalize(NormalizationForm.FormC);
    }

    private record SymptomNode(
        string Name,
        string[] Keywords,
        string Specialty,
        int Weight,
        bool IsRedFlag);
}

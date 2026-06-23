namespace PhongKham.Services;

public interface IClinicalKnowledgeService
{
    ClinicalReasoningResult Analyze(string patientText);
}

public record ClinicalReasoningResult(
    string RiskLevel,
    int RiskScore,
    string SuggestedSpecialty,
    List<string> MatchedSymptoms,
    List<string> ReasoningPaths,
    List<string> Warnings)
{
    public bool HasSignals => MatchedSymptoms.Count > 0;

    public string ToPromptContext()
    {
        if (!HasSignals)
        {
            return "Knowledge graph khong tim thay trieu chung y khoa ro rang trong cau hoi.";
        }

        return string.Join("\n", [
            $"Muc uu tien: {RiskLevel} ({RiskScore} diem)",
            $"Chuyen khoa goi y: {SuggestedSpecialty}",
            $"Trieu chung nhan dien: {string.Join(", ", MatchedSymptoms)}",
            $"Suy luan: {string.Join(" | ", ReasoningPaths)}",
            Warnings.Count > 0 ? $"Canh bao: {string.Join(" | ", Warnings)}" : "Canh bao: khong co dau hieu cap cuu ro rang"
        ]);
    }

    public string ToPatientSummary()
    {
        if (!HasSignals)
        {
            return "";
        }

        var warningText = Warnings.Count > 0
            ? $" Lưu ý: {string.Join(" ", Warnings)}"
            : "";
        return $"Phân tích từ đồ thị tri thức: mức ưu tiên {RiskLevel.ToLowerInvariant()}, gợi ý chuyên khoa {SuggestedSpecialty}. Hệ thống nhận diện: {string.Join(", ", MatchedSymptoms)}.{warningText}";
    }
}

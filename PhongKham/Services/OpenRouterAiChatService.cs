using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PhongKham.Services;

public class OpenRouterAiChatService(HttpClient httpClient, IConfiguration configuration, ILogger<OpenRouterAiChatService> logger) : IAiChatService
{
    private const string DefaultModel = "openai/gpt-4o-mini";

    public async Task<string> GetReplyAsync(IReadOnlyList<AiChatMessage> messages, CancellationToken cancellationToken = default)
    {
        var apiKey = configuration["OpenRouter:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return "Hệ thống AI chưa được cấu hình khóa OpenRouter. Vui lòng liên hệ quản trị viên.";
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.TryAddWithoutValidation("HTTP-Referer", configuration["OpenRouter:Referer"] ?? "http://localhost");
        request.Headers.TryAddWithoutValidation("X-Title", configuration["OpenRouter:Title"] ?? "PhongKham AI Chat");

        var payload = new
        {
            model = configuration["OpenRouter:Model"] ?? DefaultModel,
            messages = messages.Select(x => new { role = x.Role, content = x.Content }).ToArray(),
            temperature = 0.3,
            max_tokens = 700
        };

        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("OpenRouter request failed with status {StatusCode}: {Body}", response.StatusCode, body);
                return "Xin lỗi, trợ lý AI đang gặp sự cố kết nối. Bạn vui lòng thử lại sau ít phút.";
            }

            using var document = JsonDocument.Parse(body);
            var content = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return string.IsNullOrWhiteSpace(content)
                ? "Xin lỗi, trợ lý AI chưa tạo được phản hồi phù hợp. Bạn vui lòng hỏi lại rõ hơn một chút."
                : content.Trim();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OpenRouter request failed.");
            return "Xin lỗi, trợ lý AI đang tạm thời không phản hồi. Bạn vui lòng thử lại sau.";
        }
    }
}

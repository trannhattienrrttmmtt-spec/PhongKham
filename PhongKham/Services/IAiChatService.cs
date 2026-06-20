namespace PhongKham.Services;

public record AiChatMessage(string Role, string Content);

public interface IAiChatService
{
    Task<string> GetReplyAsync(IReadOnlyList<AiChatMessage> messages, CancellationToken cancellationToken = default);
}

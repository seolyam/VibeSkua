using System.Threading.Tasks;

namespace Skua.Core.Interfaces;

public interface IDiscordWebhookService
{
    void Initialize();
    Task SendMessageAsync(string message);
    Task SendScreenshotAsync(string message);
    Task TestWebhookAsync();
    bool SuppressDefaultNotifications { get; set; }
}

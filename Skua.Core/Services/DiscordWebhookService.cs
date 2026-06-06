using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Skua.Core.Interfaces;
using Skua.Core.Messaging;

namespace Skua.Core.Services;

public class DiscordWebhookService : IDiscordWebhookService
{
    private readonly ISettingsService _settingsService;
    private readonly HttpClient _httpClient;
    private readonly System.Timers.Timer _pingTimer;
    private bool _initialized;
    private DateTime? _scriptStartTime;
    public bool SuppressDefaultNotifications { get; set; }

    private readonly IScreenshotService _screenshotService;

    public DiscordWebhookService(ISettingsService settingsService, IScreenshotService screenshotService)
    {
        _settingsService = settingsService;
        _screenshotService = screenshotService;
        _httpClient = new HttpClient();
        
        _pingTimer = new System.Timers.Timer();
        _pingTimer.Elapsed += async (s, e) => await OnPingTimerElapsed();
    }

    public void Initialize()
    {
        if (_initialized) return;
        
        StrongReferenceMessenger.Default.Register<DiscordWebhookService, ScriptStartedMessage, int>(this, (int)MessageChannels.ScriptStatus, (r, m) => r.OnScriptStarted(m));
        StrongReferenceMessenger.Default.Register<DiscordWebhookService, ScriptStoppedMessage, int>(this, (int)MessageChannels.ScriptStatus, (r, m) => r.OnScriptStopped(m));
        StrongReferenceMessenger.Default.Register<DiscordWebhookService, ScriptErrorMessage, int>(this, (int)MessageChannels.ScriptStatus, (r, m) => r.OnScriptError(m));
        StrongReferenceMessenger.Default.Register<DiscordWebhookService, ReloginTriggeredMessage, int>(this, (int)MessageChannels.GameEvents, (r, m) => r.OnRelogin(m));
        StrongReferenceMessenger.Default.Register<DiscordWebhookService, ItemDroppedMessage, int>(this, (int)MessageChannels.GameEvents, (r, m) => r.OnItemDropped(m));
        
        _initialized = true;
    }

    private void OnScriptStarted(ScriptStartedMessage msg)
    {
        _scriptStartTime = DateTime.Now;
        var settings = _settingsService.GetClient();
        if (settings != null && settings.WebhookNotifyStarted && !SuppressDefaultNotifications)
            _ = SendMessageAsync("🟢 **Script Started** - Skua Bot has begun execution.");
            
        StartPingTimer();
    }

    private void OnScriptStopped(ScriptStoppedMessage msg)
    {
        var settings = _settingsService.GetClient();
        if (settings != null && settings.WebhookNotifyStopped && !SuppressDefaultNotifications)
        {
            string timeString = "";
            if (_scriptStartTime.HasValue)
            {
                var elapsed = DateTime.Now - _scriptStartTime.Value;
                timeString = $" (Ran for {(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00})";
            }
            _ = SendMessageAsync($"⏹️ **Bot Stopped**{timeString}");
        }
            
        StopPingTimer();
    }

    private void OnScriptError(ScriptErrorMessage msg)
    {
        var settings = _settingsService.GetClient();
        if (settings != null && settings.WebhookNotifyCrashed)
            _ = SendMessageAsync($"🔴 **Script Error** - Skua Bot crashed:\n```{msg.Exception.Message}```");
            
        StopPingTimer();
    }

    private void OnRelogin(ReloginTriggeredMessage msg)
    {
        var settings = _settingsService.GetClient();
        if (settings != null && settings.WebhookNotifyRelogged)
            _ = SendMessageAsync(msg.WasKicked ? "⚠️ **Kicked & Relogging** - Skua Bot is attempting to relog." : "🔄 **Disconnected & Relogging** - Skua Bot is attempting to relog.");
    }

    private void StartPingTimer()
    {
        var settings = _settingsService.GetClient();
        if (settings == null || settings.WebhookPingInterval <= 0) return;
        
        _pingTimer.Interval = TimeSpan.FromMinutes(settings.WebhookPingInterval).TotalMilliseconds;
        _pingTimer.Start();
    }

    private void StopPingTimer()
    {
        _pingTimer.Stop();
    }

    private async Task OnPingTimerElapsed()
    {
        var settings = _settingsService.GetClient();
        if (settings == null || settings.WebhookPingInterval <= 0)
        {
            StopPingTimer();
            return;
        }
        
        await SendMessageAsync("📡 **Health Ping** - Skua Bot is still running and farming.");
    }

    public async Task TestWebhookAsync()
    {
        await SendMessageAsync("✅ **Webhook Test Successful!** - Your Skua Discord Webhooks are working perfectly.");
    }

    public async Task SendMessageAsync(string message)
    {
        try
        {
            var url = _settingsService.GetClient()?.DiscordWebhookUrl;
            if (string.IsNullOrWhiteSpace(url)) return;

            var json = $"{{\"content\": \"{message.Replace("\"", "\\\"").Replace("\n", "\\n")}\"}}";
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            await _httpClient.PostAsync(url, content);
        }
        catch { }
    }

    private void OnItemDropped(ItemDroppedMessage msg)
    {
        var settings = _settingsService.GetClient();
        if (settings == null || !settings.WebhookNotifyItemDrops) return;

        var dropList = settings.WebhookNotifyItemDropsList?.ToLower().Split(',') ?? Array.Empty<string>();
        foreach (var item in dropList)
        {
            if (msg.Item.Name.ToLower().Contains(item.Trim()))
            {
                _ = SendScreenshotAsync($"🏆 **Rare Drop!** - You just got {msg.Item.Name} x{msg.Item.Quantity}!");
                break;
            }
        }
    }

    public async Task SendScreenshotAsync(string message)
    {
        try
        {
            var url = _settingsService.GetClient()?.DiscordWebhookUrl;
            if (string.IsNullOrWhiteSpace(url)) return;

            byte[] screenshotBytes = await _screenshotService.TakeScreenshotAsync();

            using var formData = new MultipartFormDataContent();
            formData.Add(new StringContent(message), "content");

            if (screenshotBytes != null && screenshotBytes.Length > 0)
            {
                var imageContent = new ByteArrayContent(screenshotBytes);
                imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                formData.Add(imageContent, "file", "screenshot.png");
            }

            await _httpClient.PostAsync(url, formData);
        }
        catch { }
    }
}

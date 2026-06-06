using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Skua.Core.Interfaces;
using Skua.Core.Messaging;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Linq;
using Skua.Core.Models;

namespace Skua.Core.ViewModels;

public partial class ScriptSchedulerViewModel : BotControlViewModelBase
{
    private readonly IScriptManager _manager;
    private readonly IFileDialogService _fileDialog;
    private readonly IDiscordWebhookService _discord;
    private readonly ISettingsService _settingsService;
    private readonly IWindowService _windowService;
    private Stopwatch _scriptStopwatch = new();
    
    public ScriptSchedulerViewModel(IScriptManager manager, IFileDialogService fileDialog, IDiscordWebhookService discord, ISettingsService settingsService, IWindowService windowService) : base("Scheduler")
    {
        _manager = manager;
        _fileDialog = fileDialog;
        _discord = discord;
        _settingsService = settingsService;
        _windowService = windowService;
        
        StrongReferenceMessenger.Default.Register<ScriptSchedulerViewModel, ScriptStoppedMessage, int>(this, (int)MessageChannels.ScriptStatus, (r, m) => r.OnScriptStopped());
        StrongReferenceMessenger.Default.Register<ScriptSchedulerViewModel, QueueScriptMessage, int>(this, (int)MessageChannels.ScriptStatus, (r, m) => r.OnQueueScript(m));
    }

    private void OnQueueScript(QueueScriptMessage message)
    {
        if (!string.IsNullOrEmpty(message.Path))
        {
            ScriptQueue.Add(new ScriptItemViewModel(message.Path));
        }
    }

    [ObservableProperty]
    private ObservableCollection<ScriptItemViewModel> _scriptQueue = new();

    [ObservableProperty]
    private bool _isRunningQueue;

    [ObservableProperty]
    private int _currentIndex = 0;

    [RelayCommand]
    private void OpenScriptRepo()
    {
        _windowService.ShowManagedWindow("Script Repo");
    }

    [RelayCommand]
    private void RemoveScript(ScriptItemViewModel item)
    {
        if (ScriptQueue.Contains(item))
            ScriptQueue.Remove(item);
    }

    [RelayCommand]
    private void StartQueue()
    {
        if (ScriptQueue.Count == 0 || IsRunningQueue) return;
        
        IsRunningQueue = true;
        _discord.SuppressDefaultNotifications = true;
        CurrentIndex = 0;
        
        foreach (var item in ScriptQueue)
            item.Status = "Queued";
            
        RunNextScript();
    }

    [RelayCommand]
    private async Task StopQueue()
    {
        IsRunningQueue = false;
        _discord.SuppressDefaultNotifications = false;
        if (CurrentIndex < ScriptQueue.Count)
        {
            ScriptQueue[CurrentIndex].Status = "Stopped";
            ScriptQueue[CurrentIndex].Duration = GetFormattedDuration();
        }
        await _manager.StopScript();
        _ = _discord.SendMessageAsync($"⏹️ **Scheduler Stopped** - Playlist execution manually halted.");
    }

    private string GetFormattedDuration()
    {
        var ts = _scriptStopwatch.Elapsed;
        return $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
    }

    private async void RunNextScript()
    {
        if (!IsRunningQueue) return;

        if (CurrentIndex >= ScriptQueue.Count)
        {
            IsRunningQueue = false;
            _discord.SuppressDefaultNotifications = false;
            _ = _discord.SendMessageAsync($"🎉 **Scheduler Finished** - All scripts in the playlist have completed!");
            return;
        }

        var nextScript = ScriptQueue[CurrentIndex];
        
        if (File.Exists(nextScript.Path))
        {
            nextScript.Status = "Running";
            _scriptStopwatch.Restart();
            
            _ = _discord.SendMessageAsync($"🔄 **Scheduler** [{CurrentIndex + 1}/{ScriptQueue.Count}] - Now running: {nextScript.Name}");
            
            _manager.SetLoadedScript(nextScript.Path);
            await _manager.StartScript();
        }
        else
        {
            nextScript.Status = "File Not Found";
            CurrentIndex++;
            RunNextScript();
        }
    }

    private void OnScriptStopped()
    {
        if (!IsRunningQueue) return;

        if (CurrentIndex < ScriptQueue.Count)
        {
            _scriptStopwatch.Stop();
            ScriptQueue[CurrentIndex].Status = "Completed";
            ScriptQueue[CurrentIndex].Duration = GetFormattedDuration();
        }

        CurrentIndex++;

        // Delay slightly to let the engine completely finish unloading the previous script
        Task.Run(async () => 
        {
            await Task.Delay(2000);
            RunNextScript();
        });
    }
}

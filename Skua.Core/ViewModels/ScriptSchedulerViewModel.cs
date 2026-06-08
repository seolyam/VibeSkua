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
using System;
using Skua.Core.Models;

namespace Skua.Core.ViewModels;

public partial class ScriptSchedulerViewModel : BotControlViewModelBase
{
    private readonly IScriptManager _manager;
    private readonly IFileDialogService _fileDialog;
    private readonly IDiscordWebhookService _discord;
    private readonly ISettingsService _settingsService;
    private readonly IWindowService _windowService;
    private readonly IDialogService _dialogService;
    private Stopwatch _scriptStopwatch = new();
    
    public ScriptSchedulerViewModel(IScriptManager manager, IFileDialogService fileDialog, IDiscordWebhookService discord, ISettingsService settingsService, IWindowService windowService, IDialogService dialogService) : base("Scheduler")
    {
        _manager = manager;
        _fileDialog = fileDialog;
        _discord = discord;
        _settingsService = settingsService;
        _windowService = windowService;
        _dialogService = dialogService;
        
        StrongReferenceMessenger.Default.Register<ScriptSchedulerViewModel, ScriptStoppedMessage, int>(this, (int)MessageChannels.ScriptStatus, (r, m) => r.OnScriptStopped());
        StrongReferenceMessenger.Default.Register<ScriptSchedulerViewModel, QueueScriptMessage, int>(this, (int)MessageChannels.ScriptStatus, (r, m) => r.OnQueueScript(m));
    }

    private void OnQueueScript(QueueScriptMessage message)
    {
        if (!string.IsNullOrEmpty(message.Path))
        {
            var item = new ScriptItemViewModel(message.Path);
            int count = ScriptQueue.Count(x => x.Path == message.Path);
            if (count > 0)
                item.Name = $"{Path.GetFileNameWithoutExtension(message.Path)} ({count + 1}){Path.GetExtension(message.Path)}";
            
            ScriptQueue.Add(item);
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
    private async Task EditScriptConfig(ScriptItemViewModel item)
    {
        if (_manager.ScriptRunning)
        {
            _dialogService.ShowMessageBox("Script currently running. Stop the script to change its options.", "Script Running");
            return;
        }

        try
        {
            object compiled = await Task.Run(() => _manager.Compile(File.ReadAllText(item.Path))!);
            _manager.OverrideStorage = item.Storage;
            _manager.LoadScriptConfig(compiled);
            if (_manager.Config!.Options.Count > 0 || _manager.Config.MultipleOptions.Count > 0)
                _manager.Config.Configure();
            else
                _dialogService.ShowMessageBox("The loaded script has no options to configure.", "No Options");
            
            _manager.OverrideStorage = null;
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessageBox($"Script cannot be configured as it has compilation errors:\r\n{ex}", "Script Error");
        }
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
            
            _manager.OverrideStorage = nextScript.Storage;
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
        _manager.OverrideStorage = null;

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

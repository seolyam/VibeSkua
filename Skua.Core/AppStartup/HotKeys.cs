using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Skua.Core.Interfaces;
using Skua.Core.Messaging;
using System.Runtime.InteropServices;

namespace Skua.Core.AppStartup;

public class HotKeys
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    public const int WM_SKUA_HOTKEY = 0x0400 + 445;

    public static bool IsHostProcess { get; set; } = false;
    public static IntPtr ActiveChildHwnd { get; set; } = IntPtr.Zero;

    public enum HotkeyAction
    {
        ToggleScript = 1,
        LoadScript = 2,
        OpenBank = 3,
        OpenConsole = 4,
        ToggleAutoAttack = 5,
        ToggleAutoHunt = 6,
        ToggleLagKiller = 7
    }

    public static void ExecuteHotkeyAction(int actionId)
    {
        switch ((HotkeyAction)actionId)
        {
            case HotkeyAction.ToggleScript:
                ToggleScriptLocal();
                break;
            case HotkeyAction.LoadScript:
                LoadScriptLocal();
                break;
            case HotkeyAction.OpenBank:
                Ioc.Default.GetRequiredService<IScriptBank>().Open();
                break;
            case HotkeyAction.OpenConsole:
                OpenConsoleLocal();
                break;
            case HotkeyAction.ToggleAutoAttack:
                ToggleAutoAttackLocal();
                break;
            case HotkeyAction.ToggleAutoHunt:
                ToggleAutoHuntLocal();
                break;
            case HotkeyAction.ToggleLagKiller:
                ToggleLagKillerLocal();
                break;
        }
    }

    private static void ExecuteOrForward(HotkeyAction action, Action localExecute)
    {
        if (IsHostProcess)
        {
            if (ActiveChildHwnd != IntPtr.Zero)
            {
                PostMessage(ActiveChildHwnd, WM_SKUA_HOTKEY, (IntPtr)action, IntPtr.Zero);
            }
        }
        else
        {
            localExecute();
        }
    }

    internal static Dictionary<string, IRelayCommand> CreateHotKeys(IServiceProvider s)
    {
        Dictionary<string, IRelayCommand> hotKeys = new()
        {
            { "ToggleScript", new RelayCommand(() => ExecuteOrForward(HotkeyAction.ToggleScript, ToggleScriptLocal), CanExecuteHotKey) },
            { "LoadScript", new RelayCommand(() => ExecuteOrForward(HotkeyAction.LoadScript, LoadScriptLocal), CanExecuteHotKey) },
            { "OpenBank", new RelayCommand(() => ExecuteOrForward(HotkeyAction.OpenBank, Ioc.Default.GetRequiredService<IScriptBank>().Open), CanExecuteHotKey) },
            { "OpenConsole", new RelayCommand(() => ExecuteOrForward(HotkeyAction.OpenConsole, OpenConsoleLocal), CanExecuteHotKey) },
            { "ToggleAutoAttack", new RelayCommand(() => ExecuteOrForward(HotkeyAction.ToggleAutoAttack, ToggleAutoAttackLocal), CanExecuteHotKey) },
            { "ToggleAutoHunt", new RelayCommand(() => ExecuteOrForward(HotkeyAction.ToggleAutoHunt, ToggleAutoHuntLocal), CanExecuteHotKey) },
            { "ToggleLagKiller", new RelayCommand(() => ExecuteOrForward(HotkeyAction.ToggleLagKiller, ToggleLagKillerLocal), CanExecuteHotKey) }
        };

        return hotKeys;
    }

    private static bool CanExecuteHotKey()
    {
        try
        {
            IntPtr foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
                return false;

            GetWindowThreadProcessId(foregroundWindow, out uint foregroundProcessId);
            return foregroundProcessId == (uint)Environment.ProcessId;
        }
        catch
        {
            return false;
        }
    }

    private static void ToggleAutoHuntLocal()
    {
        if (Ioc.Default.GetRequiredService<IScriptAuto>().IsRunning)
        {
            StrongReferenceMessenger.Default.Send<StopAutoMessage>();
            return;
        }

        StrongReferenceMessenger.Default.Send<StartAutoHuntMessage>();
    }

    private static void ToggleAutoAttackLocal()
    {
        if (Ioc.Default.GetRequiredService<IScriptAuto>().IsRunning)
        {
            StrongReferenceMessenger.Default.Send<StopAutoMessage>();
            return;
        }

        StrongReferenceMessenger.Default.Send<StartAutoAttackMessage>();
    }

    private static void OpenConsoleLocal()
    {
        Ioc.Default.GetRequiredService<IWindowService>().ShowManagedWindow("Console");
    }

    private static void ToggleScriptLocal()
    {
        StrongReferenceMessenger.Default.Send<ToggleScriptMessage, int>((int)MessageChannels.ScriptStatus);
    }

    private static void LoadScriptLocal()
    {
        StrongReferenceMessenger.Default.Send<LoadScriptMessage, int>(new(null), (int)MessageChannels.ScriptStatus);
    }

    private static void ToggleLagKillerLocal()
    {
        IScriptOption options = Ioc.Default.GetRequiredService<IScriptOption>();
        options.LagKiller = !options.LagKiller;
    }
}

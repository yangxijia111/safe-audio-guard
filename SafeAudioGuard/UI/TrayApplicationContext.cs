using System.Diagnostics;
using System.Drawing;
using SafeAudioGuard.Audio;
using SafeAudioGuard.Infrastructure;

namespace SafeAudioGuard.UI;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AppLog _log = new();
    private readonly SettingsStore _settingsStore = new();
    private readonly AppSettings _settings;
    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _statusMenu;
    private readonly ToolStripMenuItem _pauseMenu;
    private readonly ToolStripMenuItem _autoStartMenu;
    private readonly ToolStripMenuItem _lastActionMenu;
    private readonly Control _uiDispatcher = new();
    private readonly AudioGuardMonitor _monitor;
    private bool _suppressStartupChange;

    internal TrayApplicationContext()
    {
        _settings = _settingsStore.Load();
        _log.Write("SafeAudioGuard 进程启动。");

        var menu = new ContextMenuStrip();
        _statusMenu = new ToolStripMenuItem("音量保护：已启用（上限 45%）") { Enabled = false };
        menu.Items.Add(_statusMenu);
        _lastActionMenu = new ToolStripMenuItem("最近调整：暂无") { Enabled = false };
        menu.Items.Add(_lastActionMenu);
        menu.Items.Add(new ToolStripSeparator());

        _pauseMenu = new ToolStripMenuItem("暂停保护") { CheckOnClick = true };
        _pauseMenu.CheckedChanged += (_, _) =>
        {
            _monitor!.SetPaused(_pauseMenu.Checked);
            _pauseMenu.Text = _pauseMenu.Checked ? "恢复保护" : "暂停保护";
            _statusMenu.Text = _pauseMenu.Checked ? "音量保护：已暂停" : "音量保护：已启用（上限 45%）";
        };
        menu.Items.Add(_pauseMenu);

        menu.Items.Add("立即检查所有活动输出", null, (_, _) =>
            _monitor!.CheckAllActiveOutputs("用户手动检查"));

        _autoStartMenu = new ToolStripMenuItem("开机自动启动")
        {
            CheckOnClick = true, Checked = _settings.StartWithWindows
        };
        _autoStartMenu.CheckedChanged += (_, _) =>
        {
            if (_suppressStartupChange) return;
            try
            {
                StartupManager.Configure(_autoStartMenu.Checked);
                _settings.StartWithWindows = _autoStartMenu.Checked;
                _settingsStore.Save(_settings);
                _log.Write("开机自启设置：" + (_autoStartMenu.Checked ? "启用" : "关闭"));
            }
            catch (Exception ex)
            {
                _suppressStartupChange = true;
                _autoStartMenu.Checked = _settings.StartWithWindows;
                _suppressStartupChange = false;
                _log.Write("开机自启设置失败：" + ex.Message);
                MessageBox.Show(ex.Message, "无法更改开机自启", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        menu.Items.Add(_autoStartMenu);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("查看运行日志", null, (_, _) =>
        {
            if (!File.Exists(_log.FilePath)) _log.Write("打开日志文件。");
            Process.Start(new ProcessStartInfo("notepad.exe")
            {
                ArgumentList = { _log.FilePath }, UseShellExecute = true
            });
        });
        menu.Items.Add("退出程序", null, (_, _) => ExitThread());

        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Shield,
            Text = "SafeAudioGuard · 音量保护 45%",
            ContextMenuStrip = menu,
            Visible = true
        };
        // 创建消息分发句柄，让后台线程的状态报告安全地返回 UI 线程。
        _ = _uiDispatcher.Handle;
        _monitor = new AudioGuardMonitor(_log, ShowAdjustment);

        try
        {
            StartupManager.Configure(_settings.StartWithWindows);
            _settingsStore.Save(_settings);
        }
        catch (Exception ex)
        {
            _log.Write("开机自启初始化失败：" + ex.Message);
            _tray.ShowBalloonTip(3500, "SafeAudioGuard",
                "音量监听已启动，但开机自启配置失败。请查看日志。", ToolTipIcon.Warning);
        }
    }

    private void ShowAdjustment(string message)
    {
        try
        {
            if (_uiDispatcher.IsHandleCreated && !_uiDispatcher.IsDisposed)
                _uiDispatcher.BeginInvoke((MethodInvoker)(() =>
                {
                    _lastActionMenu.Text = message.Length > 70 ? message[..70] + "…" : message;
                }));
        }
        catch (InvalidOperationException) { /* 退出期间 UI 句柄已关闭 */ }
    }

    protected override void ExitThreadCore()
    {
        _monitor.Dispose();
        _tray.Visible = false;
        _tray.ContextMenuStrip?.Dispose();
        _tray.Dispose();
        _uiDispatcher.Dispose();
        base.ExitThreadCore();
    }
}

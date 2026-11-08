using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CourseCapture
{
    internal sealed class TrayAppContext : ApplicationContext
    {
        private readonly AppConfig _config;
        private readonly CaptureEngine _engine;
        private readonly NotifyIcon _notifyIcon;
        private readonly Icon _trayIcon;
        private readonly ContextMenuStrip _menu;
        private readonly ToolStripMenuItem _statusItem;
        private readonly ToolStripMenuItem _pauseItem;
        private readonly ToolStripMenuItem _openRecentItem;
        private readonly ToolStripMenuItem _autoStartItem;
        private readonly Timer _timer;
        private readonly Form _marshalForm;
        private readonly bool _autostart;

        private string _lastSavedDir;
        private StatusForm _statusForm;
        private bool _exiting;
        private bool _disposed;

        public TrayAppContext(bool autostart)
        {
            _autostart = autostart;
            _config = AppConfig.Load();
            try
            {
                Directory.CreateDirectory(_config.SaveRoot);
                Directory.CreateDirectory(AppPaths.LogDir);
                Directory.CreateDirectory(AppPaths.TempRoot);
            }
            catch (Exception ex)
            {
                Logger.Warn("创建默认目录失败: " + ex.Message);
            }

            // 后台事件需要切回 UI 线程；ApplicationContext 本身没有窗口句柄，创建一个隐藏窗口做消息泵。
            _marshalForm = new Form();
            _marshalForm.Text = "CourseCaptureMarshal";
            _marshalForm.ShowInTaskbar = false;
            _marshalForm.FormBorderStyle = FormBorderStyle.None;
            _marshalForm.StartPosition = FormStartPosition.Manual;
            _marshalForm.Bounds = new Rectangle(-32000, -32000, 1, 1);
            IntPtr forceHandle = _marshalForm.Handle;

            _trayIcon = IconFactory.CreateTrayIcon();
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = _trayIcon;
            _notifyIcon.Text = "课件捕手 · 待机中";
            _notifyIcon.Visible = true;
            Logger.Info("托盘图标已创建");
            _notifyIcon.DoubleClick += delegate { OpenFolder(_config.SaveRoot); };
            _notifyIcon.BalloonTipClicked += delegate
            {
                if (!string.IsNullOrEmpty(_lastSavedDir) && Directory.Exists(_lastSavedDir))
                    OpenFolder(_lastSavedDir);
                else
                    OpenFolder(_config.SaveRoot);
            };

            _menu = new ContextMenuStrip();
            _menu.Font = new Font("Microsoft YaHei UI", 9f);
            _menu.ShowImageMargin = false;

            _statusItem = new ToolStripMenuItem("待机中");
            _statusItem.Enabled = false;
            _menu.Items.Add(_statusItem);
            _menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem openRootItem = new ToolStripMenuItem("打开课件文件夹");
            openRootItem.Click += delegate { OpenFolder(_config.SaveRoot); };
            _menu.Items.Add(openRootItem);

            _openRecentItem = new ToolStripMenuItem("打开最近保存的课件");
            _openRecentItem.Enabled = false;
            _openRecentItem.Click += delegate
            {
                if (!string.IsNullOrEmpty(_lastSavedDir) && Directory.Exists(_lastSavedDir))
                    OpenFolder(_lastSavedDir);
            };
            _menu.Items.Add(_openRecentItem);

            ToolStripMenuItem showWindowItem = new ToolStripMenuItem("显示状态窗口");
            showWindowItem.Click += delegate { ShowStatusWindow(); };
            _menu.Items.Add(showWindowItem);
            _menu.Items.Add(new ToolStripSeparator());

            _pauseItem = new ToolStripMenuItem("暂停记录");
            _pauseItem.Click += delegate { TogglePause(); };
            _menu.Items.Add(_pauseItem);

            ToolStripMenuItem settingsItem = new ToolStripMenuItem("设置...");
            settingsItem.Click += delegate { OpenSettings(); };
            _menu.Items.Add(settingsItem);

            ToolStripMenuItem diagnosticsItem = new ToolStripMenuItem("诊断信息...");
            diagnosticsItem.Click += delegate { OpenDiagnostics(); };
            _menu.Items.Add(diagnosticsItem);
            _menu.Items.Add(new ToolStripSeparator());

            _autoStartItem = new ToolStripMenuItem("开机自动启动");
            _autoStartItem.Click += delegate { ToggleAutoStart(); };
            _menu.Items.Add(_autoStartItem);

            ToolStripMenuItem aboutItem = new ToolStripMenuItem("关于 / 使用说明");
            aboutItem.Click += delegate { ShowAbout(); };
            _menu.Items.Add(aboutItem);

            _menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += delegate { ExitApp(); };
            _menu.Items.Add(exitItem);

            _menu.Opening += delegate
            {
                _autoStartItem.Checked = AutoStartManager.IsEnabled();
                _openRecentItem.Enabled = !string.IsNullOrEmpty(_lastSavedDir) && Directory.Exists(_lastSavedDir);
                UpdateStatus();
            };
            _notifyIcon.ContextMenuStrip = _menu;

            _engine = new CaptureEngine(_config);
            _engine.SessionStarted += OnSessionStarted;
            _engine.SessionFinalized += OnSessionFinalized;
            _engine.StatusChanged += OnStatusChanged;

            _timer = new Timer();
            _timer.Interval = 1000;
            _timer.Tick += delegate { UpdateStatus(); };
            _timer.Start();

            SystemEvents.SessionEnding += OnSessionEnding;
            try
            {
                _engine.Start();
            }
            catch (Exception ex)
            {
                Logger.Error("启动自动捕获失败", ex);
                MessageBox.Show("无法启动自动捕获：\r\n" + ex.Message, "课件捕手",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            UpdateStatus();
            string startText = _autostart
                ? "正在后台自动记录。老师打开 PPT 或白板后，退出时会自动保存。"
                : "双击托盘图标打开课件文件夹。老师打开 PPT 或白板后会自动记录。";
            ShowBalloon("课件捕手已启动", startText, 6000);
        }

        private void OnSessionStarted(object sender, SessionEventArgs e)
        {
            if (_disposed) return;
            Post(delegate { HandleSessionStarted(e.Session); });
        }

        private void OnSessionFinalized(object sender, SessionEventArgs e)
        {
            if (_disposed) return;
            Post(delegate { HandleSessionFinalized(e.Session); });
        }

        private void OnStatusChanged(object sender, EventArgs e)
        {
            if (_disposed) return;
            Post(UpdateStatus);
        }

        private void Post(MethodInvoker action)
        {
            try
            {
                if (_disposed || _marshalForm == null || _marshalForm.IsDisposed) return;
                if (_marshalForm.InvokeRequired) _marshalForm.BeginInvoke(action);
                else action();
            }
            catch (Exception ex)
            {
                Logger.Warn("界面更新失败: " + ex.Message);
            }
        }

        private void HandleSessionStarted(CaptureSession session)
        {
            if (_exiting) return;
            string subject = string.IsNullOrEmpty(session.Subject) ? "未分类" : session.Subject;
            string title = FileUtil.CleanTitle(session.Title);
            ShowBalloon("开始记录课件", subject + " · " + Shorten(title, 38), 4500);
            UpdateStatus();
        }

        private void HandleSessionFinalized(CaptureSession session)
        {
            if (_exiting) return;
            if (!string.IsNullOrEmpty(session.TargetDir))
            {
                _lastSavedDir = session.TargetDir;
                _openRecentItem.Enabled = Directory.Exists(_lastSavedDir);
            }

            string subject = string.IsNullOrEmpty(session.Subject) ? "未分类" : session.Subject;
            string title = FileUtil.CleanTitle(session.Title);
            string detail;
            if (!string.IsNullOrEmpty(session.LastError)) detail = "保存时出现问题：" + session.LastError;
            else if (session.PdfCreated)
            {
                detail = "已生成 " + session.PdfPages + " 页带字迹的 PDF 笔记";
            }
            else detail = "已保存课件和课堂截图";
            ShowBalloon("课件已保存", subject + " · " + title + "\r\n" + detail, 9000);
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (_disposed) return;
            string status;
            if (_engine.IsPaused)
            {
                status = "已暂停记录";
            }
            else
            {
                System.Collections.Generic.List<SessionInfo> sessions = _engine.GetActiveSessions();
                if (sessions.Count == 0)
                {
                    status = "待机中 · 自动监听课件窗口";
                }
                else
                {
                    SessionInfo first = sessions[0];
                    status = "正在记录：" + first.Subject + " · " + Shorten(first.Title, 26);
                    if (sessions.Count > 1) status += " 等 " + sessions.Count + " 个";
                }
            }

            _notifyIcon.Text = Truncate("课件捕手 · " + status, 63);
            _statusItem.Text = status;
            _pauseItem.Text = _engine.IsPaused ? "继续记录" : "暂停记录";
            _pauseItem.Checked = _engine.IsPaused;
        }

        private void TogglePause()
        {
            if (_exiting) return;
            if (_engine.IsPaused)
            {
                _engine.Resume();
                ShowBalloon("已继续记录", "课件窗口会重新开始自动记录。", 3000);
            }
            else
            {
                _engine.Pause();
                ShowBalloon("已暂停记录", "当前正在记录的课件会在你继续后恢复。", 3000);
            }
            UpdateStatus();
        }

        private void ToggleAutoStart()
        {
            bool enable = !AutoStartManager.IsEnabled();
            string error;
            bool ok = AutoStartManager.SetEnabled(enable, out error);
            _autoStartItem.Checked = AutoStartManager.IsEnabled();

            if (!ok)
            {
                MessageBox.Show("设置开机自动启动失败：\r\n" + (error ?? "未知原因"),
                    "课件捕手", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (enable)
            {
                MessageBox.Show(
                    "已设置当前账户的开机自动启动。\r\n\r\n" +
                    "如果系统限制了启动项，也可以每次开机后双击一次程序，" +
                    "之后它会常驻托盘自动工作。",
                    "开机自动启动", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                ShowBalloon("已关闭开机自动启动", "下次开机需要手动双击运行。", 4000);
            }
        }

        private void OpenSettings()
        {
            if (_exiting) return;
            bool wasPaused = _engine.IsPaused;
            if (!wasPaused) _engine.Pause();
            try
            {
                using (SettingsForm form = new SettingsForm(_config.Copy()))
                {
                    if (form.ShowDialog() != DialogResult.OK) return;
                    AppConfig updated = form.Result;
                    if (updated == null) return;
                    try
                    {
                        updated.Save();
                        _config.SaveRoot = updated.SaveRoot;
                        _config.CaptureIntervalSeconds = updated.CaptureIntervalSeconds;
                        _config.IdleFinalizeMinutes = updated.IdleFinalizeMinutes;
                        _config.MaxFrames = updated.MaxFrames;
                        _config.EnablePdf = updated.EnablePdf;
                        _config.KeepTimelinePdf = updated.KeepTimelinePdf;
                        _config.EnableCom = updated.EnableCom;
                        _config.CaptureOnlyForeground = updated.CaptureOnlyForeground;
                        _config.MonitoredProcesses = updated.MonitoredProcesses;
                        _config.SearchRoots = updated.SearchRoots;
                        _config.Subjects = updated.Subjects;
                        _engine.UpdateConfig(_config);
                        try { Directory.CreateDirectory(_config.SaveRoot); }
                        catch (Exception ex) { Logger.Warn("创建保存目录失败: " + ex.Message); }
                        ShowBalloon("设置已保存", "课件保存位置：" + _config.SaveRoot, 5000);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("保存设置失败", ex);
                        MessageBox.Show("设置没有保存：\r\n" + ex.Message, "课件捕手",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            finally
            {
                if (!wasPaused) _engine.Resume();
                UpdateStatus();
            }
        }

        public void RequestShowStatusWindow()
        {
            Post(delegate { ShowStatusWindow(); });
        }

        public void ShowStatusWindow()
        {
            if (_disposed) return;
            try
            {
                if (_statusForm == null || _statusForm.IsDisposed)
                {
                    _statusForm = new StatusForm(_engine);
                    _statusForm.OpenFolderAction = delegate { OpenFolder(_config.SaveRoot); };
                    _statusForm.OpenSettingsAction = delegate { OpenSettings(); };
                    _statusForm.ExitAction = delegate { ExitApp(); };
                }
                _statusForm.Show();
                if (_statusForm.WindowState == FormWindowState.Minimized)
                    _statusForm.WindowState = FormWindowState.Normal;
                _statusForm.Activate();
            }
            catch (Exception ex)
            {
                Logger.Error("显示状态窗口失败", ex);
            }
        }

        private void OpenDiagnostics()
        {
            using (DiagnosticsForm form = new DiagnosticsForm(_config, _engine))
            {
                form.ShowDialog();
            }
        }

        private void ShowAbout()
        {
            string text =
                "课件捕手 " + AppPaths.Version + "\r\n" +
                "自动保存老师的课件和课堂板书\r\n\r\n" +
                "使用方法：\r\n" +
                "1. 让本程序常驻托盘（屏幕右下角）。\r\n" +
                "2. 老师打开 PowerPoint、WPS 或希沃白板讲课时，程序自动记录屏幕画面。\r\n" +
                "3. 老师关闭课件后，程序会自动整理原 PPT 和带字迹的 PDF。\r\n\r\n" +
                "课件位置：\r\n" + _config.SaveRoot + "\r\n\r\n" +
                "如果右下角看不到托盘图标，双击程序会打开状态窗口；也可以在任务栏的 ^ 里找，\r\n" +
                "或到 设置 → 个性化 → 任务栏 → 其他系统托盘图标 中打开“课件捕手”。\r\n\r\n" +
                "如果开机启动未生效，每次开机后双击一次程序即可，之后无需操作。";

            MessageBox.Show(text, "关于课件捕手", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnSessionEnding(object sender, SessionEndingEventArgs e)
        {
            if (_exiting) return;
            _exiting = true;
            try
            {
                Logger.Info("检测到注销/关机，正在保存正在记录的课件");
                _engine.Stop();
            }
            catch (Exception ex)
            {
                Logger.Error("关机前保存失败", ex);
            }
        }

        private void ExitApp()
        {
            if (_exiting) return;
            int active = 0;
            try { active = _engine.GetActiveSessions().Count; }
            catch { }
            if (active > 0)
            {
                DialogResult result = MessageBox.Show(
                    "当前有 " + active + " 个课件正在记录。\r\n退出前会自动整理并保存，可能需要几秒钟。\r\n\r\n确定退出吗？",
                    "退出课件捕手", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result != DialogResult.Yes) return;
            }

            _exiting = true;
            ExitThread();
        }

        private void ShowBalloon(string title, string text, int timeout)
        {
            if (_disposed || _exiting) return;
            try
            {
                _notifyIcon.BalloonTipTitle = title;
                _notifyIcon.BalloonTipText = text;
                _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
                _notifyIcon.ShowBalloonTip(timeout);
            }
            catch (Exception ex)
            {
                Logger.Warn("显示托盘提示失败: " + ex.Message);
            }
        }

        private void OpenFolder(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return;
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = "explorer.exe";
                info.Arguments = "\"" + path + "\"";
                info.UseShellExecute = true;
                Process.Start(info);
            }
            catch (Exception ex)
            {
                Logger.Warn("打开文件夹失败: " + ex.Message);
                MessageBox.Show("无法打开文件夹：\r\n" + path + "\r\n\r\n" + ex.Message,
                    "课件捕手", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static string Shorten(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return "(未命名)";
            text = text.Trim();
            if (text.Length <= max) return text;
            if (max <= 1) return "…";
            return text.Substring(0, max - 1) + "…";
        }

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.Length <= max) return text;
            return text.Substring(0, max);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                _exiting = true;
                try { _timer.Stop(); _timer.Dispose(); } catch { }
                try { if (_statusForm != null) _statusForm.Dispose(); } catch { }
                try { _notifyIcon.Visible = false; } catch { }
                try { SystemEvents.SessionEnding -= OnSessionEnding; } catch { }
                try { _engine.Stop(); } catch (Exception ex) { Logger.Error("退出时保存失败", ex); }
                try { _engine.Dispose(); } catch { }
                try { _notifyIcon.Dispose(); } catch { }
                try { _menu.Dispose(); } catch { }
                try { _trayIcon.Dispose(); } catch { }
                try { _marshalForm.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}

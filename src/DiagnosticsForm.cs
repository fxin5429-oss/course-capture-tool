using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;

namespace CourseCapture
{
    internal sealed class DiagnosticsForm : Form
    {
        private readonly AppConfig _config;
        private readonly CaptureEngine _engine;
        private readonly TextBox _textBox;

        public DiagnosticsForm(AppConfig config, CaptureEngine engine)
        {
            _config = config;
            _engine = engine;

            Text = "课件捕手诊断信息";
            Font = new Font("Microsoft YaHei UI", 9f);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(780, 650);
            MinimumSize = new Size(620, 460);
            ShowInTaskbar = false;

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 2;
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));
            Controls.Add(root);

            _textBox = new TextBox();
            _textBox.Multiline = true;
            _textBox.ReadOnly = true;
            _textBox.ScrollBars = ScrollBars.Both;
            _textBox.WordWrap = false;
            _textBox.Font = new Font("Consolas", 9f);
            _textBox.BackColor = Color.White;
            _textBox.Dock = DockStyle.Fill;
            _textBox.Margin = new Padding(12);
            _textBox.Text = BuildText();
            root.Controls.Add(_textBox, 0, 0);

            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Fill;
            bottom.BackColor = Color.FromArgb(248, 250, 252);
            root.Controls.Add(bottom, 0, 1);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.WrapContents = false;
            buttons.AutoSize = true;
            buttons.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttons.Location = new Point(430, 11);
            bottom.Controls.Add(buttons);

            Button closeButton = new Button();
            closeButton.Text = "关闭";
            closeButton.Size = new Size(84, 30);
            closeButton.Click += delegate { Close(); };
            buttons.Controls.Add(closeButton);

            Button openSaveButton = new Button();
            openSaveButton.Text = "打开课件文件夹";
            openSaveButton.Size = new Size(124, 30);
            openSaveButton.Click += delegate { OpenFolder(_config.SaveRoot); };
            buttons.Controls.Add(openSaveButton);

            Button openLogButton = new Button();
            openLogButton.Text = "打开日志文件夹";
            openLogButton.Size = new Size(124, 30);
            openLogButton.Click += delegate { OpenFolder(AppPaths.LogDir); };
            buttons.Controls.Add(openLogButton);

            Button copyButton = new Button();
            copyButton.Text = "复制诊断信息";
            copyButton.Size = new Size(124, 30);
            copyButton.Click += delegate { CopyText(); };
            buttons.Controls.Add(copyButton);

            Label hint = new Label();
            hint.Text = "把这些信息发给维护人员，可以快速定位问题。";
            hint.AutoSize = true;
            hint.ForeColor = Color.FromArgb(100, 116, 139);
            hint.Location = new Point(12, 19);
            bottom.Controls.Add(hint);
        }

        private string BuildText()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("课件捕手 诊断信息");
            sb.AppendLine("========================================");
            sb.AppendLine("时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("版本：" + AppPaths.Version);
            sb.AppendLine("程序路径：" + AppPaths.ExePath);
            sb.AppendLine("程序目录：" + AppPaths.BaseDir);
            sb.AppendLine("配置文件：" + AppConfig.ConfigPath);
            sb.AppendLine("日志目录：" + AppPaths.LogDir);
            sb.AppendLine("临时目录：" + AppPaths.TempRoot);
            sb.AppendLine("操作系统：" + Environment.OSVersion.VersionString +
                "（64 位系统：" + Environment.Is64BitOperatingSystem + "）");
            sb.AppendLine("当前用户：" + Environment.UserName + "，管理员：" + IsAdministrator());
            sb.AppendLine(".NET 版本：" + Environment.Version);
            try
            {
                Screen screen = Screen.PrimaryScreen;
                if (screen != null)
                {
                    sb.AppendLine("主屏分辨率：" + screen.Bounds.Width + " × " + screen.Bounds.Height +
                        "，工作区：" + screen.WorkingArea.Width + " × " + screen.WorkingArea.Height);
                }
            }
            catch { }
            sb.AppendLine("程序是否在系统盘：" + AutoStartManager.IsOnSystemDrive());
            sb.AppendLine("开机自动启动：" + (AutoStartManager.IsEnabled() ? "已启用" : "未启用") +
                "（" + AutoStartManager.GetMethod() + "）");
            sb.AppendLine("启动快捷方式：" + AutoStartManager.StartupShortcutPath);
            sb.AppendLine();
            sb.AppendLine("保存根目录：" + _config.SaveRoot);
            sb.AppendLine("保存目录状态：" + TestWrite(_config.SaveRoot));
            sb.AppendLine("保存盘剩余空间：" + GetFreeSpace(_config.SaveRoot));
            sb.AppendLine();
            sb.AppendLine("当前状态：" + (_engine != null ? _engine.CurrentStatus : "未知") +
                "，已暂停：" + (_engine != null && _engine.IsPaused));
            sb.AppendLine("活动会话：");
            try
            {
                List<SessionInfo> sessions = _engine != null ? _engine.GetActiveSessions() : new List<SessionInfo>();
                if (sessions.Count == 0)
                {
                    sb.AppendLine("  （无）");
                }
                else
                {
                    foreach (SessionInfo info in sessions)
                    {
                        sb.AppendLine("  - " + info.Subject + " | " + info.Title + " | " +
                            info.ProcessName + " | " + info.FrameCount + " 张 | 已记录 " +
                            info.Minutes.ToString("0.0") + " 分钟");
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("  读取失败：" + ex.Message);
            }

            sb.AppendLine();
            sb.AppendLine("配置：");
            sb.AppendLine("  截图间隔：" + _config.CaptureIntervalSeconds + " 秒");
            sb.AppendLine("  无操作收尾：" + _config.IdleFinalizeMinutes + " 分钟");
            sb.AppendLine("  最多画面数：" + _config.MaxFrames);
            sb.AppendLine("  生成 PDF：" + _config.EnablePdf + "，保留时间线：" + _config.KeepTimelinePdf);
            sb.AppendLine("  使用 COM：" + _config.EnableCom + "，只记录前台：" + _config.CaptureOnlyForeground);
            sb.AppendLine("  监控进程：" + string.Join(", ", _config.MonitoredProcesses.ToArray()));
            sb.AppendLine("  搜索目录：");
            foreach (string root in _config.SearchRoots)
            {
                sb.AppendLine("    - " + root);
            }
            sb.AppendLine("  科目数量：" + _config.Subjects.Count);
            foreach (SubjectRule rule in _config.Subjects)
            {
                sb.AppendLine("    - " + rule.Name + "：" + rule.KeywordText);
            }


            sb.AppendLine();
            sb.AppendLine("最近日志（最多 40 行）：");
            sb.AppendLine(ReadLogTail(40));
            return sb.ToString();
        }

        private static string TestWrite(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return "路径为空";
                Directory.CreateDirectory(path);
                string test = Path.Combine(path, "课件捕手写入测试_" + Guid.NewGuid().ToString("N") + ".tmp");
                File.WriteAllText(test, "test");
                try { File.Delete(test); } catch { }
                return "可写";
            }
            catch (Exception ex)
            {
                return "不可写：" + ex.Message;
            }
        }

        private static string GetFreeSpace(string path)
        {
            try
            {
                string root = Path.GetPathRoot(Path.GetFullPath(path));
                DriveInfo drive = new DriveInfo(root);
                if (!drive.IsReady) return "不可用";
                return drive.Name + " 剩余 " + (drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0).ToString("0.0") + " GB";
            }
            catch (Exception ex)
            {
                return "读取失败：" + ex.Message;
            }
        }

        private static bool IsAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private static string ReadLogTail(int lineCount)
        {
            try
            {
                if (!Directory.Exists(AppPaths.LogDir)) return "（没有日志目录）";
                string[] files = Directory.GetFiles(AppPaths.LogDir, "*.log");
                if (files.Length == 0) return "（没有日志文件）";
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                string latest = files[files.Length - 1];
                string[] lines = File.ReadAllLines(latest);
                int start = Math.Max(0, lines.Length - lineCount);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("文件：" + Path.GetFileName(latest));
                for (int i = start; i < lines.Length; i++) sb.AppendLine(lines[i]);
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "（读取日志失败：" + ex.Message + "）";
            }
        }

        private void CopyText()
        {
            try
            {
                Clipboard.SetText(_textBox.Text);
                MessageBox.Show("诊断信息已复制到剪贴板。", "课件捕手",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("复制失败：" + ex.Message, "课件捕手",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static void OpenFolder(string path)
        {
            try
            {
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                System.Diagnostics.Process.Start("explorer.exe", "\"" + path + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法打开文件夹：\r\n" + ex.Message, "课件捕手",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}

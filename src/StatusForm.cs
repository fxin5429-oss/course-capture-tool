using System;
using System.Drawing;
using System.Windows.Forms;

namespace CourseCapture
{
    /// <summary>
    /// 手动启动时显示的小窗口：让用户一眼看到程序在运行，并提供不依赖托盘图标的入口。
    /// </summary>
    internal sealed class StatusForm : Form
    {
        private readonly Label _statusLabel;
        private readonly Timer _timer;

        public Action OpenFolderAction;
        public Action OpenSettingsAction;
        public Action ExitAction;

        public StatusForm(CaptureEngine engine)
        {
            Text = "课件捕手";
            ClientSize = new Size(460, 300);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei UI", 9f);
            try { Icon = IconFactory.CreateIcon(32); } catch { }

            Label title = new Label();
            title.Text = "课件捕手已在运行";
            title.Font = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold);
            title.AutoSize = true;
            title.Location = new Point(20, 16);
            Controls.Add(title);

            Label body = new Label();
            body.Text = "它会常驻在屏幕右下角的托盘里，老师打开课件后自动开始记录。\r\n\r\n" +
                "如果右下角看不到图标：\r\n" +
                "· 点任务栏时钟旁的 ^，在隐藏图标里找“课件捕手”\r\n" +
                "· 或打开 设置 → 个性化 → 任务栏 → 其他系统托盘图标，把它打开\r\n\r\n" +
                "这个窗口可以直接操作，不需要先找到托盘图标。";
            body.AutoSize = false;
            body.Size = new Size(420, 148);
            body.Location = new Point(20, 56);
            Controls.Add(body);

            _statusLabel = new Label();
            _statusLabel.Text = "当前状态：" + SafeStatus(engine);
            _statusLabel.AutoSize = false;
            _statusLabel.Size = new Size(420, 22);
            _statusLabel.Location = new Point(20, 210);
            _statusLabel.ForeColor = Color.FromArgb(71, 85, 105);
            Controls.Add(_statusLabel);

            Button openButton = MakeButton("打开课件文件夹", 20, 248);
            openButton.Click += delegate { if (OpenFolderAction != null) OpenFolderAction(); };
            Controls.Add(openButton);

            Button settingsButton = MakeButton("设置...", 152, 248);
            settingsButton.Click += delegate { if (OpenSettingsAction != null) OpenSettingsAction(); };
            Controls.Add(settingsButton);

            Button hideButton = MakeButton("隐藏到托盘", 244, 248);
            hideButton.Click += delegate { Hide(); };
            Controls.Add(hideButton);

            Button exitButton = MakeButton("退出程序", 356, 248);
            exitButton.Click += delegate { if (ExitAction != null) ExitAction(); };
            Controls.Add(exitButton);

            _timer = new Timer();
            _timer.Interval = 1000;
            _timer.Tick += delegate { _statusLabel.Text = "当前状态：" + SafeStatus(engine); };
            _timer.Start();

            FormClosing += OnFormClosing;
        }

        private static string SafeStatus(CaptureEngine engine)
        {
            try { return engine.CurrentStatus; }
            catch { return "待机中"; }
        }

        private static Button MakeButton(string text, int x, int y)
        {
            Button button = new Button();
            button.Text = text;
            button.Size = new Size(100, 30);
            button.Location = new Point(x, y);
            return button;
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _timer.Stop(); _timer.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}

using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace CourseCapture
{
    internal static class Program
    {
        private const string MutexName = "Local\\CourseCapture_SingleInstance_v1";
        private const string ShowWindowEventName = "Local\\CourseCapture_ShowWindow_v1";

        [STAThread]
        private static void Main(string[] args)
        {
            NativeMethods.SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool createdNew;
            using (Mutex mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    NotifyRunningInstance(args);
                    return;
                }

                try
                {
                    Directory.CreateDirectory(AppPaths.LogDir);
                    Directory.CreateDirectory(AppPaths.TempRoot);
                }
                catch { }

                Logger.Info("========================================");
                Logger.Info("课件捕手 " + AppPaths.Version + " 启动，程序目录: " + AppPaths.BaseDir);
                Logger.Info("操作系统: " + Environment.OSVersion.VersionString + "，64 位: " +
                    Environment.Is64BitOperatingSystem + "，用户: " + Environment.UserName);

                Application.ThreadException += OnThreadException;
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

                bool autostart = false;
                foreach (string arg in args)
                {
                    if (string.Equals(arg, "--autostart", StringComparison.OrdinalIgnoreCase))
                    {
                        autostart = true;
                        break;
                    }
                }

                if (autostart)
                {
                    // 登录后稍等片刻，让资源管理器和其他启动项先就绪。
                    Thread.Sleep(2500);
                }

                try
                {
                    using (EventWaitHandle showWindowEvent = new EventWaitHandle(
                        false, EventResetMode.AutoReset, ShowWindowEventName))
                    using (TrayAppContext context = new TrayAppContext(autostart))
                    {
                        if (!autostart)
                        {
                            // 手动启动时显示状态窗口：即使托盘图标被系统隐藏，也能看到程序并操作。
                            context.ShowStatusWindow();
                            Logger.Info("状态窗口已显示");
                        }

                        Thread listener = new Thread(delegate()
                        {
                            while (true)
                            {
                                try
                                {
                                    if (!showWindowEvent.WaitOne()) return;
                                    context.RequestShowStatusWindow();
                                }
                                catch { return; }
                            }
                        });
                        listener.IsBackground = true;
                        listener.Start();

                        Application.Run(context);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("程序异常退出", ex);
                    MessageBox.Show("课件捕手遇到错误并退出：\r\n" + ex.Message +
                        "\r\n\r\n详细信息已写入日志。", "课件捕手",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    Logger.Info("课件捕手已退出");
                }
            }
        }

        private static void NotifyRunningInstance(string[] args)
        {
            bool autostart = false;
            foreach (string arg in args)
            {
                if (string.Equals(arg, "--autostart", StringComparison.OrdinalIgnoreCase))
                {
                    autostart = true;
                    break;
                }
            }
            if (autostart) return;

            try
            {
                using (EventWaitHandle showWindowEvent = EventWaitHandle.OpenExisting(ShowWindowEventName))
                {
                    showWindowEvent.Set();
                }
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                MessageBox.Show("课件捕手已经在运行。\r\n请在屏幕右下角的托盘中找到它的图标。",
                    "课件捕手", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            Logger.Error("界面线程未处理异常", e.Exception);
            try
            {
                MessageBox.Show("操作没有完成，程序已记录错误：\r\n" + e.Exception.Message +
                    "\r\n\r\n如果问题反复出现，请打开“诊断信息”查看日志位置。",
                    "课件捕手", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch { }
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            Logger.Error("后台线程未处理异常", ex ?? new Exception("未知异常"));
        }
    }
}

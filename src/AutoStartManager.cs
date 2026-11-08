using System;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace CourseCapture
{
    public static class AutoStartManager
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "CourseCapture";

        public static string StartupShortcutPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "课件捕手.lnk");
            }
        }

        public static string StartupCommandPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "课件捕手开机启动.cmd");
            }
        }

        public static bool IsEnabled()
        {
            return GetRunValue() != null ||
                File.Exists(StartupShortcutPath) ||
                File.Exists(StartupCommandPath);
        }

        public static string GetMethod()
        {
            if (GetRunValue() != null) return "注册表当前用户启动项";
            if (File.Exists(StartupShortcutPath)) return "开始菜单启动文件夹";
            if (File.Exists(StartupCommandPath)) return "开始菜单启动脚本";
            return "未设置";
        }

        public static bool SetEnabled(bool enabled, out string error)
        {
            error = null;
            if (enabled)
            {
                bool ok = false;
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                    {
                        if (key != null)
                        {
                            key.SetValue(ValueName, "\"" + AppPaths.ExePath + "\" --autostart",
                                RegistryValueKind.String);
                            ok = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    error = "写入注册表启动项失败: " + ex.Message;
                }

                if (!ok)
                {
                    try
                    {
                        CreateStartupCommand();
                        ok = true;
                        error = null;
                    }
                    catch (Exception ex)
                    {
                        error = (error == null ? "" : error + "；") + "创建启动脚本失败: " + ex.Message;
                    }
                }
                return ok;
            }

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key != null) key.DeleteValue(ValueName, false);
                }
            }
            catch (Exception ex)
            {
                error = "删除注册表启动项失败: " + ex.Message;
            }
            try
            {
                if (File.Exists(StartupShortcutPath)) File.Delete(StartupShortcutPath);
                if (File.Exists(StartupCommandPath)) File.Delete(StartupCommandPath);
            }
            catch (Exception ex)
            {
                error = (error == null ? "" : error + "；") + "删除启动脚本失败: " + ex.Message;
            }
            return string.IsNullOrEmpty(error);
        }

        public static bool IsOnSystemDrive()
        {
            try
            {
                string root = Path.GetPathRoot(AppPaths.ExePath);
                string system = Path.GetPathRoot(Environment.SystemDirectory);
                return string.Equals(root, system, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string GetRunValue()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null) return null;
                    string value = key.GetValue(ValueName) as string;
                    if (string.IsNullOrEmpty(value)) return null;
                    return value;
                }
            }
            catch
            {
                return null;
            }
        }

        private static void CreateStartupCommand()
        {
            string startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            if (!Directory.Exists(startup)) Directory.CreateDirectory(startup);

            // 用 .cmd 作为不依赖 WScript.Shell/COM 的兜底启动方式。
            // 中文路径在中文 Windows 的 ANSI 代码页下才能被 cmd.exe 正确读取。
            StringBuilder content = new StringBuilder();
            content.AppendLine("@echo off");
            content.AppendLine("start \"\" \"" + AppPaths.ExePath + "\" --autostart");
            File.WriteAllText(StartupCommandPath, content.ToString(), Encoding.Default);
        }
    }
}

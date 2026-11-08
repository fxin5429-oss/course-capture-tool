using System;
using System.IO;
using System.Text;

namespace CourseCapture
{
    public static class Logger
    {
        private static readonly object Sync = new object();
        private static string _fallbackPath;

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Warn(string message)
        {
            Write("WARN", message);
        }

        public static void Error(string message)
        {
            Write("ERROR", message);
        }

        public static void Error(string message, Exception ex)
        {
            if (ex == null)
            {
                Error(message);
                return;
            }
            Write("ERROR", message + Environment.NewLine + ex.ToString());
        }

        private static void Write(string level, string message)
        {
            try
            {
                lock (Sync)
                {
                    string dir = AppPaths.LogDir;
                    string path;
                    try
                    {
                        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                        path = Path.Combine(dir, DateTime.Now.ToString("yyyy-MM-dd") + ".log");
                    }
                    catch
                    {
                        if (_fallbackPath == null)
                        {
                            _fallbackPath = Path.Combine(Path.GetTempPath(), "课件捕手-" +
                                DateTime.Now.ToString("yyyyMMdd") + ".log");
                        }
                        path = _fallbackPath;
                    }

                    string line = string.Format("[{0:yyyy-MM-dd HH:mm:ss.fff}] [{1}] {2}\r\n",
                        DateTime.Now, level, message);
                    File.AppendAllText(path, line, new UTF8Encoding(false));
                    TrimOldLogs(dir);
                }
            }
            catch
            {
                // 日志失败不能影响主程序。
            }
        }

        private static void TrimOldLogs(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) return;
                FileInfo[] files = new DirectoryInfo(dir).GetFiles("*.log");
                if (files.Length <= 40) return;
                Array.Sort(files, delegate(FileInfo a, FileInfo b)
                {
                    return a.LastWriteTime.CompareTo(b.LastWriteTime);
                });
                for (int i = 0; i < files.Length - 30; i++)
                {
                    try { files[i].Delete(); } catch { }
                }
            }
            catch { }
        }
    }
}

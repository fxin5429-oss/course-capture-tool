using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CourseCapture
{
    public sealed class MonitoredWindow
    {
        public IntPtr Handle;
        public uint ProcessId;
        public string ProcessName;
        public string Title;
        public string ClassName;
        public NativeMethods.RECT Bounds;
        public bool IsForeground;
        public bool IsSlideShow;
        public bool IsFullScreen;
        public bool LikelyPresenting;

        public string Signature
        {
            get
            {
                return Handle.ToInt64().ToString() + "|" + ProcessId.ToString() + "|" +
                    ProcessName + "|" + Title + "|" + Bounds.Left + "," + Bounds.Top + "," +
                    Bounds.Width + "," + Bounds.Height;
            }
        }

        public string Display
        {
            get
            {
                return (ProcessName ?? "") + " · " + (Title ?? "");
            }
        }
    }

    public sealed class WindowDetector
    {
        private readonly AppConfig _config;
        private readonly int _ownPid;

        public WindowDetector(AppConfig config)
        {
            _config = config;
            _ownPid = Process.GetCurrentProcess().Id;
        }

        public List<MonitoredWindow> FindCandidates()
        {
            List<MonitoredWindow> result = new List<MonitoredWindow>();
            IntPtr foreground = NativeMethods.GetForegroundWindow();
            uint foregroundPid = 0;
            if (foreground != IntPtr.Zero) NativeMethods.GetWindowThreadProcessId(foreground, out foregroundPid);

            NativeMethods.EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
            {
                try
                {
                    if (!NativeMethods.IsWindowVisible(hWnd)) return true;
                    if (NativeMethods.IsIconic(hWnd)) return true;

                    uint pid;
                    NativeMethods.GetWindowThreadProcessId(hWnd, out pid);
                    if (pid == 0 || pid == (uint)_ownPid) return true;

                    string title = GetTitle(hWnd);
                    if (title.Length == 0) return true;

                    string proc = GetProcessName(pid);
                    if (!IsMonitoredProcess(proc)) return true;

                    string cls = GetClassName(hWnd);
                    if (!LooksLikeCourseWindow(proc, title, cls)) return true;

                    NativeMethods.RECT rect;
                    if (!GetBestBounds(hWnd, out rect)) return true;
                    if (rect.Width < 160 || rect.Height < 120) return true;

                    MonitoredWindow w = new MonitoredWindow();
                    w.Handle = hWnd;
                    w.ProcessId = pid;
                    w.ProcessName = proc;
                    w.Title = title;
                    w.ClassName = cls;
                    w.Bounds = rect;
                    w.IsForeground = (pid == foregroundPid);
                    w.IsSlideShow = LooksLikeSlideShow(title, cls);
                    w.IsFullScreen = IsNearlyFullScreen(rect);
                    w.LikelyPresenting = w.IsSlideShow || w.IsFullScreen || IsWhiteboardProcess(proc);
                    result.Add(w);
                }
                catch
                {
                }
                return true;
            }, IntPtr.Zero);

            return result;
        }

        public MonitoredWindow FindBest()
        {
            List<MonitoredWindow> list = FindCandidates();
            if (list.Count == 0) return null;

            MonitoredWindow best = null;
            int bestScore = int.MinValue;
            foreach (MonitoredWindow w in list)
            {
                int score = 0;
                if (w.IsForeground) score += 100;
                if (w.IsSlideShow) score += 40;
                if (w.IsFullScreen) score += 20;
                if (IsWhiteboardProcess(w.ProcessName)) score += 15;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = w;
                }
            }
            return best;
        }

        public MonitoredWindow GetForegroundCandidate()
        {
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;
            uint pid;
            NativeMethods.GetWindowThreadProcessId(hwnd, out pid);
            if (pid == 0 || pid == (uint)_ownPid) return null;
            string proc = GetProcessName(pid);
            if (!IsMonitoredProcess(proc)) return null;
            string title = GetTitle(hwnd);
            if (title.Length == 0) return null;
            string cls = GetClassName(hwnd);
            if (!LooksLikeCourseWindow(proc, title, cls)) return null;

            NativeMethods.RECT rect;
            if (!GetBestBounds(hwnd, out rect)) return null;
            if (rect.Width < 160 || rect.Height < 120) return null;

            MonitoredWindow w = new MonitoredWindow();
            w.Handle = hwnd;
            w.ProcessId = pid;
            w.ProcessName = proc;
            w.Title = title;
            w.ClassName = cls;
            w.Bounds = rect;
            w.IsForeground = true;
            w.IsSlideShow = LooksLikeSlideShow(title, cls);
            w.IsFullScreen = IsNearlyFullScreen(rect);
            w.LikelyPresenting = w.IsSlideShow || w.IsFullScreen || IsWhiteboardProcess(proc);
            return w;
        }

        public bool IsMonitoredProcess(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return false;
            foreach (string p in _config.MonitoredProcesses)
            {
                if (string.IsNullOrEmpty(p)) continue;
                string clean = p.Trim();
                if (clean.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    clean = clean.Substring(0, clean.Length - 4);
                }
                if (string.Equals(clean, processName, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static bool IsWhiteboardProcess(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return false;
            string p = processName.ToLowerInvariant();
            return p.IndexOf("easinote", StringComparison.Ordinal) >= 0 ||
                   p.IndexOf("seewo", StringComparison.Ordinal) >= 0 ||
                   p.IndexOf("hitevision", StringComparison.Ordinal) >= 0 ||
                   p.IndexOf("hiteboard", StringComparison.Ordinal) >= 0;
        }

        private static bool LooksLikeCourseWindow(string proc, string title, string cls)
        {
            string t = (title ?? "").ToLowerInvariant();
            string c = (cls ?? "").ToLowerInvariant();
            bool strongClass = c == "pptframeclass" || c == "screenclass" ||
                               c.IndexOf("powerpoint", StringComparison.Ordinal) >= 0 ||
                               c.IndexOf("wps", StringComparison.Ordinal) >= 0 ||
                               c.IndexOf("kwpp", StringComparison.Ordinal) >= 0 ||
                               c.IndexOf("easinote", StringComparison.Ordinal) >= 0;

            if (strongClass) return true;

            if (t.IndexOf("powerpoint", StringComparison.Ordinal) >= 0) return true;
            if (t.IndexOf("幻灯片放映", StringComparison.Ordinal) >= 0) return true;
            if (t.IndexOf("wps", StringComparison.Ordinal) >= 0 &&
                (t.IndexOf("演示", StringComparison.Ordinal) >= 0 || t.IndexOf("ppt", StringComparison.Ordinal) >= 0)) return true;
            if (t.IndexOf("希沃", StringComparison.Ordinal) >= 0) return true;
            if (t.IndexOf("白板", StringComparison.Ordinal) >= 0) return true;
            if (t.IndexOf("课件", StringComparison.Ordinal) >= 0) return true;
            if (t.IndexOf(".pptx", StringComparison.Ordinal) >= 0 || t.IndexOf(".ppt", StringComparison.Ordinal) >= 0) return true;

            // PowerPoint 编辑窗口标题通常就是文件名，靠进程 + 窗口类判断。
            string p = (proc ?? "").ToLowerInvariant();
            if (p == "powerpnt" || p == "wpp")
            {
                if (t.Length >= 2 && t.IndexOf("错误", StringComparison.Ordinal) < 0 &&
                    t.IndexOf("error", StringComparison.Ordinal) < 0 &&
                    t.IndexOf("恢复", StringComparison.Ordinal) < 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool LooksLikeSlideShow(string title, string cls)
        {
            string t = (title ?? "").ToLowerInvariant();
            string c = (cls ?? "").ToLowerInvariant();
            if (t.IndexOf("幻灯片放映", StringComparison.Ordinal) >= 0) return true;
            if (t.IndexOf("slide show", StringComparison.Ordinal) >= 0) return true;
            if (c == "screenclass") return true;
            return false;
        }

        private static bool IsNearlyFullScreen(NativeMethods.RECT rect)
        {
            IntPtr monitor = NativeMethods.MonitorFromWindow(IntPtr.Zero, 2); // MONITOR_DEFAULTTONEAREST
            if (monitor == IntPtr.Zero) return false;
            NativeMethods.MONITORINFO mi = new NativeMethods.MONITORINFO();
            mi.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));
            if (!NativeMethods.GetMonitorInfo(monitor, ref mi)) return false;
            int mw = mi.rcMonitor.Width;
            int mh = mi.rcMonitor.Height;
            if (mw <= 0 || mh <= 0) return false;
            return rect.Width >= mw * 0.82 && rect.Height >= mh * 0.82;
        }

        private static bool GetBestBounds(IntPtr hWnd, out NativeMethods.RECT rect)
        {
            rect = new NativeMethods.RECT();
            try
            {
                NativeMethods.RECT dwm;
                int hr = NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
                    out dwm, System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.RECT)));
                if (hr == 0 && dwm.Width > 0 && dwm.Height > 0)
                {
                    rect = dwm;
                    return true;
                }
            }
            catch
            {
            }
            if (!NativeMethods.GetWindowRect(hWnd, out rect)) return false;
            return rect.Width > 0 && rect.Height > 0;
        }

        private static string GetTitle(IntPtr hWnd)
        {
            StringBuilder sb = new StringBuilder(1024);
            NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString().Trim();
        }

        private static string GetClassName(IntPtr hWnd)
        {
            StringBuilder sb = new StringBuilder(512);
            NativeMethods.GetClassName(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        private static string GetProcessName(uint pid)
        {
            try
            {
                using (Process p = Process.GetProcessById((int)pid))
                {
                    return p.ProcessName;
                }
            }
            catch
            {
                return "";
            }
        }
    }
}

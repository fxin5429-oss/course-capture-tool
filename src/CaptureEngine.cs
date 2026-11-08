using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace CourseCapture
{
    public sealed class SessionEventArgs : EventArgs
    {
        public CaptureSession Session;
        public SessionEventArgs(CaptureSession session) { Session = session; }
    }

    public sealed class CaptureEngine : IDisposable
    {
        private readonly object _sync = new object();
        private readonly List<CaptureSession> _sessions = new List<CaptureSession>();
        private AppConfig _config;
        private WindowDetector _detector;
        private PowerPointLocator _locator;
        private Thread _worker;
        private ManualResetEvent _stopEvent;
        private volatile bool _paused;
        private volatile bool _disposed;
        private DateTime _lastIdleCheck = DateTime.MinValue;
        private DateTime _lastStatusEvent = DateTime.MinValue;

        public event EventHandler<SessionEventArgs> SessionStarted;
        public event EventHandler<SessionEventArgs> SessionFinalized;
        public event EventHandler StatusChanged;

        public CaptureEngine(AppConfig config)
        {
            _config = config;
            _detector = new WindowDetector(config);
            _locator = new PowerPointLocator(config);
        }

        public bool IsPaused
        {
            get { return _paused; }
        }

        public bool IsRunning
        {
            get { return _worker != null && _worker.IsAlive; }
        }

        public void Start()
        {
            if (_worker != null && _worker.IsAlive) return;
            _stopEvent = new ManualResetEvent(false);
            _worker = new Thread(WorkerLoop);
            _worker.IsBackground = true;
            _worker.Name = "课件捕获线程";
            _worker.SetApartmentState(ApartmentState.STA);
            _worker.Start();
            Logger.Info("自动捕获已启动，保存根目录: " + _config.SaveRoot);
        }

        public void Stop()
        {
            if (_disposed) return;
            _disposed = true;
            Logger.Info("正在停止自动捕获...");
            if (_stopEvent != null) _stopEvent.Set();
            if (_worker != null)
            {
                if (!_worker.Join(60000))
                {
                    Logger.Warn("捕获线程未能在 60 秒内结束");
                }
                _worker = null;
            }
            if (_stopEvent != null)
            {
                _stopEvent.Close();
                _stopEvent = null;
            }
        }

        public void Pause()
        {
            _paused = true;
            Logger.Info("用户暂停自动捕获");
            RaiseStatus();
        }

        public void Resume()
        {
            _paused = false;
            Logger.Info("用户恢复自动捕获");
            RaiseStatus();
        }

        public void UpdateConfig(AppConfig config)
        {
            _config = config;
            _detector = new WindowDetector(config);
            _locator = new PowerPointLocator(config);
            RaiseStatus();
        }

        public string CurrentStatus
        {
            get
            {
                if (_paused) return "已暂停";
                lock (_sync)
                {
                    if (_sessions.Count == 0) return "待机中";
                    CaptureSession s = _sessions[0];
                    string name = FileUtil.CleanTitle(s.Title);
                    return "正在记录：" + name + "（" + s.FrameCount + " 张）";
                }
            }
        }

        public List<SessionInfo> GetActiveSessions()
        {
            List<SessionInfo> result = new List<SessionInfo>();
            lock (_sync)
            {
                foreach (CaptureSession s in _sessions)
                {
                    SessionInfo info = new SessionInfo();
                    info.Title = FileUtil.CleanTitle(s.Title);
                    info.ProcessName = s.ProcessName;
                    info.StartTime = s.StartTime;
                    info.FrameCount = s.FrameCount;
                    info.Subject = s.Subject;
                    info.Minutes = (DateTime.Now - s.StartTime).TotalMinutes;
                    result.Add(info);
                }
            }
            return result;
        }

        private void WorkerLoop()
        {
            try
            {
                while (!_stopEvent.WaitOne(200))
                {
                    if (_paused) continue;
                    try
                    {
                        Tick();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("捕获循环发生异常", ex);
                        Thread.Sleep(1000);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("捕获线程异常退出", ex);
            }
            finally
            {
                try
                {
                    FinalizeAll("程序退出");
                }
                catch (Exception ex)
                {
                    Logger.Error("退出时保存课件失败", ex);
                }
            }
        }

        private void Tick()
        {
            DateTime now = DateTime.Now;
            MonitoredWindow current = null;
            try
            {
                current = _config.CaptureOnlyForeground ? _detector.GetForegroundCandidate() : _detector.FindBest();
            }
            catch (Exception ex)
            {
                Logger.Error("检测窗口失败", ex);
            }

            CaptureSession active = null;
            if (current != null)
            {
                lock (_sync)
                {
                    foreach (CaptureSession s in _sessions)
                    {
                        if (s.ProcessId == current.ProcessId && !s.Finalized)
                        {
                            active = s;
                            break;
                        }
                    }

                    if (active == null)
                    {
                        active = new CaptureSession(_config, current);
                        _sessions.Add(active);
                        Logger.Info("开始记录: " + active.Title + " [" + active.ProcessName + "]");
                    }
                }

                if (active != null)
                {
                    bool isNew = active.LastCaptureTime == DateTime.MinValue;
                    active.Touch(current);
                    if (isNew)
                    {
                        RaiseSessionStarted(active);
                    }

                    if (isNew || (now - active.LastCaptureTime).TotalSeconds >= _config.CaptureIntervalSeconds)
                    {
                        CaptureFrame(active, current, now);
                    }
                }
            }

            List<CaptureSession> snapshot;
            lock (_sync) snapshot = new List<CaptureSession>(_sessions);

            foreach (CaptureSession s in snapshot)
            {
                if (s.Finalized) continue;
                bool alive = FileUtil.IsProcessAlive(s.ProcessId);
                if (!alive)
                {
                    FinalizeSession(s, "演示程序已退出");
                    continue;
                }
                if (!NativeMethods.IsWindow(s.WindowHandle) && s != active)
                {
                    FinalizeSession(s, "演示窗口已关闭");
                    continue;
                }
                if (s != active && (now - s.LastSeenTime).TotalMinutes >= 15)
                {
                    FinalizeSession(s, "已切换到其他窗口");
                    continue;
                }
            }

            if (active == null)
            {
                double idle = GetIdleSeconds();
                if ((now - _lastIdleCheck).TotalSeconds >= 5)
                {
                    _lastIdleCheck = now;
                    if (idle >= _config.IdleFinalizeMinutes * 60.0)
                    {
                        List<CaptureSession> idleSessions;
                        lock (_sync) idleSessions = new List<CaptureSession>(_sessions);
                        if (idleSessions.Count > 0)
                        {
                            Logger.Info("无操作 " + _config.IdleFinalizeMinutes + " 分钟，自动收尾");
                            foreach (CaptureSession s in idleSessions) FinalizeSession(s, "长时间无操作");
                        }
                    }
                }
            }
        }

        private void CaptureFrame(CaptureSession session, MonitoredWindow window, DateTime now)
        {
            int slideNumber = session.CurrentSlide;
            bool slideChanged = false;
            if (_config.EnableCom && IsPptProcess(session.ProcessName) &&
                (now - session.LastComCheck).TotalSeconds >= 3)
            {
                session.LastComCheck = now;
                int slide;
                string fullName;
                bool presenting;
                if (PowerPointCom.TryGetInfo(out slide, out fullName, out presenting))
                {
                    if (slide > 0)
                    {
                        if (session.CurrentSlide > 0 && slide != session.CurrentSlide) slideChanged = true;
                        session.CurrentSlide = slide;
                        slideNumber = slide;
                    }
                    if (!string.IsNullOrEmpty(fullName) && File.Exists(fullName) &&
                        FileUtil.IsPowerPointFile(fullName))
                    {
                        session.SourcePath = fullName;
                    }
                }
            }

            Bitmap bmp = null;
            try
            {
                bmp = ScreenCapturer.Capture(window, window.IsForeground);
                if (bmp == null)
                {
                    session.CaptureFailures++;
                    session.LastCaptureTime = now;
                    if (session.CaptureFailures == 1 || session.CaptureFailures % 10 == 0)
                    {
                        Logger.Warn("截图失败（第 " + session.CaptureFailures + " 次）: " + session.Title);
                    }
                    return;
                }

                byte[] fingerprint = ImageFingerprint.FromBitmap(bmp);
                if (session.LastFingerprint != null)
                {
                    double changed;
                    if (!ImageFingerprint.IsDifferent(session.LastFingerprint, fingerprint, out changed))
                    {
                        session.LastCaptureTime = now;
                        session.LastFingerprint = fingerprint;
                        return;
                    }
                }

                string file = Path.Combine(session.TempDir,
                    session.NextFrameIndex.ToString("0000", CultureInfo.InvariantCulture) + "_" +
                    now.ToString("HHmmss", CultureInfo.InvariantCulture) + ".jpg");
                ScreenCapturer.SaveJpeg(bmp, file, 1600, 82L);
                session.NextFrameIndex++;

                CapturedFrame frame = new CapturedFrame();
                frame.Path = file;
                frame.Time = now;
                frame.SlideNumber = slideNumber;
                frame.KeyFrame = session.Frames.Count == 0 || slideChanged ||
                    (session.LastFingerprint == null);
                session.LastFingerprint = fingerprint;
                session.AddFrame(frame);
                session.CaptureFailures = 0;
                session.LastError = null;
                session.CompactIfNeeded();
                RaiseStatus();
            }
            catch (Exception ex)
            {
                session.CaptureFailures++;
                session.LastError = ex.Message;
                session.LastCaptureTime = now;
                Logger.Error("保存截图失败: " + session.Title, ex);
            }
            finally
            {
                if (bmp != null) bmp.Dispose();
            }
        }

        private void FinalizeSession(CaptureSession session, string reason)
        {
            lock (session.Sync)
            {
                if (session.Finalized) return;
                session.Finalized = true;
                session.EndTime = DateTime.Now;
                session.FinalReason = reason;
            }

            lock (_sync) _sessions.Remove(session);
            Logger.Info("开始收尾: " + session.Title + "，原因: " + reason);

            try
            {
                SaveSession(session, reason);
            }
            catch (Exception ex)
            {
                session.LastError = ex.Message;
                Logger.Error("收尾失败: " + session.Title, ex);
            }

            try
            {
                if (session.PdfCreated || !string.IsNullOrEmpty(session.TargetDir))
                {
                    session.CleanupTemp();
                }
            }
            catch { }

            RaiseSessionFinalized(session);
            RaiseStatus();
        }

        private void SaveSession(CaptureSession session, string reason)
        {
            if (session.EndTime < session.StartTime) session.EndTime = DateTime.Now;
            string title = FileUtil.BaseNameWithoutExtension(session.Title);
            if (string.IsNullOrEmpty(title)) title = "课堂";

            string source = session.SourcePath;
            if (string.IsNullOrEmpty(source) || !File.Exists(source) || !FileUtil.IsPowerPointFile(source))
            {
                source = _locator.Find(session.Title, source);
            }
            session.SourcePath = source;

            string subject = SubjectClassifier.Classify(_config, session.Title, source);
            session.Subject = subject;

            string day = session.StartTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            string parent = Path.Combine(_config.SaveRoot, subject, day);
            Directory.CreateDirectory(parent);
            string folderName = session.StartTime.ToString("HHmm", CultureInfo.InvariantCulture) + "_" +
                FileUtil.SanitizeFileName(title, 80);
            string target = FileUtil.UniqueDirectory(parent, folderName);
            Directory.CreateDirectory(target);
            session.TargetDir = target;

            bool sourceCopied = false;
            if (!string.IsNullOrEmpty(source) && File.Exists(source))
            {
                try
                {
                    string dest = Path.Combine(target, Path.GetFileName(source));
                    FileUtil.CopyFileShared(source, dest);
                    sourceCopied = true;
                }
                catch (Exception ex)
                {
                    Logger.Error("复制原始课件失败: " + source, ex);
                    session.LastError = "复制原始课件失败: " + ex.Message;
                }
            }

            if (!sourceCopied)
            {
                StringBuilder missing = new StringBuilder();
                missing.AppendLine("没有自动找到原始课件文件。");
                missing.AppendLine("课件标题：" + title);
                if (!string.IsNullOrEmpty(source)) missing.AppendLine("尝试过的路径：" + source);
                missing.AppendLine("可以把原始 PPT 手动放进本文件夹，PDF 笔记不受影响。");
                missing.AppendLine();
                missing.AppendLine("查找范围：");
                foreach (string r in _config.SearchRoots) missing.AppendLine("  " + r);
                FileUtil.WriteText(Path.Combine(target, "课件原文件_未找到.txt"), missing.ToString());
            }

            List<CapturedFrame> frames = session.SnapshotFrames();
            List<CapturedFrame> compact = BuildCompactFrames(frames, 160);
            int pdfPages = 0;
            bool pdfOk = false;
            string pdfPath = Path.Combine(target, "课堂笔记.pdf");

            if (_config.EnablePdf && frames.Count > 0)
            {
                try
                {
                    string cover = Path.Combine(session.TempDir, "cover.jpg");
                    try
                    {
                        CoverRenderer.Render(cover, title, subject, session.StartTime,
                            session.EndTime < session.StartTime ? DateTime.Now : session.EndTime, frames.Count);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("生成 PDF 封面失败，将直接使用截图: " + ex.Message);
                        cover = null;
                    }

                    List<CapturedFrame> pages = new List<CapturedFrame>();
                    foreach (CapturedFrame f in compact) pages.Add(f);
                    pdfPages = WritePdf(pdfPath, cover, pages, title, subject, false);
                    pdfOk = true;
                    session.PdfCreated = true;

                    if (_config.KeepTimelinePdf)
                    {
                        try
                        {
                            string timeline = Path.Combine(target, "课堂笔记_时间线.pdf");
                            WritePdf(timeline, cover, frames, title, subject, true);
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn("生成时间线 PDF 失败: " + ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    session.LastError = "生成 PDF 失败: " + ex.Message;
                    Logger.Error("生成 PDF 失败: " + title, ex);
                }
            }

            if (!pdfOk)
            {
                if (frames.Count > 0)
                {
                    try
                    {
                        string shots = Path.Combine(target, "截图");
                        Directory.CreateDirectory(shots);
                        foreach (CapturedFrame f in frames)
                        {
                            string name = f.Time.ToString("HHmmss", CultureInfo.InvariantCulture) + "_" +
                                Path.GetFileName(f.Path);
                            File.Copy(f.Path, Path.Combine(shots, name), true);
                        }
                        FileUtil.WriteText(Path.Combine(target, "截图说明.txt"),
                            "PDF 未能生成，已把原始截图按时间顺序保存在“截图”文件夹中。\r\n" +
                            "每张图的文件名前缀是课堂时间。\r\n");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("复制截图失败", ex);
                    }
                }
            }

            int frameCount = frames.Count;
            StringBuilder info = new StringBuilder();
            info.AppendLine("课件捕手 · 课堂记录");
            info.AppendLine("==============================");
            info.AppendLine("课件标题：" + title);
            info.AppendLine("科目：" + subject);
            info.AppendLine("开始时间：" + session.StartTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            info.AppendLine("结束时间：" + session.EndTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            info.AppendLine("课堂时长：" + FileUtil.FormatDuration(session.EndTime - session.StartTime));
            info.AppendLine("识别程序：" + session.ProcessName + "（PID " + session.ProcessId.ToString(CultureInfo.InvariantCulture) + "）");
            info.AppendLine("原始课件：" + (sourceCopied ? Path.GetFileName(source) + "（已复制）" :
                (string.IsNullOrEmpty(source) ? "未找到" : source + "（复制失败）")));
            info.AppendLine("关键画面：" + frameCount.ToString(CultureInfo.InvariantCulture) + " 张");
            if (pdfOk) info.AppendLine("PDF 页数：" + pdfPages.ToString(CultureInfo.InvariantCulture) + " 页");
            info.AppendLine("收尾原因：" + reason);
            if (!string.IsNullOrEmpty(session.LastError)) info.AppendLine("备注：" + session.LastError);
            info.AppendLine("保存位置：" + target);
            info.AppendLine("生成时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            info.AppendLine();
            info.AppendLine("说明：");
            info.AppendLine("1. “课堂笔记.pdf”按时间顺序保留老师讲到的关键画面，包括屏幕上的手写批注。");
            info.AppendLine("2. 原始 PPT 若被老师修改后未保存，PDF 仍保留讲解时的画面。");
            info.AppendLine("3. 如果本科目识别错误，可以修改“配置.xml”里的科目关键词。");
            FileUtil.WriteText(Path.Combine(target, "课堂信息.txt"), info.ToString());

            session.PdfPages = pdfPages;
            Logger.Info("课件已保存: " + target + "，画面 " + frameCount + " 张，PDF " + pdfPages + " 页");
        }

        private int WritePdf(string path, string cover, List<CapturedFrame> frames, string title,
            string subject, bool timeline)
        {
            SimplePdfWriter writer = new SimplePdfWriter();
            writer.SetInfo(title, subject);
            if (!string.IsNullOrEmpty(cover) && File.Exists(cover)) writer.AddPage(cover, 1.0);

            for (int i = 0; i < frames.Count; i++)
            {
                double duration = 4.0;
                if (i + 1 < frames.Count)
                {
                    duration = (frames[i + 1].Time - frames[i].Time).TotalSeconds;
                }
                if (timeline)
                {
                    if (duration < 0.4) duration = 0.4;
                    if (duration > 180) duration = 180;
                }
                else
                {
                    if (duration < 0.6) duration = 0.6;
                    if (duration > 45) duration = 45;
                }
                writer.AddPage(frames[i].Path, duration);
            }
            writer.Save(path);
            return writer.PageCount;
        }

        private static List<CapturedFrame> BuildCompactFrames(List<CapturedFrame> all, int maxPages)
        {
            if (all.Count <= maxPages) return new List<CapturedFrame>(all);

            List<CapturedFrame> keep = new List<CapturedFrame>();
            HashSet<CapturedFrame> seen = new HashSet<CapturedFrame>();
            for (int i = 0; i < all.Count; i++)
            {
                if (i == 0 || i == all.Count - 1 || all[i].KeyFrame)
                {
                    keep.Add(all[i]);
                    seen.Add(all[i]);
                }
            }

            int slots = maxPages - keep.Count;
            if (slots > 0)
            {
                for (int i = 0; i < slots; i++)
                {
                    int index = (int)Math.Round((i + 1) * (all.Count - 1.0) / (slots + 1.0));
                    if (index <= 0) index = 1;
                    if (index >= all.Count - 1) index = all.Count - 2;
                    CapturedFrame f = all[index];
                    if (!seen.Contains(f))
                    {
                        keep.Add(f);
                        seen.Add(f);
                    }
                }
            }

            keep.Sort(delegate(CapturedFrame a, CapturedFrame b) { return a.Time.CompareTo(b.Time); });
            return keep;
        }

        private static bool IsPptProcess(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return false;
            string p = processName.ToLowerInvariant();
            return p == "powerpnt" || p == "wpp";
        }

        private static double GetIdleSeconds()
        {
            try
            {
                NativeMethods.LASTINPUTINFO info = new NativeMethods.LASTINPUTINFO();
                info.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.LASTINPUTINFO));
                if (!NativeMethods.GetLastInputInfo(ref info)) return 0;
                uint current = unchecked((uint)Environment.TickCount);
                uint idle = unchecked(current - info.dwTime);
                return idle / 1000.0;
            }
            catch
            {
                return 0;
            }
        }

        private void FinalizeAll(string reason)
        {
            List<CaptureSession> snapshot;
            lock (_sync) snapshot = new List<CaptureSession>(_sessions);
            foreach (CaptureSession s in snapshot)
            {
                FinalizeSession(s, reason);
            }
        }

        private void RaiseSessionStarted(CaptureSession session)
        {
            EventHandler<SessionEventArgs> handler = SessionStarted;
            if (handler != null)
            {
                try { handler(this, new SessionEventArgs(session)); }
                catch (Exception ex) { Logger.Error("SessionStarted 事件处理失败", ex); }
            }
        }

        private void RaiseSessionFinalized(CaptureSession session)
        {
            EventHandler<SessionEventArgs> handler = SessionFinalized;
            if (handler != null)
            {
                try { handler(this, new SessionEventArgs(session)); }
                catch (Exception ex) { Logger.Error("SessionFinalized 事件处理失败", ex); }
            }
        }

        private void RaiseStatus()
        {
            if ((DateTime.Now - _lastStatusEvent).TotalMilliseconds < 250) return;
            _lastStatusEvent = DateTime.Now;
            EventHandler handler = StatusChanged;
            if (handler != null)
            {
                try { handler(this, EventArgs.Empty); }
                catch { }
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}

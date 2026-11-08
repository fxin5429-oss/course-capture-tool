using System;
using System.Collections.Generic;
using System.IO;

namespace CourseCapture
{
    public sealed class CapturedFrame
    {
        public string Path;
        public DateTime Time;
        public int SlideNumber;
        public bool KeyFrame;
    }

    public sealed class CaptureSession
    {
        public readonly object Sync = new object();
        public Guid Id = Guid.NewGuid();
        public uint ProcessId;
        public string ProcessName;
        public IntPtr WindowHandle;
        public string Title;
        public DateTime StartTime;
        public DateTime LastSeenTime;
        public DateTime LastCaptureTime;
        public DateTime LastComCheck;
        public string TempDir;
        public string SourcePath;
        public string Subject;
        public int CurrentSlide;
        public int NextFrameIndex = 1;
        public int CaptureFailures;
        public bool Finalized;
        public bool PdfCreated;
        public int PdfPages;
        public string TargetDir;
        public string LastError;
        public string FinalReason;
        public DateTime EndTime;
        public readonly List<CapturedFrame> Frames = new List<CapturedFrame>();
        public byte[] LastFingerprint;
        public readonly AppConfig Config;


        public CaptureSession(AppConfig config, MonitoredWindow window)
        {
            Config = config;
            ProcessId = window.ProcessId;
            ProcessName = window.ProcessName;
            WindowHandle = window.Handle;
            Title = window.Title;
            StartTime = DateTime.Now;
            LastSeenTime = StartTime;
            LastCaptureTime = DateTime.MinValue;
            LastComCheck = DateTime.MinValue;
            Subject = SubjectClassifier.Classify(config, Title, null);
            TempDir = Path.Combine(AppPaths.TempRoot,
                "S_" + StartTime.ToString("yyyyMMdd_HHmmss") + "_" + ProcessId.ToString());
            try
            {
                Directory.CreateDirectory(TempDir);
            }
            catch (Exception ex)
            {
                LastError = "无法创建临时目录: " + ex.Message;
                Logger.Error(LastError, ex);
            }
        }

        public void Touch(MonitoredWindow window)
        {
            WindowHandle = window.Handle;
            if (!string.IsNullOrEmpty(window.Title) && window.Title != Title)
            {
                Title = window.Title;
            }
            LastSeenTime = DateTime.Now;
        }

        public int FrameCount
        {
            get
            {
                lock (Sync) return Frames.Count;
            }
        }

        public TimeSpan Duration
        {
            get
            {
                DateTime end = Finalized ? EndTime : DateTime.Now;
                return end - StartTime;
            }
        }

        public List<CapturedFrame> SnapshotFrames()
        {
            lock (Sync) return new List<CapturedFrame>(Frames);
        }

        public void AddFrame(CapturedFrame frame)
        {
            lock (Sync)
            {
                Frames.Add(frame);
                LastCaptureTime = frame.Time;
            }
        }

        public void CompactIfNeeded()
        {
            lock (Sync)
            {
                if (Frames.Count < Config.MaxFrames) return;
                int target = Math.Max(80, Config.MaxFrames / 2);
                if (Frames.Count <= target) return;

                List<CapturedFrame> keep = new List<CapturedFrame>();
                HashSet<CapturedFrame> key = new HashSet<CapturedFrame>();
                for (int i = 0; i < Frames.Count; i++)
                {
                    if (i == 0 || i == Frames.Count - 1 || Frames[i].KeyFrame)
                    {
                        keep.Add(Frames[i]);
                        key.Add(Frames[i]);
                    }
                }

                int remaining = target - keep.Count;
                if (remaining > 0)
                {
                    int total = Frames.Count;
                    for (int i = 0; i < remaining; i++)
                    {
                        int index = (int)Math.Round((i + 1) * (total - 1.0) / (remaining + 1.0));
                        if (index <= 0) index = 1;
                        if (index >= total - 1) index = total - 2;
                        CapturedFrame f = Frames[index];
                        if (!key.Contains(f))
                        {
                            keep.Add(f);
                            key.Add(f);
                        }
                    }
                }

                if (keep.Count == 0) return;
                keep.Sort(delegate(CapturedFrame a, CapturedFrame b) { return a.Time.CompareTo(b.Time); });

                HashSet<string> keepPaths = new HashSet<string>();
                foreach (CapturedFrame f in keep) keepPaths.Add(f.Path);
                foreach (CapturedFrame f in Frames)
                {
                    if (!keepPaths.Contains(f.Path))
                    {
                        try { File.Delete(f.Path); } catch { }
                    }
                }
                Frames.Clear();
                Frames.AddRange(keep);
                Logger.Info("关键画面过多，已压缩到 " + Frames.Count + " 张，继续记录");
            }
        }

        public void CleanupTemp()
        {
            try
            {
                if (!string.IsNullOrEmpty(TempDir) && Directory.Exists(TempDir))
                {
                    Directory.Delete(TempDir, true);
                }
            }
            catch { }
        }
    }

    public sealed class SessionInfo
    {
        public string Title;
        public string ProcessName;
        public DateTime StartTime;
        public int FrameCount;
        public string Subject;
        public double Minutes;
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CourseCapture
{
    public static class FileUtil
    {
        private static readonly char[] InvalidChars = Path.GetInvalidFileNameChars();

        public static string CleanTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return "课堂";
            string t = title.Trim();
            t = t.TrimStart('*', ' ');
            string[] suffixes = new string[]
            {
                " - PowerPoint", " - Microsoft PowerPoint", " - WPS 演示", " - WPS演示",
                " - WPS Office", " - 演示文稿", " - 兼容模式", "[兼容模式]",
                " – PowerPoint", " — PowerPoint", " - PowerPoint Slide Show"
            };
            foreach (string suffix in suffixes)
            {
                if (t.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    t = t.Substring(0, t.Length - suffix.Length).Trim();
                }
            }
            if (t.StartsWith("幻灯片放映 - ", StringComparison.Ordinal))
            {
                t = t.Substring("幻灯片放映 - ".Length).Trim();
            }
            if (t.StartsWith("幻灯片放映-", StringComparison.Ordinal))
            {
                t = t.Substring("幻灯片放映-".Length).Trim();
            }
            if (t.Length == 0) t = "课堂";
            return t;
        }

        public static string BaseNameWithoutExtension(string title)
        {
            string t = CleanTitle(title);
            try
            {
                string ext = Path.GetExtension(t);
                if (ext.Equals(".ppt", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".pptx", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".pps", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".ppsx", StringComparison.OrdinalIgnoreCase))
                {
                    t = Path.GetFileNameWithoutExtension(t);
                }
            }
            catch { }
            return t;
        }

        public static string SanitizeFileName(string name, int maxLength)
        {
            if (string.IsNullOrEmpty(name)) name = "课堂";
            StringBuilder sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                if (Array.IndexOf(InvalidChars, c) >= 0 || c < 32) sb.Append('_');
                else sb.Append(c);
            }
            string s = sb.ToString().Trim().TrimEnd('.');
            s = s.Replace("  ", " ");
            if (s.Length == 0) s = "课堂";
            if (maxLength > 0 && s.Length > maxLength) s = s.Substring(0, maxLength).Trim();
            string upper = s.ToUpperInvariant();
            string[] reserved = new string[]
            {
                "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5",
                "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4",
                "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
            };
            if (Array.IndexOf(reserved, upper) >= 0) s = "_" + s;
            return s;
        }

        public static string UniqueDirectory(string parent, string name)
        {
            string safe = SanitizeFileName(name, 90);
            string path = Path.Combine(parent, safe);
            if (!Directory.Exists(path) && !File.Exists(path)) return path;
            for (int i = 2; i < 1000; i++)
            {
                path = Path.Combine(parent, safe + "_" + i.ToString(CultureInfo.InvariantCulture));
                if (!Directory.Exists(path) && !File.Exists(path)) return path;
            }
            return Path.Combine(parent, safe + "_" + DateTime.Now.ToString("HHmmssfff"));
        }

        public static void CopyFileShared(string source, string target)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            using (FileStream input = new FileStream(source, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            using (FileStream output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                input.CopyTo(output, 1024 * 128);
            }
            try
            {
                File.SetAttributes(target, FileAttributes.Normal);
            }
            catch { }
        }

        public static void WriteText(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text, new UTF8Encoding(true));
        }

        public static string FormatDuration(TimeSpan span)
        {
            if (span.TotalHours >= 1)
            {
                return string.Format("{0:0}小时{1:00}分{2:00}秒", Math.Floor(span.TotalHours), span.Minutes, span.Seconds);
            }
            return string.Format("{0:0}分{1:00}秒", Math.Floor(span.TotalMinutes), span.Seconds);
        }

        public static string FormatSize(long bytes)
        {
            if (bytes >= 1024L * 1024L * 1024L) return (bytes / 1024.0 / 1024.0 / 1024.0).ToString("0.0") + "GB";
            if (bytes >= 1024L * 1024L) return (bytes / 1024.0 / 1024.0).ToString("0.0") + "MB";
            if (bytes >= 1024L) return (bytes / 1024.0).ToString("0") + "KB";
            return bytes.ToString(CultureInfo.InvariantCulture) + "B";
        }

        public static bool IsPowerPointFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string ext = Path.GetExtension(path);
            return ext.Equals(".ppt", StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals(".pptx", StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals(".pps", StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals(".ppsx", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsProcessAlive(uint pid)
        {
            if (pid == 0) return false;
            try
            {
                using (System.Diagnostics.Process p = System.Diagnostics.Process.GetProcessById((int)pid))
                {
                    return !p.HasExited;
                }
            }
            catch
            {
                return false;
            }
        }

        public static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string normalized = text.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder(normalized.Length);
            foreach (char c in normalized)
            {
                UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(c);
                if (uc != UnicodeCategory.NonSpacingMark) sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}

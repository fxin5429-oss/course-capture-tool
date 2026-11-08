using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace CourseCapture
{
    public sealed class PowerPointLocator
    {
        private sealed class CacheEntry
        {
            public string Path;
            public DateTime Time;
        }

        private readonly AppConfig _config;
        private readonly Dictionary<string, CacheEntry> _cache = new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly object _sync = new object();

        public PowerPointLocator(AppConfig config)
        {
            _config = config;
        }

        public string Find(string title, string knownPath)
        {
            if (!string.IsNullOrEmpty(knownPath) && File.Exists(knownPath) && FileUtil.IsPowerPointFile(knownPath))
            {
                return knownPath;
            }

            string key = (title ?? "").Trim();
            if (key.Length < 2) return null;

            lock (_sync)
            {
                CacheEntry entry;
                if (_cache.TryGetValue(key, out entry))
                {
                    if (entry.Path != null && File.Exists(entry.Path)) return entry.Path;
                    if (entry.Path == null && (DateTime.Now - entry.Time).TotalMinutes < 5) return null;
                }
            }

            string result = Scan(key);
            lock (_sync)
            {
                CacheEntry entry = new CacheEntry();
                entry.Path = result;
                entry.Time = DateTime.Now;
                _cache[key] = entry;
            }
            return result;
        }

        private string Scan(string title)
        {
            string baseName = FileUtil.BaseNameWithoutExtension(title);
            string normalizedTitle = Normalize(baseName);
            if (normalizedTitle.Length < 2) return null;

            string bestPath = null;
            int bestScore = 0;
            int scannedFiles = 0;
            Stopwatch watch = Stopwatch.StartNew();
            Queue<KeyValuePair<string, int>> queue = new Queue<KeyValuePair<string, int>>();

            foreach (string rootRaw in _config.SearchRoots)
            {
                if (string.IsNullOrEmpty(rootRaw)) continue;
                string root;
                try
                {
                    root = Path.GetFullPath(Environment.ExpandEnvironmentVariables(rootRaw.Trim().Trim('"')));
                }
                catch { continue; }
                if (!Directory.Exists(root)) continue;
                if (IsExcludedDirectory(root, true)) continue;
                queue.Enqueue(new KeyValuePair<string, int>(root, 0));
            }

            while (queue.Count > 0)
            {
                if (watch.ElapsedMilliseconds > 8000 || scannedFiles > 40000)
                {
                    Logger.Warn("查找课件源文件超时，已扫描 " + scannedFiles.ToString(CultureInfo.InvariantCulture) + " 个文件");
                    break;
                }

                KeyValuePair<string, int> item = queue.Dequeue();
                string dir = item.Key;
                int depth = item.Value;
                string[] files;
                try
                {
                    files = Directory.GetFiles(dir, "*.ppt*");
                }
                catch { files = new string[0]; }

                foreach (string file in files)
                {
                    scannedFiles++;
                    if (file.IndexOf("~$", StringComparison.Ordinal) >= 0) continue;
                    if (!FileUtil.IsPowerPointFile(file)) continue;
                    int score = Score(file, baseName, normalizedTitle);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPath = file;
                    }
                }

                if (depth >= 4) continue;
                string[] dirs;
                try { dirs = Directory.GetDirectories(dir); }
                catch { dirs = new string[0]; }
                foreach (string sub in dirs)
                {
                    if (IsExcludedDirectory(sub, false)) continue;
                    queue.Enqueue(new KeyValuePair<string, int>(sub, depth + 1));
                }
            }

            if (bestScore >= 650)
            {
                Logger.Info("找到课件源文件: " + bestPath + " (匹配分 " + bestScore.ToString(CultureInfo.InvariantCulture) + ")");
                return bestPath;
            }
            Logger.Info("未找到与标题匹配的课件源文件: " + title);
            return null;
        }

        private static int Score(string file, string baseName, string normalizedTitle)
        {
            string fileBase = Path.GetFileNameWithoutExtension(file);
            string normalizedFile = Normalize(fileBase);
            if (normalizedFile.Length == 0) return 0;
            int score = 0;

            if (string.Equals(normalizedFile, normalizedTitle, StringComparison.OrdinalIgnoreCase)) score = 1000;
            else if (normalizedFile.StartsWith(normalizedTitle, StringComparison.OrdinalIgnoreCase) ||
                     normalizedTitle.StartsWith(normalizedFile, StringComparison.OrdinalIgnoreCase)) score = 820;
            else if (normalizedFile.IndexOf(normalizedTitle, StringComparison.OrdinalIgnoreCase) >= 0 ||
                     normalizedTitle.IndexOf(normalizedFile, StringComparison.OrdinalIgnoreCase) >= 0) score = 720;
            else
            {
                int overlap = TokenOverlap(normalizedFile, normalizedTitle);
                if (overlap >= 2) score = 600 + Math.Min(overlap * 10, 80);
                else if (overlap == 1 && normalizedTitle.Length >= 4) score = 560;
            }

            if (score == 0) return 0;
            try
            {
                double hours = (DateTime.Now - File.GetLastWriteTime(file)).TotalHours;
                if (hours <= 2) score += 80;
                else if (hours <= 24) score += 50;
                else if (hours <= 24 * 7) score += 20;
            }
            catch { }
            return score;
        }

        private static int TokenOverlap(string a, string b)
        {
            HashSet<string> parts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string p in Split(a)) if (p.Length >= 2) parts.Add(p);
            int count = 0;
            foreach (string p in Split(b))
            {
                if (p.Length < 2) continue;
                if (parts.Contains(p)) count++;
            }
            return count;
        }

        private static IEnumerable<string> Split(string text)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else
                {
                    if (sb.Length > 0) { yield return sb.ToString(); sb.Length = 0; }
                }
            }
            if (sb.Length > 0) yield return sb.ToString();
        }

        private static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string s = FileUtil.RemoveDiacritics(text).ToLowerInvariant();
            StringBuilder sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            }
            string result = sb.ToString();
            string[] noise = new string[] { "副本", "copy", "最终版", "修改版", "定稿", "课件" };
            foreach (string n in noise)
            {
                if (result.EndsWith(n, StringComparison.OrdinalIgnoreCase) && result.Length > n.Length + 2)
                {
                    result = result.Substring(0, result.Length - n.Length);
                }
            }
            return result;
        }

        private bool IsExcludedDirectory(string path, bool isRoot)
        {
            try
            {
                string full = Path.GetFullPath(path).TrimEnd('\\');
                if (string.Equals(full, AppPaths.BaseDir, StringComparison.OrdinalIgnoreCase)) return true;
                string saveRoot = Path.GetFullPath(_config.SaveRoot).TrimEnd('\\');
                if (string.Equals(full, saveRoot, StringComparison.OrdinalIgnoreCase)) return true;
                if (!isRoot && full.StartsWith(saveRoot + "\\", StringComparison.OrdinalIgnoreCase)) return true;

                string name = Path.GetFileName(full).ToLowerInvariant();
                string[] blocked = new string[]
                {
                    "$recycle.bin", "system volume information", "windows", "program files",
                    "program files (x86)", "programdata", "appdata", "node_modules", ".git",
                    "临时", "temp", "tmp", "cache", "缓存", "课件捕手"
                };
                foreach (string b in blocked)
                {
                    if (name == b) return true;
                }
                if (full.Length <= 3) return false;
                return false;
            }
            catch
            {
                return true;
            }
        }
    }
}

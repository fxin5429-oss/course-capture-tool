using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace CourseCapture
{
    public sealed class SubjectRule
    {
        public string Name;
        public List<string> Keywords;

        public SubjectRule()
        {
            Name = "";
            Keywords = new List<string>();
        }

        public SubjectRule(string name, params string[] keywords)
        {
            Name = name;
            Keywords = new List<string>();
            if (keywords != null)
            {
                foreach (string k in keywords)
                {
                    if (!string.IsNullOrEmpty(k)) Keywords.Add(k);
                }
            }
        }

        public string KeywordText
        {
            get { return string.Join("，", Keywords.ToArray()); }
        }

        public SubjectRule Copy()
        {
            SubjectRule r = new SubjectRule();
            r.Name = Name;
            r.Keywords = new List<string>(Keywords);
            return r;
        }
    }

    public sealed class AppConfig
    {
        public string SaveRoot;
        public double CaptureIntervalSeconds;
        public int IdleFinalizeMinutes;
        public int MaxFrames;
        public bool EnablePdf;
        public bool KeepTimelinePdf;
        public bool EnableCom;
        public bool CaptureOnlyForeground;
        public List<string> MonitoredProcesses;
        public List<SubjectRule> Subjects;
        public List<string> SearchRoots;

        public static string ConfigPath
        {
            get { return Path.Combine(AppPaths.BaseDir, "配置.xml"); }
        }

        public AppConfig()
        {
            SaveRoot = DefaultSaveRoot();
            CaptureIntervalSeconds = 1.5;
            IdleFinalizeMinutes = 5;
            MaxFrames = 600;
            EnablePdf = true;
            KeepTimelinePdf = false;
            EnableCom = true;
            CaptureOnlyForeground = true;
            MonitoredProcesses = new List<string>();
            Subjects = new List<SubjectRule>();
            SearchRoots = new List<string>();
            SetDefaults();
        }

        public static string DefaultSaveRoot()
        {
            string baseDir = AppPaths.BaseDir;
            string root = Path.GetPathRoot(baseDir);
            bool onSystemDrive = !string.IsNullOrEmpty(root) &&
                string.Equals(root.TrimEnd('\\'), Path.GetPathRoot(Environment.SystemDirectory).TrimEnd('\\'),
                    StringComparison.OrdinalIgnoreCase);
            if (onSystemDrive && Directory.Exists("D:\\"))
            {
                return @"D:\课件捕手\课件";
            }
            return Path.Combine(baseDir, "课件");
        }

        private void SetDefaults()
        {
            if (MonitoredProcesses.Count == 0)
            {
                MonitoredProcesses.AddRange(new string[]
                {
                    "POWERPNT", "wpp", "EasiNote5", "EasiNote",
                    "SeewoWhiteboard", "HiteVision", "HiteBoard"
                });
            }

            if (SearchRoots.Count == 0)
            {
                SearchRoots.Add("%USERPROFILE%\\Desktop");
                SearchRoots.Add("%USERPROFILE%\\Documents");
                SearchRoots.Add("%USERPROFILE%\\Downloads");
                SearchRoots.Add("D:\\课件");
                SearchRoots.Add("D:\\教学");
                SearchRoots.Add("D:\\课程");
                SearchRoots.Add("D:\\教案");
                SearchRoots.Add("D:\\资料");
                SearchRoots.Add("D:\\");
            }

            if (Subjects.Count == 0)
            {
                Subjects.Add(new SubjectRule("语文", "语文", "阅读", "作文", "古诗", "文言文", "红楼梦", "乡土中国", "chinese"));
                Subjects.Add(new SubjectRule("数学", "数学", "函数", "几何", "方程", "不等式", "概率", "统计", "向量", "导数", "数列", "三角函数", "math"));
                Subjects.Add(new SubjectRule("英语", "英语", "english", "grammar", "reading", "语法", "单词", "unit"));
                Subjects.Add(new SubjectRule("物理", "物理", "力学", "电磁", "电路", "运动", "牛顿", "physics"));
                Subjects.Add(new SubjectRule("化学", "化学", "元素", "化合物", "反应", "方程式", "有机", "chemistry"));
                Subjects.Add(new SubjectRule("生物", "生物", "细胞", "遗传", "生态", "光合作用", "biology"));
                Subjects.Add(new SubjectRule("历史", "历史", "朝代", "战争", "革命", "古代史", "近代史", "history"));
                Subjects.Add(new SubjectRule("地理", "地理", "地形", "气候", "人口", "区域", "geography"));
                Subjects.Add(new SubjectRule("政治", "政治", "道德与法治", "法治", "经济", "哲学", "文化"));
                Subjects.Add(new SubjectRule("信息技术", "信息技术", "编程", "算法", "python", "计算机"));
                Subjects.Add(new SubjectRule("音乐", "音乐", "乐理", "鉴赏", "music"));
                Subjects.Add(new SubjectRule("美术", "美术", "绘画", "设计", "art"));
                Subjects.Add(new SubjectRule("体育", "体育", "运动", "篮球", "足球", "体育与健康"));
            }
        }

        public static AppConfig Load()
        {
            AppConfig cfg = new AppConfig();
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    cfg.Save();
                    return cfg;
                }

                XmlDocument doc = new XmlDocument();
                doc.Load(ConfigPath);
                XmlElement root = doc.DocumentElement;
                if (root == null || root.Name != "CourseCapture")
                {
                    throw new InvalidDataException("配置文件根节点不正确");
                }

                cfg.SaveRoot = GetString(root, "SaveRoot", cfg.SaveRoot);
                cfg.CaptureIntervalSeconds = GetDouble(root, "CaptureIntervalSeconds", cfg.CaptureIntervalSeconds);
                cfg.IdleFinalizeMinutes = GetInt(root, "IdleFinalizeMinutes", cfg.IdleFinalizeMinutes);
                cfg.MaxFrames = GetInt(root, "MaxFrames", cfg.MaxFrames);
                cfg.EnablePdf = GetBool(root, "EnablePdf", cfg.EnablePdf);
                cfg.KeepTimelinePdf = GetBool(root, "KeepTimelinePdf", cfg.KeepTimelinePdf);
                cfg.EnableCom = GetBool(root, "EnableCom", cfg.EnableCom);
                cfg.CaptureOnlyForeground = GetBool(root, "CaptureOnlyForeground", cfg.CaptureOnlyForeground);

                List<string> procs = GetList(root, "MonitoredProcesses", "Process");
                if (procs.Count > 0) cfg.MonitoredProcesses = procs;

                List<string> roots = GetList(root, "SearchRoots", "Root");
                if (roots.Count > 0) cfg.SearchRoots = roots;

                List<SubjectRule> subjects = new List<SubjectRule>();
                XmlNode subjectsNode = root.SelectSingleNode("Subjects");
                if (subjectsNode != null)
                {
                    foreach (XmlNode n in subjectsNode.SelectNodes("Subject"))
                    {
                        XmlElement e = n as XmlElement;
                        if (e == null) continue;
                        string name = (e.GetAttribute("name") ?? "").Trim();
                        if (name.Length == 0) continue;
                        SubjectRule rule = new SubjectRule();
                        rule.Name = name;
                        string keywords = e.GetAttribute("keywords") ?? "";
                        foreach (string k in SplitKeywords(keywords))
                        {
                            rule.Keywords.Add(k);
                        }
                        subjects.Add(rule);
                    }
                }
                if (subjects.Count > 0) cfg.Subjects = subjects;

                cfg.Normalize();
                return cfg;
            }
            catch (Exception ex)
            {
                Logger.Error("读取配置失败，使用默认配置: " + ex.Message);
                try
                {
                    if (File.Exists(ConfigPath))
                    {
                        File.Copy(ConfigPath, ConfigPath + ".bad", true);
                    }
                }
                catch { }
                cfg.Normalize();
                try { cfg.Save(); } catch { }
                return cfg;
            }
        }

        public void Normalize()
        {
            if (string.IsNullOrEmpty(SaveRoot)) SaveRoot = DefaultSaveRoot();
            try
            {
                SaveRoot = Path.GetFullPath(Environment.ExpandEnvironmentVariables(SaveRoot.Trim().Trim('"')));
            }
            catch { SaveRoot = DefaultSaveRoot(); }

            if (CaptureIntervalSeconds < 0.5) CaptureIntervalSeconds = 0.5;
            if (CaptureIntervalSeconds > 15) CaptureIntervalSeconds = 15;
            if (IdleFinalizeMinutes < 1) IdleFinalizeMinutes = 1;
            if (IdleFinalizeMinutes > 60) IdleFinalizeMinutes = 60;
            if (MaxFrames < 30) MaxFrames = 30;
            if (MaxFrames > 3000) MaxFrames = 3000;

            if (Subjects.Count == 0)
            {
                Subjects.Clear();
                SetDefaults();
            }
            if (MonitoredProcesses.Count == 0)
            {
                MonitoredProcesses.AddRange(new string[] { "POWERPNT", "wpp", "EasiNote5", "EasiNote" });
            }
            if (SearchRoots.Count == 0)
            {
                SearchRoots.Add("%USERPROFILE%\\Desktop");
                SearchRoots.Add("%USERPROFILE%\\Documents");
                SearchRoots.Add("D:\\");
            }
        }

        public void Save()
        {
            try
            {
                Normalize();
                Directory.CreateDirectory(AppPaths.BaseDir);
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Indent = true;
                settings.IndentChars = "  ";
                settings.Encoding = new UTF8Encoding(false);
                settings.NewLineChars = "\r\n";

                string tmp = ConfigPath + ".tmp";
                using (XmlWriter w = XmlWriter.Create(tmp, settings))
                {
                    w.WriteStartDocument();
                    w.WriteStartElement("CourseCapture");
                    w.WriteAttributeString("version", "1");
                    w.WriteElementString("SaveRoot", SaveRoot);
                    w.WriteElementString("CaptureIntervalSeconds", CaptureIntervalSeconds.ToString("0.##", CultureInfo.InvariantCulture));
                    w.WriteElementString("IdleFinalizeMinutes", IdleFinalizeMinutes.ToString(CultureInfo.InvariantCulture));
                    w.WriteElementString("MaxFrames", MaxFrames.ToString(CultureInfo.InvariantCulture));
                    w.WriteElementString("EnablePdf", EnablePdf ? "true" : "false");
                    w.WriteElementString("KeepTimelinePdf", KeepTimelinePdf ? "true" : "false");
                    w.WriteElementString("EnableCom", EnableCom ? "true" : "false");
                    w.WriteElementString("CaptureOnlyForeground", CaptureOnlyForeground ? "true" : "false");

                    w.WriteStartElement("MonitoredProcesses");
                    foreach (string p in MonitoredProcesses)
                    {
                        if (!string.IsNullOrEmpty(p)) w.WriteElementString("Process", p);
                    }
                    w.WriteEndElement();

                    w.WriteStartElement("Subjects");
                    foreach (SubjectRule r in Subjects)
                    {
                        if (r == null || string.IsNullOrEmpty(r.Name)) continue;
                        w.WriteStartElement("Subject");
                        w.WriteAttributeString("name", r.Name);
                        w.WriteAttributeString("keywords", string.Join(",", r.Keywords.ToArray()));
                        w.WriteEndElement();
                    }
                    w.WriteEndElement();

                    w.WriteStartElement("SearchRoots");
                    foreach (string r in SearchRoots)
                    {
                        if (!string.IsNullOrEmpty(r)) w.WriteElementString("Root", r);
                    }
                    w.WriteEndElement();

                    w.WriteEndElement();
                    w.WriteEndDocument();
                }

                if (File.Exists(ConfigPath))
                {
                    try { File.Copy(ConfigPath, ConfigPath + ".bak", true); }
                    catch { }
                }
                if (File.Exists(ConfigPath)) File.Delete(ConfigPath);
                File.Move(tmp, ConfigPath);
            }
            catch (Exception ex)
            {
                Logger.Error("保存配置失败: " + ex.Message);
                throw;
            }
        }

        public AppConfig Copy()
        {
            AppConfig c = new AppConfig();
            c.SaveRoot = SaveRoot;
            c.CaptureIntervalSeconds = CaptureIntervalSeconds;
            c.IdleFinalizeMinutes = IdleFinalizeMinutes;
            c.MaxFrames = MaxFrames;
            c.EnablePdf = EnablePdf;
            c.KeepTimelinePdf = KeepTimelinePdf;
            c.EnableCom = EnableCom;
            c.CaptureOnlyForeground = CaptureOnlyForeground;
            c.MonitoredProcesses = new List<string>(MonitoredProcesses);
            c.SearchRoots = new List<string>(SearchRoots);
            c.Subjects = new List<SubjectRule>();
            foreach (SubjectRule r in Subjects) c.Subjects.Add(r.Copy());
            return c;
        }

        public static IEnumerable<string> SplitKeywords(string text)
        {
            if (string.IsNullOrEmpty(text)) yield break;
            string[] parts = text.Split(new char[] { ',', '，', ';', '；', '|' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string p in parts)
            {
                string k = p.Trim();
                if (k.Length > 0) yield return k;
            }
        }

        private static string GetString(XmlElement root, string name, string fallback)
        {
            XmlNode n = root.SelectSingleNode(name);
            if (n == null || string.IsNullOrEmpty(n.InnerText)) return fallback;
            return n.InnerText.Trim();
        }

        private static int GetInt(XmlElement root, string name, int fallback)
        {
            int v;
            if (int.TryParse(GetString(root, name, ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) return v;
            return fallback;
        }

        private static double GetDouble(XmlElement root, string name, double fallback)
        {
            double v;
            if (double.TryParse(GetString(root, name, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v;
            return fallback;
        }

        private static bool GetBool(XmlElement root, string name, bool fallback)
        {
            string s = GetString(root, name, "");
            if (s.Length == 0) return fallback;
            return string.Equals(s, "true", StringComparison.OrdinalIgnoreCase) || s == "1" || s == "是";
        }

        private static List<string> GetList(XmlElement root, string container, string item)
        {
            List<string> result = new List<string>();
            XmlNode node = root.SelectSingleNode(container);
            if (node == null) return result;
            foreach (XmlNode n in node.SelectNodes(item))
            {
                string s = (n.InnerText ?? "").Trim();
                if (s.Length > 0) result.Add(s);
            }
            return result;
        }
    }

    public static class AppPaths
    {
        public static string BaseDir
        {
            get { return AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'); }
        }

        public static string ExePath
        {
            get { return Path.Combine(BaseDir, "课件捕手.exe"); }
        }

        public static string LogDir
        {
            get { return Path.Combine(BaseDir, "日志"); }
        }

        public static string TempRoot
        {
            get { return Path.Combine(BaseDir, "临时"); }
        }

        public static string Version
        {
            get { return "1.0.1"; }
        }
    }
}

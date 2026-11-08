using System;
using System.Collections.Generic;

namespace CourseCapture
{
    public static class SubjectClassifier
    {
        public const string Uncategorized = "未分类";

        public static string Classify(AppConfig config, string title, string sourcePath)
        {
            if (config == null) return Uncategorized;
            string t = (title ?? "").ToLowerInvariant();
            string p = (sourcePath ?? "").ToLowerInvariant();
            string best = Uncategorized;
            int bestScore = 0;

            foreach (SubjectRule rule in config.Subjects)
            {
                if (rule == null || string.IsNullOrEmpty(rule.Name)) continue;
                string subject = rule.Name.Trim();
                if (subject == Uncategorized) continue;
                int ruleScore = 0;

                string lowerSubject = subject.ToLowerInvariant();
                if (t.IndexOf(lowerSubject, StringComparison.Ordinal) >= 0) ruleScore += 900 + lowerSubject.Length;
                if (p.IndexOf(lowerSubject, StringComparison.Ordinal) >= 0) ruleScore += 500 + lowerSubject.Length;

                foreach (string keyword in rule.Keywords)
                {
                    if (string.IsNullOrEmpty(keyword)) continue;
                    string k = keyword.Trim().ToLowerInvariant();
                    if (k.Length == 0) continue;
                    if (t.IndexOf(k, StringComparison.Ordinal) >= 0) ruleScore += 100 + k.Length * 3;
                    if (p.IndexOf(k, StringComparison.Ordinal) >= 0) ruleScore += 30 + k.Length;
                }

                if (ruleScore > bestScore)
                {
                    bestScore = ruleScore;
                    best = subject;
                }
            }

            return SanitizeSubject(best);
        }

        public static string SanitizeSubject(string subject)
        {
            if (string.IsNullOrEmpty(subject)) return Uncategorized;
            string s = FileUtil.SanitizeFileName(subject.Trim(), 40);
            return string.IsNullOrEmpty(s) ? Uncategorized : s;
        }
    }
}

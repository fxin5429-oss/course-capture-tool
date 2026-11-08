using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CourseCapture
{
    internal sealed class SettingsForm : Form
    {
        private readonly AppConfig _working;
        private readonly List<SubjectRule> _subjects;

        private TextBox _saveRootBox;
        private NumericUpDown _intervalBox;
        private NumericUpDown _idleBox;
        private NumericUpDown _maxFramesBox;
        private CheckBox _foregroundOnlyBox;
        private CheckBox _enablePdfBox;
        private CheckBox _keepTimelineBox;
        private CheckBox _enableComBox;
        private TextBox _processBox;
        private TextBox _searchBox;
        private ListView _subjectList;

        public AppConfig Result { get; private set; }

        public SettingsForm(AppConfig config)
        {
            _working = config != null ? config.Copy() : new AppConfig();
            _subjects = new List<SubjectRule>();
            foreach (SubjectRule rule in _working.Subjects) _subjects.Add(rule.Copy());

            Text = "课件捕手设置";
            Font = new Font("Microsoft YaHei UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(680, 640);

            BuildInterface();
            LoadValues();
        }

        private void BuildInterface()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 2;
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58f));
            root.Padding = new Padding(0);
            Controls.Add(root);

            TabControl tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Font = Font;
            root.Controls.Add(tabs, 0, 0);

            TabPage basicPage = new TabPage("基本设置");
            basicPage.UseVisualStyleBackColor = true;
            basicPage.Padding = new Padding(0);
            tabs.TabPages.Add(basicPage);
            BuildBasicPage(basicPage);

            TabPage subjectPage = new TabPage("科目分类");
            subjectPage.UseVisualStyleBackColor = true;
            tabs.TabPages.Add(subjectPage);
            BuildSubjectPage(subjectPage);

            TabPage advancedPage = new TabPage("高级 / 兼容性");
            advancedPage.UseVisualStyleBackColor = true;
            tabs.TabPages.Add(advancedPage);
            BuildAdvancedPage(advancedPage);

            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Fill;
            bottom.BackColor = Color.FromArgb(248, 250, 252);
            bottom.Padding = new Padding(12, 10, 12, 10);
            root.Controls.Add(bottom, 0, 1);

            Label saveHint = new Label();
            saveHint.AutoSize = true;
            saveHint.ForeColor = Color.FromArgb(100, 116, 139);
            saveHint.Location = new Point(12, 20);
            saveHint.Text = "配置保存在程序目录的“配置.xml”中。";
            bottom.Controls.Add(saveHint);

            Button cancelButton = new Button();
            cancelButton.Text = "取消";
            cancelButton.Size = new Size(90, 30);
            cancelButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            cancelButton.Location = new Point(bottom.Width - 102, 14);
            cancelButton.DialogResult = DialogResult.Cancel;
            bottom.Controls.Add(cancelButton);

            Button saveButton = new Button();
            saveButton.Text = "保存";
            saveButton.Size = new Size(90, 30);
            saveButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            saveButton.Location = new Point(bottom.Width - 202, 14);
            saveButton.Click += delegate { SaveAndClose(); };
            bottom.Controls.Add(saveButton);

            AcceptButton = saveButton;
            CancelButton = cancelButton;
        }

        private void BuildBasicPage(TabPage page)
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.AutoScroll = false;
            panel.Padding = new Padding(12);
            page.Controls.Add(panel);

            GroupBox locationGroup = MakeGroup("保存位置", 10, 112);
            panel.Controls.Add(locationGroup);

            Label saveLabel = MakeLabel("课件保存到：", 16, 28, 90);
            locationGroup.Controls.Add(saveLabel);

            _saveRootBox = new TextBox();
            _saveRootBox.Location = new Point(108, 25);
            _saveRootBox.Size = new Size(420, 25);
            _saveRootBox.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            locationGroup.Controls.Add(_saveRootBox);

            Button browseButton = new Button();
            browseButton.Text = "浏览...";
            browseButton.Size = new Size(78, 26);
            browseButton.Location = new Point(540, 24);
            browseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            browseButton.Click += delegate { BrowseSaveRoot(); };
            locationGroup.Controls.Add(browseButton);
            browseButton.BringToFront();

            Label pathHint = MakeLabel("每节课保存为：保存位置\\科目\\日期\\时间_课件名\\", 18, 62, 600);
            pathHint.ForeColor = Color.FromArgb(100, 116, 139);
            locationGroup.Controls.Add(pathHint);

            Label warning = MakeLabel("建议放在空间充足的目录，例如 D 盘。", 18, 84, 600);
            warning.ForeColor = Color.FromArgb(180, 83, 9);
            locationGroup.Controls.Add(warning);

            GroupBox captureGroup = MakeGroup("记录方式", 136, 254);
            panel.Controls.Add(captureGroup);

            Label intervalLabel = MakeLabel("截图间隔（秒）：", 18, 31, 125);
            captureGroup.Controls.Add(intervalLabel);
            _intervalBox = new NumericUpDown();
            _intervalBox.Location = new Point(150, 28);
            _intervalBox.Size = new Size(76, 25);
            _intervalBox.DecimalPlaces = 1;
            _intervalBox.Increment = 0.5m;
            _intervalBox.Minimum = 0.5m;
            _intervalBox.Maximum = 15m;
            captureGroup.Controls.Add(_intervalBox);
            Label intervalHint = MakeLabel("默认 1.5 秒；越小越流畅，文件也越大。", 240, 31, 370);
            intervalHint.ForeColor = Color.FromArgb(100, 116, 139);
            captureGroup.Controls.Add(intervalHint);

            Label idleLabel = MakeLabel("无操作自动收尾（分钟）：", 18, 67, 175);
            captureGroup.Controls.Add(idleLabel);
            _idleBox = new NumericUpDown();
            _idleBox.Location = new Point(198, 64);
            _idleBox.Size = new Size(76, 25);
            _idleBox.Minimum = 1;
            _idleBox.Maximum = 60;
            captureGroup.Controls.Add(_idleBox);
            Label idleHint = MakeLabel("老师长时间不翻页时保存，默认 5 分钟。", 288, 67, 330);
            idleHint.ForeColor = Color.FromArgb(100, 116, 139);
            captureGroup.Controls.Add(idleHint);

            Label maxLabel = MakeLabel("单节课最多画面数：", 18, 103, 150);
            captureGroup.Controls.Add(maxLabel);
            _maxFramesBox = new NumericUpDown();
            _maxFramesBox.Location = new Point(172, 100);
            _maxFramesBox.Size = new Size(76, 25);
            _maxFramesBox.Minimum = 30;
            _maxFramesBox.Maximum = 3000;
            _maxFramesBox.ThousandsSeparator = true;
            captureGroup.Controls.Add(_maxFramesBox);
            Label maxHint = MakeLabel("超过后自动抽稀，默认 600。", 262, 103, 350);
            maxHint.ForeColor = Color.FromArgb(100, 116, 139);
            captureGroup.Controls.Add(maxHint);

            _foregroundOnlyBox = new CheckBox();
            _foregroundOnlyBox.Text = "只记录当前前台演示窗口（推荐，避免录到无关内容）";
            _foregroundOnlyBox.Location = new Point(18, 136);
            _foregroundOnlyBox.Size = new Size(590, 24);
            captureGroup.Controls.Add(_foregroundOnlyBox);

            _enablePdfBox = new CheckBox();
            _enablePdfBox.Text = "生成带老师字迹的 PDF 课堂笔记";
            _enablePdfBox.Location = new Point(18, 162);
            _enablePdfBox.Size = new Size(590, 24);
            _enablePdfBox.CheckedChanged += delegate
            {
                _keepTimelineBox.Enabled = _enablePdfBox.Checked;
            };
            captureGroup.Controls.Add(_enablePdfBox);

            _keepTimelineBox = new CheckBox();
            _keepTimelineBox.Text = "同时保留完整时间线 PDF（文件较大，默认关闭）";
            _keepTimelineBox.Location = new Point(38, 188);
            _keepTimelineBox.Size = new Size(570, 24);
            captureGroup.Controls.Add(_keepTimelineBox);

            Label captureHint = MakeLabel(
                "即使老师没有保存 PPT，截图和 PDF 也会保留板书、批注和动画画面。",
                18, 218, 600);
            captureHint.ForeColor = Color.FromArgb(100, 116, 139);
            captureGroup.Controls.Add(captureHint);

            GroupBox runGroup = MakeGroup("运行方式", 404, 104);
            panel.Controls.Add(runGroup);
            Label runLabel1 = MakeLabel("程序常驻托盘，老师打开课件后自动开始，不需要每次手动操作。", 18, 28, 600);
            runGroup.Controls.Add(runLabel1);
            Label runLabel2 = MakeLabel("若开机启动未生效，可以每次开机后手动双击一次程序。", 18, 54, 600);
            runLabel2.ForeColor = Color.FromArgb(180, 83, 9);
            runGroup.Controls.Add(runLabel2);
            Button openRootButton = new Button();
            openRootButton.Text = "打开当前保存文件夹";
            openRootButton.Size = new Size(150, 28);
            openRootButton.Location = new Point(18, 76);
            openRootButton.Click += delegate
            {
                try
                {
                    string root = Path.GetFullPath(Environment.ExpandEnvironmentVariables(_saveRootBox.Text.Trim()));
                    if (!Directory.Exists(root)) Directory.CreateDirectory(root);
                    System.Diagnostics.Process.Start("explorer.exe", "\"" + root + "\"");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("无法打开文件夹：\r\n" + ex.Message, "课件捕手",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            runGroup.Controls.Add(openRootButton);
        }
        private void BuildSubjectPage(TabPage page)
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(12);
            page.Controls.Add(panel);

            Label intro = MakeLabel(
                "标题或文件路径命中关键词后自动归入对应科目；都不命中时放入“未分类”。",
                12, 10, 640);
            intro.ForeColor = Color.FromArgb(71, 85, 105);
            panel.Controls.Add(intro);

            _subjectList = new ListView();
            _subjectList.View = View.Details;
            _subjectList.FullRowSelect = true;
            _subjectList.GridLines = true;
            _subjectList.MultiSelect = false;
            _subjectList.HideSelection = false;
            _subjectList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            _subjectList.Location = new Point(12, 40);
            _subjectList.Size = new Size(640, 390);
            _subjectList.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            _subjectList.Columns.Add("科目", 150);
            _subjectList.Columns.Add("关键词（逗号分隔）", 460);
            _subjectList.SelectedIndexChanged += delegate { UpdateSubjectButtons(); };
            _subjectList.DoubleClick += delegate { EditSubject(); };
            panel.Controls.Add(_subjectList);

            Button addButton = MakeButton("添加", 12, 442);
            addButton.Click += delegate { AddSubject(); };
            panel.Controls.Add(addButton);

            Button editButton = MakeButton("编辑", 104, 442);
            editButton.Click += delegate { EditSubject(); };
            panel.Controls.Add(editButton);

            Button deleteButton = MakeButton("删除", 196, 442);
            deleteButton.Click += delegate { DeleteSubject(); };
            panel.Controls.Add(deleteButton);

            Button resetButton = MakeButton("恢复默认科目", 288, 442);
            resetButton.Width = 120;
            resetButton.Click += delegate { ResetSubjects(); };
            panel.Controls.Add(resetButton);

            Label hint = MakeLabel("双击一行也可以编辑。科目名称会作为文件夹名，不能包含 \\ / : * ? \" < > |。",
                12, 482, 640);
            hint.ForeColor = Color.FromArgb(100, 116, 139);
            panel.Controls.Add(hint);
        }

        private void BuildAdvancedPage(TabPage page)
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.AutoScroll = false;
            panel.Padding = new Padding(12);
            page.Controls.Add(panel);

            GroupBox processGroup = MakeGroup("监控程序（每行一个进程名）", 8, 172);
            panel.Controls.Add(processGroup);

            _processBox = new TextBox();
            _processBox.Multiline = true;
            _processBox.ScrollBars = ScrollBars.Vertical;
            _processBox.Font = new Font("Consolas", 9f);
            _processBox.Location = new Point(16, 25);
            _processBox.Size = new Size(608, 112);
            _processBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            processGroup.Controls.Add(_processBox);

            Label processHint = MakeLabel(
                "POWERPNT=PowerPoint，wpp=WPS 演示，EasiNote5=希沃白板。一般不用修改。",
                16, 144, 608);
            processHint.ForeColor = Color.FromArgb(100, 116, 139);
            processGroup.Controls.Add(processHint);

            GroupBox searchGroup = MakeGroup("课件搜索目录（每行一个）", 188, 210);
            panel.Controls.Add(searchGroup);

            _searchBox = new TextBox();
            _searchBox.Multiline = true;
            _searchBox.ScrollBars = ScrollBars.Vertical;
            _searchBox.Font = new Font("Consolas", 9f);
            _searchBox.Location = new Point(16, 25);
            _searchBox.Size = new Size(608, 150);
            _searchBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            searchGroup.Controls.Add(_searchBox);

            Label searchHint = MakeLabel(
                "老师退出后，程序按标题在这些目录中查找原 PPT；支持 %USERPROFILE% 等环境变量。",
                16, 180, 608);
            searchHint.ForeColor = Color.FromArgb(100, 116, 139);
            searchGroup.Controls.Add(searchHint);

            GroupBox comGroup = MakeGroup("兼容性", 406, 112);
            panel.Controls.Add(comGroup);

            _enableComBox = new CheckBox();
            _enableComBox.Text = "尝试通过 PowerPoint COM 读取原文件路径和幻灯片页码（推荐）";
            _enableComBox.Location = new Point(18, 26);
            _enableComBox.Size = new Size(600, 24);
            comGroup.Controls.Add(_enableComBox);

            Label comHint1 = MakeLabel(
                "如果学校电脑没有 Office 或出现 COM 报错，可以关闭；关闭后仍会截图并按标题搜索原文件。",
                18, 56, 600);
            comHint1.ForeColor = Color.FromArgb(100, 116, 139);
            comGroup.Controls.Add(comHint1);

            Label comHint2 = MakeLabel("高级选项一般无需修改。", 18, 82, 600);
            comHint2.ForeColor = Color.FromArgb(100, 116, 139);
            comGroup.Controls.Add(comHint2);
        }

        private GroupBox MakeGroup(string text, int y, int height)
        {
            GroupBox group = new GroupBox();
            group.Text = text;
            group.Location = new Point(12, y);
            group.Size = new Size(640, height);
            group.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            return group;
        }

        private static Label MakeLabel(string text, int x, int y, int width)
        {
            Label label = new Label();
            label.Text = text;
            label.Location = new Point(x, y);
            label.Size = new Size(width, 22);
            label.AutoSize = false;
            return label;
        }

        private static Button MakeButton(string text, int x, int y)
        {
            Button button = new Button();
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(84, 28);
            return button;
        }

        private void LoadValues()
        {
            _saveRootBox.Text = _working.SaveRoot;
            _intervalBox.Value = Clamp((decimal)_working.CaptureIntervalSeconds, _intervalBox.Minimum, _intervalBox.Maximum);
            _idleBox.Value = Clamp(_working.IdleFinalizeMinutes, (int)_idleBox.Minimum, (int)_idleBox.Maximum);
            _maxFramesBox.Value = Clamp(_working.MaxFrames, (int)_maxFramesBox.Minimum, (int)_maxFramesBox.Maximum);
            _foregroundOnlyBox.Checked = _working.CaptureOnlyForeground;
            _enablePdfBox.Checked = _working.EnablePdf;
            _keepTimelineBox.Checked = _working.KeepTimelinePdf;
            _keepTimelineBox.Enabled = _working.EnablePdf;
            _enableComBox.Checked = _working.EnableCom;
            _processBox.Text = string.Join("\r\n", _working.MonitoredProcesses.ToArray());
            _searchBox.Text = string.Join("\r\n", _working.SearchRoots.ToArray());

            RefreshSubjectList();
        }

        private static decimal Clamp(decimal value, decimal min, decimal max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private void BrowseSaveRoot()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择课件保存文件夹（建议放在 D 盘）";
                dialog.ShowNewFolderButton = true;
                try
                {
                    string current = Path.GetFullPath(Environment.ExpandEnvironmentVariables(_saveRootBox.Text.Trim()));
                    if (Directory.Exists(current)) dialog.SelectedPath = current;
                }
                catch { }
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _saveRootBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void RefreshSubjectList()
        {
            if (_subjectList == null) return;
            _subjectList.BeginUpdate();
            try
            {
                _subjectList.Items.Clear();
                foreach (SubjectRule rule in _subjects)
                {
                    ListViewItem item = new ListViewItem(rule.Name);
                    item.SubItems.Add(rule.KeywordText);
                    item.Tag = rule;
                    _subjectList.Items.Add(item);
                }
            }
            finally
            {
                _subjectList.EndUpdate();
            }
            UpdateSubjectButtons();
        }

        private void UpdateSubjectButtons()
        {
            // 按钮位置固定，按索引查找即可（0 添加、1 编辑、2 删除）。
            if (_subjectList == null || _subjectList.Parent == null) return;
            bool has = _subjectList.SelectedItems.Count > 0;
            foreach (Control control in _subjectList.Parent.Controls)
            {
                Button button = control as Button;
                if (button == null) continue;
                if (button.Text == "编辑" || button.Text == "删除") button.Enabled = has;
            }
        }

        private SubjectRule GetSelectedSubject()
        {
            if (_subjectList.SelectedItems.Count == 0) return null;
            return _subjectList.SelectedItems[0].Tag as SubjectRule;
        }

        private void AddSubject()
        {
            using (SubjectEditDialog dialog = new SubjectEditDialog("", ""))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (FindSubject(dialog.ResultName) != null)
                {
                    MessageBox.Show("已经有同名科目了。", "课件捕手", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                SubjectRule rule = new SubjectRule();
                rule.Name = dialog.ResultName;
                rule.Keywords = ParseKeywords(dialog.ResultKeywords);
                _subjects.Add(rule);
                RefreshSubjectList();
                SelectSubject(rule);
            }
        }

        private void EditSubject()
        {
            SubjectRule selected = GetSelectedSubject();
            if (selected == null) return;
            using (SubjectEditDialog dialog = new SubjectEditDialog(selected.Name, selected.KeywordText))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                SubjectRule same = FindSubject(dialog.ResultName);
                if (same != null && !object.ReferenceEquals(same, selected))
                {
                    MessageBox.Show("已经有同名科目了。", "课件捕手", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                selected.Name = dialog.ResultName;
                selected.Keywords = ParseKeywords(dialog.ResultKeywords);
                RefreshSubjectList();
                SelectSubject(selected);
            }
        }

        private void DeleteSubject()
        {
            SubjectRule selected = GetSelectedSubject();
            if (selected == null) return;
            if (MessageBox.Show("确定删除科目“" + selected.Name + "”吗？\r\n已有的课件文件夹不会被删除。",
                "删除科目", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _subjects.Remove(selected);
            RefreshSubjectList();
        }

        private void ResetSubjects()
        {
            if (MessageBox.Show("恢复为内置的默认科目和关键词？\r\n你自定义的科目会被替换。",
                "恢复默认科目", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            AppConfig defaults = new AppConfig();
            _subjects.Clear();
            foreach (SubjectRule rule in defaults.Subjects) _subjects.Add(rule.Copy());
            RefreshSubjectList();
        }

        private SubjectRule FindSubject(string name)
        {
            foreach (SubjectRule rule in _subjects)
            {
                if (string.Equals(rule.Name, name, StringComparison.OrdinalIgnoreCase)) return rule;
            }
            return null;
        }

        private void SelectSubject(SubjectRule rule)
        {
            foreach (ListViewItem item in _subjectList.Items)
            {
                if (object.ReferenceEquals(item.Tag, rule))
                {
                    item.Selected = true;
                    item.EnsureVisible();
                    break;
                }
            }
        }

        private static List<string> ParseKeywords(string text)
        {
            List<string> result = new List<string>();
            foreach (string keyword in AppConfig.SplitKeywords(text))
            {
                bool exists = false;
                foreach (string existing in result)
                {
                    if (string.Equals(existing, keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists) result.Add(keyword);
            }
            return result;
        }

        private void SaveAndClose()
        {
            string root = _saveRootBox.Text.Trim();
            if (root.Length == 0)
            {
                MessageBox.Show("请填写课件保存位置。", "课件捕手", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _saveRootBox.Focus();
                return;
            }
            try
            {
                root = Path.GetFullPath(Environment.ExpandEnvironmentVariables(root.Trim('"')));
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存位置不是有效路径：\r\n" + ex.Message, "课件捕手",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _saveRootBox.Focus();
                return;
            }

            try
            {
                Directory.CreateDirectory(root);
                string testFile = Path.Combine(root, "课件捕手写入测试_" + Guid.NewGuid().ToString("N") + ".tmp");
                File.WriteAllText(testFile, "test");
                try { File.Delete(testFile); } catch { }
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法写入这个文件夹：\r\n" + root + "\r\n\r\n" + ex.Message,
                    "课件捕手", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _saveRootBox.Focus();
                return;
            }

            List<string> processes = SplitProcesses(_processBox.Text);
            if (processes.Count == 0)
            {
                MessageBox.Show("至少保留一个监控程序（例如 POWERPNT）。", "课件捕手",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            List<string> searchRoots = SplitLines(_searchBox.Text);
            if (searchRoots.Count == 0)
            {
                DialogResult answer = MessageBox.Show(
                    "没有填写课件搜索目录。\r\n\r\n程序仍会尝试通过 PowerPoint COM 获取原文件；" +
                    "如果取不到，可能只能保存截图，找不到原 PPT。\r\n\r\n仍然继续吗？",
                    "没有搜索目录", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (answer != DialogResult.Yes) return;
            }

            if (_subjects.Count == 0)
            {
                MessageBox.Show("至少保留一个科目。", "课件捕手", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _working.SaveRoot = root;
            _working.CaptureIntervalSeconds = (double)_intervalBox.Value;
            _working.IdleFinalizeMinutes = (int)_idleBox.Value;
            _working.MaxFrames = (int)_maxFramesBox.Value;
            _working.CaptureOnlyForeground = _foregroundOnlyBox.Checked;
            _working.EnablePdf = _enablePdfBox.Checked;
            _working.KeepTimelinePdf = _keepTimelineBox.Checked;
            _working.EnableCom = _enableComBox.Checked;
            _working.MonitoredProcesses = processes;
            _working.SearchRoots = searchRoots;
            _working.Subjects = new List<SubjectRule>();
            foreach (SubjectRule rule in _subjects) _working.Subjects.Add(rule.Copy());

            try
            {
                _working.Normalize();
                _working.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存配置失败：\r\n" + ex.Message, "课件捕手",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Result = _working;
            DialogResult = DialogResult.OK;
            Close();
        }

        private static List<string> SplitProcesses(string text)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(text)) return result;
            string[] parts = text.Split(new char[] { '\r', '\n', ',', '，', ';', '；' },
                StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                string value = part.Trim();
                if (value.Length == 0) continue;
                bool exists = false;
                foreach (string existing in result)
                {
                    if (string.Equals(existing, value, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists) result.Add(value);
            }
            return result;
        }

        private static List<string> SplitLines(string text)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(text)) return result;
            string[] parts = text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                string value = part.Trim().Trim('"');
                if (value.Length == 0) continue;
                bool exists = false;
                foreach (string existing in result)
                {
                    if (string.Equals(existing, value, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists) result.Add(value);
            }
            return result;
        }
    }

    internal sealed class SubjectEditDialog : Form
    {
        private readonly TextBox _nameBox;
        private readonly TextBox _keywordBox;

        public string ResultName { get; private set; }
        public string ResultKeywords { get; private set; }

        public SubjectEditDialog(string name, string keywords)
        {
            Text = string.IsNullOrEmpty(name) ? "添加科目" : "编辑科目";
            Font = new Font("Microsoft YaHei UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(476, 250);

            Label nameLabel = new Label();
            nameLabel.Text = "科目名称：";
            nameLabel.Location = new Point(18, 24);
            nameLabel.Size = new Size(90, 22);
            Controls.Add(nameLabel);

            _nameBox = new TextBox();
            _nameBox.Location = new Point(108, 21);
            _nameBox.Size = new Size(340, 25);
            _nameBox.Text = name ?? "";
            Controls.Add(_nameBox);

            Label keywordLabel = new Label();
            keywordLabel.Text = "关键词：";
            keywordLabel.Location = new Point(18, 62);
            keywordLabel.Size = new Size(90, 22);
            Controls.Add(keywordLabel);

            _keywordBox = new TextBox();
            _keywordBox.Location = new Point(108, 59);
            _keywordBox.Size = new Size(340, 90);
            _keywordBox.Multiline = true;
            _keywordBox.ScrollBars = ScrollBars.Vertical;
            _keywordBox.Text = keywords ?? "";
            Controls.Add(_keywordBox);

            Label hint = new Label();
            hint.Text = "关键词用逗号分隔，例如：数学, 函数, 几何, 导数。\r\n命中标题或文件路径即可分类。";
            hint.Location = new Point(108, 154);
            hint.Size = new Size(340, 40);
            hint.ForeColor = Color.FromArgb(100, 116, 139);
            Controls.Add(hint);

            Button cancelButton = new Button();
            cancelButton.Text = "取消";
            cancelButton.Location = new Point(268, 206);
            cancelButton.Size = new Size(84, 28);
            cancelButton.DialogResult = DialogResult.Cancel;
            Controls.Add(cancelButton);

            Button okButton = new Button();
            okButton.Text = "确定";
            okButton.Location = new Point(364, 206);
            okButton.Size = new Size(84, 28);
            okButton.Click += delegate { Confirm(); };
            Controls.Add(okButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        private void Confirm()
        {
            string name = (_nameBox.Text ?? "").Trim();
            if (name.Length == 0)
            {
                MessageBox.Show("请填写科目名称。", "课件捕手", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _nameBox.Focus();
                return;
            }
            if (name.Length > 30)
            {
                MessageBox.Show("科目名称不要超过 30 个字符。", "课件捕手",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _nameBox.Focus();
                return;
            }
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char c in invalid)
            {
                if (name.IndexOf(c) >= 0)
                {
                    MessageBox.Show("科目名称不能包含字符：" + c, "课件捕手",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _nameBox.Focus();
                    return;
                }
            }
            if (name.IndexOf('\\') >= 0 || name.IndexOf('/') >= 0)
            {
                MessageBox.Show("科目名称不能包含斜杠。", "课件捕手",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _nameBox.Focus();
                return;
            }

            List<string> keywords = new List<string>();
            foreach (string keyword in AppConfig.SplitKeywords(_keywordBox.Text))
            {
                bool exists = false;
                foreach (string existing in keywords)
                {
                    if (string.Equals(existing, keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists) keywords.Add(keyword);
            }

            ResultName = name;
            ResultKeywords = string.Join("，", keywords.ToArray());
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}

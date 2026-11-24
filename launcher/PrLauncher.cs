using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PrLauncher
{
    public class ProjectItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public DateTime LastModified { get; set; }
        public long SizeBytes { get; set; }
        public bool Exists { get; set; }
        public bool IsPinned { get; set; }

        public string FormattedSize
        {
            get
            {
                if (SizeBytes <= 0) return "0 Б";
                if (SizeBytes < 1024 * 1024) return string.Format("{0:F1} КБ", SizeBytes / 1024.0);
                return string.Format("{0:F1} МБ", SizeBytes / (1024.0 * 1024.0));
            }
        }

        public string FormattedDate
        {
            get
            {
                var now = DateTime.Now;
                if (LastModified.Date == now.Date)
                    return "Сегодня, " + LastModified.ToString("HH:mm");
                if (LastModified.Date == now.AddDays(-1).Date)
                    return "Вчера, " + LastModified.ToString("HH:mm");
                return LastModified.ToString("dd.MM.yyyy HH:mm");
            }
        }
    }

    public class MainForm : Form
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        private List<ProjectItem> allProjects = new List<ProjectItem>();
        private List<ProjectItem> filteredProjects = new List<ProjectItem>();
        private HashSet<string> pinnedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private string premiereExePath = "";
        private string configFilePath;
        private string activePrefsPath = "";

        // UI элементы
        private Panel headerPanel;
        private Label lblTitle;
        private Label lblSubtitle;
        private Label lblPremiereStatus;
        private Button btnBrowsePr;

        private Panel searchPanel;
        private TextBox txtSearch;
        private Button btnNewProject;
        private Button btnCleanCache;
        private Button btnRefresh;

        private ListView lvProjects;
        private Panel footerPanel;
        private CheckBox chkDisableHome;
        private Label lblStats;
        private Button btnLaunchEmpty;

        private Color cBg = Color.FromArgb(22, 23, 28);
        private Color cCard = Color.FromArgb(30, 32, 39);
        private Color cCardHover = Color.FromArgb(38, 41, 50);
        private Color cLine = Color.FromArgb(51, 55, 68);
        private Color cText = Color.FromArgb(233, 235, 242);
        private Color cDim = Color.FromArgb(139, 144, 163);
        private Color cAccent = Color.FromArgb(139, 92, 246);
        private Color cAccentCyan = Color.FromArgb(34, 211, 238);
        private Color cGreen = Color.FromArgb(52, 211, 153);
        private Color cRed = Color.FromArgb(248, 113, 113);

        public MainForm()
        {
            try { SetProcessDPIAware(); } catch { }

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string cfgDir = Path.Combine(appData, "PrLauncher");
            if (!Directory.Exists(cfgDir)) Directory.CreateDirectory(cfgDir);
            configFilePath = Path.Combine(cfgDir, "config.json");

            InitializeComponent();
            LoadConfig();
            DetectPremiere();
            RefreshProjects();
            CheckStartupTweak();
        }

        private void InitializeComponent()
        {
            this.Text = "PrLauncher — Быстрый запуск Premiere Pro";
            this.Size = new Size(880, 620);
            this.MinimumSize = new Size(680, 480);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = cBg;
            this.ForeColor = cText;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.DoubleBuffered = true;

            // Header
            headerPanel = new Panel { Dock = DockStyle.Top, Height = 74, BackColor = cBg, Padding = new Padding(20, 12, 20, 10) };

            lblTitle = new Label
            {
                Text = "PrLauncher",
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(20, 12)
            };

            lblSubtitle = new Label
            {
                Text = "Мгновенный запуск проектов в обход начального экрана",
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                ForeColor = cDim,
                AutoSize = true,
                Location = new Point(22, 42)
            };

            lblPremiereStatus = new Label
            {
                Text = "Поиск Premiere Pro...",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = cAccentCyan,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(560, 18)
            };

            btnBrowsePr = new Button
            {
                Text = "Выбрать Pr.exe",
                Size = new Size(110, 26),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(730, 15),
                FlatStyle = FlatStyle.Flat,
                BackColor = cCard,
                ForeColor = cDim,
                Cursor = Cursors.Hand
            };
            btnBrowsePr.FlatAppearance.BorderColor = cLine;
            btnBrowsePr.Click += BtnBrowsePr_Click;

            headerPanel.Controls.Add(lblTitle);
            headerPanel.Controls.Add(lblSubtitle);
            headerPanel.Controls.Add(lblPremiereStatus);
            headerPanel.Controls.Add(btnBrowsePr);

            // Search and Toolbar Panel
            searchPanel = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = cBg, Padding = new Padding(20, 4, 20, 10) };

            txtSearch = new TextBox
            {
                Location = new Point(20, 8),
                Size = new Size(380, 30),
                BackColor = cCard,
                ForeColor = cText,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Regular)
            };
            txtSearch.TextChanged += (s, e) => ApplyFilter();

            btnNewProject = CreateStyledButton("+ Новый проект", 130, cAccent, Color.White);
            btnNewProject.Location = new Point(415, 7);
            btnNewProject.Click += BtnNewProject_Click;

            btnCleanCache = CreateStyledButton("Очистить кэш", 120, cCard, cText);
            btnCleanCache.Location = new Point(555, 7);
            btnCleanCache.Click += BtnCleanCache_Click;

            btnRefresh = CreateStyledButton("Обновить", 95, cCard, cText);
            btnRefresh.Location = new Point(685, 7);
            btnRefresh.Click += (s, e) => RefreshProjects();

            searchPanel.Controls.Add(txtSearch);
            searchPanel.Controls.Add(btnNewProject);
            searchPanel.Controls.Add(btnCleanCache);
            searchPanel.Controls.Add(btnRefresh);

            // ListView for Projects (OwnerDrawn for sleek modern look)
            lvProjects = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                OwnerDraw = true,
                BackColor = cBg,
                ForeColor = cText,
                BorderStyle = BorderStyle.None,
                HeaderStyle = ColumnHeaderStyle.None
            };
            lvProjects.Columns.Add("Main", 820);
            lvProjects.DrawItem += LvProjects_DrawItem;
            lvProjects.DoubleClick += LvProjects_DoubleClick;
            lvProjects.KeyDown += LvProjects_KeyDown;
            lvProjects.MouseClick += LvProjects_MouseClick;

            // Footer Panel
            footerPanel = new Panel { Dock = DockStyle.Bottom, Height = 46, BackColor = cCard, Padding = new Padding(20, 10, 20, 10) };

            chkDisableHome = new CheckBox
            {
                Text = "Отключить начальный экран в Premiere Pro",
                AutoSize = true,
                ForeColor = cDim,
                Location = new Point(20, 12),
                Cursor = Cursors.Hand
            };
            chkDisableHome.CheckedChanged += ChkDisableHome_CheckedChanged;

            lblStats = new Label
            {
                Text = "Проектов: 0",
                AutoSize = true,
                ForeColor = cDim,
                Location = new Point(350, 14)
            };

            btnLaunchEmpty = CreateStyledButton("Открыть Premiere Pro", 170, cCard, cText);
            btnLaunchEmpty.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnLaunchEmpty.Location = new Point(670, 8);
            btnLaunchEmpty.Click += (s, e) => LaunchProject(null);

            footerPanel.Controls.Add(chkDisableHome);
            footerPanel.Controls.Add(lblStats);
            footerPanel.Controls.Add(btnLaunchEmpty);

            this.Controls.Add(lvProjects);
            this.Controls.Add(searchPanel);
            this.Controls.Add(headerPanel);
            this.Controls.Add(footerPanel);

            this.Resize += (s, e) =>
            {
                if (lvProjects.Columns.Count > 0)
                    lvProjects.Columns[0].Width = lvProjects.ClientSize.Width - 5;
            };
        }

        private Button CreateStyledButton(string text, int width, Color bg, Color fg)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(width, 32),
                BackColor = bg,
                ForeColor = fg,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = cLine;
            btn.FlatAppearance.BorderSize = 1;
            return btn;
        }

        private void DetectPremiere()
        {
            if (!string.IsNullOrEmpty(premiereExePath) && File.Exists(premiereExePath))
            {
                UpdatePrStatus();
                return;
            }

            // Проверяем реестр App Paths
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\Adobe Premiere Pro.exe"))
                {
                    if (k != null)
                    {
                        var val = k.GetValue("") as string;
                        if (!string.IsNullOrEmpty(val) && File.Exists(val))
                        {
                            premiereExePath = val;
                            UpdatePrStatus();
                            return;
                        }
                    }
                }
            }
            catch { }

            // Проверяем типовые папки в Program Files
            string[] versions = new string[] { "2026", "2025", "2024", "2023", "2022", "2021", "2020", "CC" };
            string[] drives = new string[] { "C:", "D:", "E:" };

            foreach (var d in drives)
            {
                foreach (var v in versions)
                {
                    string candidate = string.Format(@"{0}\Program Files\Adobe\Adobe Premiere Pro {1}\Adobe Premiere Pro.exe", d, v);
                    if (File.Exists(candidate))
                    {
                        premiereExePath = candidate;
                        UpdatePrStatus();
                        return;
                    }
                }
            }

            lblPremiereStatus.Text = "Premiere Pro не найден";
            lblPremiereStatus.ForeColor = cRed;
        }

        private void UpdatePrStatus()
        {
            try
            {
                var vi = FileVersionInfo.GetVersionInfo(premiereExePath);
                string verName = "Adobe Premiere Pro " + vi.ProductVersion;
                if (premiereExePath.Contains("2026")) verName = "Premiere Pro 2026";
                else if (premiereExePath.Contains("2025")) verName = "Premiere Pro 2025";
                else if (premiereExePath.Contains("2024")) verName = "Premiere Pro 2024";

                lblPremiereStatus.Text = verName;
                lblPremiereStatus.ForeColor = cGreen;
                SaveConfig();
            }
            catch
            {
                lblPremiereStatus.Text = "Premiere Pro найден";
                lblPremiereStatus.ForeColor = cGreen;
            }
        }

        private void BtnBrowsePr_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "Adobe Premiere Pro|Adobe Premiere Pro.exe|Исполняемые файлы (*.exe)|*.exe";
                dlg.Title = "Выберите Adobe Premiere Pro.exe";
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    premiereExePath = dlg.FileName;
                    UpdatePrStatus();
                }
            }
        }

        private void RefreshProjects()
        {
            allProjects.Clear();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Поиск в документах пользователя: Documents\Adobe\Premiere Pro\*\Profile-*\Adobe Premiere Pro Prefs
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string prDocs = Path.Combine(docs, "Adobe", "Premiere Pro");

            if (Directory.Exists(prDocs))
            {
                try
                {
                    var prefFiles = Directory.GetFiles(prDocs, "Adobe Premiere Pro Prefs", SearchOption.AllDirectories);
                    Array.Sort(prefFiles, (a, b) => File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a)));

                    if (prefFiles.Length > 0)
                        activePrefsPath = prefFiles[0];

                    foreach (var pref in prefFiles)
                    {
                        ParsePrefsMru(pref, seenPaths);
                    }
                }
                catch { }
            }

            // 2. Сканируем файлы .prproj в папке Documents\Adobe\Premiere Pro
            if (Directory.Exists(prDocs))
            {
                try
                {
                    var directProjects = Directory.GetFiles(prDocs, "*.prproj", SearchOption.TopDirectoryOnly);
                    foreach (var dp in directProjects)
                    {
                        if (seenPaths.Add(dp))
                        {
                            var fi = new FileInfo(dp);
                            allProjects.Add(new ProjectItem
                            {
                                Name = Path.GetFileNameWithoutExtension(dp),
                                Path = dp,
                                LastModified = fi.LastWriteTime,
                                SizeBytes = fi.Length,
                                Exists = true,
                                IsPinned = pinnedPaths.Contains(dp)
                            });
                        }
                    }
                }
                catch { }
            }

            // Сортировка: закрепленные сверху, затем по дате изменения
            allProjects.Sort((a, b) =>
            {
                if (a.IsPinned != b.IsPinned) return b.IsPinned.CompareTo(a.IsPinned);
                return b.LastModified.CompareTo(a.LastModified);
            });

            ApplyFilter();
            UpdateCacheSizeStats();
        }

        private void ParsePrefsMru(string prefsPath, HashSet<string> seenPaths)
        {
            try
            {
                string text = File.ReadAllText(prefsPath);
                // Поиск тегов <BE.Prefs.MRU.Document.N>...</BE.Prefs.MRU.Document.N>
                var matches = Regex.Matches(text, @"<BE\.Prefs\.MRU\.Document\.(\d+)>(.*?)</BE\.Prefs\.MRU\.Document\.\1>");

                foreach (Match m in matches)
                {
                    string rawPath = m.Groups[2].Value.Trim();
                    string cleanPath = rawPath;
                    if (cleanPath.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
                        cleanPath = @"\\" + cleanPath.Substring(8);
                    else if (cleanPath.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
                        cleanPath = cleanPath.Substring(4);

                    if (!string.IsNullOrEmpty(cleanPath) && seenPaths.Add(cleanPath))
                    {
                        bool exists = File.Exists(cleanPath);
                        DateTime dt = DateTime.MinValue;
                        long size = 0;

                        if (exists)
                        {
                            try
                            {
                                var fi = new FileInfo(cleanPath);
                                dt = fi.LastWriteTime;
                                size = fi.Length;
                            }
                            catch { }
                        }
                        else
                        {
                            // Извлекаем сохраненную дату и размер из префов
                            string idx = m.Groups[1].Value;
                            var dateMatch = Regex.Match(text, string.Format(@"<BE\.Prefs\.MRU\.Document\.Date\.{0}>(.*?)</BE\.Prefs\.MRU\.Document\.Date\.{0}>", idx));
                            if (dateMatch.Success) DateTime.TryParse(dateMatch.Groups[1].Value, out dt);

                            var sizeMatch = Regex.Match(text, string.Format(@"<BE\.Prefs\.MRU\.Document\.Size\.{0}>(.*?)</BE\.Prefs\.MRU\.Document\.Size\.{0}>", idx));
                            if (sizeMatch.Success) long.TryParse(sizeMatch.Groups[1].Value, out size);
                        }

                        allProjects.Add(new ProjectItem
                        {
                            Name = Path.GetFileNameWithoutExtension(cleanPath),
                            Path = cleanPath,
                            LastModified = dt == DateTime.MinValue ? DateTime.Now : dt,
                            SizeBytes = size,
                            Exists = exists,
                            IsPinned = pinnedPaths.Contains(cleanPath)
                        });
                    }
                }
            }
            catch { }
        }

        private void ApplyFilter()
        {
            string q = txtSearch.Text.Trim().ToLowerInvariant();
            filteredProjects = allProjects.FindAll(p => string.IsNullOrEmpty(q) || p.Name.ToLowerInvariant().Contains(q) || p.Path.ToLowerInvariant().Contains(q));

            lvProjects.BeginUpdate();
            lvProjects.Items.Clear();
            foreach (var p in filteredProjects)
            {
                var lvi = new ListViewItem(p.Name) { Tag = p };
                lvProjects.Items.Add(lvi);
            }
            lvProjects.EndUpdate();

            lblStats.Text = string.Format("Проектов: {0} из {1}", filteredProjects.Count, allProjects.Count);
        }

        private void LvProjects_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            var p = e.Item.Tag as ProjectItem;
            if (p == null) return;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = e.Bounds;

            // Background
            bool isSelected = (e.State & ListViewItemStates.Selected) != 0;
            Color bg = isSelected ? cCardHover : cCard;

            using (var b = new SolidBrush(bg))
            {
                g.FillRectangle(b, rect.X + 4, rect.Y + 2, rect.Width - 8, rect.Height - 4);
            }

            // Акцентная полоса слева для выделенного или закрепленного
            if (p.IsPinned)
            {
                using (var pen = new Pen(cAccentCyan, 3))
                    g.DrawLine(pen, rect.X + 5, rect.Y + 4, rect.X + 5, rect.Bottom - 6);
            }
            else if (isSelected)
            {
                using (var pen = new Pen(cAccent, 3))
                    g.DrawLine(pen, rect.X + 5, rect.Y + 4, rect.X + 5, rect.Bottom - 6);
            }

            // Pin icon
            int x = rect.X + 16;
            if (p.IsPinned)
            {
                using (var brush = new SolidBrush(cAccentCyan))
                    g.DrawString("★", new Font("Segoe UI", 11f, FontStyle.Bold), brush, x, rect.Y + 12);
                x += 20;
            }

            // Project Name
            using (var brush = new SolidBrush(p.Exists ? Color.White : cDim))
            {
                var font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
                g.DrawString(p.Name, font, brush, x, rect.Y + 8);
            }

            // Project Path
            using (var brush = new SolidBrush(cDim))
            {
                var font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
                string drawPath = p.Path;
                if (drawPath.Length > 85) drawPath = drawPath.Substring(0, 40) + "..." + drawPath.Substring(drawPath.Length - 40);
                g.DrawString(drawPath, font, brush, x, rect.Y + 30);
            }

            // Right side: Status, Date, Size
            int rightX = rect.Right - 20;

            // Size
            using (var brush = new SolidBrush(cDim))
            {
                var font = new Font("Segoe UI", 9f, FontStyle.Regular);
                var sf = new StringFormat { Alignment = StringAlignment.Far };
                g.DrawString(p.FormattedSize, font, brush, rightX, rect.Y + 28, sf);
            }

            // Date
            using (var brush = new SolidBrush(Color.FromArgb(200, 205, 215)))
            {
                var font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
                var sf = new StringFormat { Alignment = StringAlignment.Far };
                g.DrawString(p.FormattedDate, font, brush, rightX, rect.Y + 8, sf);
            }

            // Status indicator badge
            int statusX = rightX - 180;
            Color statusColor = p.Exists ? cGreen : cRed;
            string statusText = p.Exists ? "● Доступен" : "● Диск отключен";

            using (var brush = new SolidBrush(statusColor))
            {
                var font = new Font("Segoe UI", 9f, FontStyle.Bold);
                g.DrawString(statusText, font, brush, statusX, rect.Y + 9);
            }
        }

        private void LvProjects_DoubleClick(object sender, EventArgs e)
        {
            if (lvProjects.SelectedItems.Count > 0)
            {
                var p = lvProjects.SelectedItems[0].Tag as ProjectItem;
                if (p != null) LaunchProject(p.Path);
            }
        }

        private void LvProjects_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && lvProjects.SelectedItems.Count > 0)
            {
                var p = lvProjects.SelectedItems[0].Tag as ProjectItem;
                if (p != null) LaunchProject(p.Path);
            }
        }

        private void LvProjects_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && lvProjects.SelectedItems.Count > 0)
            {
                var p = lvProjects.SelectedItems[0].Tag as ProjectItem;
                if (p == null) return;

                var cm = new ContextMenuStrip();
                cm.Items.Add("▶ Запустить проект (в обход Home Screen)", null, (s, ev) => LaunchProject(p.Path));
                cm.Items.Add(p.IsPinned ? "Открепить из избранного" : "★ Закрепить в избранном", null, (s, ev) => TogglePin(p));
                cm.Items.Add("Открыть папку проекта", null, (s, ev) =>
                {
                    try { Process.Start("explorer.exe", string.Format("/select,\"{0}\"", p.Path)); } catch { }
                });
                cm.Items.Add("Скопировать путь к файлу", null, (s, ev) =>
                {
                    try { Clipboard.SetText(p.Path); } catch { }
                });
                cm.Show(lvProjects, e.Location);
            }
        }

        private void TogglePin(ProjectItem p)
        {
            p.IsPinned = !p.IsPinned;
            if (p.IsPinned) pinnedPaths.Add(p.Path);
            else pinnedPaths.Remove(p.Path);
            SaveConfig();
            RefreshProjects();
        }

        private void LaunchProject(string prprojPath)
        {
            if (string.IsNullOrEmpty(premiereExePath) || !File.Exists(premiereExePath))
            {
                MessageBox.Show("Исполняемый файл Adobe Premiere Pro.exe не найден!\nНажмите кнопку 'Выбрать Pr.exe' в правом верхнем углу.", "Ошибка запуска", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var psi = new ProcessStartInfo();
                psi.FileName = premiereExePath;
                if (!string.IsNullOrEmpty(prprojPath))
                {
                    if (!File.Exists(prprojPath))
                    {
                        var dr = MessageBox.Show("Файл проекта не найден на диске:\n" + prprojPath + "\n\nПопробовать запустить Premiere Pro всё равно?", "Файл не найден", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (dr != DialogResult.Yes) return;
                    }
                    psi.Arguments = string.Format("\"{0}\"", prprojPath);
                }

                Process.Start(psi);
                // Сворачиваем лаунчер
                this.WindowState = FormWindowState.Minimized;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при запуске Premiere Pro:\n" + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnNewProject_Click(object sender, EventArgs e)
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter = "Проект Premiere Pro (*.prproj)|*.prproj";
                dlg.Title = "Создать новый проект Premiere Pro";
                dlg.FileName = "Новый проект.prproj";
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        CreateBlankProject(dlg.FileName);
                        LaunchProject(dlg.FileName);
                        RefreshProjects();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Не удалось создать проект:\n" + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void CreateBlankProject(string targetPath)
        {
            // Минимальный валидный сжатый .prproj XML шаблон
            string safeName = System.Security.SecurityElement.Escape(Path.GetFileName(targetPath));
            string xml = @"<?xml version=""1.0"" encoding=""UTF-8"" ?>
<PremiereData Version=""3"">
	<Project ObjectRef=""1""/>
	<Project ObjectID=""1"" ClassID=""62ad66dd-0dcd-42da-a660-6d8fbde94876"" Version=""45"">
		<Node Version=""1"">
			<Properties Version=""1"">
				<Project.Name>" + safeName + @"</Project.Name>
			</Properties>
		</Node>
		<RootProjectItem ObjectRef=""2""/>
	</Project>
	<ProjectItem ObjectID=""2"" ClassID=""e782bc4b-7036-4074-b5b6-7eb43329f60f"" Version=""1"">
		<Name>Root</Name>
	</ProjectItem>
</PremiereData>";

            using (var fs = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
            using (var gz = new GZipStream(fs, CompressionMode.Compress))
            using (var sw = new StreamWriter(gz, Encoding.UTF8))
            {
                sw.Write(xml);
            }
        }

        private void BtnCleanCache_Click(object sender, EventArgs e)
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string[] cacheDirs = new string[]
            {
                Path.Combine(appData, "Adobe", "Common", "Media Cache Files"),
                Path.Combine(appData, "Adobe", "Common", "Media Cache"),
                Path.Combine(appData, "Adobe", "Common", "Peak Files")
            };

            long totalBytes = 0;
            int totalFiles = 0;

            foreach (var d in cacheDirs)
            {
                if (Directory.Exists(d))
                {
                    try
                    {
                        var files = Directory.GetFiles(d, "*.*", SearchOption.AllDirectories);
                        totalFiles += files.Length;
                        foreach (var f in files)
                        {
                            try { totalBytes += new FileInfo(f).Length; } catch { }
                        }
                    }
                    catch { }
                }
            }

            double mb = Math.Round(totalBytes / (1024.0 * 1024.0), 1);
            var res = MessageBox.Show(string.Format("Найдено файлов кэша: {0} ({1} МБ).\nОчистить кэш сейчас?\n(Premiere Pro должна быть закрыта)", totalFiles, mb), "Очистка Media Cache", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (res == DialogResult.Yes)
            {
                int deleted = 0;
                foreach (var d in cacheDirs)
                {
                    if (Directory.Exists(d))
                    {
                        try
                        {
                            var files = Directory.GetFiles(d, "*.*", SearchOption.TopDirectoryOnly);
                            foreach (var f in files)
                            {
                                try { File.Delete(f); deleted++; } catch { }
                            }
                        }
                        catch { }
                    }
                }
                MessageBox.Show(string.Format("Очистка завершена! Удалено файлов: {0}", deleted), "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
                UpdateCacheSizeStats();
            }
        }

        private void UpdateCacheSizeStats()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string cacheFiles = Path.Combine(appData, "Adobe", "Common", "Media Cache Files");
                long bytes = 0;
                if (Directory.Exists(cacheFiles))
                {
                    var files = Directory.GetFiles(cacheFiles, "*.*", SearchOption.TopDirectoryOnly);
                    foreach (var f in files)
                    {
                        try { bytes += new FileInfo(f).Length; } catch { }
                    }
                }
                double mb = Math.Round(bytes / (1024.0 * 1024.0), 1);
                btnCleanCache.Text = string.Format("Кэш: {0} МБ", mb);
            }
            catch { }
        }

        private void CheckStartupTweak()
        {
            if (string.IsNullOrEmpty(activePrefsPath) || !File.Exists(activePrefsPath)) return;
            try
            {
                string text = File.ReadAllText(activePrefsPath);
                chkDisableHome.Checked = text.Contains("<MZ.Prefs.ShowQuickstartDialog>false</MZ.Prefs.ShowQuickstartDialog>");
            }
            catch { }
        }

        private void ChkDisableHome_CheckedChanged(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(activePrefsPath) || !File.Exists(activePrefsPath)) return;

            try
            {
                string text = File.ReadAllText(activePrefsPath);
                bool disable = chkDisableHome.Checked;

                if (disable)
                {
                    if (text.Contains("<MZ.Prefs.ShowQuickstartDialog>"))
                        text = Regex.Replace(text, @"<MZ\.Prefs\.ShowQuickstartDialog>.*?</MZ\.Prefs\.ShowQuickstartDialog>", "<MZ.Prefs.ShowQuickstartDialog>false</MZ.Prefs.ShowQuickstartDialog>");
                    else
                        text = text.Replace("<Properties Version=\"1\">", "<Properties Version=\"1\">\r\n\t\t\t<MZ.Prefs.ShowQuickstartDialog>false</MZ.Prefs.ShowQuickstartDialog>");

                    if (!text.Contains("<MZ.Prefs.DontShowWelcomeScreenOnOpen>"))
                        text = text.Replace("<Properties Version=\"1\">", "<Properties Version=\"1\">\r\n\t\t\t<MZ.Prefs.DontShowWelcomeScreenOnOpen>true</MZ.Prefs.DontShowWelcomeScreenOnOpen>");
                }
                else
                {
                    text = Regex.Replace(text, @"<MZ\.Prefs\.ShowQuickstartDialog>.*?</MZ\.Prefs\.ShowQuickstartDialog>", "<MZ.Prefs.ShowQuickstartDialog>true</MZ.Prefs.ShowQuickstartDialog>");
                    text = Regex.Replace(text, @"<MZ\.Prefs\.DontShowWelcomeScreenOnOpen>.*?</MZ\.Prefs\.DontShowWelcomeScreenOnOpen>", "<MZ.Prefs.DontShowWelcomeScreenOnOpen>false</MZ.Prefs.DontShowWelcomeScreenOnOpen>");
                }

                File.WriteAllText(activePrefsPath, text);
            }
            catch { }
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(configFilePath))
                {
                    string json = File.ReadAllText(configFilePath);
                    var prMatch = Regex.Match(json, @"""premiereExe"":\s*""(.*?)""");
                    if (prMatch.Success) premiereExePath = prMatch.Groups[1].Value.Replace(@"\\", @"\");

                    var pinMatches = Regex.Matches(json, @"""pinned"":\s*\[(.*?)\]", RegexOptions.Singleline);
                    if (pinMatches.Count > 0)
                    {
                        var items = Regex.Matches(pinMatches[0].Groups[1].Value, @"""(.*?)""");
                        foreach (Match im in items)
                        {
                            string p = im.Groups[1].Value.Replace(@"\\", @"\");
                            if (!string.IsNullOrEmpty(p)) pinnedPaths.Add(p);
                        }
                    }
                }
            }
            catch { }
        }

        private void SaveConfig()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine(string.Format("  \"premiereExe\": \"{0}\",", premiereExePath.Replace(@"\", @"\\").Replace("\"", "\\\"")));
                sb.AppendLine("  \"pinned\": [");
                int count = 0;
                foreach (var p in pinnedPaths)
                {
                    sb.AppendFormat("    \"{0}\"{1}\r\n", p.Replace(@"\", @"\\").Replace("\"", "\\\""), count < pinnedPaths.Count - 1 ? "," : "");
                    count++;
                }
                sb.AppendLine("  ]");
                sb.AppendLine("}");
                File.WriteAllText(configFilePath, sb.ToString());
            }
            catch { }
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}

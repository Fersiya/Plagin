using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PrLauncher
{
    public enum AppKind
    {
        PR,
        AE
    }

    public class ProjectItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public AppKind Kind { get; set; }
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
                if (LastModified.Year == now.Year)
                    return LastModified.ToString("dd MMM, HH:mm");
                return LastModified.ToString("dd.MM.yyyy HH:mm");
            }
        }
    }

    public static class UITheme
    {
        public static Color Bg = Color.FromArgb(18, 20, 26);             // #12141a
        public static Color CardBg = Color.FromArgb(24, 27, 36);         // #181b24
        public static Color CardBorder = Color.FromArgb(38, 43, 58);     // #262b3a
        public static Color CardHover = Color.FromArgb(32, 36, 49);      // #202431
        public static Color CardHoverBorder = Color.FromArgb(56, 63, 86);// #383f56
        public static Color CardSelected = Color.FromArgb(37, 32, 59);   // #25203b
        public static Color CardSelectedBorder = Color.FromArgb(139, 92, 246); // #8b5cf6

        public static Color TextPrimary = Color.FromArgb(248, 250, 252); // #f8fafc
        public static Color TextSecondary = Color.FromArgb(148, 163, 184); // #94a3b8
        public static Color TextDim = Color.FromArgb(100, 116, 139);     // #64748b

        // Premiere Colors
        public static Color PrBgTop = Color.FromArgb(59, 24, 95);
        public static Color PrBgBottom = Color.FromArgb(36, 17, 66);
        public static Color PrBorder = Color.FromArgb(107, 33, 168);
        public static Color PrText = Color.FromArgb(233, 213, 255);

        // After Effects Colors
        public static Color AeBgTop = Color.FromArgb(15, 30, 68);
        public static Color AeBgBottom = Color.FromArgb(10, 18, 44);
        public static Color AeBorder = Color.FromArgb(29, 78, 216);
        public static Color AeText = Color.FromArgb(191, 219, 254);

        public static Color Accent = Color.FromArgb(139, 92, 246);       // #8b5cf6
        public static Color AccentHover = Color.FromArgb(167, 139, 250); // #a78bfa
        public static Color AccentDark = Color.FromArgb(109, 40, 217);   // #6d28d9

        public static Color Green = Color.FromArgb(16, 185, 129);        // #10b981
        public static Color GreenBg = Color.FromArgb(6, 78, 59);         // #064e3b
        public static Color GreenBorder = Color.FromArgb(5, 150, 105);   // #059669

        public static Color Red = Color.FromArgb(239, 68, 68);           // #ef4444
        public static Color RedBg = Color.FromArgb(69, 10, 10);          // #450a0a
        public static Color RedBorder = Color.FromArgb(153, 27, 27);     // #991b1b

        public static Color StarGold = Color.FromArgb(251, 191, 36);     // #fbbf24

        public static GraphicsPath CreateRoundedRectangle(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            if (d <= 0)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static PointF[] CreateStarPoints(float cx, float cy, float rOuter, float rInner)
        {
            PointF[] pts = new PointF[10];
            double angle = -Math.PI / 2.0;
            double step = Math.PI / 5.0;

            for (int i = 0; i < 10; i++)
            {
                float r = (i % 2 == 0) ? rOuter : rInner;
                pts[i] = new PointF(
                    (float)(cx + r * Math.Cos(angle)),
                    (float)(cy + r * Math.Sin(angle))
                );
                angle += step;
            }
            return pts;
        }
    }

    public class FilterTabBar : Control
    {
        public enum FilterMode { All, PR, AE }
        private FilterMode currentMode = FilterMode.All;
        private int countAll = 0;
        private int countPr = 0;
        private int countAe = 0;
        private int hoverTab = -1;

        public event EventHandler FilterChanged;

        public FilterTabBar()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            this.DoubleBuffered = true;
            this.Height = 36;
            this.BackColor = UITheme.Bg;
            this.Cursor = Cursors.Hand;
        }

        public FilterMode Mode
        {
            get { return currentMode; }
            set
            {
                if (currentMode != value)
                {
                    currentMode = value;
                    this.Invalidate();
                    if (FilterChanged != null) FilterChanged(this, EventArgs.Empty);
                }
            }
        }

        public void SetCounts(int all, int pr, int ae)
        {
            countAll = all;
            countPr = pr;
            countAe = ae;
            this.Invalidate();
        }

        private Rectangle[] GetTabRects()
        {
            int h = this.Height - 4;
            int x = 0;
            var r1 = new Rectangle(x, 2, 85, h);
            x += r1.Width + 6;
            var r2 = new Rectangle(x, 2, 140, h);
            x += r2.Width + 6;
            var r3 = new Rectangle(x, 2, 140, h);
            return new Rectangle[] { r1, r2, r3 };
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var rects = GetTabRects();
            int hov = -1;
            for (int i = 0; i < rects.Length; i++)
            {
                if (rects[i].Contains(e.Location)) { hov = i; break; }
            }
            if (hov != hoverTab)
            {
                hoverTab = hov;
                this.Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hoverTab != -1)
            {
                hoverTab = -1;
                this.Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            var rects = GetTabRects();
            if (rects[0].Contains(e.Location)) Mode = FilterMode.All;
            else if (rects[1].Contains(e.Location)) Mode = FilterMode.PR;
            else if (rects[2].Contains(e.Location)) Mode = FilterMode.AE;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var rects = GetTabRects();
            string[] titles = new string[]
            {
                string.Format("Все ({0})", countAll),
                string.Format("Premiere Pro ({0})", countPr),
                string.Format("After Effects ({0})", countAe)
            };

            for (int i = 0; i < 3; i++)
            {
                bool isSel = (i == 0 && currentMode == FilterMode.All) ||
                             (i == 1 && currentMode == FilterMode.PR) ||
                             (i == 2 && currentMode == FilterMode.AE);
                bool isHov = (i == hoverTab);
                var r = rects[i];

                Color bg = isSel ? UITheme.CardSelected : (isHov ? UITheme.CardHover : UITheme.CardBg);
                Color border = isSel ? UITheme.Accent : (isHov ? UITheme.CardHoverBorder : UITheme.CardBorder);
                Color fg = isSel ? UITheme.TextPrimary : (isHov ? UITheme.TextPrimary : UITheme.TextSecondary);

                using (var path = UITheme.CreateRoundedRectangle(r, 6))
                {
                    using (var brush = new SolidBrush(bg))
                        g.FillPath(brush, path);
                    using (var pen = new Pen(border, isSel ? 1.5f : 1f))
                        g.DrawPath(pen, path);
                }

                // App Mini dot for Pr and Ae
                if (i == 1)
                {
                    using (var b = new SolidBrush(Color.FromArgb(168, 85, 247)))
                        g.FillEllipse(b, r.X + 10, r.Y + r.Height / 2 - 3, 7, 7);
                }
                else if (i == 2)
                {
                    using (var b = new SolidBrush(Color.FromArgb(59, 130, 246)))
                        g.FillEllipse(b, r.X + 10, r.Y + r.Height / 2 - 3, 7, 7);
                }

                using (var font = new Font("Segoe UI", 9f, isSel ? FontStyle.Bold : FontStyle.Regular))
                using (var brush = new SolidBrush(fg))
                using (var sf = new StringFormat { Alignment = (i == 0 ? StringAlignment.Center : StringAlignment.Near), LineAlignment = StringAlignment.Center })
                {
                    Rectangle textRect = i == 0 ? r : new Rectangle(r.X + 22, r.Y, r.Width - 24, r.Height);
                    g.DrawString(titles[i], font, brush, textRect, sf);
                }
            }
        }
    }

    public class ProjectCardList : Control
    {
        private List<ProjectItem> items = new List<ProjectItem>();
        private int selectedIndex = -1;
        private int hoverIndex = -1;
        private bool isHoverStar = false;
        private bool isHoverLaunch = false;

        private int scrollValue = 0;
        private int maxScroll = 0;
        private bool isDraggingScroll = false;
        private int dragStartY = 0;
        private int dragStartScroll = 0;

        private int itemHeight = 68;
        private int itemSpacing = 6;
        private int paddingX = 18;
        private int paddingY = 8;
        private int scrollBarWidth = 8;

        public event EventHandler<ProjectItem> ProjectLaunched;
        public event EventHandler<ProjectItem> PinToggled;
        public event EventHandler<ProjectItem> ProjectRemoveRequested;
        public event EventHandler<ProjectItem> AutoSaveRecoveryRequested;
        public event EventHandler SelectionChanged;

        public ProjectCardList()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                          ControlStyles.Selectable, true);
            this.DoubleBuffered = true;
            this.BackColor = UITheme.Bg;
            this.Cursor = Cursors.Default;
        }

        public List<ProjectItem> Items
        {
            get { return items; }
            set
            {
                items = value ?? new List<ProjectItem>();
                if (selectedIndex >= items.Count) selectedIndex = items.Count - 1;
                UpdateScroll();
                this.Invalidate();
            }
        }

        public int SelectedIndex
        {
            get { return selectedIndex; }
            set
            {
                if (selectedIndex != value)
                {
                    selectedIndex = value;
                    if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
                    EnsureVisible(selectedIndex);
                    this.Invalidate();
                }
            }
        }

        public ProjectItem SelectedProject
        {
            get
            {
                if (selectedIndex >= 0 && selectedIndex < items.Count)
                    return items[selectedIndex];
                return null;
            }
        }

        private void UpdateScroll()
        {
            int totalHeight = paddingY * 2 + items.Count * (itemHeight + itemSpacing);
            int viewHeight = this.ClientSize.Height;

            if (totalHeight > viewHeight)
            {
                maxScroll = totalHeight - viewHeight;
                if (scrollValue > maxScroll) scrollValue = maxScroll;
            }
            else
            {
                maxScroll = 0;
                scrollValue = 0;
            }
        }

        public void EnsureVisible(int index)
        {
            if (index < 0 || index >= items.Count || maxScroll <= 0) return;
            int itemY = paddingY + index * (itemHeight + itemSpacing);
            int viewHeight = this.ClientSize.Height;

            if (itemY < scrollValue)
            {
                scrollValue = Math.Max(0, itemY - paddingY);
            }
            else if (itemY + itemHeight > scrollValue + viewHeight)
            {
                scrollValue = Math.Min(maxScroll, itemY + itemHeight + paddingY - viewHeight);
            }
            this.Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateScroll();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (maxScroll > 0)
            {
                int delta = (e.Delta / 120) * (itemHeight + itemSpacing) * 2;
                scrollValue -= delta;
                if (scrollValue < 0) scrollValue = 0;
                if (scrollValue > maxScroll) scrollValue = maxScroll;
                UpdateHover(e.Location);
                this.Invalidate();
            }
        }

        private int GetItemIndexAt(Point pt)
        {
            int y = pt.Y + scrollValue - paddingY;
            if (y < 0) return -1;
            int idx = y / (itemHeight + itemSpacing);
            int rem = y % (itemHeight + itemSpacing);
            if (rem > itemHeight) return -1;
            if (idx >= 0 && idx < items.Count) return idx;
            return -1;
        }

        private Rectangle GetCardBounds(int index)
        {
            int y = paddingY + index * (itemHeight + itemSpacing) - scrollValue;
            int w = this.ClientSize.Width - (paddingX * 2) - (maxScroll > 0 ? (scrollBarWidth + 6) : 0);
            return new Rectangle(paddingX, y, w, itemHeight);
        }

        private Rectangle GetStarBounds(Rectangle cardRect)
        {
            return new Rectangle(cardRect.X + 10, cardRect.Y + 20, 26, 26);
        }

        private Rectangle GetLaunchBtnBounds(Rectangle cardRect)
        {
            return new Rectangle(cardRect.Right - 94, cardRect.Y + 34, 80, 24);
        }

        private Rectangle GetScrollThumbRect()
        {
            if (maxScroll <= 0) return Rectangle.Empty;
            int trackH = this.ClientSize.Height - 16;
            int totalH = paddingY * 2 + items.Count * (itemHeight + itemSpacing);
            int thumbH = Math.Max(30, (int)((float)this.ClientSize.Height / totalH * trackH));
            int thumbY = 8 + (int)((float)scrollValue / maxScroll * (trackH - thumbH));
            int thumbX = this.ClientSize.Width - scrollBarWidth - 4;
            return new Rectangle(thumbX, thumbY, scrollBarWidth, thumbH);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (isDraggingScroll)
            {
                int trackH = this.ClientSize.Height - 16;
                int totalH = paddingY * 2 + items.Count * (itemHeight + itemSpacing);
                int thumbH = Math.Max(30, (int)((float)this.ClientSize.Height / totalH * trackH));
                int availableTrack = trackH - thumbH;

                if (availableTrack > 0)
                {
                    int dy = e.Y - dragStartY;
                    int newScroll = dragStartScroll + (int)((float)dy / availableTrack * maxScroll);
                    if (newScroll < 0) newScroll = 0;
                    if (newScroll > maxScroll) newScroll = maxScroll;
                    scrollValue = newScroll;
                    this.Invalidate();
                }
                return;
            }

            UpdateHover(e.Location);
        }

        private void UpdateHover(Point pt)
        {
            int idx = GetItemIndexAt(pt);
            bool hoverStar = false;
            bool hoverLaunch = false;

            if (idx >= 0 && idx < items.Count)
            {
                var cardRect = GetCardBounds(idx);
                var starRect = GetStarBounds(cardRect);
                var launchRect = GetLaunchBtnBounds(cardRect);

                if (starRect.Contains(pt)) hoverStar = true;
                if (launchRect.Contains(pt)) hoverLaunch = true;
            }

            if (idx != hoverIndex || hoverStar != isHoverStar || hoverLaunch != isHoverLaunch)
            {
                hoverIndex = idx;
                isHoverStar = hoverStar;
                isHoverLaunch = hoverLaunch;
                this.Cursor = (hoverStar || hoverLaunch) ? Cursors.Hand : Cursors.Default;
                this.Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hoverIndex != -1 || isHoverStar || isHoverLaunch)
            {
                hoverIndex = -1;
                isHoverStar = false;
                isHoverLaunch = false;
                this.Cursor = Cursors.Default;
                this.Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            this.Focus();

            if (maxScroll > 0)
            {
                var thumb = GetScrollThumbRect();
                if (thumb.Contains(e.Location))
                {
                    isDraggingScroll = true;
                    dragStartY = e.Y;
                    dragStartScroll = scrollValue;
                    return;
                }
                else if (e.X >= this.ClientSize.Width - scrollBarWidth - 8)
                {
                    if (e.Y < thumb.Y) scrollValue = Math.Max(0, scrollValue - this.ClientSize.Height);
                    else scrollValue = Math.Min(maxScroll, scrollValue + this.ClientSize.Height);
                    this.Invalidate();
                    return;
                }
            }

            int idx = GetItemIndexAt(e.Location);
            if (idx >= 0 && idx < items.Count)
            {
                var p = items[idx];
                var cardRect = GetCardBounds(idx);
                var starRect = GetStarBounds(cardRect);
                var launchRect = GetLaunchBtnBounds(cardRect);

                if (e.Button == MouseButtons.Left)
                {
                    if (starRect.Contains(e.Location))
                    {
                        if (PinToggled != null) PinToggled(this, p);
                        return;
                    }
                    if (launchRect.Contains(e.Location))
                    {
                        SelectedIndex = idx;
                        if (ProjectLaunched != null) ProjectLaunched(this, p);
                        return;
                    }

                    SelectedIndex = idx;
                }
                else if (e.Button == MouseButtons.Right)
                {
                    SelectedIndex = idx;
                    ShowContextMenu(p, e.Location);
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            isDraggingScroll = false;
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            if (selectedIndex >= 0 && selectedIndex < items.Count)
            {
                if (ProjectLaunched != null) ProjectLaunched(this, items[selectedIndex]);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Up)
            {
                if (selectedIndex > 0) SelectedIndex--;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Down)
            {
                if (selectedIndex < items.Count - 1) SelectedIndex++;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                if (selectedIndex >= 0 && selectedIndex < items.Count)
                {
                    if (ProjectLaunched != null) ProjectLaunched(this, items[selectedIndex]);
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Space)
            {
                if (selectedIndex >= 0 && selectedIndex < items.Count)
                {
                    if (PinToggled != null) PinToggled(this, items[selectedIndex]);
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete)
            {
                if (selectedIndex >= 0 && selectedIndex < items.Count)
                {
                    if (ProjectRemoveRequested != null) ProjectRemoveRequested(this, items[selectedIndex]);
                }
                e.Handled = true;
            }
        }

        private void ShowContextMenu(ProjectItem p, Point pt)
        {
            var menu = new ContextMenuStrip();
            menu.Renderer = new DarkMenuRenderer();

            string appTitle = (p.Kind == AppKind.AE) ? "After Effects" : "Premiere Pro";
            var itmOpen = menu.Items.Add(string.Format("▶  Открыть в {0}", appTitle));
            itmOpen.Click += (s, e) => { if (ProjectLaunched != null) ProjectLaunched(this, p); };

            var itmAutoSave = menu.Items.Add("⏱  Восстановить из автосохранения...");
            itmAutoSave.Click += (s, e) =>
            {
                if (AutoSaveRecoveryRequested != null) AutoSaveRecoveryRequested(this, p);
            };

            var itmFolder = menu.Items.Add("📂  Показать в Проводнике");
            itmFolder.Click += (s, e) =>
            {
                try
                {
                    if (File.Exists(p.Path))
                        Process.Start("explorer.exe", string.Format("/select,\"{0}\"", p.Path));
                    else
                        MessageBox.Show("Файл проекта не найден:\n" + p.Path, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch { }
            };

            menu.Items.Add(new ToolStripSeparator());

            var itmPin = menu.Items.Add(p.IsPinned ? "★  Открепить" : "★  Закрепить вверху");
            itmPin.Click += (s, e) => { if (PinToggled != null) PinToggled(this, p); };

            var itmCopy = menu.Items.Add("📋  Копировать полный путь");
            itmCopy.Click += (s, e) =>
            {
                try { Clipboard.SetText(p.Path); } catch { }
            };

            var itmRemove = menu.Items.Add("✕  Удалить из недавних");
            itmRemove.Click += (s, e) =>
            {
                if (ProjectRemoveRequested != null) ProjectRemoveRequested(this, p);
            };

            menu.Show(this, pt);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            using (var b = new SolidBrush(this.BackColor))
            {
                g.FillRectangle(b, this.ClientRectangle);
            }

            if (items.Count == 0)
            {
                DrawEmptyState(g);
                return;
            }

            int clientH = this.ClientSize.Height;

            for (int i = 0; i < items.Count; i++)
            {
                var cardRect = GetCardBounds(i);
                if (cardRect.Bottom < 0) continue;
                if (cardRect.Y > clientH) break;

                bool isSel = (i == selectedIndex);
                bool isHov = (i == hoverIndex);
                var p = items[i];

                DrawCard(g, p, cardRect, isSel, isHov, isHov && isHoverStar, isHov && isHoverLaunch);
            }

            // Custom Slim Dark Scrollbar
            if (maxScroll > 0)
            {
                var thumb = GetScrollThumbRect();
                Color thumbColor = isDraggingScroll ? UITheme.Accent : Color.FromArgb(50, 56, 75);
                using (var path = UITheme.CreateRoundedRectangle(thumb, 3))
                using (var brush = new SolidBrush(thumbColor))
                {
                    g.FillPath(brush, path);
                }
            }
        }

        private void DrawCard(Graphics g, ProjectItem p, Rectangle r, bool isSelected, bool isHover, bool starHover, bool launchHover)
        {
            // 1. Card Background & Border
            Color bg = isSelected ? UITheme.CardSelected : (isHover ? UITheme.CardHover : UITheme.CardBg);
            Color border = isSelected ? UITheme.CardSelectedBorder : (isHover ? UITheme.CardHoverBorder : UITheme.CardBorder);

            using (var path = UITheme.CreateRoundedRectangle(r, 8))
            {
                using (var brush = new SolidBrush(bg))
                    g.FillPath(brush, path);
                using (var pen = new Pen(border, isSelected ? 1.5f : 1f))
                    g.DrawPath(pen, path);
            }

            // 2. Left Selection Accent Bar
            if (isSelected)
            {
                Rectangle barRect = new Rectangle(r.X + 2, r.Y + 14, 4, r.Height - 28);
                using (var path = UITheme.CreateRoundedRectangle(barRect, 2))
                using (var brush = new SolidBrush(UITheme.Accent))
                    g.FillPath(brush, path);
            }
            else if (p.IsPinned)
            {
                Rectangle barRect = new Rectangle(r.X + 2, r.Y + 16, 3, r.Height - 32);
                using (var path = UITheme.CreateRoundedRectangle(barRect, 2))
                using (var brush = new SolidBrush(UITheme.StarGold))
                    g.FillPath(brush, path);
            }

            // 3. Pin Star Button
            var starRect = GetStarBounds(r);
            if (starHover)
            {
                using (var path = UITheme.CreateRoundedRectangle(starRect, 13))
                using (var brush = new SolidBrush(Color.FromArgb(40, 251, 191, 36)))
                    g.FillPath(brush, path);
            }

            float starCx = starRect.X + starRect.Width / 2f;
            float starCy = starRect.Y + starRect.Height / 2f;
            PointF[] starPts = UITheme.CreateStarPoints(starCx, starCy, 8.5f, 4.2f);

            if (p.IsPinned)
            {
                using (var brush = new SolidBrush(UITheme.StarGold))
                using (var pen = new Pen(Color.FromArgb(217, 119, 6), 1f))
                {
                    g.FillPolygon(brush, starPts);
                    g.DrawPolygon(pen, starPts);
                }
            }
            else
            {
                using (var pen = new Pen(starHover ? UITheme.StarGold : Color.FromArgb(75, 85, 99), 1.4f))
                {
                    if (starHover)
                    {
                        using (var brush = new SolidBrush(Color.FromArgb(60, 251, 191, 36)))
                            g.FillPolygon(brush, starPts);
                    }
                    g.DrawPolygon(pen, starPts);
                }
            }

            // 4. Project Icon Badge (Pr or Ae)
            Rectangle iconRect = new Rectangle(r.X + 44, r.Y + 15, 38, 38);
            using (var path = UITheme.CreateRoundedRectangle(iconRect, 7))
            {
                Color topBg = (p.Kind == AppKind.AE) ? UITheme.AeBgTop : UITheme.PrBgTop;
                Color botBg = (p.Kind == AppKind.AE) ? UITheme.AeBgBottom : UITheme.PrBgBottom;
                Color icoBorder = (p.Kind == AppKind.AE) ? UITheme.AeBorder : UITheme.PrBorder;
                Color icoFg = (p.Kind == AppKind.AE) ? UITheme.AeText : UITheme.PrText;
                string icoText = (p.Kind == AppKind.AE) ? "Ae" : "Pr";

                using (var lgb = new LinearGradientBrush(iconRect, topBg, botBg, 45f))
                    g.FillPath(lgb, path);
                using (var pen = new Pen(icoBorder, 1f))
                    g.DrawPath(pen, path);

                using (var font = new Font("Segoe UI", 11.5f, FontStyle.Bold))
                using (var brush = new SolidBrush(icoFg))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(icoText, font, brush, iconRect, sf);
                }
            }

            // 5. Project Name & Path
            int textX = r.X + 94;
            int textMaxW = r.Width - 340;
            if (textMaxW < 120) textMaxW = 120;

            // Name
            using (var font = new Font("Segoe UI", 11f, FontStyle.Bold))
            {
                Rectangle nameRect = new Rectangle(textX, r.Y + 12, textMaxW, 22);
                TextRenderer.DrawText(g, p.Name, font, nameRect, p.Exists ? UITheme.TextPrimary : UITheme.TextSecondary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            // Path
            using (var font = new Font("Segoe UI", 8.8f, FontStyle.Regular))
            {
                Rectangle pathRect = new Rectangle(textX, r.Y + 36, textMaxW, 18);
                TextRenderer.DrawText(g, p.Path, font, pathRect, UITheme.TextDim,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.PathEllipsis);
            }

            // 6. Right Side: Status Badge, Date, Size, Quick Launch
            int rightX = r.Right - 16;

            // Status Pill Badge
            string statusText = p.Exists ? "● Доступен" : "✕ Не найден";
            Color stFg = p.Exists ? UITheme.Green : UITheme.Red;
            Color stBg = p.Exists ? UITheme.GreenBg : UITheme.RedBg;
            Color stBorder = p.Exists ? UITheme.GreenBorder : UITheme.RedBorder;

            Size stSize = TextRenderer.MeasureText(statusText, new Font("Segoe UI", 8.5f, FontStyle.Bold));
            int stW = stSize.Width + 12;
            int stH = 20;
            int stX = rightX - stW;
            int stY = r.Y + 13;

            Rectangle stRect = new Rectangle(stX, stY, stW, stH);
            using (var path = UITheme.CreateRoundedRectangle(stRect, 10))
            {
                using (var brush = new SolidBrush(stBg))
                    g.FillPath(brush, path);
                using (var pen = new Pen(stBorder, 1f))
                    g.DrawPath(pen, path);
            }
            using (var font = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var brush = new SolidBrush(stFg))
            {
                g.DrawString(statusText, font, brush, stRect, sf);
            }

            // Date
            using (var font = new Font("Segoe UI", 9f, FontStyle.Regular))
            {
                string dtStr = p.FormattedDate;
                Size dtSize = TextRenderer.MeasureText(dtStr, font);
                int dtX = stX - dtSize.Width - 14;
                Rectangle dtRect = new Rectangle(dtX, r.Y + 14, dtSize.Width, 18);
                TextRenderer.DrawText(g, dtStr, font, dtRect, Color.FromArgb(203, 213, 225),
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }

            // Line 2: Size Pill & Hover Launch Button
            int sizePillW = 68;
            int sizePillH = 19;
            int sizePillX = rightX - sizePillW;
            int sizePillY = r.Y + 37;

            if (isHover)
            {
                var launchRect = GetLaunchBtnBounds(r);
                Color btnBg = launchHover ? UITheme.AccentHover : UITheme.Accent;
                using (var path = UITheme.CreateRoundedRectangle(launchRect, 6))
                {
                    using (var brush = new SolidBrush(btnBg))
                        g.FillPath(brush, path);
                }
                using (var font = new Font("Segoe UI", 8.5f, FontStyle.Bold))
                using (var brush = new SolidBrush(Color.White))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString("▶ Запуск", font, brush, launchRect, sf);
                }

                sizePillX = launchRect.X - sizePillW - 8;
            }

            Rectangle sizeRect = new Rectangle(sizePillX, sizePillY, sizePillW, sizePillH);
            using (var path = UITheme.CreateRoundedRectangle(sizeRect, 5))
            {
                using (var brush = new SolidBrush(Color.FromArgb(30, 34, 46)))
                    g.FillPath(brush, path);
                using (var pen = new Pen(Color.FromArgb(46, 52, 70), 1f))
                    g.DrawPath(pen, path);
            }
            using (var font = new Font("Segoe UI", 8.2f, FontStyle.Regular))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var brush = new SolidBrush(UITheme.TextSecondary))
            {
                g.DrawString(p.FormattedSize, font, brush, sizeRect, sf);
            }
        }

        private void DrawEmptyState(Graphics g)
        {
            Rectangle r = this.ClientRectangle;
            int cy = r.Height / 2 - 30;

            using (var pen = new Pen(Color.FromArgb(55, 65, 81), 3f))
            {
                g.DrawEllipse(pen, r.Width / 2 - 16, cy - 35, 24, 24);
                g.DrawLine(pen, r.Width / 2 + 4, cy - 15, r.Width / 2 + 16, cy - 3);
            }

            using (var font = new Font("Segoe UI", 13f, FontStyle.Bold))
            using (var brush = new SolidBrush(UITheme.TextSecondary))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center })
            {
                g.DrawString("Проекты не найдены", font, brush, new Rectangle(0, cy + 10, r.Width, 26), sf);
            }

            using (var font = new Font("Segoe UI", 9.5f, FontStyle.Regular))
            using (var brush = new SolidBrush(UITheme.TextDim))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center })
            {
                g.DrawString("Перетащите .prproj или .aep в окно, или создайте проект кнопкой «+ Проект со структурой»", font, brush, new Rectangle(0, cy + 38, r.Width, 22), sf);
            }
        }
    }

    public class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable()) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected)
            {
                Rectangle r = new Rectangle(2, 0, e.Item.Width - 4, e.Item.Height);
                using (var path = UITheme.CreateRoundedRectangle(r, 4))
                using (var b = new SolidBrush(UITheme.CardHover))
                {
                    e.Graphics.FillPath(b, path);
                }
            }
            else
            {
                base.OnRenderMenuItemBackground(e);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = UITheme.TextPrimary;
            base.OnRenderItemText(e);
        }

        private class DarkColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Color.FromArgb(24, 27, 36); } }
            public override Color MenuBorder { get { return Color.FromArgb(45, 52, 70); } }
            public override Color MenuItemSelected { get { return Color.FromArgb(37, 43, 58); } }
            public override Color MenuItemBorder { get { return Color.Transparent; } }
            public override Color SeparatorDark { get { return Color.FromArgb(45, 52, 70); } }
            public override Color SeparatorLight { get { return Color.Transparent; } }
            public override Color ImageMarginGradientBegin { get { return Color.FromArgb(24, 27, 36); } }
            public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(24, 27, 36); } }
            public override Color ImageMarginGradientEnd { get { return Color.FromArgb(24, 27, 36); } }
        }
    }

    public class ModernSearchBox : Panel
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern Int32 SendMessage(IntPtr hWnd, int msg, int wParam, [MarshalAs(UnmanagedType.LPWStr)] string lParam);
        private const int EM_SETCUEBANNER = 0x1501;

        private TextBox innerText;
        private Label lblPlaceholder;
        private bool isFocused = false;
        private bool isClearHover = false;

        public event EventHandler TextChangedEvent;

        public ModernSearchBox()
        {
            this.DoubleBuffered = true;
            this.Size = new Size(340, 36);
            this.BackColor = UITheme.CardBg;
            this.Cursor = Cursors.IBeam;

            innerText = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = UITheme.CardBg,
                ForeColor = UITheme.TextPrimary,
                Font = new Font("Segoe UI", 10f, FontStyle.Regular),
                Location = new Point(34, 9),
                Width = this.Width - 64
            };

            lblPlaceholder = new Label
            {
                Text = "Поиск по названию или пути...",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                ForeColor = UITheme.TextDim,
                BackColor = UITheme.CardBg,
                AutoSize = true,
                Location = new Point(34, 8),
                Cursor = Cursors.IBeam
            };
            lblPlaceholder.Click += (s, e) => innerText.Focus();

            innerText.GotFocus += (s, e) => { isFocused = true; lblPlaceholder.Visible = false; this.Invalidate(); };
            innerText.LostFocus += (s, e) => { isFocused = false; lblPlaceholder.Visible = (innerText.Text.Length == 0); this.Invalidate(); };
            innerText.TextChanged += (s, e) =>
            {
                lblPlaceholder.Visible = (innerText.Text.Length == 0);
                if (TextChangedEvent != null) TextChangedEvent(this, EventArgs.Empty);
                this.Invalidate();
            };

            this.Click += (s, e) => innerText.Focus();
            this.Controls.Add(lblPlaceholder);
            this.Controls.Add(innerText);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                SendMessage(innerText.Handle, EM_SETCUEBANNER, 1, "Поиск по названию или пути...");
            }
            catch { }
        }

        public string SearchText
        {
            get { return innerText.Text; }
            set { innerText.Text = value; }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (innerText != null) innerText.Width = this.Width - 64;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool hoverClear = (innerText.Text.Length > 0 && e.X >= this.Width - 28 && e.X <= this.Width - 8);
            if (hoverClear != isClearHover)
            {
                isClearHover = hoverClear;
                this.Cursor = isClearHover ? Cursors.Hand : Cursors.IBeam;
                this.Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (isClearHover)
            {
                isClearHover = false;
                this.Cursor = Cursors.IBeam;
                this.Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (innerText.Text.Length > 0 && e.X >= this.Width - 28 && e.X <= this.Width - 8)
            {
                innerText.Clear();
                innerText.Focus();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle r = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            using (var path = UITheme.CreateRoundedRectangle(r, 7))
            {
                using (var brush = new SolidBrush(UITheme.CardBg))
                    g.FillPath(brush, path);
                using (var pen = new Pen(isFocused ? UITheme.Accent : UITheme.CardBorder, isFocused ? 1.5f : 1f))
                    g.DrawPath(pen, path);
            }

            // Magnifying glass
            using (var pen = new Pen(isFocused ? UITheme.Accent : UITheme.TextDim, 1.6f))
            {
                g.DrawEllipse(pen, 11, 11, 11, 11);
                g.DrawLine(pen, 20, 20, 25, 25);
            }

            // Clear Button
            if (innerText.Text.Length > 0)
            {
                Rectangle clearRect = new Rectangle(this.Width - 26, 8, 18, 18);
                using (var font = new Font("Segoe UI", 9f, FontStyle.Bold))
                using (var brush = new SolidBrush(isClearHover ? UITheme.TextPrimary : UITheme.TextDim))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString("✕", font, brush, clearRect, sf);
                }
            }
        }
    }

    public class AutoSaveRecoveryDialog : Form
    {
        private ProjectItem project;
        private List<FileInfo> autoSaveFiles = new List<FileInfo>();
        private ListBox lstFiles;
        private Label lblHeader;
        private Label lblSelectedInfo;
        private Button btnLaunch;
        private Button btnRestoreOriginal;
        private Button btnShowFolder;
        private Button btnClose;

        public AutoSaveRecoveryDialog(ProjectItem p)
        {
            this.project = p;
            this.Text = "Восстановление из автосохранения — " + p.Name;
            this.Size = new Size(680, 460);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = UITheme.Bg;
            this.ForeColor = UITheme.TextPrimary;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            InitializeUI();
            ScanAutoSaves();
        }

        private void InitializeUI()
        {
            lblHeader = new Label
            {
                Text = string.Format("Автосохранения для проекта «{0}»:", project.Name),
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = UITheme.TextPrimary,
                Location = new Point(20, 16),
                AutoSize = true
            };

            lstFiles = new ListBox
            {
                Location = new Point(20, 48),
                Size = new Size(624, 250),
                BackColor = UITheme.CardBg,
                ForeColor = UITheme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5f),
                ItemHeight = 22
            };
            lstFiles.SelectedIndexChanged += LstFiles_SelectedIndexChanged;
            lstFiles.DoubleClick += (s, e) => LaunchSelectedAutoSave();

            lblSelectedInfo = new Label
            {
                Location = new Point(20, 310),
                Size = new Size(624, 40),
                ForeColor = UITheme.TextSecondary,
                Font = new Font("Segoe UI", 8.8f)
            };

            btnLaunch = MainForm.CreateCustomButton("▶ Открыть копию", 140, UITheme.Accent, Color.White, UITheme.Accent, UITheme.AccentHover);
            btnLaunch.Location = new Point(20, 365);
            btnLaunch.Height = 34;
            btnLaunch.Enabled = false;
            btnLaunch.Click += (s, e) => LaunchSelectedAutoSave();

            btnRestoreOriginal = MainForm.CreateCustomButton("🔄 Заменить оригинал", 160, UITheme.CardBg, UITheme.TextPrimary, UITheme.CardBorder, UITheme.CardHover);
            btnRestoreOriginal.Location = new Point(170, 365);
            btnRestoreOriginal.Height = 34;
            btnRestoreOriginal.Enabled = false;
            btnRestoreOriginal.Click += (s, e) => RestoreOriginal();

            btnShowFolder = MainForm.CreateCustomButton("📂 В папке", 100, UITheme.CardBg, UITheme.TextPrimary, UITheme.CardBorder, UITheme.CardHover);
            btnShowFolder.Location = new Point(340, 365);
            btnShowFolder.Height = 34;
            btnShowFolder.Enabled = false;
            btnShowFolder.Click += (s, e) => ShowFolder();

            btnClose = MainForm.CreateCustomButton("Закрыть", 100, UITheme.CardBg, UITheme.TextSecondary, UITheme.CardBorder, UITheme.CardHover);
            btnClose.Location = new Point(544, 365);
            btnClose.Height = 34;
            btnClose.Click += (s, e) => this.Close();

            this.Controls.Add(lblHeader);
            this.Controls.Add(lstFiles);
            this.Controls.Add(lblSelectedInfo);
            this.Controls.Add(btnLaunch);
            this.Controls.Add(btnRestoreOriginal);
            this.Controls.Add(btnShowFolder);
            this.Controls.Add(btnClose);
        }

        private void ScanAutoSaves()
        {
            autoSaveFiles.Clear();
            lstFiles.Items.Clear();

            string projDir = Path.GetDirectoryName(project.Path);
            string baseName = Path.GetFileNameWithoutExtension(project.Path);
            string ext = (project.Kind == AppKind.AE) ? "*.aep" : "*.prproj";

            var searchDirs = new List<string>();

            // 1. Project subfolder Auto-Save
            string subDir = (project.Kind == AppKind.AE) ? "Adobe After Effects Auto-Save" : "Adobe Premiere Pro Auto-Save";
            if (!string.IsNullOrEmpty(projDir))
            {
                searchDirs.Add(Path.Combine(projDir, subDir));
                searchDirs.Add(projDir);
            }

            // 2. Documents Auto-Save
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (project.Kind == AppKind.AE)
            {
                searchDirs.Add(Path.Combine(docs, "Adobe", "After Effects"));
            }
            else
            {
                string prDocs = Path.Combine(docs, "Adobe", "Premiere Pro");
                if (Directory.Exists(prDocs))
                {
                    try
                    {
                        var autoSub = Directory.GetDirectories(prDocs, "Adobe Premiere Pro Auto-Save", SearchOption.AllDirectories);
                        searchDirs.AddRange(autoSub);
                    }
                    catch { }
                }
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var dir in searchDirs)
            {
                if (Directory.Exists(dir))
                {
                    try
                    {
                        var files = Directory.GetFiles(dir, ext, SearchOption.TopDirectoryOnly);
                        foreach (var f in files)
                        {
                            string fname = Path.GetFileName(f);
                            if (string.Equals(f, project.Path, StringComparison.OrdinalIgnoreCase)) continue;

                            if (fname.StartsWith(baseName, StringComparison.OrdinalIgnoreCase) || fname.Contains(baseName))
                            {
                                if (seen.Add(f))
                                {
                                    autoSaveFiles.Add(new FileInfo(f));
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            autoSaveFiles.Sort((a, b) => b.LastWriteTime.CompareTo(a.LastWriteTime));

            if (autoSaveFiles.Count == 0)
            {
                lstFiles.Items.Add("Автосохранения не найдены для данного проекта.");
                lblSelectedInfo.Text = "Проверены папки:\n" + string.Join("\n", searchDirs.ToArray());
            }
            else
            {
                foreach (var fi in autoSaveFiles)
                {
                    string sz = fi.Length < 1024 * 1024 ? string.Format("{0:F1} КБ", fi.Length / 1024.0) : string.Format("{0:F1} МБ", fi.Length / (1024.0 * 1024.0));
                    lstFiles.Items.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}  |  {1}  |  {2}", fi.LastWriteTime, sz, fi.Name));
                }
                lstFiles.SelectedIndex = 0;
            }
        }

        private void LstFiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            int idx = lstFiles.SelectedIndex;
            if (idx >= 0 && idx < autoSaveFiles.Count)
            {
                var fi = autoSaveFiles[idx];
                lblSelectedInfo.Text = "Полный путь:\n" + fi.FullName;
                btnLaunch.Enabled = true;
                btnRestoreOriginal.Enabled = true;
                btnShowFolder.Enabled = true;
            }
            else
            {
                lblSelectedInfo.Text = "";
                btnLaunch.Enabled = false;
                btnRestoreOriginal.Enabled = false;
                btnShowFolder.Enabled = false;
            }
        }

        private void LaunchSelectedAutoSave()
        {
            int idx = lstFiles.SelectedIndex;
            if (idx >= 0 && idx < autoSaveFiles.Count)
            {
                var fi = autoSaveFiles[idx];
                MainForm.GlobalInstance.LaunchSpecificFile(fi.FullName, project.Kind);
                this.Close();
            }
        }

        private void RestoreOriginal()
        {
            int idx = lstFiles.SelectedIndex;
            if (idx >= 0 && idx < autoSaveFiles.Count)
            {
                var fi = autoSaveFiles[idx];
                var dr = MessageBox.Show(string.Format("Текущий проект «{0}» будет перезаписан выбранной копией от {1:dd.MM.yyyy HH:mm:ss}.\nСоздать резервную копию оригинала перед заменой?", project.Name, fi.LastWriteTime),
                    "Подтверждение восстановления", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

                if (dr == DialogResult.Cancel) return;

                try
                {
                    if (dr == DialogResult.Yes && File.Exists(project.Path))
                    {
                        string backupPath = project.Path + ".backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        File.Copy(project.Path, backupPath, true);
                    }

                    File.Copy(fi.FullName, project.Path, true);
                    MessageBox.Show("Оригинал проекта успешно восстановлен из автосохранения!", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    MainForm.GlobalInstance.LaunchSpecificFile(project.Path, project.Kind);
                    this.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка при восстановлении:\n" + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ShowFolder()
        {
            int idx = lstFiles.SelectedIndex;
            if (idx >= 0 && idx < autoSaveFiles.Count)
            {
                try
                {
                    Process.Start("explorer.exe", string.Format("/select,\"{0}\"", autoSaveFiles[idx].FullName));
                }
                catch { }
            }
        }
    }

    public class NewProjectDialog : Form
    {
        private TextBox txtName;
        private TextBox txtFolder;
        private Button btnBrowse;
        private RadioButton rbPr;
        private RadioButton rbAe;
        private RadioButton rbBoth;
        private CheckBox chkStructure;
        private CheckBox chkOpenAfter;
        private Button btnCreate;
        private Button btnCancel;

        public NewProjectDialog(string defaultFolder)
        {
            this.Text = "Создать проект со структурой";
            this.Size = new Size(540, 360);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = UITheme.Bg;
            this.ForeColor = UITheme.TextPrimary;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            InitializeUI(defaultFolder);
        }

        private void InitializeUI(string defaultFolder)
        {
            var lbl1 = new Label { Text = "Название проекта:", Location = new Point(24, 20), AutoSize = true, ForeColor = UITheme.TextSecondary };
            txtName = new TextBox
            {
                Location = new Point(24, 44),
                Width = 475,
                BackColor = UITheme.CardBg,
                ForeColor = UITheme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10f),
                Text = "Новый_Проект_" + DateTime.Now.ToString("ddMMyy")
            };

            var lbl2 = new Label { Text = "Папка для размещения:", Location = new Point(24, 82), AutoSize = true, ForeColor = UITheme.TextSecondary };
            txtFolder = new TextBox
            {
                Location = new Point(24, 106),
                Width = 370,
                BackColor = UITheme.CardBg,
                ForeColor = UITheme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5f),
                Text = string.IsNullOrEmpty(defaultFolder) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : defaultFolder
            };

            btnBrowse = MainForm.CreateCustomButton("Обзор...", 95, UITheme.CardBg, UITheme.TextPrimary, UITheme.CardBorder, UITheme.CardHover);
            btnBrowse.Location = new Point(404, 104);
            btnBrowse.Height = 27;
            btnBrowse.Click += (s, e) =>
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    fbd.SelectedPath = txtFolder.Text;
                    if (fbd.ShowDialog() == DialogResult.OK)
                        txtFolder.Text = fbd.SelectedPath;
                }
            };

            var lbl3 = new Label { Text = "Тип проекта:", Location = new Point(24, 146), AutoSize = true, ForeColor = UITheme.TextSecondary };

            rbPr = new RadioButton { Text = "Premiere Pro (.prproj)", Location = new Point(24, 172), AutoSize = true, Checked = true, ForeColor = UITheme.TextPrimary };
            rbAe = new RadioButton { Text = "After Effects (.aep)", Location = new Point(200, 172), AutoSize = true, ForeColor = UITheme.TextPrimary };
            rbBoth = new RadioButton { Text = "Оба (Pr + Ae)", Location = new Point(360, 172), AutoSize = true, ForeColor = UITheme.TextPrimary };

            chkStructure = new CheckBox
            {
                Text = "Создать структуру папок (01_RAW, 02_AUDIO, 03_ASSETS, 04_PROJECT, 05_EXPORTS)",
                Location = new Point(24, 212),
                AutoSize = true,
                Checked = true,
                ForeColor = UITheme.TextSecondary
            };

            chkOpenAfter = new CheckBox
            {
                Text = "Открыть проект сразу после создания",
                Location = new Point(24, 238),
                AutoSize = true,
                Checked = true,
                ForeColor = UITheme.TextSecondary
            };

            btnCreate = MainForm.CreateCustomButton("Создать проект", 140, UITheme.Accent, Color.White, UITheme.Accent, UITheme.AccentHover);
            btnCreate.Location = new Point(235, 275);
            btnCreate.Height = 34;
            btnCreate.Click += BtnCreate_Click;

            btnCancel = MainForm.CreateCustomButton("Отмена", 100, UITheme.CardBg, UITheme.TextSecondary, UITheme.CardBorder, UITheme.CardHover);
            btnCancel.Location = new Point(385, 275);
            btnCancel.Height = 34;
            btnCancel.Click += (s, e) => this.Close();

            this.Controls.Add(lbl1);
            this.Controls.Add(txtName);
            this.Controls.Add(lbl2);
            this.Controls.Add(txtFolder);
            this.Controls.Add(btnBrowse);
            this.Controls.Add(lbl3);
            this.Controls.Add(rbPr);
            this.Controls.Add(rbAe);
            this.Controls.Add(rbBoth);
            this.Controls.Add(chkStructure);
            this.Controls.Add(chkOpenAfter);
            this.Controls.Add(btnCreate);
            this.Controls.Add(btnCancel);
        }

        private void BtnCreate_Click(object sender, EventArgs e)
        {
            string name = txtName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Введите название проекта.", "Предупреждение", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string baseDir = txtFolder.Text.Trim();
            if (!Directory.Exists(baseDir))
            {
                try { Directory.CreateDirectory(baseDir); }
                catch (Exception ex)
                {
                    MessageBox.Show("Не удалось создать папку:\n" + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            try
            {
                string projectDir = Path.Combine(baseDir, name);
                if (!Directory.Exists(projectDir)) Directory.CreateDirectory(projectDir);

                string filesFolder = projectDir;
                if (chkStructure.Checked)
                {
                    Directory.CreateDirectory(Path.Combine(projectDir, "01_RAW"));
                    Directory.CreateDirectory(Path.Combine(projectDir, "02_AUDIO"));
                    Directory.CreateDirectory(Path.Combine(projectDir, "03_ASSETS"));
                    filesFolder = Path.Combine(projectDir, "04_PROJECT");
                    Directory.CreateDirectory(filesFolder);
                    Directory.CreateDirectory(Path.Combine(projectDir, "05_EXPORTS"));
                }

                string prFile = null;
                string aeFile = null;

                if (rbPr.Checked || rbBoth.Checked)
                {
                    prFile = Path.Combine(filesFolder, name + ".prproj");
                    MainForm.CreateBlankPremiereProject(prFile);
                }

                if (rbAe.Checked || rbBoth.Checked)
                {
                    aeFile = Path.Combine(filesFolder, name + ".aep");
                    // Create an empty template or touch file
                    File.WriteAllBytes(aeFile, new byte[0]);
                }

                MainForm.GlobalInstance.RefreshProjects();

                if (chkOpenAfter.Checked)
                {
                    if (prFile != null && File.Exists(prFile))
                        MainForm.GlobalInstance.LaunchSpecificFile(prFile, AppKind.PR);
                    else if (aeFile != null && File.Exists(aeFile))
                        MainForm.GlobalInstance.LaunchSpecificFile(aeFile, AppKind.AE);
                }
                else
                {
                    MessageBox.Show(string.Format("Проект «{0}» успешно создан в папке:\n{1}", name, projectDir), "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка создания структуры:\n" + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    public class ExeConfigDialog : Form
    {
        private TextBox txtPr;
        private TextBox txtAe;
        private Button btnBrowsePr;
        private Button btnBrowseAe;
        private Button btnSave;
        private Button btnCancel;

        public ExeConfigDialog(string prPath, string aePath)
        {
            this.Text = "Настройки исполняемых файлов Adobe (.exe)";
            this.Size = new Size(620, 260);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = UITheme.Bg;
            this.ForeColor = UITheme.TextPrimary;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            InitializeUI(prPath, aePath);
        }

        private void InitializeUI(string prPath, string aePath)
        {
            var lbl1 = new Label { Text = "Adobe Premiere Pro (.exe):", Location = new Point(24, 20), AutoSize = true, ForeColor = UITheme.TextSecondary };
            txtPr = new TextBox
            {
                Location = new Point(24, 44),
                Width = 450,
                BackColor = UITheme.CardBg,
                ForeColor = UITheme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5f),
                Text = prPath ?? ""
            };
            btnBrowsePr = MainForm.CreateCustomButton("Обзор...", 95, UITheme.CardBg, UITheme.TextPrimary, UITheme.CardBorder, UITheme.CardHover);
            btnBrowsePr.Location = new Point(484, 42);
            btnBrowsePr.Height = 27;
            btnBrowsePr.Click += (s, e) =>
            {
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Filter = "Adobe Premiere Pro.exe|Adobe Premiere Pro.exe|Исполняемые файлы (*.exe)|*.exe";
                    if (dlg.ShowDialog() == DialogResult.OK) txtPr.Text = dlg.FileName;
                }
            };

            var lbl2 = new Label { Text = "Adobe After Effects (AfterFX.exe):", Location = new Point(24, 88), AutoSize = true, ForeColor = UITheme.TextSecondary };
            txtAe = new TextBox
            {
                Location = new Point(24, 112),
                Width = 450,
                BackColor = UITheme.CardBg,
                ForeColor = UITheme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5f),
                Text = aePath ?? ""
            };
            btnBrowseAe = MainForm.CreateCustomButton("Обзор...", 95, UITheme.CardBg, UITheme.TextPrimary, UITheme.CardBorder, UITheme.CardHover);
            btnBrowseAe.Location = new Point(484, 110);
            btnBrowseAe.Height = 27;
            btnBrowseAe.Click += (s, e) =>
            {
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Filter = "AfterFX.exe|AfterFX.exe|Исполняемые файлы (*.exe)|*.exe";
                    if (dlg.ShowDialog() == DialogResult.OK) txtAe.Text = dlg.FileName;
                }
            };

            btnSave = MainForm.CreateCustomButton("Сохранить", 120, UITheme.Accent, Color.White, UITheme.Accent, UITheme.AccentHover);
            btnSave.Location = new Point(334, 170);
            btnSave.Height = 34;
            btnSave.Click += (s, e) =>
            {
                MainForm.GlobalInstance.UpdateExePaths(txtPr.Text.Trim(), txtAe.Text.Trim());
                this.Close();
            };

            btnCancel = MainForm.CreateCustomButton("Отмена", 100, UITheme.CardBg, UITheme.TextSecondary, UITheme.CardBorder, UITheme.CardHover);
            btnCancel.Location = new Point(464, 170);
            btnCancel.Height = 34;
            btnCancel.Click += (s, e) => this.Close();

            this.Controls.Add(lbl1);
            this.Controls.Add(txtPr);
            this.Controls.Add(btnBrowsePr);
            this.Controls.Add(lbl2);
            this.Controls.Add(txtAe);
            this.Controls.Add(btnBrowseAe);
            this.Controls.Add(btnSave);
            this.Controls.Add(btnCancel);
        }
    }

    public class MainForm : Form
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_TEXT_COLOR = 36;

        public static MainForm GlobalInstance { get; private set; }

        private List<ProjectItem> allProjects = new List<ProjectItem>();
        private List<ProjectItem> filteredProjects = new List<ProjectItem>();
        private HashSet<string> pinnedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private string premiereExePath = "";
        private string afterEffectsExePath = "";
        private string configFilePath;
        private string activePrefsPath = "";

        // UI Controls
        private Panel headerPanel;
        private Label lblAppStatus;
        private Button btnConfigExe;

        private Panel filterPanel;
        private FilterTabBar filterTabBar;

        private Panel searchPanel;
        private ModernSearchBox searchBox;
        private Button btnNewProject;
        private Button btnCleanCache;
        private Button btnRefresh;

        private ProjectCardList cardList;

        private Panel footerPanel;
        private CheckBox chkDisableHome;
        private Label lblStats;
        private Button btnLaunchEmpty;

        public MainForm()
        {
            GlobalInstance = this;
            try { SetProcessDPIAware(); } catch { }

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string cfgDir = Path.Combine(appData, "PrLauncher");
            if (!Directory.Exists(cfgDir)) Directory.CreateDirectory(cfgDir);
            configFilePath = Path.Combine(cfgDir, "config.json");

            this.AllowDrop = true;
            this.DragEnter += MainForm_DragEnter;
            this.DragDrop += MainForm_DragDrop;

            InitializeComponent();
            SetAppIcon();
            this.Shown += (s, e) => this.ActiveControl = cardList;
            LoadConfig();
            DetectAdobeApps();
            RefreshProjects();
            CheckStartupTweak();
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                bool valid = false;
                foreach (var f in files)
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext == ".prproj" || ext == ".aep") { valid = true; break; }
                }
                e.Effect = valid ? DragDropEffects.Copy : DragDropEffects.None;
            }
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                int added = 0;
                foreach (var f in files)
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext == ".prproj" || ext == ".aep")
                    {
                        var fi = new FileInfo(f);
                        var item = new ProjectItem
                        {
                            Name = Path.GetFileNameWithoutExtension(f),
                            Path = f,
                            Kind = ext == ".aep" ? AppKind.AE : AppKind.PR,
                            LastModified = fi.LastWriteTime,
                            SizeBytes = fi.Length,
                            Exists = true,
                            IsPinned = true
                        };
                        pinnedPaths.Add(f);
                        allProjects.RemoveAll(x => string.Equals(x.Path, f, StringComparison.OrdinalIgnoreCase));
                        allProjects.Insert(0, item);
                        added++;
                    }
                }
                if (added > 0)
                {
                    SaveConfig();
                    SortProjects();
                    ApplyFilter();
                }
            }
        }

        private void SetAppIcon()
        {
            try
            {
                using (var bmp = new Bitmap(32, 32))
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    Rectangle r = new Rectangle(0, 0, 31, 31);
                    using (var path = UITheme.CreateRoundedRectangle(r, 6))
                    {
                        using (var lgb = new LinearGradientBrush(r, Color.FromArgb(67, 24, 110), Color.FromArgb(15, 30, 68), 45f))
                            g.FillPath(lgb, path);
                        using (var pen = new Pen(Color.FromArgb(168, 85, 247), 1.5f))
                            g.DrawPath(pen, path);
                    }
                    using (var font = new Font("Segoe UI", 10f, FontStyle.Bold))
                    using (var brush = new SolidBrush(Color.White))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString("Pr|Ae", font, brush, r, sf);
                    }
                    IntPtr hIcon = bmp.GetHicon();
                    this.Icon = Icon.FromHandle(hIcon);
                }
            }
            catch { }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int dark = 1;
                DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
                DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref dark, sizeof(int));
                int titleBg = 0x001A1412;
                DwmSetWindowAttribute(this.Handle, DWMWA_CAPTION_COLOR, ref titleBg, sizeof(int));
                int titleFg = 0x00F8FAFC;
                DwmSetWindowAttribute(this.Handle, DWMWA_TEXT_COLOR, ref titleFg, sizeof(int));
            }
            catch { }
        }

        private void InitializeComponent()
        {
            this.Text = "Adobe Studio Launcher — Premiere Pro & After Effects";
            this.Size = new Size(960, 700);
            this.MinimumSize = new Size(760, 520);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = UITheme.Bg;
            this.ForeColor = UITheme.TextPrimary;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.DoubleBuffered = true;

            // 1. Header Panel (Height: 80px)
            headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = UITheme.Bg,
                Padding = new Padding(20, 16, 20, 12)
            };
            headerPanel.Paint += HeaderPanel_Paint;
            headerPanel.Resize += (s, e) => LayoutHeaderControls();

            lblAppStatus = new Label
            {
                Text = "● Поиск Adobe CC...",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = UITheme.Green,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            btnConfigExe = CreateCustomButton("Настройки .exe...", 130, UITheme.CardBg, UITheme.TextSecondary, UITheme.CardBorder, UITheme.CardHover);
            btnConfigExe.Height = 32;
            btnConfigExe.Click += (s, e) =>
            {
                using (var dlg = new ExeConfigDialog(premiereExePath, afterEffectsExePath))
                {
                    dlg.ShowDialog(this);
                }
            };

            headerPanel.Controls.Add(lblAppStatus);
            headerPanel.Controls.Add(btnConfigExe);
            LayoutHeaderControls();

            // 2. Filter Tab Bar Panel (Height: 42px)
            filterPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 42,
                BackColor = UITheme.Bg,
                Padding = new Padding(20, 2, 20, 4)
            };

            filterTabBar = new FilterTabBar
            {
                Location = new Point(20, 2),
                Width = 400,
                Height = 36
            };
            filterTabBar.FilterChanged += (s, e) => ApplyFilter();
            filterPanel.Controls.Add(filterTabBar);

            // 3. Search & Action Panel (Height: 52px)
            searchPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = UITheme.Bg,
                Padding = new Padding(20, 6, 20, 10)
            };

            searchBox = new ModernSearchBox
            {
                Location = new Point(20, 6),
                Width = 340,
                Height = 36
            };
            searchBox.TextChangedEvent += (s, e) => ApplyFilter();

            btnNewProject = CreateCustomButton("+ Проект со структурой", 180, UITheme.Accent, Color.White, UITheme.Accent, UITheme.AccentHover);
            btnNewProject.Location = new Point(375, 6);
            btnNewProject.Height = 36;
            btnNewProject.Click += (s, e) =>
            {
                string defFolder = allProjects.Count > 0 && !string.IsNullOrEmpty(allProjects[0].Path)
                    ? Path.GetDirectoryName(allProjects[0].Path)
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                using (var dlg = new NewProjectDialog(defFolder))
                {
                    dlg.ShowDialog(this);
                }
            };

            btnCleanCache = CreateCustomButton("Кэш: 0 МБ", 130, UITheme.CardBg, UITheme.TextPrimary, UITheme.CardBorder, UITheme.CardHover);
            btnCleanCache.Location = new Point(568, 6);
            btnCleanCache.Height = 36;
            btnCleanCache.Click += BtnCleanCache_Click;

            btnRefresh = CreateCustomButton("Обновить", 100, UITheme.CardBg, UITheme.TextPrimary, UITheme.CardBorder, UITheme.CardHover);
            btnRefresh.Location = new Point(710, 6);
            btnRefresh.Height = 36;
            btnRefresh.Click += (s, e) => RefreshProjects();

            searchPanel.Controls.Add(searchBox);
            searchPanel.Controls.Add(btnNewProject);
            searchPanel.Controls.Add(btnCleanCache);
            searchPanel.Controls.Add(btnRefresh);

            // 4. Project Card List
            cardList = new ProjectCardList
            {
                Dock = DockStyle.Fill
            };
            cardList.ProjectLaunched += (s, p) => LaunchProject(p);
            cardList.PinToggled += (s, p) => TogglePin(p);
            cardList.ProjectRemoveRequested += (s, p) => RemoveFromRecent(p);
            cardList.AutoSaveRecoveryRequested += (s, p) =>
            {
                using (var dlg = new AutoSaveRecoveryDialog(p))
                {
                    dlg.ShowDialog(this);
                }
            };

            // 5. Footer Panel (Height: 48px)
            footerPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                BackColor = UITheme.Bg,
                Padding = new Padding(20, 10, 20, 10)
            };
            footerPanel.Paint += (s, e) =>
            {
                using (var pen = new Pen(UITheme.CardBorder, 1f))
                    e.Graphics.DrawLine(pen, 0, 0, footerPanel.Width, 0);
            };

            chkDisableHome = new CheckBox
            {
                Text = "Отключить медленный начальный экран",
                AutoSize = true,
                ForeColor = UITheme.TextSecondary,
                Location = new Point(20, 14),
                Cursor = Cursors.Hand
            };
            chkDisableHome.CheckedChanged += ChkDisableHome_CheckedChanged;

            lblStats = new Label
            {
                Text = "Проектов: 0",
                AutoSize = true,
                ForeColor = UITheme.TextDim,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(340, 15)
            };

            btnLaunchEmpty = CreateCustomButton("▶ Запустить Premiere Pro", 195, UITheme.CardBg, UITheme.TextPrimary, UITheme.CardBorder, UITheme.CardHover);
            btnLaunchEmpty.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnLaunchEmpty.Location = new Point(735, 8);
            btnLaunchEmpty.Height = 32;
            btnLaunchEmpty.Click += (s, e) =>
            {
                if (filterTabBar.Mode == FilterTabBar.FilterMode.AE)
                    LaunchEmptyAE();
                else
                    LaunchEmptyPR();
            };

            footerPanel.Controls.Add(chkDisableHome);
            footerPanel.Controls.Add(lblStats);
            footerPanel.Controls.Add(btnLaunchEmpty);

            // Add all controls to Form
            this.Controls.Add(cardList);
            this.Controls.Add(searchPanel);
            this.Controls.Add(filterPanel);
            this.Controls.Add(headerPanel);
            this.Controls.Add(footerPanel);
        }

        private void LayoutHeaderControls()
        {
            if (btnConfigExe == null || lblAppStatus == null) return;
            btnConfigExe.Location = new Point(headerPanel.Width - btnConfigExe.Width - 20, 22);
            lblAppStatus.Location = new Point(btnConfigExe.Left - lblAppStatus.Width - 16, 28);
        }

        private void HeaderPanel_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 1. Dual Logo Badges (Pr and Ae)
            Rectangle prRect = new Rectangle(20, 18, 36, 42);
            using (var path = UITheme.CreateRoundedRectangle(prRect, 6))
            {
                using (var lgb = new LinearGradientBrush(prRect, UITheme.PrBgTop, UITheme.PrBgBottom, 45f))
                    g.FillPath(lgb, path);
                using (var pen = new Pen(UITheme.PrBorder, 1.2f))
                    g.DrawPath(pen, path);
            }
            using (var font = new Font("Segoe UI", 12f, FontStyle.Bold))
            using (var brush = new SolidBrush(UITheme.PrText))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString("Pr", font, brush, prRect, sf);
            }

            Rectangle aeRect = new Rectangle(58, 18, 36, 42);
            using (var path = UITheme.CreateRoundedRectangle(aeRect, 6))
            {
                using (var lgb = new LinearGradientBrush(aeRect, UITheme.AeBgTop, UITheme.AeBgBottom, 45f))
                    g.FillPath(lgb, path);
                using (var pen = new Pen(UITheme.AeBorder, 1.2f))
                    g.DrawPath(pen, path);
            }
            using (var font = new Font("Segoe UI", 12f, FontStyle.Bold))
            using (var brush = new SolidBrush(UITheme.AeText))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString("Ae", font, brush, aeRect, sf);
            }

            // 2. Title & Version Tag
            int titleX = 106;
            using (var font = new Font("Segoe UI", 15f, FontStyle.Bold))
            using (var brush = new SolidBrush(UITheme.TextPrimary))
            {
                g.DrawString("Adobe Studio Launcher", font, brush, titleX, 15);
            }

            // Version Pill
            Rectangle verRect = new Rectangle(titleX + 225, 20, 48, 19);
            using (var path = UITheme.CreateRoundedRectangle(verRect, 6))
            {
                using (var brush = new SolidBrush(Color.FromArgb(46, 16, 101)))
                    g.FillPath(brush, path);
                using (var pen = new Pen(Color.FromArgb(124, 58, 237), 1f))
                    g.DrawPath(pen, path);
            }
            using (var font = new Font("Segoe UI", 8.2f, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.FromArgb(192, 132, 252)))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString("v1.4.0", font, brush, verRect, sf);
            }

            // 3. Subtitle
            using (var font = new Font("Segoe UI", 9f, FontStyle.Regular))
            using (var brush = new SolidBrush(UITheme.TextSecondary))
            {
                g.DrawString("Быстрый запуск Premiere Pro и After Effects без начального экрана", font, brush, titleX, 44);
            }
        }

        public static Button CreateCustomButton(string text, int width, Color bg, Color fg, Color border, Color hoverBg)
        {
            var btn = new Button
            {
                Text = text,
                Width = width,
                BackColor = bg,
                ForeColor = fg,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.2f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btn.FlatAppearance.BorderColor = border;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = hoverBg;

            btn.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
                using (var path = UITheme.CreateRoundedRectangle(r, 6))
                {
                    using (var pen = new Pen(border, 1f))
                        e.Graphics.DrawPath(pen, path);
                }
            };

            return btn;
        }

        public void UpdateExePaths(string pr, string ae)
        {
            premiereExePath = pr;
            afterEffectsExePath = ae;
            SaveConfig();
            UpdateAppStatus();
        }

        private void DetectAdobeApps()
        {
            // Premiere Detection
            if (string.IsNullOrEmpty(premiereExePath) || !File.Exists(premiereExePath))
            {
                premiereExePath = FindPremiereExe();
            }

            // After Effects Detection
            if (string.IsNullOrEmpty(afterEffectsExePath) || !File.Exists(afterEffectsExePath))
            {
                afterEffectsExePath = FindAfterEffectsExe();
            }

            UpdateAppStatus();
        }

        private string FindPremiereExe()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Adobe\Premiere Pro"))
                {
                    if (key != null)
                    {
                        var subKeyNames = key.GetSubKeyNames();
                        Array.Sort(subKeyNames);
                        Array.Reverse(subKeyNames);
                        foreach (var name in subKeyNames)
                        {
                            using (var subKey = key.OpenSubKey(name))
                            {
                                if (subKey != null)
                                {
                                    string dir = subKey.GetValue("ApplicationPath") as string;
                                    if (!string.IsNullOrEmpty(dir))
                                    {
                                        string exe = Path.Combine(dir, "Adobe Premiere Pro.exe");
                                        if (File.Exists(exe)) return exe;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            string[] versions = new string[] { "2026", "2025", "2024", "2023", "2022", "2021", "2020", "CC" };
            string[] drives = new string[] { "C:", "D:", "E:" };
            foreach (var d in drives)
            {
                foreach (var v in versions)
                {
                    string candidate = string.Format(@"{0}\Program Files\Adobe\Adobe Premiere Pro {1}\Adobe Premiere Pro.exe", d, v);
                    if (File.Exists(candidate)) return candidate;
                }
            }
            return "";
        }

        private string FindAfterEffectsExe()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Adobe\After Effects"))
                {
                    if (key != null)
                    {
                        var subKeyNames = key.GetSubKeyNames();
                        Array.Sort(subKeyNames);
                        Array.Reverse(subKeyNames);
                        foreach (var name in subKeyNames)
                        {
                            using (var subKey = key.OpenSubKey(name))
                            {
                                if (subKey != null)
                                {
                                    string dir = subKey.GetValue("ApplicationPath") as string;
                                    if (!string.IsNullOrEmpty(dir))
                                    {
                                        string exe = Path.Combine(dir, "Support Files", "AfterFX.exe");
                                        if (File.Exists(exe)) return exe;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            string[] versions = new string[] { "2026", "2025", "2024", "2023", "2022", "2021", "2020", "CC" };
            string[] drives = new string[] { "C:", "D:", "E:" };
            foreach (var d in drives)
            {
                foreach (var v in versions)
                {
                    string candidate = string.Format(@"{0}\Program Files\Adobe\Adobe After Effects {1}\Support Files\AfterFX.exe", d, v);
                    if (File.Exists(candidate)) return candidate;
                }
            }
            return "";
        }

        private void UpdateAppStatus()
        {
            bool hasPr = !string.IsNullOrEmpty(premiereExePath) && File.Exists(premiereExePath);
            bool hasAe = !string.IsNullOrEmpty(afterEffectsExePath) && File.Exists(afterEffectsExePath);

            string prVer = "Pr";
            if (hasPr)
            {
                if (premiereExePath.Contains("2026")) prVer = "Pr 2026";
                else if (premiereExePath.Contains("2025")) prVer = "Pr 2025";
                else if (premiereExePath.Contains("2024")) prVer = "Pr 2024";
            }

            string aeVer = "Ae";
            if (hasAe)
            {
                if (afterEffectsExePath.Contains("2026")) aeVer = "Ae 2026";
                else if (afterEffectsExePath.Contains("2025")) aeVer = "Ae 2025";
                else if (afterEffectsExePath.Contains("2024")) aeVer = "Ae 2024";
            }

            if (hasPr && hasAe)
            {
                lblAppStatus.Text = string.Format("● {0}  ● {1}", prVer, aeVer);
                lblAppStatus.ForeColor = UITheme.Green;
            }
            else if (hasPr)
            {
                lblAppStatus.Text = string.Format("● {0}  ✕ Ae не найден", prVer);
                lblAppStatus.ForeColor = Color.FromArgb(234, 179, 8);
            }
            else if (hasAe)
            {
                lblAppStatus.Text = string.Format("✕ Pr не найден  ● {0}", aeVer);
                lblAppStatus.ForeColor = Color.FromArgb(234, 179, 8);
            }
            else
            {
                lblAppStatus.Text = "✕ Приложения Adobe не найдены";
                lblAppStatus.ForeColor = UITheme.Red;
            }

            LayoutHeaderControls();
        }

        public void RefreshProjects()
        {
            allProjects.Clear();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Premiere Pro Preferences MRU
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
                        ParsePremierePrefsMru(pref, seenPaths);
                    }
                }
                catch { }

                try
                {
                    var directProjects = Directory.GetFiles(prDocs, "*.prproj", SearchOption.TopDirectoryOnly);
                    foreach (var dp in directProjects)
                    {
                        AddProjectItem(dp, AppKind.PR, seenPaths);
                    }
                }
                catch { }
            }

            // 2. After Effects Preferences & Recent
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string aeAppData = Path.Combine(appData, "Adobe", "After Effects");
            if (Directory.Exists(aeAppData))
            {
                try
                {
                    var aePrefs = Directory.GetFiles(aeAppData, "*Prefs*.txt", SearchOption.AllDirectories);
                    foreach (var ap in aePrefs)
                    {
                        ParseAfterEffectsPrefs(ap, seenPaths);
                    }
                    var aeRusPrefs = Directory.GetFiles(aeAppData, "*Установки*.txt", SearchOption.AllDirectories);
                    foreach (var ap in aeRusPrefs)
                    {
                        ParseAfterEffectsPrefs(ap, seenPaths);
                    }
                }
                catch { }
            }

            // 3. Scan Common User Render/Projects Folders (e.g. E:\render)
            string[] commonFolders = new string[] { @"E:\render", @"D:\render", Path.Combine(docs, "Adobe") };
            foreach (var cf in commonFolders)
            {
                if (Directory.Exists(cf))
                {
                    try
                    {
                        var prFiles = Directory.GetFiles(cf, "*.prproj", SearchOption.TopDirectoryOnly);
                        foreach (var f in prFiles) AddProjectItem(f, AppKind.PR, seenPaths);

                        var aeFiles = Directory.GetFiles(cf, "*.aep", SearchOption.TopDirectoryOnly);
                        foreach (var f in aeFiles) AddProjectItem(f, AppKind.AE, seenPaths);
                    }
                    catch { }
                }
            }

            // 4. Also keep pinned projects even if not in MRU
            foreach (var p in pinnedPaths)
            {
                if (!seenPaths.Contains(p) && File.Exists(p))
                {
                    string ext = Path.GetExtension(p).ToLowerInvariant();
                    AddProjectItem(p, ext == ".aep" ? AppKind.AE : AppKind.PR, seenPaths);
                }
            }

            SortProjects();
            ApplyFilter();
            UpdateCacheSizeStats();
        }

        private void AddProjectItem(string path, AppKind kind, HashSet<string> seenPaths)
        {
            if (string.IsNullOrEmpty(path) || !seenPaths.Add(path)) return;

            bool exists = File.Exists(path);
            DateTime dt = DateTime.MinValue;
            long size = 0;

            if (exists)
            {
                try
                {
                    var fi = new FileInfo(path);
                    dt = fi.LastWriteTime;
                    size = fi.Length;
                }
                catch { }
            }

            allProjects.Add(new ProjectItem
            {
                Name = Path.GetFileNameWithoutExtension(path),
                Path = path,
                Kind = kind,
                LastModified = dt == DateTime.MinValue ? DateTime.Now : dt,
                SizeBytes = size,
                Exists = exists,
                IsPinned = pinnedPaths.Contains(path)
            });
        }

        private void ParsePremierePrefsMru(string prefsPath, HashSet<string> seenPaths)
        {
            try
            {
                string text = File.ReadAllText(prefsPath);
                var matches = Regex.Matches(text, @"<BE\.Prefs\.MRU\.Document\.(\d+)>(.*?)</BE\.Prefs\.MRU\.Document\.\1>");

                foreach (Match m in matches)
                {
                    string cleanPath = m.Groups[2].Value.Trim();
                    if (cleanPath.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
                        cleanPath = @"\\" + cleanPath.Substring(8);
                    else if (cleanPath.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
                        cleanPath = cleanPath.Substring(4);

                    AddProjectItem(cleanPath, AppKind.PR, seenPaths);
                }
            }
            catch { }
        }

        private void ParseAfterEffectsPrefs(string prefsPath, HashSet<string> seenPaths)
        {
            try
            {
                string[] lines = File.ReadAllLines(prefsPath);
                foreach (var line in lines)
                {
                    if (line.Contains(".aep") || line.Contains(".AEP"))
                    {
                        var m = Regex.Match(line, @"""(.*?[.]aep)""", RegexOptions.IgnoreCase);
                        if (m.Success)
                        {
                            string p = m.Groups[1].Value.Trim().Replace(@"\\", @"\");
                            if (File.Exists(p)) AddProjectItem(p, AppKind.AE, seenPaths);
                        }
                    }
                }
            }
            catch { }
        }

        private void SortProjects()
        {
            allProjects.Sort((a, b) =>
            {
                if (a.IsPinned != b.IsPinned) return b.IsPinned.CompareTo(a.IsPinned);
                return b.LastModified.CompareTo(a.LastModified);
            });
        }

        private void ApplyFilter()
        {
            string q = searchBox.SearchText.Trim().ToLowerInvariant();
            var mode = filterTabBar.Mode;

            filteredProjects = allProjects.FindAll(p =>
            {
                if (mode == FilterTabBar.FilterMode.PR && p.Kind != AppKind.PR) return false;
                if (mode == FilterTabBar.FilterMode.AE && p.Kind != AppKind.AE) return false;

                if (!string.IsNullOrEmpty(q))
                {
                    if (!p.Name.ToLowerInvariant().Contains(q) && !p.Path.ToLowerInvariant().Contains(q))
                        return false;
                }
                return true;
            });

            int totalAll = allProjects.Count;
            int totalPr = allProjects.FindAll(p => p.Kind == AppKind.PR).Count;
            int totalAe = allProjects.FindAll(p => p.Kind == AppKind.AE).Count;
            filterTabBar.SetCounts(totalAll, totalPr, totalAe);

            cardList.Items = filteredProjects;
            lblStats.Text = string.Format("Проектов: {0} из {1}", filteredProjects.Count, allProjects.Count);

            if (mode == FilterTabBar.FilterMode.AE)
                btnLaunchEmpty.Text = "▶  Запустить After Effects";
            else
                btnLaunchEmpty.Text = "▶  Запустить Premiere Pro";
        }

        private void TogglePin(ProjectItem p)
        {
            p.IsPinned = !p.IsPinned;
            if (p.IsPinned) pinnedPaths.Add(p.Path);
            else pinnedPaths.Remove(p.Path);

            SaveConfig();
            SortProjects();
            ApplyFilter();
        }

        private void RemoveFromRecent(ProjectItem p)
        {
            allProjects.Remove(p);
            pinnedPaths.Remove(p.Path);
            SaveConfig();
            ApplyFilter();
        }

        public void LaunchProject(ProjectItem p)
        {
            LaunchSpecificFile(p.Path, p.Kind);
        }

        public void LaunchSpecificFile(string filePath, AppKind kind)
        {
            string exe = (kind == AppKind.AE) ? afterEffectsExePath : premiereExePath;
            string appName = (kind == AppKind.AE) ? "After Effects" : "Premiere Pro";

            if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
            {
                DetectAdobeApps();
                exe = (kind == AppKind.AE) ? afterEffectsExePath : premiereExePath;
                if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
                {
                    MessageBox.Show(string.Format("Не удалось найти исполняемый файл {0}.\nПожалуйста, укажите путь в «Настройки .exe...».", appName),
                        "Предупреждение", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            try
            {
                var psi = new ProcessStartInfo();
                psi.FileName = exe;
                if (!string.IsNullOrEmpty(filePath))
                {
                    if (!File.Exists(filePath))
                    {
                        var dr = MessageBox.Show(string.Format("Файл проекта не найден:\n{0}\n\nОткрыть пустое приложение {1}?", filePath, appName),
                            "Файл отсутствует", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (dr != DialogResult.Yes) return;
                    }
                    else
                    {
                        psi.Arguments = string.Format("\"{0}\"", filePath);
                    }
                }
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("Ошибка запуска {0}:\n{1}", appName, ex.Message), "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void LaunchEmptyPR()
        {
            LaunchSpecificFile(null, AppKind.PR);
        }

        public void LaunchEmptyAE()
        {
            LaunchSpecificFile(null, AppKind.AE);
        }

        public static void CreateBlankPremiereProject(string targetPath)
        {
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
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            var cacheDirs = new List<string>
            {
                Path.Combine(appData, "Adobe", "Common", "Media Cache Files"),
                Path.Combine(appData, "Adobe", "Common", "Media Cache"),
                Path.Combine(appData, "Adobe", "Common", "Peak Files")
            };

            // After Effects Cache
            string aeDir = Path.Combine(appData, "Adobe", "After Effects");
            if (Directory.Exists(aeDir))
            {
                try
                {
                    var sub = Directory.GetDirectories(aeDir, "Cache", SearchOption.AllDirectories);
                    cacheDirs.AddRange(sub);
                    var subDisk = Directory.GetDirectories(aeDir, "Disk Cache", SearchOption.AllDirectories);
                    cacheDirs.AddRange(subDisk);
                }
                catch { }
            }

            string aeLocalDir = Path.Combine(localAppData, "Adobe", "After Effects");
            if (Directory.Exists(aeLocalDir))
            {
                try
                {
                    var subLocal = Directory.GetDirectories(aeLocalDir, "Disk Cache", SearchOption.AllDirectories);
                    cacheDirs.AddRange(subLocal);
                }
                catch { }
            }

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

            double mb = totalBytes / (1024.0 * 1024.0);
            string msg = string.Format("Найдено файлов кэша Premiere Pro и After Effects: {0}\nОбщий объём: {1:F1} МБ ({2:F2} ГБ)\n\nБудут очищены:\n- Media Cache Files (.cfa)\n- Peak Files (.pek)\n- Media Cache (.mcdb)\n- After Effects Disk Cache\n\nОчистить кэш сейчас? (Premiere Pro и After Effects должны быть закрыты)",
                totalFiles, mb, mb / 1024.0);

            var dr = MessageBox.Show(msg, "Очистка кэша Adobe CC", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (dr == DialogResult.Yes)
            {
                int deleted = 0;
                foreach (var d in cacheDirs)
                {
                    if (Directory.Exists(d))
                    {
                        try
                        {
                            var files = Directory.GetFiles(d, "*.*", SearchOption.AllDirectories);
                            foreach (var f in files)
                            {
                                try { File.Delete(f); deleted++; } catch { }
                            }
                        }
                        catch { }
                    }
                }
                MessageBox.Show(string.Format("Успешно удалено файлов кэша: {0}", deleted), "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
                UpdateCacheSizeStats();
            }
        }

        private void UpdateCacheSizeStats()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                var cacheDirs = new List<string>
                {
                    Path.Combine(appData, "Adobe", "Common", "Media Cache Files"),
                    Path.Combine(appData, "Adobe", "Common", "Media Cache"),
                    Path.Combine(appData, "Adobe", "Common", "Peak Files")
                };

                string aeDir = Path.Combine(appData, "Adobe", "After Effects");
                if (Directory.Exists(aeDir))
                {
                    try
                    {
                        var sub = Directory.GetDirectories(aeDir, "Cache", SearchOption.AllDirectories);
                        cacheDirs.AddRange(sub);
                    }
                    catch { }
                }

                long totalBytes = 0;
                foreach (var d in cacheDirs)
                {
                    if (Directory.Exists(d))
                    {
                        var files = Directory.GetFiles(d, "*.*", SearchOption.TopDirectoryOnly);
                        foreach (var f in files)
                        {
                            try { totalBytes += new FileInfo(f).Length; } catch { }
                        }
                    }
                }
                double mb = totalBytes / (1024.0 * 1024.0);
                if (mb > 1024)
                    btnCleanCache.Text = string.Format("Кэш: {0:F1} ГБ", mb / 1024.0);
                else
                    btnCleanCache.Text = string.Format("Кэш: {0:F1} МБ", mb);
            }
            catch
            {
                btnCleanCache.Text = "Очистить кэш";
            }
        }

        private void CheckStartupTweak()
        {
            if (string.IsNullOrEmpty(activePrefsPath) || !File.Exists(activePrefsPath)) return;
            try
            {
                string text = File.ReadAllText(activePrefsPath);
                chkDisableHome.CheckedChanged -= ChkDisableHome_CheckedChanged;
                chkDisableHome.Checked = text.Contains("<MZ.Prefs.ShowQuickstartDialog>false</MZ.Prefs.ShowQuickstartDialog>");
                chkDisableHome.CheckedChanged += ChkDisableHome_CheckedChanged;
            }
            catch { }
        }

        private void ChkDisableHome_CheckedChanged(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(activePrefsPath) || !File.Exists(activePrefsPath))
            {
                MessageBox.Show("Файл настроек Premiere Pro пока не найден. Откройте Premiere Pro хотя бы раз.", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                string text = File.ReadAllText(activePrefsPath);
                string val = chkDisableHome.Checked ? "false" : "true";

                if (text.Contains("<MZ.Prefs.ShowQuickstartDialog>"))
                {
                    text = Regex.Replace(text, @"<MZ\.Prefs\.ShowQuickstartDialog>(.*?)</MZ\.Prefs\.ShowQuickstartDialog>",
                        string.Format("<MZ.Prefs.ShowQuickstartDialog>{0}</MZ.Prefs.ShowQuickstartDialog>", val));
                }
                else
                {
                    int idx = text.IndexOf("</PremiereData>");
                    if (idx > 0)
                    {
                        string inject = string.Format("\t<MZ.Prefs.ShowQuickstartDialog>{0}</MZ.Prefs.ShowQuickstartDialog>\r\n</PremiereData>", val);
                        text = text.Substring(0, idx) + inject;
                    }
                }

                File.WriteAllText(activePrefsPath, text);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось изменить настройки:\n" + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
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

                    var aeMatch = Regex.Match(json, @"""afterEffectsExe"":\s*""(.*?)""");
                    if (aeMatch.Success) afterEffectsExePath = aeMatch.Groups[1].Value.Replace(@"\\", @"\");

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
                sb.AppendLine(string.Format("  \"premiereExe\": \"{0}\",", (premiereExePath ?? "").Replace(@"\", @"\\").Replace("\"", "\\\"")));
                sb.AppendLine(string.Format("  \"afterEffectsExe\": \"{0}\",", (afterEffectsExePath ?? "").Replace(@"\", @"\\").Replace("\"", "\\\"")));
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

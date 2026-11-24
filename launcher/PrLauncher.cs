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
            double angle = -Math.PI / 2.0; // start at top point
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
                    // Click track to jump
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

            var itmOpen = menu.Items.Add("▶  Открыть в Premiere Pro");
            itmOpen.Click += (s, e) => { if (ProjectLaunched != null) ProjectLaunched(this, p); };

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

            // 3. Pin Star Button (crisp vector polygon star!)
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

            // 4. Project Icon Badge (38x38 square with rounded corners)
            Rectangle iconRect = new Rectangle(r.X + 44, r.Y + 15, 38, 38);
            using (var path = UITheme.CreateRoundedRectangle(iconRect, 7))
            {
                using (var lgb = new LinearGradientBrush(iconRect, Color.FromArgb(59, 24, 95), Color.FromArgb(36, 17, 66), 45f))
                    g.FillPath(lgb, path);
                using (var pen = new Pen(Color.FromArgb(107, 33, 168), 1f))
                    g.DrawPath(pen, path);
            }
            using (var font = new Font("Segoe UI", 11.5f, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.FromArgb(233, 213, 255)))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString("Pr", font, brush, iconRect, sf);
            }

            // 5. Project Name & Path (Spacious two-row typography!)
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

            // Status Pill Badge (Available / Missing)
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

            // Date (to the left of status badge)
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

            // Draw crisp vector search icon
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
                g.DrawString("Попробуйте изменить поисковый запрос или создайте новый проект кнопкой «+ Новый проект»", font, brush, new Rectangle(0, cy + 38, r.Width, 22), sf);
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
            this.Size = new Size(380, 36);
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
                Text = "Поиск по названию или пути проекта...",
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
                SendMessage(innerText.Handle, EM_SETCUEBANNER, 1, "Поиск по названию или пути проекта...");
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

            // Crisp vector magnifying glass
            using (var pen = new Pen(isFocused ? UITheme.Accent : UITheme.TextDim, 1.6f))
            {
                g.DrawEllipse(pen, 11, 11, 11, 11);
                g.DrawLine(pen, 20, 20, 25, 25);
            }

            // Draw Clear Button (✕)
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

        private List<ProjectItem> allProjects = new List<ProjectItem>();
        private List<ProjectItem> filteredProjects = new List<ProjectItem>();
        private HashSet<string> pinnedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private string premiereExePath = "";
        private string configFilePath;
        private string activePrefsPath = "";

        // UI Controls
        private Panel headerPanel;
        private Label lblPremiereStatus;
        private Button btnBrowsePr;

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
            try { SetProcessDPIAware(); } catch { }

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string cfgDir = Path.Combine(appData, "PrLauncher");
            if (!Directory.Exists(cfgDir)) Directory.CreateDirectory(cfgDir);
            configFilePath = Path.Combine(cfgDir, "config.json");

            InitializeComponent();
            SetAppIcon();
            this.Shown += (s, e) => this.ActiveControl = cardList;
            LoadConfig();
            DetectPremiere();
            RefreshProjects();
            CheckStartupTweak();
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
                        using (var lgb = new LinearGradientBrush(r, Color.FromArgb(67, 24, 110), Color.FromArgb(36, 17, 66), 45f))
                            g.FillPath(lgb, path);
                        using (var pen = new Pen(Color.FromArgb(168, 85, 247), 1.5f))
                            g.DrawPath(pen, path);
                    }
                    using (var font = new Font("Segoe UI", 12f, FontStyle.Bold))
                    using (var brush = new SolidBrush(Color.FromArgb(245, 208, 254)))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString("Pr", font, brush, r, sf);
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
                int titleBg = 0x001A1412; // COLORREF: 0x00BBGGRR -> #12141a
                DwmSetWindowAttribute(this.Handle, DWMWA_CAPTION_COLOR, ref titleBg, sizeof(int));
                int titleFg = 0x00F8FAFC;
                DwmSetWindowAttribute(this.Handle, DWMWA_TEXT_COLOR, ref titleFg, sizeof(int));
            }
            catch { }
        }

        private void InitializeComponent()
        {
            this.Text = "PrLauncher — Быстрый запуск Premiere Pro";
            this.Size = new Size(940, 660);
            this.MinimumSize = new Size(740, 500);
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

            lblPremiereStatus = new Label
            {
                Text = "● Поиск Premiere Pro...",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = UITheme.Green,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            btnBrowsePr = CreateStyledButton("Сменить .exe", 105, UITheme.CardBg, UITheme.TextSecondary, UITheme.CardBorder, UITheme.CardHover);
            btnBrowsePr.Height = 32;
            btnBrowsePr.Click += BtnBrowsePr_Click;

            headerPanel.Controls.Add(lblPremiereStatus);
            headerPanel.Controls.Add(btnBrowsePr);
            LayoutHeaderControls();

            // 2. Search & Action Panel (Height: 52px)
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
                Width = 380,
                Height = 36
            };
            searchBox.TextChangedEvent += (s, e) => ApplyFilter();

            btnNewProject = CreateStyledButton("+ Новый проект", 145, UITheme.Accent, Color.White, UITheme.Accent, UITheme.AccentHover);
            btnNewProject.Location = new Point(415, 6);
            btnNewProject.Height = 36;
            btnNewProject.Click += BtnNewProject_Click;

            btnCleanCache = CreateStyledButton("Очистить кэш", 130, UITheme.CardBg, UITheme.TextPrimary, UITheme.CardBorder, UITheme.CardHover);
            btnCleanCache.Location = new Point(570, 6);
            btnCleanCache.Height = 36;
            btnCleanCache.Click += BtnCleanCache_Click;

            btnRefresh = CreateStyledButton("Обновить", 105, UITheme.CardBg, UITheme.TextPrimary, UITheme.CardBorder, UITheme.CardHover);
            btnRefresh.Location = new Point(710, 6);
            btnRefresh.Height = 36;
            btnRefresh.Click += (s, e) => RefreshProjects();

            searchPanel.Controls.Add(searchBox);
            searchPanel.Controls.Add(btnNewProject);
            searchPanel.Controls.Add(btnCleanCache);
            searchPanel.Controls.Add(btnRefresh);

            // 3. Project Card List
            cardList = new ProjectCardList
            {
                Dock = DockStyle.Fill
            };
            cardList.ProjectLaunched += (s, p) => LaunchProject(p.Path);
            cardList.PinToggled += (s, p) => TogglePin(p);
            cardList.ProjectRemoveRequested += (s, p) => RemoveFromRecent(p);

            // 4. Footer Panel (Height: 48px)
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
                Text = "Отключить начальный экран в Premiere Pro",
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
                Location = new Point(370, 15)
            };

            btnLaunchEmpty = CreateStyledButton("▶  Открыть Premiere Pro", 195, UITheme.CardBg, UITheme.TextPrimary, UITheme.CardBorder, UITheme.CardHover);
            btnLaunchEmpty.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnLaunchEmpty.Location = new Point(720, 8);
            btnLaunchEmpty.Height = 32;
            btnLaunchEmpty.Click += (s, e) => LaunchProject(null);

            footerPanel.Controls.Add(chkDisableHome);
            footerPanel.Controls.Add(lblStats);
            footerPanel.Controls.Add(btnLaunchEmpty);

            // Add all to Form
            this.Controls.Add(cardList);
            this.Controls.Add(searchPanel);
            this.Controls.Add(headerPanel);
            this.Controls.Add(footerPanel);
        }

        private void LayoutHeaderControls()
        {
            if (btnBrowsePr == null || lblPremiereStatus == null) return;
            btnBrowsePr.Location = new Point(headerPanel.Width - btnBrowsePr.Width - 20, 22);
            lblPremiereStatus.Location = new Point(btnBrowsePr.Left - lblPremiereStatus.Width - 16, 28);
        }

        private void HeaderPanel_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 1. Logo Badge (42x42 square)
            Rectangle logoRect = new Rectangle(20, 18, 42, 42);
            using (var path = UITheme.CreateRoundedRectangle(logoRect, 8))
            {
                using (var lgb = new LinearGradientBrush(logoRect, Color.FromArgb(67, 24, 110), Color.FromArgb(36, 17, 66), 45f))
                    g.FillPath(lgb, path);
                using (var pen = new Pen(Color.FromArgb(139, 92, 246), 1.5f))
                    g.DrawPath(pen, path);
            }
            using (var font = new Font("Segoe UI", 13.5f, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.FromArgb(245, 208, 254)))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString("Pr", font, brush, logoRect, sf);
            }

            // 2. Title & Version Tag
            int titleX = 72;
            using (var font = new Font("Segoe UI", 16f, FontStyle.Bold))
            using (var brush = new SolidBrush(UITheme.TextPrimary))
            {
                g.DrawString("PrLauncher", font, brush, titleX, 15);
            }

            // Version Pill
            Rectangle verRect = new Rectangle(titleX + 130, 22, 46, 19);
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
                g.DrawString("v1.3.0", font, brush, verRect, sf);
            }

            // 3. Subtitle
            using (var font = new Font("Segoe UI", 9f, FontStyle.Regular))
            using (var brush = new SolidBrush(UITheme.TextSecondary))
            {
                g.DrawString("Мгновенный запуск проектов в обход медленного начального экрана", font, brush, titleX, 44);
            }
        }

        private Button CreateStyledButton(string text, int width, Color bg, Color fg, Color border, Color hoverBg)
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

        private void DetectPremiere()
        {
            if (!string.IsNullOrEmpty(premiereExePath) && File.Exists(premiereExePath))
            {
                UpdatePrStatus();
                return;
            }

            // Registry Search
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
                                        if (File.Exists(exe))
                                        {
                                            premiereExePath = exe;
                                            UpdatePrStatus();
                                            return;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // Common Program Files Paths
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

            lblPremiereStatus.Text = "✕ Premiere Pro не найден";
            lblPremiereStatus.ForeColor = UITheme.Red;
            LayoutHeaderControls();
        }

        private void UpdatePrStatus()
        {
            try
            {
                var vi = FileVersionInfo.GetVersionInfo(premiereExePath);
                string verName = "Premiere Pro " + vi.ProductVersion;
                if (premiereExePath.Contains("2026")) verName = "Premiere Pro 2026";
                else if (premiereExePath.Contains("2025")) verName = "Premiere Pro 2025";
                else if (premiereExePath.Contains("2024")) verName = "Premiere Pro 2024";

                lblPremiereStatus.Text = "● " + verName + " найден";
                lblPremiereStatus.ForeColor = UITheme.Green;
                SaveConfig();
            }
            catch
            {
                lblPremiereStatus.Text = "● Premiere Pro найден";
                lblPremiereStatus.ForeColor = UITheme.Green;
            }
            LayoutHeaderControls();
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

            SortProjects();
            ApplyFilter();
            UpdateCacheSizeStats();
        }

        private void SortProjects()
        {
            allProjects.Sort((a, b) =>
            {
                if (a.IsPinned != b.IsPinned) return b.IsPinned.CompareTo(a.IsPinned);
                return b.LastModified.CompareTo(a.LastModified);
            });
        }

        private void ParsePrefsMru(string prefsPath, HashSet<string> seenPaths)
        {
            try
            {
                string text = File.ReadAllText(prefsPath);
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
            string q = searchBox.SearchText.Trim().ToLowerInvariant();
            filteredProjects = allProjects.FindAll(p =>
                string.IsNullOrEmpty(q) ||
                p.Name.ToLowerInvariant().Contains(q) ||
                p.Path.ToLowerInvariant().Contains(q));

            cardList.Items = filteredProjects;
            lblStats.Text = string.Format("Проектов: {0} из {1}", filteredProjects.Count, allProjects.Count);
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

        private void LaunchProject(string projectPath)
        {
            if (string.IsNullOrEmpty(premiereExePath) || !File.Exists(premiereExePath))
            {
                DetectPremiere();
                if (string.IsNullOrEmpty(premiereExePath) || !File.Exists(premiereExePath))
                {
                    MessageBox.Show("Не удалось найти Adobe Premiere Pro.exe.\nПожалуйста, укажите путь кнопкой «Сменить .exe».", "Предупреждение", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    BtnBrowsePr_Click(this, EventArgs.Empty);
                    return;
                }
            }

            try
            {
                var psi = new ProcessStartInfo();
                psi.FileName = premiereExePath;
                if (!string.IsNullOrEmpty(projectPath))
                {
                    if (!File.Exists(projectPath))
                    {
                        var dr = MessageBox.Show(string.Format("Файл проекта не найден по пути:\n{0}\n\nОткрыть просто Premiere Pro?", projectPath), "Файл отсутствует", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (dr != DialogResult.Yes) return;
                    }
                    else
                    {
                        psi.Arguments = string.Format("\"{0}\"", projectPath);
                    }
                }

                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка запуска Premiere Pro:\n" + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnNewProject_Click(object sender, EventArgs e)
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "Проект Premiere Pro (*.prproj)|*.prproj";
                sfd.Title = "Создать новый проект Premiere Pro";
                sfd.FileName = "Новый проект.prproj";
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                sfd.InitialDirectory = Path.Combine(docs, "Adobe", "Premiere Pro");

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        CreateBlankProject(sfd.FileName);
                        RefreshProjects();
                        LaunchProject(sfd.FileName);
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

            double mb = totalBytes / (1024.0 * 1024.0);
            string msg = string.Format("Найдено файлов кэша Premiere Pro: {0}\nОбщий объём: {1:F1} МБ ({2:F2} ГБ)\n\nПапки:\n- Media Cache Files (.cfa)\n- Peak Files (.pek)\n- Media Cache (.mcdb)\n\nОчистить кэш сейчас? (Premiere Pro должен быть закрыт)",
                totalFiles, mb, mb / 1024.0);

            var dr = MessageBox.Show(msg, "Очистка кэша Premiere Pro", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
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
                string[] cacheDirs = new string[]
                {
                    Path.Combine(appData, "Adobe", "Common", "Media Cache Files"),
                    Path.Combine(appData, "Adobe", "Common", "Media Cache"),
                    Path.Combine(appData, "Adobe", "Common", "Peak Files")
                };

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

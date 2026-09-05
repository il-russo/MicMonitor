// MicMonitor - lightweight microphone monitoring for Windows.
//
// Routes the selected capture device straight to the selected playback device
// using WASAPI shared mode. No virtual audio driver is installed, so other
// applications (Discord, OBS, games) keep receiving the untouched microphone
// signal exactly as they did before this app was running.
//
// The interface follows the "rack unit" skin described in design/skin.html:
// a graphite chassis with recessed panels, engraved mono labels and a teal
// signal path. Everything is drawn with GDI+, no external resources.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace MicMonitor
{
    internal static class Program
    {
        internal static readonly uint ShowMessage = RegisterWindowMessage("MicMonitor.Show.6F1C");

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern uint RegisterWindowMessage(string message);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private static readonly IntPtr HwndBroadcast = new IntPtr(0xffff);

        [STAThread]
        private static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbedded;

            bool isFirstInstance;
            using (Mutex instanceLock = new Mutex(true, "MicMonitor.SingleInstance.6F1C", out isFirstInstance))
            {
                if (!isFirstInstance)
                {
                    PostMessage(HwndBroadcast, ShowMessage, IntPtr.Zero, IntPtr.Zero);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                bool startHidden = false;
                foreach (string arg in args)
                {
                    if (string.Equals(arg, "--tray", StringComparison.OrdinalIgnoreCase)) startHidden = true;
                }

                RunApplication(startHidden);
                GC.KeepAlive(instanceLock);
            }
        }

        // Kept out of Main so the JIT does not need to load NAudio before the
        // assembly resolver above is installed.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RunApplication(bool startHidden)
        {
            Application.Run(new MainForm(startHidden));
        }

        private static Assembly ResolveEmbedded(object sender, ResolveEventArgs e)
        {
            string simpleName = new AssemblyName(e.Name).Name;
            if (simpleName != "NAudio") return null;

            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("NAudio.dll"))
            {
                if (stream == null) return null;
                byte[] raw = new byte[stream.Length];
                int read = 0;
                while (read < raw.Length)
                {
                    int chunk = stream.Read(raw, read, raw.Length - read);
                    if (chunk <= 0) break;
                    read += chunk;
                }
                return Assembly.Load(raw);
            }
        }
    }

    #region Theme

    internal static class Theme
    {
        public static readonly Color ChassisTop = Color.FromArgb(0x17, 0x1B, 0x21);
        public static readonly Color ChassisBottom = Color.FromArgb(0x0E, 0x11, 0x16);
        public static readonly Color TitleTop = Color.FromArgb(0x25, 0x2B, 0x33);
        public static readonly Color TitleBottom = Color.FromArgb(0x1B, 0x20, 0x27);
        public static readonly Color Panel = Color.FromArgb(0x1C, 0x21, 0x29);
        public static readonly Color Well = Color.FromArgb(0x0A, 0x0D, 0x11);
        public static readonly Color Shadow = Color.FromArgb(0x0A, 0x0D, 0x10);
        public static readonly Color Edge = Color.FromArgb(0x2B, 0x32, 0x3B);
        public static readonly Color EdgeSoft = Color.FromArgb(0x22, 0x28, 0x30);
        public static readonly Color Highlight = Color.FromArgb(0x3C, 0x45, 0x52);

        public static readonly Color Text = Color.FromArgb(0xE6, 0xEA, 0xF0);
        public static readonly Color TextSoft = Color.FromArgb(0xC9, 0xD2, 0xDE);
        public static readonly Color Dim = Color.FromArgb(0x7C, 0x85, 0x93);
        public static readonly Color Faint = Color.FromArgb(0x4C, 0x55, 0x61);

        public static readonly Color Teal = Color.FromArgb(0x00, 0xE5, 0xC0);
        public static readonly Color TealLow = Color.FromArgb(0x0A, 0x8C, 0x7A);
        public static readonly Color TealDeep = Color.FromArgb(0x00, 0xB9, 0x9C);
        public static readonly Color Lime = Color.FromArgb(0x9B, 0xE8, 0x55);
        public static readonly Color Amber = Color.FromArgb(0xFF, 0xB0, 0x20);
        public static readonly Color Red = Color.FromArgb(0xFF, 0x4D, 0x4D);
        public static readonly Color RedDeep = Color.FromArgb(0xD9, 0x3A, 0x3A);

        public static readonly Font Display = Pick(12f, FontStyle.Regular,
            "Bahnschrift SemiBold Condensed", "Bahnschrift Condensed", "Bahnschrift", "Segoe UI Semibold");
        public static readonly Font DisplaySmall = Pick(10.5f, FontStyle.Regular,
            "Bahnschrift SemiBold Condensed", "Bahnschrift Condensed", "Bahnschrift", "Segoe UI Semibold");
        public static readonly Font DisplayLarge = Pick(14.5f, FontStyle.Regular,
            "Bahnschrift SemiBold Condensed", "Bahnschrift Condensed", "Bahnschrift", "Segoe UI Semibold");

        public static readonly Font Mono = Pick(7.5f, FontStyle.Regular, "Cascadia Mono", "Consolas", "Courier New");
        public static readonly Font MonoTiny = Pick(6.5f, FontStyle.Regular, "Cascadia Mono", "Consolas", "Courier New");
        public static readonly Font MonoRead = Pick(9f, FontStyle.Regular, "Cascadia Mono", "Consolas", "Courier New");

        public static readonly Font Body = Pick(9f, FontStyle.Regular, "Segoe UI", "Tahoma");
        public static readonly Font Icon = Pick(11f, FontStyle.Regular,
            "Segoe Fluent Icons", "Segoe MDL2 Assets", "Segoe UI Symbol");
        public static readonly Font IconSmall = Pick(8.5f, FontStyle.Regular,
            "Segoe Fluent Icons", "Segoe MDL2 Assets", "Segoe UI Symbol");
        public static readonly Font IconLarge = Pick(15f, FontStyle.Regular,
            "Segoe Fluent Icons", "Segoe MDL2 Assets", "Segoe UI Symbol");

        /// <summary>Returns the first font family that is actually installed.</summary>
        private static Font Pick(float size, FontStyle style, params string[] families)
        {
            foreach (string family in families)
            {
                try
                {
                    using (FontFamily candidate = new FontFamily(family))
                    {
                        return new Font(candidate, size, style, GraphicsUnit.Point);
                    }
                }
                catch (ArgumentException) { }
            }
            return new Font(FontFamily.GenericSansSerif, size, style, GraphicsUnit.Point);
        }

        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            if (d <= 0 || bounds.Width <= d || bounds.Height <= d)
            {
                path.AddRectangle(bounds);
                return path;
            }
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>Draws text with manual letter spacing, which GDI+ does not offer.</summary>
        public static int DrawTracked(Graphics g, string text, Font font, Color color, float x, float y, float tracking)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            float cursor = x;
            using (SolidBrush brush = new SolidBrush(color))
            {
                foreach (char c in text)
                {
                    string glyph = c.ToString();
                    g.DrawString(glyph, font, brush, cursor, y, StringFormat.GenericTypographic);
                    cursor += MeasureGlyph(g, glyph, font) + tracking;
                }
            }
            return (int)Math.Ceiling(cursor - tracking - x);
        }

        public static int MeasureTracked(Graphics g, string text, Font font, float tracking)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            float total = 0f;
            foreach (char c in text) total += MeasureGlyph(g, c.ToString(), font) + tracking;
            return (int)Math.Ceiling(total - tracking);
        }

        private static float MeasureGlyph(Graphics g, string glyph, Font font)
        {
            if (glyph == " ") return font.SizeInPoints * g.DpiX / 72f * 0.30f;
            return g.MeasureString(glyph, font, PointF.Empty, StringFormat.GenericTypographic).Width;
        }
    }

    /// <summary>Segoe MDL2 / Fluent icon code points used by the interface.</summary>
    internal static class Glyphs
    {
        public const string Microphone = "\uE720";
        public const string Headphone = "\uE7F6";
        public const string Play = "\uE768";
        public const string Stop = "\uE71A";
        public const string ChevronDown = "\uE70D";
        public const string Minimize = "\uE921";
        public const string Close = "\uE8BB";
        public const string Refresh = "\uE72C";
    }

    #endregion

    #region Custom controls

    /// <summary>Base for every owner-drawn control in the app.</summary>
    internal abstract class PaintedControl : Control
    {
        protected PaintedControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Panel;
            TabStop = false;
        }
    }

    /// <summary>Mono label with letter spacing, optionally clickable.</summary>
    internal sealed class TrackedLabel : PaintedControl
    {
        private bool hovered;

        public float Tracking = 2.4f;
        public Color Color = Theme.Dim;
        public Color HoverColor = Theme.Text;
        public bool Clickable;
        public ContentAlignment Align = ContentAlignment.MiddleLeft;

        public TrackedLabel()
        {
            Font = Theme.Mono;
            Height = 14;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (!Clickable) return;
            hovered = true;
            Cursor = Cursors.Hand;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hovered = false;
            Invalidate();
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (SolidBrush brush = new SolidBrush(BackColor)) g.FillRectangle(brush, ClientRectangle);

            int textWidth = Theme.MeasureTracked(g, Text, Font, Tracking);
            float x = 0f;
            if (Align == ContentAlignment.MiddleRight) x = Width - textWidth;
            else if (Align == ContentAlignment.MiddleCenter) x = (Width - textWidth) / 2f;

            float y = (Height - Font.GetHeight(g)) / 2f;
            Theme.DrawTracked(g, Text, Font, hovered ? HoverColor : Color, x, y, Tracking);
        }
    }

    /// <summary>Recessed panel: dark outer edge, lighter inner edge, flat fill.</summary>
    internal sealed class RecessedPanel : PaintedControl
    {
        public int Radius = 8;

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (SolidBrush brush = new SolidBrush(BackColor)) g.FillRectangle(brush, ClientRectangle);

            Rectangle outer = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.RoundedRect(outer, Radius))
            using (SolidBrush fill = new SolidBrush(Theme.Panel))
            using (Pen pen = new Pen(Theme.Shadow))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }

            Rectangle inner = new Rectangle(1, 1, Width - 3, Height - 3);
            using (GraphicsPath path = Theme.RoundedRect(inner, Radius - 1))
            using (Pen pen = new Pen(Theme.EdgeSoft))
            {
                g.DrawPath(pen, path);
            }
        }
    }

    /// <summary>Device picker drawn as a recessed well with an icon and a caret.</summary>
    internal sealed class DeviceSelect : ComboBox
    {
        private const int WmPaint = 0x000F;
        private const int WmPrintClient = 0x0318;

        private bool hovered;

        public string Glyph = Glyphs.Microphone;

        public DeviceSelect()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            FlatStyle = FlatStyle.Flat;
            DrawMode = DrawMode.OwnerDrawFixed;
            BackColor = Theme.Well;
            ForeColor = Theme.TextSoft;
            Font = Theme.Body;
            ItemHeight = 21;
            Height = 32;
            DrawItem += OnDrawItem;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = false; Invalidate(); }

        private void OnDrawItem(object sender, DrawItemEventArgs e)
        {
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using (SolidBrush background = new SolidBrush(selected ? Color.FromArgb(0x10, 0x2A, 0x28) : Theme.Well))
            {
                e.Graphics.FillRectangle(background, e.Bounds);
            }
            if (e.Index < 0 || e.Index >= Items.Count) return;

            Rectangle bounds = new Rectangle(e.Bounds.X + 7, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, Items[e.Index].ToString(), Theme.Body, bounds,
                selected ? Theme.Teal : Theme.TextSoft,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            // The closed combo box is painted by the system in light colours, so
            // its client area is fully redrawn on top of the themed result.
            if (m.Msg == WmPaint)
            {
                using (Graphics g = Graphics.FromHwnd(Handle)) Render(g);
            }
            else if (m.Msg == WmPrintClient && m.WParam != IntPtr.Zero)
            {
                using (Graphics g = Graphics.FromHdc(m.WParam)) Render(g);
            }
        }

        private void Render(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (SolidBrush brush = new SolidBrush(Theme.Panel))
            {
                g.FillRectangle(brush, 0, 0, Width, Height);
            }

            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.RoundedRect(bounds, 6))
            using (LinearGradientBrush fill = new LinearGradientBrush(
                       new Rectangle(0, 0, Width, Height), Theme.Well, Color.FromArgb(0x14, 0x18, 0x20), 90f))
            using (Pen pen = new Pen(Enabled && hovered ? Theme.TealLow : Theme.Edge))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }

            Color glyphColor = Enabled ? Theme.TealLow : Theme.Faint;
            TextRenderer.DrawText(g, Glyph, Theme.IconSmall, new Rectangle(8, 0, 18, Height), glyphColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            string text = SelectedItem == null ? string.Empty : SelectedItem.ToString();
            Rectangle textBounds = new Rectangle(29, 0, Width - 52, Height);
            TextRenderer.DrawText(g, text, Font, textBounds, Enabled ? Theme.TextSoft : Theme.Dim,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);

            TextRenderer.DrawText(g, Glyphs.ChevronDown, Theme.IconSmall,
                new Rectangle(Width - 24, 0, 20, Height), Enabled ? Theme.Faint : Theme.EdgeSoft,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    /// <summary>Recessed track with a teal fill and a knurled thumb.</summary>
    internal sealed class Slider : PaintedControl
    {
        private int minimum;
        private int maximum = 100;
        private int current = 50;
        private bool dragging;
        private bool hovered;

        public event EventHandler ValueChanged;

        public Slider()
        {
            Height = 22;
        }

        public int Minimum { get { return minimum; } set { minimum = value; Value = current; Invalidate(); } }
        public int Maximum { get { return maximum; } set { maximum = value; Value = current; Invalidate(); } }

        public int Value
        {
            get { return current; }
            set
            {
                int clamped = Math.Max(minimum, Math.Min(maximum, value));
                if (clamped == current) return;
                current = clamped;
                Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        private Rectangle Track
        {
            get { return new Rectangle(9, Height / 2 - 3, Math.Max(1, Width - 18), 6); }
        }

        private int ThumbX
        {
            get
            {
                Rectangle t = Track;
                double fraction = maximum == minimum ? 0.0 : (double)(current - minimum) / (maximum - minimum);
                return t.X + (int)Math.Round(fraction * t.Width);
            }
        }

        private void SetFromMouse(int x)
        {
            Rectangle t = Track;
            double fraction = (double)(x - t.X) / Math.Max(1, t.Width);
            fraction = Math.Max(0.0, Math.Min(1.0, fraction));
            Value = minimum + (int)Math.Round(fraction * (maximum - minimum));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || !Enabled) return;
            dragging = true;
            Capture = true;
            SetFromMouse(e.X);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging) SetFromMouse(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragging = false;
            Capture = false;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = false; Invalidate(); }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Value = current + (e.Delta > 0 ? 1 : -1);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush brush = new SolidBrush(BackColor)) g.FillRectangle(brush, ClientRectangle);

            Rectangle track = Track;
            using (GraphicsPath path = Theme.RoundedRect(track, 3))
            using (LinearGradientBrush fill = new LinearGradientBrush(
                       new Rectangle(track.X, track.Y - 1, track.Width, track.Height + 2),
                       Color.FromArgb(0x08, 0x0A, 0x0D), Color.FromArgb(0x12, 0x16, 0x1B), 90f))
            using (Pen pen = new Pen(Theme.EdgeSoft))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }

            int thumbX = ThumbX;
            int fillWidth = thumbX - track.X;
            if (fillWidth > 2)
            {
                Rectangle filled = new Rectangle(track.X, track.Y, fillWidth, track.Height);
                using (GraphicsPath path = Theme.RoundedRect(filled, 3))
                using (LinearGradientBrush brush = new LinearGradientBrush(
                           new Rectangle(filled.X, filled.Y, filled.Width, filled.Height),
                           Theme.TealLow, Theme.Teal, 0f))
                {
                    g.FillPath(brush, path);
                }

                // Soft glow underneath, faked with two translucent outlines.
                using (GraphicsPath path = Theme.RoundedRect(
                           new Rectangle(filled.X - 1, filled.Y - 1, filled.Width + 2, filled.Height + 2), 4))
                using (Pen pen = new Pen(Color.FromArgb(60, Theme.Teal)))
                {
                    g.DrawPath(pen, path);
                }
            }

            int radius = Enabled ? (hovered || dragging ? 9 : 8) : 7;
            Rectangle thumb = new Rectangle(thumbX - radius, Height / 2 - radius, radius * 2, radius * 2);

            if (Enabled && (hovered || dragging))
            {
                using (SolidBrush halo = new SolidBrush(Color.FromArgb(36, Theme.Teal)))
                {
                    g.FillEllipse(halo, Rectangle.Inflate(thumb, 4, 4));
                }
            }

            using (LinearGradientBrush brush = new LinearGradientBrush(thumb,
                       Color.FromArgb(0xF2, 0xF6, 0xFA), Color.FromArgb(0xB0, 0xBA, 0xC7), 90f))
            {
                g.FillEllipse(brush, thumb);
            }
            using (Pen pen = new Pen(Theme.Shadow))
            {
                g.DrawEllipse(pen, thumb);
            }

            // Knurling.
            using (Pen pen = new Pen(Color.FromArgb(0x8C, 0x97, 0xA5)))
            {
                for (int i = -2; i <= 2; i += 2)
                {
                    g.DrawLine(pen, thumbX + i, thumb.Y + 5, thumbX + i, thumb.Bottom - 5);
                }
            }
        }
    }

    /// <summary>24-segment LED meter with peak hold and an engraved dB scale.</summary>
    internal sealed class LevelMeter : PaintedControl
    {
        private const int Segments = 24;

        private float level;
        private float peakHold;
        private int holdFrames;

        public LevelMeter()
        {
            Height = 40;
        }

        private static readonly string[] ScaleLabels = { "-60", "-40", "-24", "-12", "-6", "0 dB" };

        /// <param name="peak">Linear peak amplitude in the 0..1 range.</param>
        public void Push(float peak)
        {
            float db = peak <= 0.00001f ? -60f : (float)(20.0 * Math.Log10(peak));
            float normalized = (db + 60f) / 60f;
            if (normalized < 0f) normalized = 0f;
            if (normalized > 1f) normalized = 1f;

            level = normalized > level ? normalized : level * 0.72f + normalized * 0.28f;

            if (level >= peakHold)
            {
                peakHold = level;
                holdFrames = 25;
            }
            else if (holdFrames > 0)
            {
                holdFrames--;
            }
            else
            {
                peakHold = Math.Max(level, peakHold - 0.018f);
            }

            Invalidate();
        }

        public void Reset()
        {
            level = 0f;
            peakHold = 0f;
            holdFrames = 0;
            Invalidate();
        }

        private static Color SegmentColor(int index)
        {
            float position = (float)index / Segments;
            if (position > 0.92f) return Theme.Red;
            if (position > 0.78f) return Theme.Amber;
            if (position > 0.60f) return Theme.Lime;
            return Theme.Teal;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush brush = new SolidBrush(BackColor)) g.FillRectangle(brush, ClientRectangle);

            const int gap = 2;
            const int barHeight = 26;
            int segmentWidth = (Width - gap * (Segments - 1)) / Segments;
            int lit = (int)Math.Round(level * Segments);
            int peakIndex = (int)Math.Round(peakHold * Segments) - 1;

            for (int i = 0; i < Segments; i++)
            {
                Rectangle cell = new Rectangle(i * (segmentWidth + gap), 0, segmentWidth, barHeight);
                using (GraphicsPath path = Theme.RoundedRect(cell, 2))
                {
                    if (i < lit)
                    {
                        Color on = SegmentColor(i);
                        using (LinearGradientBrush brush = new LinearGradientBrush(
                                   new Rectangle(cell.X, cell.Y - 1, cell.Width, cell.Height + 2),
                                   ControlPaint.Light(on, 0.25f), on, 90f))
                        {
                            g.FillPath(brush, path);
                        }
                    }
                    else if (i == peakIndex)
                    {
                        using (SolidBrush brush = new SolidBrush(Color.FromArgb(120, SegmentColor(i))))
                        {
                            g.FillPath(brush, path);
                        }
                    }
                    else
                    {
                        using (SolidBrush brush = new SolidBrush(Color.FromArgb(0x0F, 0x13, 0x17)))
                        using (Pen pen = new Pen(Color.FromArgb(0x1C, 0x21, 0x28)))
                        {
                            g.FillPath(brush, path);
                            g.DrawPath(pen, path);
                        }
                    }
                }
            }

            float y = barHeight + 5f;
            for (int i = 0; i < ScaleLabels.Length; i++)
            {
                int textWidth = Theme.MeasureTracked(g, ScaleLabels[i], Theme.MonoTiny, 1.2f);
                float x = i == 0
                    ? 0f
                    : i == ScaleLabels.Length - 1
                        ? Width - textWidth
                        : (Width - textWidth) * i / (float)(ScaleLabels.Length - 1);
                Theme.DrawTracked(g, ScaleLabels[i], Theme.MonoTiny, Theme.Faint, x, y, 1.2f);
            }
        }
    }

    /// <summary>Main action button: gradient face, icon glyph and tracked caption.</summary>
    internal sealed class PowerButton : PaintedControl
    {
        private bool hovered;
        private bool pressed;

        public Color GradientTop = Color.FromArgb(0x00, 0xF5, 0xCE);
        public Color GradientBottom = Theme.TealDeep;
        public Color Base = Color.FromArgb(0x00, 0x5F, 0x51);
        public Color Face = Color.FromArgb(0x04, 0x23, 0x1E);
        public string Glyph = Glyphs.Play;

        public PowerButton()
        {
            BackColor = Theme.ChassisBottom;
            Font = Theme.DisplayLarge;
            Cursor = Cursors.Hand;
            Height = 52;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = false; pressed = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); pressed = true; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); pressed = false; Invalidate(); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush brush = new SolidBrush(BackColor)) g.FillRectangle(brush, ClientRectangle);

            int lift = pressed ? 1 : hovered ? -1 : 0;
            Rectangle body = new Rectangle(0, 2 + lift, Width - 1, Height - 5);
            Rectangle plinth = new Rectangle(0, body.Y + 2, Width - 1, body.Height);

            using (GraphicsPath path = Theme.RoundedRect(plinth, 9))
            using (SolidBrush brush = new SolidBrush(Base))
            {
                g.FillPath(brush, path);
            }

            using (GraphicsPath path = Theme.RoundedRect(body, 9))
            using (LinearGradientBrush brush = new LinearGradientBrush(
                       new Rectangle(body.X, body.Y - 1, body.Width, body.Height + 2),
                       hovered ? ControlPaint.Light(GradientTop, 0.15f) : GradientTop, GradientBottom, 90f))
            using (Pen pen = new Pen(Color.FromArgb(120, Color.White)))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }

            int glyphWidth = 20;
            int textWidth = Theme.MeasureTracked(g, Text, Font, 3.2f);
            int totalWidth = glyphWidth + 6 + textWidth;
            int startX = (Width - totalWidth) / 2;

            TextRenderer.DrawText(g, Glyph, Theme.Icon,
                new Rectangle(startX, body.Y, glyphWidth, body.Height), Face,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            float textY = body.Y + (body.Height - Font.GetHeight(g)) / 2f;
            Theme.DrawTracked(g, Text, Font, Face, startX + glyphWidth + 6, textY, 3.2f);
        }
    }

    /// <summary>Pill toggle with a mono caption.</summary>
    internal sealed class ToggleSwitch : PaintedControl
    {
        private bool selected;
        private bool hovered;

        public event EventHandler CheckedChanged;

        public ToggleSwitch()
        {
            BackColor = Theme.ChassisBottom;
            Font = Theme.Mono;
            Cursor = Cursors.Hand;
            Height = 20;
        }

        public bool Checked
        {
            get { return selected; }
            set
            {
                if (selected == value) return;
                selected = value;
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = false; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) Checked = !Checked;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush brush = new SolidBrush(BackColor)) g.FillRectangle(brush, ClientRectangle);

            Rectangle pill = new Rectangle(0, Height / 2 - 8, 30, 16);
            using (GraphicsPath path = Theme.RoundedRect(pill, 8))
            using (SolidBrush fill = new SolidBrush(selected ? Color.FromArgb(0x06, 0x3A, 0x33) : Theme.Well))
            using (Pen pen = new Pen(selected ? Theme.TealLow : Theme.Edge))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }

            Rectangle knob = new Rectangle(selected ? pill.Right - 14 : pill.X + 2, pill.Y + 2, 12, 12);
            if (selected)
            {
                using (SolidBrush halo = new SolidBrush(Color.FromArgb(70, Theme.Teal)))
                {
                    g.FillEllipse(halo, Rectangle.Inflate(knob, 3, 3));
                }
            }
            using (SolidBrush brush = new SolidBrush(selected ? Theme.Teal : Color.FromArgb(0x4A, 0x53, 0x5F)))
            {
                g.FillEllipse(brush, knob);
            }

            Color color = selected ? Theme.TextSoft : hovered ? Theme.Dim : Theme.Faint;
            float y = (Height - Font.GetHeight(g)) / 2f;
            Theme.DrawTracked(g, Text, Font, color, 38f, y, 1.6f);
        }
    }

    #endregion

    #region Audio processing

    /// <summary>Mixes an interleaved multi-channel source down to a single channel.</summary>
    internal sealed class MonoDownmixProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly int sourceChannels;
        private readonly WaveFormat format;
        private float[] scratch;

        public MonoDownmixProvider(ISampleProvider source)
        {
            this.source = source;
            sourceChannels = source.WaveFormat.Channels;
            format = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 1);
        }

        public WaveFormat WaveFormat { get { return format; } }

        public int Read(float[] buffer, int offset, int count)
        {
            int needed = count * sourceChannels;
            if (scratch == null || scratch.Length < needed) scratch = new float[needed];

            int read = source.Read(scratch, 0, needed);
            int frames = read / sourceChannels;
            float scale = 1f / sourceChannels;

            for (int frame = 0; frame < frames; frame++)
            {
                float sum = 0f;
                int baseIndex = frame * sourceChannels;
                for (int channel = 0; channel < sourceChannels; channel++) sum += scratch[baseIndex + channel];
                buffer[offset + frame] = sum * scale;
            }
            return frames;
        }
    }

    /// <summary>Applies make-up gain, an optional noise gate, and tracks the peak level.</summary>
    internal sealed class MonitorProcessor : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly float attackCoefficient;
        private readonly float releaseCoefficient;
        private readonly float envelopeCoefficient;

        private float envelope;
        private float gateGain;
        private float peak;

        public MonitorProcessor(ISampleProvider source)
        {
            this.source = source;
            int rate = source.WaveFormat.SampleRate;
            attackCoefficient = CoefficientFor(0.003, rate);
            releaseCoefficient = CoefficientFor(0.120, rate);
            envelopeCoefficient = CoefficientFor(0.015, rate);
            gateGain = 1f;
        }

        private static float CoefficientFor(double seconds, int sampleRate)
        {
            return (float)Math.Exp(-1.0 / (seconds * sampleRate));
        }

        /// <summary>Linear output gain. 1.0 leaves the signal untouched.</summary>
        public volatile float Gain = 1f;

        /// <summary>Linear gate threshold. Zero disables the gate entirely.</summary>
        public volatile float GateThreshold = 0f;

        public volatile bool Muted;

        public WaveFormat WaveFormat { get { return source.WaveFormat; } }

        /// <summary>Returns the loudest sample seen since the previous call and clears it.</summary>
        public float ReadAndResetPeak()
        {
            float value = peak;
            peak = 0f;
            return value;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int read = source.Read(buffer, offset, count);

            float gain = Gain;
            float threshold = GateThreshold;
            bool muted = Muted;
            float localPeak = peak;

            for (int i = 0; i < read; i++)
            {
                float sample = buffer[offset + i];

                float magnitude = sample < 0f ? -sample : sample;
                if (magnitude > localPeak) localPeak = magnitude;
                envelope = magnitude + envelopeCoefficient * (envelope - magnitude);

                if (threshold > 0f)
                {
                    float target = envelope >= threshold ? 1f : 0f;
                    float coefficient = target > gateGain ? attackCoefficient : releaseCoefficient;
                    gateGain = target + coefficient * (gateGain - target);
                    sample *= gateGain;
                }

                sample *= gain;

                if (sample > 1f) sample = 1f;
                else if (sample < -1f) sample = -1f;

                buffer[offset + i] = muted ? 0f : sample;
            }

            peak = localPeak;
            return read;
        }
    }

    /// <summary>Spreads a mono source across the first two channels of a wider output.</summary>
    internal sealed class MonoSpreadProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly int outputChannels;
        private readonly WaveFormat format;
        private float[] scratch;

        public MonoSpreadProvider(ISampleProvider source, int outputChannels)
        {
            this.source = source;
            this.outputChannels = outputChannels;
            format = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, outputChannels);
        }

        public WaveFormat WaveFormat { get { return format; } }

        public int Read(float[] buffer, int offset, int count)
        {
            int frames = count / outputChannels;
            if (scratch == null || scratch.Length < frames) scratch = new float[frames];

            int read = source.Read(scratch, 0, frames);
            for (int frame = 0; frame < read; frame++)
            {
                int baseIndex = offset + frame * outputChannels;
                float value = scratch[frame];
                buffer[baseIndex] = value;
                if (outputChannels > 1) buffer[baseIndex + 1] = value;
                for (int channel = 2; channel < outputChannels; channel++) buffer[baseIndex + channel] = 0f;
            }
            return read * outputChannels;
        }
    }

    internal sealed class AudioEngineStoppedEventArgs : EventArgs
    {
        public AudioEngineStoppedEventArgs(Exception error) { Error = error; }
        public Exception Error { get; private set; }
    }

    /// <summary>Owns the capture/render pair and the sample pipeline between them.</summary>
    internal sealed class AudioEngine : IDisposable
    {
        private WasapiCapture capture;
        private WasapiOut output;
        private BufferedWaveProvider queue;
        private MonitorProcessor processor;
        private int maxQueuedBytes;
        private volatile bool running;

        public event EventHandler<AudioEngineStoppedEventArgs> Stopped;

        public bool IsRunning { get { return running; } }
        public string InputFormatText { get; private set; }
        public string OutputFormatText { get; private set; }
        public int LatencyMilliseconds { get; private set; }

        public float Gain
        {
            get { return processor == null ? 1f : processor.Gain; }
            set { if (processor != null) processor.Gain = value; }
        }

        public float GateThreshold
        {
            get { return processor == null ? 0f : processor.GateThreshold; }
            set { if (processor != null) processor.GateThreshold = value; }
        }

        public bool Muted
        {
            get { return processor != null && processor.Muted; }
            set { if (processor != null) processor.Muted = value; }
        }

        public float ReadAndResetPeak()
        {
            return processor == null ? 0f : processor.ReadAndResetPeak();
        }

        public void Start(string inputDeviceId, string outputDeviceId, int latencyMilliseconds, float gain, float gateThreshold, bool muted)
        {
            Stop();

            MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
            MMDevice inputDevice = enumerator.GetDevice(inputDeviceId);
            MMDevice outputDevice = enumerator.GetDevice(outputDeviceId);

            LatencyMilliseconds = latencyMilliseconds;

            capture = new WasapiCapture(inputDevice, true, latencyMilliseconds);
            WaveFormat captureFormat = capture.WaveFormat;
            WaveFormat renderFormat = outputDevice.AudioClient.MixFormat;

            InputFormatText = DescribeFormat(captureFormat);
            OutputFormatText = DescribeFormat(renderFormat);

            queue = new BufferedWaveProvider(captureFormat);
            queue.BufferDuration = TimeSpan.FromMilliseconds(Math.Max(400, latencyMilliseconds * 8));
            queue.DiscardOnBufferOverflow = true;
            queue.ReadFully = true;

            // Anything beyond this means the two device clocks have drifted apart
            // (or the UI thread stalled); the queue is dropped rather than letting
            // the monitoring delay grow without bound.
            maxQueuedBytes = (int)(captureFormat.AverageBytesPerSecond * (latencyMilliseconds * 4 + 60) / 1000.0);

            ISampleProvider chain = queue.ToSampleProvider();
            if (chain.WaveFormat.Channels > 1) chain = new MonoDownmixProvider(chain);

            processor = new MonitorProcessor(chain);
            processor.Gain = gain;
            processor.GateThreshold = gateThreshold;
            processor.Muted = muted;
            chain = processor;

            if (chain.WaveFormat.SampleRate != renderFormat.SampleRate)
            {
                chain = new WdlResamplingSampleProvider(chain, renderFormat.SampleRate);
            }
            chain = new MonoSpreadProvider(chain, renderFormat.Channels);

            capture.DataAvailable += OnDataAvailable;
            capture.RecordingStopped += OnRecordingStopped;

            output = new WasapiOut(outputDevice, AudioClientShareMode.Shared, true, latencyMilliseconds);
            output.PlaybackStopped += OnPlaybackStopped;
            output.Init(chain);

            running = true;
            output.Play();
            capture.StartRecording();
        }

        private static string DescribeFormat(WaveFormat format)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.#} kHz {1} ch",
                format.SampleRate / 1000.0, format.Channels);
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            BufferedWaveProvider target = queue;
            if (target == null || e.BytesRecorded <= 0) return;

            if (target.BufferedBytes > maxQueuedBytes) target.ClearBuffer();
            target.AddSamples(e.Buffer, 0, e.BytesRecorded);
        }

        private void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            if (running && e.Exception != null) RaiseStopped(e.Exception);
        }

        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            if (running && e.Exception != null) RaiseStopped(e.Exception);
        }

        private void RaiseStopped(Exception error)
        {
            running = false;
            EventHandler<AudioEngineStoppedEventArgs> handler = Stopped;
            if (handler != null) handler(this, new AudioEngineStoppedEventArgs(error));
        }

        public void Stop()
        {
            running = false;

            if (capture != null)
            {
                capture.DataAvailable -= OnDataAvailable;
                capture.RecordingStopped -= OnRecordingStopped;
                try { capture.StopRecording(); }
                catch (Exception) { }
                try { capture.Dispose(); }
                catch (Exception) { }
                capture = null;
            }

            if (output != null)
            {
                output.PlaybackStopped -= OnPlaybackStopped;
                try { output.Stop(); }
                catch (Exception) { }
                try { output.Dispose(); }
                catch (Exception) { }
                output = null;
            }

            queue = null;
            processor = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }

    #endregion

    #region Settings

    internal sealed class Settings
    {
        public string InputDeviceId = "";
        public string OutputDeviceId = "";
        public int Volume = 100;
        public int Latency = 25;
        public int Gate = 0;
        public bool AutoStartMonitoring = false;
        public bool MinimizeToTray = true;

        private static string FilePath
        {
            get
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MicMonitor");
                return Path.Combine(folder, "settings.ini");
            }
        }

        public static Settings Load()
        {
            Settings settings = new Settings();
            try
            {
                if (!File.Exists(FilePath)) return settings;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int separator = line.IndexOf('=');
                    if (separator <= 0) continue;
                    string key = line.Substring(0, separator).Trim();
                    string value = line.Substring(separator + 1).Trim();

                    switch (key)
                    {
                        case "input": settings.InputDeviceId = value; break;
                        case "output": settings.OutputDeviceId = value; break;
                        case "volume": settings.Volume = ParseInt(value, 100); break;
                        case "latency": settings.Latency = ParseInt(value, 25); break;
                        case "gate": settings.Gate = ParseInt(value, 0); break;
                        case "autostart": settings.AutoStartMonitoring = value == "1"; break;
                        case "tray": settings.MinimizeToTray = value == "1"; break;
                    }
                }
            }
            catch (Exception) { }
            return settings;
        }

        private static int ParseInt(string text, int fallback)
        {
            int result;
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) ? result : fallback;
        }

        public void Save()
        {
            try
            {
                string path = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                StringBuilder builder = new StringBuilder();
                builder.AppendLine("input=" + InputDeviceId);
                builder.AppendLine("output=" + OutputDeviceId);
                builder.AppendLine("volume=" + Volume.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine("latency=" + Latency.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine("gate=" + Gate.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine("autostart=" + (AutoStartMonitoring ? "1" : "0"));
                builder.AppendLine("tray=" + (MinimizeToTray ? "1" : "0"));

                File.WriteAllText(path, builder.ToString());
            }
            catch (Exception) { }
        }
    }

    internal static class WindowsStartup
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "MicMonitor";

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    return key != null && key.GetValue(ValueName) != null;
                }
            }
            catch (Exception) { return false; }
        }

        public static void SetEnabled(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (key == null) return;
                    if (enabled)
                    {
                        key.SetValue(ValueName, "\"" + Application.ExecutablePath + "\" --tray");
                    }
                    else if (key.GetValue(ValueName) != null)
                    {
                        key.DeleteValue(ValueName, false);
                    }
                }
            }
            catch (Exception) { }
        }
    }

    #endregion

    internal sealed class DeviceItem
    {
        public DeviceItem(string id, string name) { Id = id; Name = name; }
        public string Id { get; private set; }
        public string Name { get; private set; }
        public override string ToString() { return Name; }
    }

    internal sealed class MainForm : Form
    {
        #region Interop

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private const int WmNcLButtonDown = 0x00A1;
        private const int HtCaption = 2;

        #endregion

        private const int FormWidth = 474;
        private const int FormHeight = 684;
        private const int TitleBarHeight = 38;
        private const int SideMargin = 14;

        private readonly AudioEngine engine = new AudioEngine();
        private readonly Settings settings;
        private readonly WinFormsTimer uiTimer = new WinFormsTimer();
        private readonly WinFormsTimer startupTimer = new WinFormsTimer();

        private DeviceSelect inputSelect;
        private DeviceSelect outputSelect;
        private Slider volumeSlider;
        private Slider latencySlider;
        private Slider gateSlider;
        private TrackedLabel volumeReadout;
        private TrackedLabel latencyReadout;
        private TrackedLabel gateReadout;
        private TrackedLabel signalLabel;
        private TrackedLabel statusLabel;
        private LevelMeter meter;
        private PowerButton powerButton;
        private ToggleSwitch autoMonitorToggle;
        private ToggleSwitch startupToggle;
        private NotifyIcon trayIcon;

        private Rectangle minimizeButton;
        private Rectangle closeButton;
        private int hoveredTitleButton = -1;
        private Color statusLedColor = Theme.Faint;

        private bool loadingSettings = true;
        private bool exitRequested;
        private bool autoStartAttempted;
        private readonly bool startHidden;

        public MainForm(bool startHidden)
        {
            this.startHidden = startHidden;
            settings = Settings.Load();

            BuildUi();
            BuildTrayIcon();

            engine.Stopped += OnEngineStopped;

            uiTimer.Interval = 40;
            uiTimer.Tick += OnUiTick;
            uiTimer.Start();

            // Fires once shortly after the message loop starts. Using a timer
            // covers the tray-only launch, where OnShown never runs.
            startupTimer.Interval = 250;
            startupTimer.Tick += delegate
            {
                startupTimer.Stop();
                TryAutoStart();
            };
            startupTimer.Start();
        }

        #region UI construction

        private void BuildUi()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);

            Text = "Mic Monitor";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(FormWidth, FormHeight);
            BackColor = Theme.ChassisBottom;
            ForeColor = Theme.Text;
            Font = Theme.Body;
            Icon = IconFactory.CreateAppIcon();
            KeyPreview = true;

            minimizeButton = new Rectangle(FormWidth - 74, 6, 32, 26);
            closeButton = new Rectangle(FormWidth - 40, 6, 32, 26);

            int panelWidth = FormWidth - SideMargin * 2;

            // ---- routing panel ----
            RecessedPanel routing = new RecessedPanel();
            routing.Location = new Point(SideMargin, 118);
            routing.Size = new Size(panelWidth, 150);
            Controls.Add(routing);

            routing.Controls.Add(MakeSectionLabel("ROUTING", 14, 11));

            TrackedLabel rescan = MakeSectionLabel("RESCAN", 0, 11);
            rescan.Color = Theme.Faint;
            rescan.Align = ContentAlignment.MiddleRight;
            rescan.Clickable = true;
            rescan.Size = new Size(70, 14);
            rescan.Location = new Point(routing.Width - 84, 11);
            rescan.Click += OnRescanClicked;
            routing.Controls.Add(rescan);

            routing.Controls.Add(MakeFieldLabel("SOURCE", 14, 34));
            inputSelect = MakeSelect(Glyphs.Microphone, 14, 48, panelWidth - 28);
            routing.Controls.Add(inputSelect);

            routing.Controls.Add(MakeFieldLabel("DESTINATION  ·  USA LE CUFFIE", 14, 90));
            outputSelect = MakeSelect(Glyphs.Headphone, 14, 104, panelWidth - 28);
            routing.Controls.Add(outputSelect);

            // ---- signal panel ----
            RecessedPanel signal = new RecessedPanel();
            signal.Location = new Point(SideMargin, 278);
            signal.Size = new Size(panelWidth, 178);
            Controls.Add(signal);

            signal.Controls.Add(MakeSectionLabel("SIGNAL", 14, 11));

            signalLabel = MakeSectionLabel("STANDBY", 0, 11);
            signalLabel.Color = Theme.Faint;
            signalLabel.Align = ContentAlignment.MiddleRight;
            signalLabel.Size = new Size(190, 14);
            signalLabel.Location = new Point(signal.Width - 204, 11);
            signal.Controls.Add(signalLabel);

            signal.Controls.Add(MakeFieldLabel("MONITOR LEVEL", 14, 34));
            volumeReadout = MakeReadout(signal.Width - 90, 32);
            signal.Controls.Add(volumeReadout);
            volumeSlider = MakeSlider(11, 52, panelWidth - 22, 0, 200);
            volumeSlider.ValueChanged += OnVolumeChanged;
            signal.Controls.Add(volumeSlider);

            signal.Controls.Add(MakeFieldLabel("LATENCY", 14, 80));
            latencyReadout = MakeReadout(signal.Width - 90, 78);
            signal.Controls.Add(latencyReadout);
            latencySlider = MakeSlider(11, 98, panelWidth - 22, 5, 120);
            latencySlider.ValueChanged += OnLatencyChanged;
            signal.Controls.Add(latencySlider);

            signal.Controls.Add(MakeFieldLabel("NOISE GATE", 14, 126));
            gateReadout = MakeReadout(signal.Width - 90, 124);
            signal.Controls.Add(gateReadout);
            gateSlider = MakeSlider(11, 144, panelWidth - 22, 0, 50);
            gateSlider.ValueChanged += OnGateChanged;
            signal.Controls.Add(gateSlider);

            // ---- meter panel ----
            RecessedPanel levelPanel = new RecessedPanel();
            levelPanel.Location = new Point(SideMargin, 466);
            levelPanel.Size = new Size(panelWidth, 86);
            Controls.Add(levelPanel);

            levelPanel.Controls.Add(MakeSectionLabel("INPUT LEVEL", 14, 11));

            TrackedLabel hold = MakeSectionLabel("PEAK HOLD", 0, 11);
            hold.Color = Theme.Faint;
            hold.Align = ContentAlignment.MiddleRight;
            hold.Size = new Size(100, 14);
            hold.Location = new Point(levelPanel.Width - 114, 11);
            levelPanel.Controls.Add(hold);

            meter = new LevelMeter();
            meter.Location = new Point(14, 32);
            meter.Size = new Size(panelWidth - 28, 40);
            levelPanel.Controls.Add(meter);

            // ---- action ----
            powerButton = new PowerButton();
            powerButton.Text = "AVVIA ASCOLTO";
            powerButton.Location = new Point(SideMargin, 562);
            powerButton.Size = new Size(panelWidth, 52);
            powerButton.Click += OnPowerClicked;
            Controls.Add(powerButton);

            statusLabel = new TrackedLabel();
            statusLabel.Font = Theme.Mono;
            statusLabel.Tracking = 1.1f;
            statusLabel.Color = Theme.Dim;
            statusLabel.BackColor = Theme.ChassisBottom;
            statusLabel.Align = ContentAlignment.MiddleLeft;
            statusLabel.Location = new Point(SideMargin + 16, 620);
            statusLabel.Size = new Size(panelWidth - 16, 16);
            statusLabel.Text = "PRONTO";
            Controls.Add(statusLabel);

            autoMonitorToggle = new ToggleSwitch();
            autoMonitorToggle.Text = "AUTO-START";
            autoMonitorToggle.Location = new Point(SideMargin + 2, 652);
            autoMonitorToggle.Size = new Size(180, 20);
            autoMonitorToggle.CheckedChanged += OnAutoMonitorChanged;
            Controls.Add(autoMonitorToggle);

            startupToggle = new ToggleSwitch();
            startupToggle.Text = "AVVIO CON WINDOWS";
            startupToggle.Location = new Point(FormWidth - 218, 652);
            startupToggle.Size = new Size(204, 20);
            startupToggle.CheckedChanged += OnStartupChanged;
            Controls.Add(startupToggle);

            RefreshDevices();
            ApplySettingsToUi();
            loadingSettings = false;
        }

        private static TrackedLabel MakeSectionLabel(string text, int x, int y)
        {
            TrackedLabel label = new TrackedLabel();
            label.Text = text;
            label.Font = Theme.Mono;
            label.Tracking = 3.2f;
            label.Color = Theme.TealLow;
            label.Location = new Point(x, y);
            label.Size = new Size(220, 14);
            return label;
        }

        private static TrackedLabel MakeFieldLabel(string text, int x, int y)
        {
            TrackedLabel label = new TrackedLabel();
            label.Text = text;
            label.Font = Theme.Mono;
            label.Tracking = 1.5f;
            label.Color = Theme.Dim;
            label.Location = new Point(x, y);
            label.Size = new Size(280, 14);
            return label;
        }

        private static TrackedLabel MakeReadout(int x, int y)
        {
            TrackedLabel label = new TrackedLabel();
            label.Font = Theme.MonoRead;
            label.Tracking = 0.6f;
            label.Color = Theme.Text;
            label.Align = ContentAlignment.MiddleRight;
            label.Location = new Point(x, y);
            label.Size = new Size(76, 18);
            return label;
        }

        private static Slider MakeSlider(int x, int y, int width, int minimum, int maximum)
        {
            Slider slider = new Slider();
            slider.Location = new Point(x, y);
            slider.Size = new Size(width, 22);
            slider.Minimum = minimum;
            slider.Maximum = maximum;
            return slider;
        }

        private DeviceSelect MakeSelect(string glyph, int x, int y, int width)
        {
            DeviceSelect select = new DeviceSelect();
            select.Glyph = glyph;
            select.Location = new Point(x, y);
            select.Size = new Size(width, 32);
            select.SelectedIndexChanged += OnDeviceSelectionChanged;
            return select;
        }

        private void BuildTrayIcon()
        {
            trayIcon = new NotifyIcon();
            trayIcon.Icon = IconFactory.CreateAppIcon();
            trayIcon.Text = "Mic Monitor";
            trayIcon.Visible = true;
            trayIcon.DoubleClick += delegate { ShowFromTray(); };

            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem toggleItem = new ToolStripMenuItem("Avvia / ferma ascolto");
            toggleItem.Click += delegate { ToggleMonitoring(); };
            ToolStripMenuItem showItem = new ToolStripMenuItem("Mostra finestra");
            showItem.Click += delegate { ShowFromTray(); };
            ToolStripMenuItem exitItem = new ToolStripMenuItem("Esci");
            exitItem.Click += delegate { exitRequested = true; Close(); };

            menu.Items.Add(toggleItem);
            menu.Items.Add(showItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);
            trayIcon.ContextMenuStrip = menu;
        }

        #endregion

        #region Chassis painting

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            using (LinearGradientBrush brush = new LinearGradientBrush(
                       new Rectangle(0, 0, Width, Height), Theme.ChassisTop, Theme.ChassisBottom, 90f))
            {
                g.FillRectangle(brush, ClientRectangle);
            }

            // Brushed-metal streaks.
            using (Pen pen = new Pen(Color.FromArgb(6, Color.White)))
            {
                for (int x = 0; x < Width; x += 3) g.DrawLine(pen, x, TitleBarHeight, x, Height);
            }

            DrawTitleBar(g);
            DrawBrand(g);
            DrawStatusLed(g);

            using (Pen pen = new Pen(Color.FromArgb(0x33, 0x3B, 0x46)))
            {
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }

        private void DrawTitleBar(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle bar = new Rectangle(0, 0, Width, TitleBarHeight);
            using (LinearGradientBrush brush = new LinearGradientBrush(bar, Theme.TitleTop, Theme.TitleBottom, 90f))
            {
                g.FillRectangle(brush, bar);
            }
            using (Pen pen = new Pen(Color.FromArgb(0x0B, 0x0E, 0x11)))
            {
                g.DrawLine(pen, 0, TitleBarHeight - 1, Width, TitleBarHeight - 1);
            }
            using (Pen pen = new Pen(Color.FromArgb(14, Color.White)))
            {
                g.DrawLine(pen, 1, 1, Width - 2, 1);
            }

            Color led = engine.IsRunning ? Theme.Teal : Theme.Faint;
            using (SolidBrush halo = new SolidBrush(Color.FromArgb(engine.IsRunning ? 70 : 0, Theme.Teal)))
            {
                g.FillEllipse(halo, 10, TitleBarHeight / 2 - 8, 16, 16);
            }
            using (SolidBrush brush = new SolidBrush(led))
            {
                g.FillEllipse(brush, 14, TitleBarHeight / 2 - 4, 9, 9);
            }

            Theme.DrawTracked(g, "MIC MONITOR", Theme.DisplaySmall, Color.FromArgb(0xAE, 0xB7, 0xC4),
                32f, TitleBarHeight / 2f - Theme.DisplaySmall.GetHeight(g) / 2f - 1f, 2.6f);

            DrawTitleButton(g, minimizeButton, Glyphs.Minimize, 0);
            DrawTitleButton(g, closeButton, Glyphs.Close, 1);
        }

        private void DrawTitleButton(Graphics g, Rectangle bounds, string glyph, int index)
        {
            bool hovered = hoveredTitleButton == index;
            if (hovered)
            {
                using (GraphicsPath path = Theme.RoundedRect(bounds, 5))
                using (SolidBrush brush = new SolidBrush(index == 1
                           ? Color.FromArgb(0x3A, 0x1A, 0x1F)
                           : Color.FromArgb(0x26, 0x2C, 0x34)))
                {
                    g.FillPath(brush, path);
                }
            }

            Color color = hovered
                ? (index == 1 ? Theme.Red : Theme.Text)
                : Color.FromArgb(0x6D, 0x77, 0x84);
            TextRenderer.DrawText(g, glyph, Theme.IconSmall, bounds, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        private void DrawBrand(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle mark = new Rectangle(20, 55, 34, 34);
            using (GraphicsPath path = Theme.RoundedRect(mark, 9))
            using (LinearGradientBrush brush = new LinearGradientBrush(mark,
                       Color.FromArgb(0x0F, 0x3D, 0x36), Color.FromArgb(0x0A, 0x1A, 0x19), 60f))
            using (Pen pen = new Pen(Color.FromArgb(0x1D, 0x5B, 0x51)))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }
            TextRenderer.DrawText(g, Glyphs.Microphone, Theme.Icon, mark, Theme.Teal,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            Theme.DrawTracked(g, "MIC MONITOR", Theme.Display, Color.FromArgb(0xED, 0xF1, 0xF6), 67f, 56f, 3.0f);
            Theme.DrawTracked(g, "DIRECT MONITORING  ·  NO VIRTUAL DRIVER", Theme.MonoTiny, Theme.Faint, 68f, 79f, 1.4f);

            using (Pen pen = new Pen(Color.FromArgb(0x0C, 0x0F, 0x12)))
            {
                g.DrawLine(pen, 0, 106, Width, 106);
            }
            using (Pen pen = new Pen(Color.FromArgb(9, Color.White)))
            {
                g.DrawLine(pen, 0, 107, Width, 107);
            }
        }

        private void DrawStatusLed(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle led = new Rectangle(SideMargin + 2, 624, 8, 8);
            using (SolidBrush halo = new SolidBrush(Color.FromArgb(60, statusLedColor)))
            {
                g.FillEllipse(halo, Rectangle.Inflate(led, 3, 3));
            }
            using (SolidBrush brush = new SolidBrush(statusLedColor))
            {
                g.FillEllipse(brush, led);
            }

            using (Pen pen = new Pen(Color.FromArgb(0x0C, 0x0F, 0x12)))
            {
                g.DrawLine(pen, 0, 642, Width, 642);
            }
            using (Pen pen = new Pen(Color.FromArgb(8, Color.White)))
            {
                g.DrawLine(pen, 0, 643, Width, 643);
            }
        }

        #endregion

        #region Window chrome behaviour

        protected override CreateParams CreateParams
        {
            get
            {
                // CS_DROPSHADOW: the borderless chassis still casts a shadow.
                CreateParams parameters = base.CreateParams;
                parameters.ClassStyle |= 0x00020000;
                return parameters;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int hovered = minimizeButton.Contains(e.Location) ? 0 : closeButton.Contains(e.Location) ? 1 : -1;
            if (hovered == hoveredTitleButton) return;
            hoveredTitleButton = hovered;
            Invalidate(new Rectangle(0, 0, Width, TitleBarHeight));
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hoveredTitleButton == -1) return;
            hoveredTitleButton = -1;
            Invalidate(new Rectangle(0, 0, Width, TitleBarHeight));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            if (minimizeButton.Contains(e.Location))
            {
                WindowState = FormWindowState.Minimized;
                return;
            }
            if (closeButton.Contains(e.Location))
            {
                Close();
                return;
            }
            if (e.Y < TitleBarHeight)
            {
                ReleaseCapture();
                SendMessage(Handle, WmNcLButtonDown, new IntPtr(HtCaption), IntPtr.Zero);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) WindowState = FormWindowState.Minimized;
        }

        protected override void SetVisibleCore(bool value)
        {
            if (startHidden && !IsHandleCreated)
            {
                CreateHandle();
                base.SetVisibleCore(false);
                return;
            }
            base.SetVisibleCore(value);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            TryAutoStart();
        }

        private void TryAutoStart()
        {
            if (autoStartAttempted) return;
            autoStartAttempted = true;
            if (settings.AutoStartMonitoring && !engine.IsRunning) StartMonitoring();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == (int)Program.ShowMessage)
            {
                ShowFromTray();
                return;
            }
            base.WndProc(ref m);
        }

        #endregion

        #region Settings binding

        private void ApplySettingsToUi()
        {
            volumeSlider.Value = settings.Volume;
            latencySlider.Value = settings.Latency;
            gateSlider.Value = settings.Gate;
            autoMonitorToggle.Checked = settings.AutoStartMonitoring;
            startupToggle.Checked = WindowsStartup.IsEnabled();

            // An empty id means "never chosen": keep the system default that
            // RefreshDevices already selected instead of falling back to item 0.
            if (!string.IsNullOrEmpty(settings.InputDeviceId)) SelectDevice(inputSelect, settings.InputDeviceId);
            if (!string.IsNullOrEmpty(settings.OutputDeviceId)) SelectDevice(outputSelect, settings.OutputDeviceId);

            UpdateVolumeReadout();
            UpdateLatencyReadout();
            UpdateGateReadout();
        }

        private static void SelectDevice(ComboBox combo, string deviceId)
        {
            if (combo.Items.Count == 0) return;

            if (!string.IsNullOrEmpty(deviceId))
            {
                for (int i = 0; i < combo.Items.Count; i++)
                {
                    DeviceItem item = combo.Items[i] as DeviceItem;
                    if (item != null && item.Id == deviceId)
                    {
                        combo.SelectedIndex = i;
                        return;
                    }
                }
            }
            combo.SelectedIndex = 0;
        }

        private void PersistSettings()
        {
            DeviceItem input = inputSelect.SelectedItem as DeviceItem;
            DeviceItem output = outputSelect.SelectedItem as DeviceItem;

            settings.InputDeviceId = input == null ? "" : input.Id;
            settings.OutputDeviceId = output == null ? "" : output.Id;
            settings.Volume = volumeSlider.Value;
            settings.Latency = latencySlider.Value;
            settings.Gate = gateSlider.Value;
            settings.AutoStartMonitoring = autoMonitorToggle.Checked;
            settings.Save();
        }

        #endregion

        #region Devices

        private void RefreshDevices()
        {
            string previousInput = SelectedId(inputSelect);
            string previousOutput = SelectedId(outputSelect);

            List<DeviceItem> inputs = new List<DeviceItem>();
            List<DeviceItem> outputs = new List<DeviceItem>();
            string defaultInputId = null;
            string defaultOutputId = null;

            try
            {
                MMDeviceEnumerator enumerator = new MMDeviceEnumerator();

                if (enumerator.HasDefaultAudioEndpoint(DataFlow.Capture, Role.Communications))
                {
                    defaultInputId = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications).ID;
                }
                if (enumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
                {
                    defaultOutputId = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID;
                }

                foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
                {
                    inputs.Add(new DeviceItem(device.ID, Decorate(device.FriendlyName, device.ID == defaultInputId)));
                }
                foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                {
                    outputs.Add(new DeviceItem(device.ID, Decorate(device.FriendlyName, device.ID == defaultOutputId)));
                }
            }
            catch (Exception ex)
            {
                SetStatus("DISPOSITIVI NON LEGGIBILI: " + ex.Message, Theme.Red);
            }

            bool wasLoading = loadingSettings;
            loadingSettings = true;

            inputSelect.Items.Clear();
            foreach (DeviceItem item in inputs) inputSelect.Items.Add(item);
            outputSelect.Items.Clear();
            foreach (DeviceItem item in outputs) outputSelect.Items.Add(item);

            SelectDevice(inputSelect, previousInput ?? defaultInputId);
            SelectDevice(outputSelect, previousOutput ?? defaultOutputId);

            loadingSettings = wasLoading;
        }

        private static string Decorate(string name, bool isDefault)
        {
            return isDefault ? name + "  ·  predefinito" : name;
        }

        private static string SelectedId(ComboBox combo)
        {
            if (combo == null) return null;
            DeviceItem item = combo.SelectedItem as DeviceItem;
            return item == null ? null : item.Id;
        }

        #endregion

        #region Monitoring

        private void OnPowerClicked(object sender, EventArgs e)
        {
            ToggleMonitoring();
        }

        private void ToggleMonitoring()
        {
            if (engine.IsRunning) StopMonitoring("ASCOLTO FERMATO");
            else StartMonitoring();
        }

        private void StartMonitoring()
        {
            string inputId = SelectedId(inputSelect);
            string outputId = SelectedId(outputSelect);

            if (string.IsNullOrEmpty(inputId) || string.IsNullOrEmpty(outputId))
            {
                SetStatus("SELEZIONA MICROFONO E USCITA", Theme.Amber);
                return;
            }

            try
            {
                engine.Start(inputId, outputId, latencySlider.Value, GainFromSlider(), GateFromSlider(), false);
            }
            catch (Exception ex)
            {
                engine.Stop();
                UpdatePowerButton();
                SetStatus("ERRORE: " + ex.Message.ToUpperInvariant(), Theme.Red);
                return;
            }

            UpdatePowerButton();
            PersistSettings();
            signalLabel.Text = engine.InputFormatText.ToUpperInvariant();
            signalLabel.Color = Theme.TealLow;
            SetStatus(string.Format(CultureInfo.InvariantCulture, "IN ASCOLTO  ·  {0} -> {1}  ·  BUFFER {2} MS",
                engine.InputFormatText, engine.OutputFormatText, engine.LatencyMilliseconds).ToUpperInvariant(),
                Theme.Teal);
        }

        private void StopMonitoring(string message)
        {
            engine.Stop();
            meter.Reset();
            UpdatePowerButton();
            signalLabel.Text = "STANDBY";
            signalLabel.Color = Theme.Faint;
            SetStatus(message, Theme.Dim);
        }

        private void UpdatePowerButton()
        {
            bool running = engine.IsRunning;

            powerButton.Text = running ? "FERMA ASCOLTO" : "AVVIA ASCOLTO";
            powerButton.Glyph = running ? Glyphs.Stop : Glyphs.Play;
            powerButton.GradientTop = running ? Color.FromArgb(0xFF, 0x6B, 0x6B) : Color.FromArgb(0x00, 0xF5, 0xCE);
            powerButton.GradientBottom = running ? Theme.RedDeep : Theme.TealDeep;
            powerButton.Base = running ? Color.FromArgb(0x7A, 0x1E, 0x1E) : Color.FromArgb(0x00, 0x5F, 0x51);
            powerButton.Face = running ? Color.FromArgb(0x2B, 0x0A, 0x0C) : Color.FromArgb(0x04, 0x23, 0x1E);
            powerButton.Invalidate();

            inputSelect.Enabled = !running;
            outputSelect.Enabled = !running;

            if (trayIcon != null) trayIcon.Text = running ? "Mic Monitor - in ascolto" : "Mic Monitor";

            Invalidate(new Rectangle(0, 0, Width, TitleBarHeight));
        }

        private void OnEngineStopped(object sender, AudioEngineStoppedEventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new EventHandler<AudioEngineStoppedEventArgs>(OnEngineStopped), sender, e);
                return;
            }
            StopMonitoring("INTERROTTO: " +
                (e.Error == null ? "DISPOSITIVO NON DISPONIBILE" : e.Error.Message.ToUpperInvariant()));
        }

        private float GainFromSlider()
        {
            return volumeSlider.Value / 100f;
        }

        /// <summary>Maps the gate slider (0 = off, 1..50) to a linear threshold.</summary>
        private float GateFromSlider()
        {
            int value = gateSlider.Value;
            if (value <= 0) return 0f;
            double db = -70.0 + (value / 50.0) * 45.0; // -70 dB .. -25 dB
            return (float)Math.Pow(10.0, db / 20.0);
        }

        #endregion

        #region Event handlers

        private void OnVolumeChanged(object sender, EventArgs e)
        {
            UpdateVolumeReadout();
            engine.Gain = GainFromSlider();
            if (!loadingSettings) PersistSettings();
        }

        private void OnLatencyChanged(object sender, EventArgs e)
        {
            UpdateLatencyReadout();
            if (loadingSettings) return;
            PersistSettings();
            if (engine.IsRunning)
            {
                // The buffer size is fixed when the WASAPI clients are created,
                // so the pair has to be rebuilt for the new value to take effect.
                StartMonitoring();
            }
        }

        private void OnGateChanged(object sender, EventArgs e)
        {
            UpdateGateReadout();
            engine.GateThreshold = GateFromSlider();
            if (!loadingSettings) PersistSettings();
        }

        private void OnDeviceSelectionChanged(object sender, EventArgs e)
        {
            if (loadingSettings) return;
            PersistSettings();
        }

        private void OnAutoMonitorChanged(object sender, EventArgs e)
        {
            if (loadingSettings) return;
            PersistSettings();
        }

        private void OnStartupChanged(object sender, EventArgs e)
        {
            if (loadingSettings) return;
            WindowsStartup.SetEnabled(startupToggle.Checked);
        }

        private void OnRescanClicked(object sender, EventArgs e)
        {
            if (engine.IsRunning)
            {
                SetStatus("FERMA L'ASCOLTO PRIMA DI AGGIORNARE", Theme.Amber);
                return;
            }
            RefreshDevices();
            SetStatus("ELENCO DISPOSITIVI AGGIORNATO", Theme.Dim);
        }

        private void UpdateVolumeReadout()
        {
            volumeReadout.Text = volumeSlider.Value.ToString(CultureInfo.InvariantCulture) + " %";
        }

        private void UpdateLatencyReadout()
        {
            latencyReadout.Text = latencySlider.Value.ToString(CultureInfo.InvariantCulture) + " ms";
        }

        private void UpdateGateReadout()
        {
            if (gateSlider.Value <= 0)
            {
                gateReadout.Text = "OFF";
                gateReadout.Color = Theme.Faint;
                return;
            }
            double db = -70.0 + (gateSlider.Value / 50.0) * 45.0;
            gateReadout.Color = Theme.Text;
            gateReadout.Text = db.ToString("0", CultureInfo.InvariantCulture) + " dB";
        }

        private void SetStatus(string text, Color color)
        {
            statusLabel.Text = text;
            statusLabel.Color = color == Theme.Teal ? Theme.Dim : color;
            statusLedColor = color;
            Invalidate(new Rectangle(0, 616, Width, 30));
        }

        private void OnUiTick(object sender, EventArgs e)
        {
            if (!engine.IsRunning) return;
            if (WindowState == FormWindowState.Minimized || !Visible) return;
            meter.Push(engine.ReadAndResetPeak());
        }

        private void ShowFromTray()
        {
            Show();
            ShowInTaskbar = true;
            WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!exitRequested && e.CloseReason == CloseReason.UserClosing && settings.MinimizeToTray && engine.IsRunning)
            {
                e.Cancel = true;
                Hide();
                ShowInTaskbar = false;
                trayIcon.ShowBalloonTip(2000, "Mic Monitor",
                    "L'ascolto continua in background. Clicca l'icona per riaprire.", ToolTipIcon.Info);
                return;
            }

            PersistSettings();
            uiTimer.Stop();
            engine.Stop();
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
            base.OnFormClosing(e);
        }

        #endregion
    }

    /// <summary>Builds the application icon at runtime so no external resources are needed.</summary>
    internal static class IconFactory
    {
        private static Icon cached;

        public static Icon CreateAppIcon()
        {
            if (cached != null) return cached;

            using (Bitmap bitmap = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);

                    using (SolidBrush brush = new SolidBrush(Theme.Teal))
                    using (Pen pen = new Pen(Theme.Teal, 2.4f))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;

                        using (GraphicsPath capsule = Theme.RoundedRect(new Rectangle(12, 5, 8, 15), 4))
                        {
                            g.FillPath(brush, capsule);
                        }
                        g.DrawArc(pen, 7, 10, 18, 15, 20, 140);
                        g.DrawLine(pen, 16, 25, 16, 28);
                        g.DrawLine(pen, 11, 28, 21, 28);
                    }
                }

                cached = Icon.FromHandle(bitmap.GetHicon());
            }
            return cached;
        }
    }
}

using System.Drawing.Drawing2D;

namespace DayZManager
{
    internal static class UI
    {
        public static readonly Color Bg = Color.FromArgb(15, 23, 42);   // slate-900
        public static readonly Color Card = Color.FromArgb(30, 41, 59);   // slate-800
        public static readonly Color CardAlt = Color.FromArgb(17, 24, 39);   // slate-900-ish
        public static readonly Color Border = Color.FromArgb(51, 65, 85);   // slate-700
        public static readonly Color Text = Color.FromArgb(241, 245, 249);// slate-50
        public static readonly Color Muted = Color.FromArgb(148, 163, 184);// slate-400
        public static readonly Color Accent = Color.FromArgb(14, 165, 233); // sky-500
        public static readonly Color AccentHover = Color.FromArgb(2, 132, 199);  // sky-600
        public static readonly Color Success = Color.FromArgb(34, 197, 94);  // green-500
        public static readonly Font TitleFont = new Font("Segoe UI", 16, FontStyle.Bold);
        public static readonly Font SubTitleFont = new Font("Segoe UI", 10, FontStyle.Regular);
        public static readonly Font MonoFont = new Font(FontFamily.GenericMonospace, 11, FontStyle.Regular);
        public static readonly Font BaseFont = new Font("Segoe UI", 10, FontStyle.Regular);

        public static void StyleForm(Form f, string title)
        {
            f.Text = title;
            f.BackColor = Bg;
            f.ForeColor = Text;
            f.Font = BaseFont;
        }

        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int r = Math.Max(0, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2));
            int d = r * 2;
            var path = new GraphicsPath();
            if (r == 0)
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

    }

    internal class CardPanel : Panel
    {
        public int Radius { get; set; } = 14;
        public Color Fill { get; set; } = UI.Card;
        public Color Stroke { get; set; } = UI.Border;

        public CardPanel()
        {
            DoubleBuffered = true;
            Padding = new Padding(14);
            ForeColor = UI.Text;
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = UI.RoundedRect(rect, Radius);
            using var br = new SolidBrush(Fill);
            using var pen = new Pen(Stroke);
            e.Graphics.FillPath(br, path);
            e.Graphics.DrawPath(pen, path);
        }
    }

    internal class AccentButton : Button
    {
        public AccentButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = UI.Accent;
            ForeColor = Color.White;
            Height = 40;
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
            Padding = new Padding(10, 6, 10, 6);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); BackColor = UI.AccentHover; }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); BackColor = UI.Accent; }
    }

    internal class OutlineButton : Button
    {
        public OutlineButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 1;
            FlatAppearance.BorderColor = UI.Border;
            BackColor = UI.CardAlt;
            ForeColor = UI.Text;
            Height = 40;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); FlatAppearance.BorderColor = UI.Accent; }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); FlatAppearance.BorderColor = UI.Border; }
    }

    internal class HeaderPanel : Panel
    {
        public string Title { get; set; } = "";
        public string Subtitle { get; set; } = "";

        public HeaderPanel()
        {
            Dock = DockStyle.Top;
            Height = 90;
            Padding = new Padding(18, 18, 18, 12);
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = ClientRectangle;
            using var lg = new LinearGradientBrush(rect,
                Color.FromArgb(40, UI.Accent),
                Color.FromArgb(0, UI.Accent),
                LinearGradientMode.Horizontal);
            using var bg = new SolidBrush(UI.Bg);
            g.FillRectangle(bg, rect);
            g.FillRectangle(lg, rect);

            using var title = new SolidBrush(UI.Text);
            using var sub = new SolidBrush(UI.Muted);
            g.DrawString(Title, UI.TitleFont, title, 6, 6);
            g.DrawString(Subtitle, UI.SubTitleFont, sub, 8, 46);
        }
    }
}

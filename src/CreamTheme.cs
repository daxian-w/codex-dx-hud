using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CodexRateMonitorNative
{
    internal static class CreamTheme
    {
        public static readonly Color Background = Color.FromArgb(246, 235, 214);
        public static readonly Color Card = Color.FromArgb(255, 252, 244);
        public static readonly Color Text = Color.FromArgb(78, 85, 58);
        public static readonly Color Muted = Color.FromArgb(101, 99, 79);
        public static readonly Color GlassText = Color.FromArgb(87, 85, 66);
        public static readonly Color Track = Color.FromArgb(220, 209, 189);
        public static readonly Color Orange = Color.FromArgb(255, 154, 98);
        public static readonly Color Purple = Color.FromArgb(181, 122, 230);
        public static readonly Color Blue = Color.FromArgb(83, 188, 239);
        public static readonly Color Danger = Color.FromArgb(142, 53, 45);
        public static readonly Color Warning = Color.FromArgb(116, 74, 12);

        public static void Glass(Graphics g, RectangleF bounds, float radius, Color tint, float opacity, float glow,
            bool solidFill = false, Color? highlight = null)
        {
            if (glow > 0)
                for (int spread = 10; spread >= 1; spread--)
                {
                    RectangleF shadow = RectangleF.Inflate(bounds, spread * 0.65f, spread * 0.65f);
                    shadow.Offset(0, 3);
                    using (var brush = new SolidBrush(Color.FromArgb((int)(glow * (11 - spread) * 0.8f), 122, 103, 64)))
                    using (GraphicsPath path = DrawingHelpers.RoundRect(shadow, radius + spread * 0.65f))
                        g.FillPath(brush, path);
                }
            int alpha = Math.Max(0, Math.Min(255, (int)Math.Round(opacity * 255)));
            using (GraphicsPath path = DrawingHelpers.RoundRect(bounds, radius))
            using (Brush fill = solidFill ? (Brush)new SolidBrush(Color.FromArgb(alpha, tint)) :
                new LinearGradientBrush(bounds, Color.FromArgb(alpha, highlight ?? Card),
                    Color.FromArgb(alpha, tint), LinearGradientMode.ForwardDiagonal))
            using (var border = new Pen(Color.FromArgb(225, Color.White), 1.5f))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }
        }

        public static void TextAt(Graphics g, string value, float size, Color color, RectangleF bounds,
            bool bold, StringAlignment alignment)
        {
            using (var font = new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat { Alignment = alignment, LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(value ?? "", font, brush, bounds, format);
        }

        public static void Progress(Graphics g, RectangleF bounds, double? value, Color start, Color end)
        {
            using (GraphicsPath track = DrawingHelpers.RoundRect(bounds, bounds.Height / 2))
            using (var brush = new SolidBrush(Track)) g.FillPath(brush, track);
            if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value) || value <= 0) return;
            RectangleF fillBounds = bounds;
            fillBounds.Width *= (float)Math.Max(0, Math.Min(1, value.Value));
            using (GraphicsPath fillPath = DrawingHelpers.RoundRect(fillBounds, bounds.Height / 2))
            using (var fill = new LinearGradientBrush(bounds, start, end, 0f))
            {
                if (start == Orange && end == Purple)
                    fill.InterpolationColors = new ColorBlend { Colors = new[] { Orange, Color.FromArgb(230, 161, 202), Purple },
                        Positions = new[] { 0f, 0.55f, 1f } };
                g.FillPath(fill, fillPath);
            }
        }

        public static void Apply(Control control)
        {
            control.ForeColor = Text;
            var button = control as Button;
            if (button != null)
            {
                if (button.Tag is string && typeof(StyleSettings).GetProperty((string)button.Tag) != null) return;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = Color.FromArgb(218, 204, 177);
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(251, 232, 193);
                button.BackColor = Card;
                button.ForeColor = Text;
            }
            else if (control is TextBoxBase || control is ComboBox || control is NumericUpDown)
                control.BackColor = Card;
            else
            {
                bool inCard = control is GroupBox;
                for (Control parent = control.Parent; !inCard && parent != null; parent = parent.Parent)
                    inCard = parent is GroupBox;
                control.BackColor = inCard ? Card : Background;
            }
            foreach (Control child in control.Controls) Apply(child);
        }
    }
}

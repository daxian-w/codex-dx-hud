using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CodexRateMonitorNative
{
    internal sealed class CreamButton : Button
    {
        private bool hover;
        public CreamButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            MouseEnter += delegate { hover = true; Invalidate(); };
            MouseLeave += delegate { hover = false; Invalidate(); };
            Cursor = Cursors.Hand;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent == null ? CreamTheme.Background : Parent.BackColor);
            RectangleF bounds = new RectangleF(1, 1, Width - 3, Height - 3);
            bool primary = (Tag as string) == "primary";
            Color top = primary ? Color.FromArgb(255, 227, 154) : CreamTheme.Card;
            Color bottom = primary ? Color.FromArgb(246, 202, 103) : hover ? Color.FromArgb(250, 231, 195) : Color.FromArgb(248, 240, 225);
            using (var path = DrawingHelpers.RoundRect(bounds, 10f * DeviceDpi / 96f))
            using (var fill = new LinearGradientBrush(bounds, top, bottom, 90f))
            using (var border = new Pen(Color.FromArgb(215, 198, 166), 1))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, Rectangle.Round(bounds), Enabled ? CreamTheme.Text : CreamTheme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            if (Focused && ShowFocusCues)
            {
                Rectangle focus = ClientRectangle;
                focus.Inflate(-5, -5);
                ControlPaint.DrawFocusRectangle(e.Graphics, focus, CreamTheme.Text, CreamTheme.Card);
            }
        }
    }
}

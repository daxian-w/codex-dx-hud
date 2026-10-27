using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CodexRateMonitorNative
{
    internal sealed class CreamMenuRenderer : ToolStripProfessionalRenderer
    {
        public CreamMenuRenderer() : base(new CreamColorTable()) { RoundedEdges = true; }
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.Graphics.Clear(CreamTheme.Card);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var fill = new LinearGradientBrush(e.AffectedBounds, CreamTheme.Card, CreamTheme.Background, 90f))
                e.Graphics.FillRectangle(fill, e.AffectedBounds);
        }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = DrawingHelpers.RoundRect(new RectangleF(3, 1, e.Item.Width - 6, e.Item.Height - 2), 8))
            using (var brush = new SolidBrush(Color.FromArgb(247, 222, 172))) e.Graphics.FillPath(brush, path);
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? CreamTheme.Text : CreamTheme.Muted;
            base.OnRenderItemText(e);
        }
        private sealed class CreamColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return CreamTheme.Card; } }
            public override Color ImageMarginGradientBegin { get { return CreamTheme.Card; } }
            public override Color ImageMarginGradientMiddle { get { return CreamTheme.Card; } }
            public override Color ImageMarginGradientEnd { get { return CreamTheme.Background; } }
            public override Color MenuBorder { get { return Color.FromArgb(221, 207, 180); } }
            public override Color CheckBackground { get { return Color.FromArgb(246, 220, 164); } }
            public override Color CheckSelectedBackground { get { return CheckBackground; } }
        }
    }
}

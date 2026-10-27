using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace CodexRateMonitorNative
{
    internal sealed partial class OverlayForm
    {
        private readonly OrbRenderer orbRenderer = new OrbRenderer();
        private System.Windows.Forms.Timer orbTimer;
        private Point orbAnchor;
        private bool orbAnchorInitialized;
        private bool orbPressed;
        private Point orbPressPoint;
        private Point orbPressAnchor;
        private float orbScale = 1;
        private float orbExpansion;
        private float orbAnimationFrom;
        private bool orbExpandedTarget;
        private bool? orbPendingTarget;
        private double orbPendingAt;
        private double orbAnimationAt;
        private TaskSnapshot taskSnapshot = new TaskSnapshot { State = "loading", Coverage = "partial", ErrorCode = "bootstrap-pending" };
        private string orbConnection = "loading";
        private string orbCreditsState = "loading";
        private bool orbSettingsOpen;
        private bool orbDirectionInitialized;
        private bool orbExpandRight;
        private bool orbExpandDown;
        private string orbDirectionMonitor;

        public event Action<OrbPosition> OrbLayoutChanged;
        public event Action OrbSettingsRequested;
        public bool IsOrb { get { return overlayMode == "desktop" && settings.DesktopWidgetStyle == "orb"; } }
        public bool OrbExpanded { get { return IsOrb && orbExpansion > 0; } }
        public bool OrbTaskMonitoringEnabled { get { return IsOrb && settings.Orb.ShowTaskStatus; } }
        private static double OrbNow { get { return Stopwatch.GetTimestamp() * 1000d / Stopwatch.Frequency; } }

        private void InitializeOrb()
        {
            orbTimer = new System.Windows.Forms.Timer { Interval = 16 };
            orbTimer.Tick += OrbTick;
        }
        private void DisposeOrb()
        {
            if (orbTimer != null) { orbTimer.Stop(); orbTimer.Dispose(); }
            orbRenderer.Dispose();
        }
        private void ResetOrb()
        {
            if (orbTimer != null) orbTimer.Stop();
            orbExpansion = 0;
            orbAnimationAt = 0;
            orbExpandedTarget = false;
            orbPendingTarget = null;
            orbPressed = false;
            Capture = false;
            orbAnchorInitialized = false;
            orbDirectionInitialized = false;
        }
        private Rectangle OrbArea
        {
            get
            {
                Screen screen = orbAnchorInitialized ? Screen.FromPoint(orbAnchor) : OrbGeometry.ResolveScreen(settings.Orb.Position);
                return screen.WorkingArea;
            }
        }
        private void UpdateOrbBounds()
        {
            Rectangle area = OrbArea;
            OrbSettings orb = settings.Orb;
            orbScale = OrbGeometry.Scale(orb, overlayDpi, area);
            Size compact = OrbGeometry.PixelSize(false, orb.ShowTaskStatus, orbScale);
            Size expanded = OrbGeometry.PixelSize(true, orb.ShowTaskStatus, orbScale);
            int margin = (int)Math.Round(8d * overlayDpi / 96d);
            if (!orbAnchorInitialized)
            {
                OrbPosition saved = orb.Position;
                orbAnchor = saved.HasSavedPosition
                    ? new Point(area.Left + (int)Math.Round(saved.OffsetXDip * overlayDpi / 96d),
                        area.Top + (int)Math.Round(saved.OffsetYDip * overlayDpi / 96d))
                    : new Point(area.Right - compact.Width - (int)(16d * overlayDpi / 96d),
                        area.Bottom - compact.Height - (int)(16d * overlayDpi / 96d));
                orbAnchorInitialized = true;
            }
            orbAnchor = OrbGeometry.Clamp(orbAnchor, compact, area, margin);
            Rectangle full = OrbExpandedBounds(compact, expanded, area, margin);
            int x = (int)Math.Round(orbAnchor.X + (full.X - orbAnchor.X) * orbExpansion);
            int y = (int)Math.Round(orbAnchor.Y + (full.Y - orbAnchor.Y) * orbExpansion);
            int width = (int)Math.Round(compact.Width + (expanded.Width - compact.Width) * orbExpansion);
            int height = (int)Math.Round(compact.Height + (expanded.Height - compact.Height) * orbExpansion);
            if (Bounds == new Rectangle(x, y, width, height)) return;
            if (IsHandleCreated)
                NativeMethods.SetWindowPos(Handle, IntPtr.Zero, x, y, width, height,
                    NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
            else Bounds = new Rectangle(x, y, width, height);
        }
        private Rectangle OrbExpandedBounds(Size compact, Size expanded, Rectangle area, int margin)
        {
            string monitor = Screen.FromPoint(orbAnchor).DeviceName;
            if (!orbDirectionInitialized || orbDirectionMonitor != monitor)
            {
                orbExpandRight = orbAnchor.X + compact.Width / 2 < area.Left + area.Width / 2;
                bool fitsRight = orbAnchor.X + expanded.Width <= area.Right - margin;
                bool fitsLeft = orbAnchor.X + compact.Width - expanded.Width >= area.Left + margin;
                if (orbExpandRight && !fitsRight && fitsLeft) orbExpandRight = false;
                else if (!orbExpandRight && !fitsLeft && fitsRight) orbExpandRight = true;
                orbExpandDown = orbAnchor.Y + expanded.Height <= area.Bottom - margin;
                orbDirectionMonitor = monitor;
                orbDirectionInitialized = true;
            }
            Rectangle result = OrbGeometry.ExpandedBoundsFixed(orbAnchor, compact, expanded, area, margin, orbExpandRight, orbExpandDown);
            if (orbExpansion == 0 && !orbExpandedTarget) orbDirectionInitialized = false;
            return result;
        }
        private bool OrbContainsPointer()
        {
            if (!IsOrb || !IsHandleCreated) return false;
            Point local = PointToClient(Control.MousePosition);
            float inset = 12 * orbScale;
            RectangleF card = new RectangleF(inset, inset, Math.Max(1, Width - inset * 2), Math.Max(1, Height - inset * 2));
            using (var path = DrawingHelpers.RoundRect(card, (float)((40 - 4 * orbExpansion) * settings.Orb.CornerRadiusScale) * orbScale))
                return path.IsVisible(local);
        }
        private void RequestOrbTarget(bool expanded, bool delayed)
        {
            if (!IsOrb || dragging || orbPressed || orbSettingsOpen) return;
            if (orbPendingTarget.HasValue && orbPendingTarget.Value == expanded) return;
            if (!expanded && ContextMenuStrip != null && ContextMenuStrip.Visible) return;
            if (orbExpandedTarget == expanded && !orbPendingTarget.HasValue) return;
            orbPendingTarget = expanded;
            orbPendingAt = OrbNow + (delayed ? expanded ? settings.Orb.ExpandDelayMs : settings.Orb.CollapseDelayMs : 0);
            orbTimer.Start();
        }
        private void OrbTick(object sender, EventArgs e)
        {
            if (!IsOrb || !IsHandleCreated || !NativeMethods.IsWindowVisible(Handle))
            {
                orbTimer.Stop();
                return;
            }
            double now = OrbNow;
            if (orbPendingTarget.HasValue && now >= orbPendingAt)
            {
                bool target = orbPendingTarget.Value;
                orbPendingTarget = null;
                if (dragging || orbPressed || orbSettingsOpen || (!target && ContextMenuStrip != null && ContextMenuStrip.Visible)) return;
                if (settings.Orb.ExpandOnHover && target != OrbContainsPointer())
                {
                    if (orbAnimationAt == 0) orbTimer.Stop();
                    return;
                }
                orbExpandedTarget = target;
                orbAnimationFrom = orbExpansion;
                orbAnimationAt = now;
            }
            if (orbAnimationAt != 0)
            {
                double fraction = settings.Orb.AnimationEnabled ? Math.Min(1, (now - orbAnimationAt) / settings.Orb.AnimationDurationMs) : 1;
                double ease = 1 - Math.Pow(1 - fraction, 3);
                orbExpansion = (float)(orbAnimationFrom + ((orbExpandedTarget ? 1 : 0) - orbAnimationFrom) * ease);
                UpdateOrbBounds();
                RefreshSurface();
                if (fraction >= 1)
                {
                    orbAnimationAt = 0;
                    if (settings.Orb.ExpandOnHover && OrbContainsPointer() != orbExpandedTarget)
                        RequestOrbTarget(OrbContainsPointer(), true);
                }
            }
            if (!orbPendingTarget.HasValue && orbAnimationAt == 0) orbTimer.Stop();
        }
        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (IsOrb && settings.Orb.ExpandOnHover) RequestOrbTarget(OrbContainsPointer(), true);
        }
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (IsOrb && settings.Orb.ExpandOnHover && !OrbContainsPointer()) RequestOrbTarget(false, true);
        }
        private void OrbMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            orbPressed = true;
            orbPressPoint = Control.MousePosition;
            orbPressAnchor = orbAnchor;
            orbPendingTarget = null;
            orbTimer.Stop();
            orbAnimationAt = 0;
            orbExpansion = orbExpandedTarget ? 1 : 0;
            UpdateOrbBounds();
            RefreshSurface();
            Capture = true;
        }
        private void OrbMouseMove(MouseEventArgs e)
        {
            if (!orbPressed)
            {
                Cursor = Cursors.Hand;
                if (settings.Orb.ExpandOnHover)
                {
                    bool inside = OrbContainsPointer();
                    if (orbPendingTarget.HasValue && orbPendingTarget.Value != inside) orbPendingTarget = null;
                    if (inside != orbExpandedTarget) RequestOrbTarget(inside, true);
                }
                return;
            }
            Point mouse = Control.MousePosition;
            Size threshold = SystemInformation.DragSize;
            if (!dragging && Math.Abs(mouse.X - orbPressPoint.X) < threshold.Width / 2 &&
                Math.Abs(mouse.Y - orbPressPoint.Y) < threshold.Height / 2) return;
            dragging = true;
            Cursor = Cursors.SizeAll;
            orbAnimationAt = 0;
            orbExpansion = orbExpandedTarget ? 1 : 0;
            orbAnchor = new Point(orbPressAnchor.X + mouse.X - orbPressPoint.X, orbPressAnchor.Y + mouse.Y - orbPressPoint.Y);
            UpdateOrbBounds();
            RefreshSurface();
        }
        private void OrbMouseUp(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !orbPressed) return;
            bool wasDragging = dragging;
            orbPressed = false;
            dragging = false;
            Capture = false;
            if (wasDragging)
            {
                orbDirectionInitialized = false;
                UpdateOrbBounds();
                RefreshSurface();
                Screen screen = Screen.FromPoint(orbAnchor);
                var saved = new OrbPosition { HasSavedPosition = true, MonitorId = screen.DeviceName,
                    OffsetXDip = (orbAnchor.X - screen.WorkingArea.Left) * 96d / overlayDpi,
                    OffsetYDip = (orbAnchor.Y - screen.WorkingArea.Top) * 96d / overlayDpi };
                settings.Orb.Position = saved.Clone();
                var handler = OrbLayoutChanged;
                if (handler != null) handler(saved);
            }
            else if (!settings.Orb.ExpandOnHover) RequestOrbTarget(!orbExpandedTarget, false);
            if (settings.Orb.ExpandOnHover) RequestOrbTarget(OrbContainsPointer(), true);
        }
        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (IsOrb && e.Button == MouseButtons.Left && !dragging)
            {
                var handler = OrbSettingsRequested;
                if (handler != null) handler();
            }
        }
        public void SetOrbSettingsOpen(bool value)
        {
            orbSettingsOpen = value;
            if (value) orbPendingTarget = null;
            else if (IsOrb && settings.Orb.ExpandOnHover) RequestOrbTarget(OrbContainsPointer(), true);
        }
        public void ResumeOrbAfterMenu()
        {
            if (IsOrb && settings.Orb.ExpandOnHover) RequestOrbTarget(OrbContainsPointer(), true);
        }
        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture && orbPressed)
            {
                orbPressed = false;
                if (dragging) { orbAnchor = orbPressAnchor; UpdateOrbBounds(); RefreshSurface(); }
                dragging = false;
                ResumeOrbAfterMenu();
            }
        }
        public void SetTaskSnapshot(TaskSnapshot value)
        {
            taskSnapshot = value == null ? new TaskSnapshot() : value.Clone();
            if (IsOrb) RefreshSurface();
        }
        public string TaskUsageHint()
        {
            return taskSnapshot.StatusText(settings.Language) + "\n" + taskSnapshot.DetailText(settings.Language) +
                (taskSnapshot.LastConfirmedAtUtc == DateTime.MinValue ? "" : "\n" +
                    I18n.Translate("UpdatedAt", settings.Language).Replace("{0}",
                        I18n.FormatDate(taskSnapshot.LastConfirmedAtUtc.ToLocalTime(), settings.Language)));
        }
        private void CollapseOrbForHide()
        {
            if (!IsOrb) return;
            orbTimer.Stop();
            orbExpansion = 0;
            orbExpandedTarget = false;
            orbPendingTarget = null;
            orbAnimationAt = 0;
            UpdateOrbBounds();
        }
        public void SetOrbDataState(string connection, string creditsState)
        {
            bool changed = orbConnection != connection || orbCreditsState != creditsState;
            orbConnection = connection;
            orbCreditsState = creditsState;
            if (changed && IsOrb) RefreshSurface();
        }
        private Bitmap CreateOrbSurface()
        {
            bool expanded = orbExpansion >= 1;
            if (orbExpansion == 0 || expanded)
                return orbRenderer.CreateBitmap(settings, snapshot, resetCredits, taskSnapshot,
                    orbConnection, orbCreditsState, expanded, orbScale);
            var result = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
            Rectangle area = OrbArea;
            Size compactSize = OrbGeometry.PixelSize(false, settings.Orb.ShowTaskStatus, orbScale);
            Size fullSize = OrbGeometry.PixelSize(true, settings.Orb.ShowTaskStatus, orbScale);
            Rectangle fullBounds = OrbExpandedBounds(compactSize, fullSize, area, (int)(8d * overlayDpi / 96d));
            try
            {
                using (Graphics graphics = Graphics.FromImage(result))
                using (Bitmap compact = orbRenderer.CreateBitmap(settings, snapshot, resetCredits, taskSnapshot,
                    orbConnection, orbCreditsState, false, orbScale))
                using (Bitmap full = orbRenderer.CreateBitmap(settings, snapshot, resetCredits, taskSnapshot,
                    orbConnection, orbCreditsState, true, orbScale))
                {
                    graphics.Clear(Color.Transparent);
                    BlendOrb(graphics, compact, new Point(orbAnchor.X - Left, orbAnchor.Y - Top), 1 - orbExpansion);
                    BlendOrb(graphics, full, new Point(fullBounds.X - Left, fullBounds.Y - Top), orbExpansion);
                }
                return result;
            }
            catch { result.Dispose(); throw; }
        }
        private static void BlendOrb(Graphics g, Bitmap bitmap, Point location, float opacity)
        {
            using (var attributes = new ImageAttributes())
            {
                var matrix = new ColorMatrix { Matrix33 = opacity };
                attributes.SetColorMatrix(matrix);
                g.DrawImage(bitmap, new Rectangle(location, bitmap.Size), 0, 0, bitmap.Width, bitmap.Height,
                    GraphicsUnit.Pixel, attributes);
            }
        }
    }
}

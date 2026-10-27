using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;

namespace CodexRateMonitorNative
{
    internal sealed class OrbRenderer : IDisposable
    {
        // Per-render palette: previews must never change the running overlay's skin.
        private sealed class Palette
        {
            public Color Background = CreamTheme.Background, Highlight = CreamTheme.Card;
            public Color Inner = Color.FromArgb(248, 239, 223);
            public Color PrimaryCard = Color.FromArgb(236, 248, 251), SecondaryCard = Color.FromArgb(249, 235, 249);
            public Color CreditsCard = Color.FromArgb(255, 239, 219);
            public Color Start = CreamTheme.Orange, End = CreamTheme.Purple;
            public static Palette For(string skin)
            {
                var palette = new Palette();
                if (skin == "glacier")
                {
                    palette.Background = Color.FromArgb(225, 241, 252);
                    palette.Highlight = Color.FromArgb(246, 253, 255);
                    palette.Inner = Color.FromArgb(216, 239, 252);
                    palette.PrimaryCard = Color.FromArgb(218, 243, 252);
                    palette.SecondaryCard = Color.FromArgb(225, 234, 253);
                    palette.CreditsCard = Color.FromArgb(224, 246, 243);
                    palette.Start = Color.FromArgb(58, 190, 217);
                    palette.End = Color.FromArgb(100, 139, 237);
                }
                else if (skin == "lavender")
                {
                    palette.Background = Color.FromArgb(244, 231, 253);
                    palette.Highlight = Color.FromArgb(254, 248, 255);
                    palette.Inner = Color.FromArgb(240, 224, 250);
                    palette.PrimaryCard = Color.FromArgb(232, 226, 253);
                    palette.SecondaryCard = Color.FromArgb(245, 224, 246);
                    palette.CreditsCard = Color.FromArgb(251, 229, 239);
                    palette.Start = Color.FromArgb(161, 121, 227);
                    palette.End = Color.FromArgb(230, 143, 191);
                }
                return palette;
            }
        }
        private Image logo;
        private SvgMark brandMark;
        public OrbRenderer() : this(AppDomain.CurrentDomain.BaseDirectory) { }
        public OrbRenderer(string resourceDirectory)
        {
            string brand = Path.Combine(resourceDirectory, "assets", "openai-mark.svg");
            if (File.Exists(brand))
            {
                try { brandMark = new SvgMark(brand); } catch (Exception) { }
            }
            string path = Path.Combine(resourceDirectory, "assets", "logo.png");
            if (brandMark == null && File.Exists(path))
            {
                try { using (Image source = Image.FromFile(path)) logo = new Bitmap(source); }
                catch (Exception) { }
            }
        }
        public void Dispose()
        {
            if (logo != null) { logo.Dispose(); logo = null; }
            if (brandMark != null) { brandMark.Dispose(); brandMark = null; }
        }

        public Bitmap CreateBitmap(MonitorSettings settings, RateSnapshot quota, ResetCreditsInfo credits,
            TaskSnapshot tasks, string connection, string creditsState, bool expanded, float scale)
        {
            Size size = OrbGeometry.PixelSize(expanded, settings.Orb.ShowTaskStatus, scale);
            var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
            try
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    g.ScaleTransform(scale, scale);
                    if (expanded) Expanded(g, settings, quota, credits, tasks ?? new TaskSnapshot(), connection, creditsState);
                    else Compact(g, settings, quota, connection);
                }
                return bitmap;
            }
            catch { bitmap.Dispose(); throw; }
        }

        private static double? Value(WindowUsage usage, string mode)
        {
            if (usage == null || double.IsNaN(usage.UsedPercent) || double.IsInfinity(usage.UsedPercent)) return null;
            return Math.Max(0, Math.Min(100, UsageDisplayTools.GetDisplayedPercent(usage.UsedPercent, mode)));
        }
        private static string Percent(double? value)
        {
            return value.HasValue ? UsageDisplayTools.FormatPercent(value.Value) : "—";
        }
        private static string PrimaryResetTime(RateSnapshot quota)
        {
            if (quota == null || quota.Primary == null || !quota.Primary.ResetsAt.HasValue) return "—";
            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(quota.Primary.ResetsAt.Value)
                    .LocalDateTime.ToString("HH:mm", CultureInfo.InvariantCulture);
            }
            catch (ArgumentOutOfRangeException) { return "—"; }
        }
        private static Color QuotaText(MonitorSettings settings, double? value)
        {
            if (!value.HasValue) return CreamTheme.GlassText;
            return UsageDisplayTools.GetProgressColor(value.Value, settings.UsageDisplay,
                CreamTheme.Text, CreamTheme.Warning, CreamTheme.Danger);
        }
        private static void Ring(Graphics g, RectangleF bounds, double? fraction, float width, Palette palette)
        {
            using (var track = new Pen(CreamTheme.Track, width)) g.DrawEllipse(track, bounds);
            if (!fraction.HasValue || double.IsNaN(fraction.Value) || double.IsInfinity(fraction.Value) || fraction <= 0) return;
            double value = Math.Max(0, Math.Min(1, fraction.Value));
            float sweep = (float)(360 * value);
            int segments = Math.Max(1, (int)Math.Ceiling(sweep / 2));
            // A linear brush maps colors to screen Y, not distance along the arc.
            // Interpolate short overlapping arcs in traversal order. Round joins
            // avoid antialiased seams at the 50% size; explicit caps retain end colors.
            using (var pen = new Pen(palette.Start, width))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                for (int index = 0; index < segments; index++)
                {
                    double t = (index + 0.5) / segments;
                    pen.Color = Color.FromArgb(
                        (int)Math.Round(palette.Start.R + (palette.End.R - palette.Start.R) * t),
                        (int)Math.Round(palette.Start.G + (palette.End.G - palette.Start.G) * t),
                        (int)Math.Round(palette.Start.B + (palette.End.B - palette.Start.B) * t));
                    g.DrawArc(pen, bounds, -90f + (float)(360 * value * index / segments),
                        sweep / segments + (index + 1 < segments ? 0.1f : 0));
                }
            }
            float centerX = bounds.X + bounds.Width / 2, centerY = bounds.Y + bounds.Height / 2;
            using (var cap = new SolidBrush(palette.Start))
            {
                g.FillEllipse(cap, centerX - width / 2, bounds.Top - width / 2, width, width);
                double endAngle = (-90 + sweep) * Math.PI / 180;
                float endX = centerX + bounds.Width / 2 * (float)Math.Cos(endAngle);
                float endY = centerY + bounds.Height / 2 * (float)Math.Sin(endAngle);
                cap.Color = palette.End;
                g.FillEllipse(cap, endX - width / 2, endY - width / 2, width, width);
            }
        }

        private void Compact(Graphics g, MonitorSettings settings, RateSnapshot quota, string connection)
        {
            OrbSettings orb = settings.Orb;
            Palette palette = Palette.For(orb.Skin);
            CreamTheme.Glass(g, new RectangleF(12, 12, 216, 144), 40 * (float)orb.CornerRadiusScale,
                palette.Background, (float)orb.GlassOpacity, (float)orb.GlowStrength, false, palette.Highlight);
            CreamTheme.Glass(g, new RectangleF(14, 14, 212, 104), 38 * (float)orb.CornerRadiusScale,
                palette.Inner, 0.66f, 0.25f, false, palette.Highlight);
            if (brandMark != null) brandMark.Draw(g, new RectangleF(33, 36, 64, 64), CreamTheme.Text);
            else if (logo != null) g.DrawImage(logo, new RectangleF(32, 36, 64, 64));
            using (var separator = new Pen(Color.FromArgb(214, 202, 181), 1))
                g.DrawLine(separator, 118, 32, 118, 104);
            Color health = connection == "live" ? Color.FromArgb(40, 180, 95) : connection == "error"
                ? CreamTheme.Danger : connection == "loading" ? Color.Gray : CreamTheme.Warning;
            using (var brush = new SolidBrush(health))
            using (var border = new Pen(Color.White, 2))
            { g.FillEllipse(brush, 87, 89, 14, 14); g.DrawEllipse(border, 87, 89, 14, 14); }
            double? primary = Value(quota == null ? null : quota.Primary, settings.UsageDisplay);
            Ring(g, new RectangleF(138, 32, 70, 70), primary / 100d, 8, palette);
            CreamTheme.TextAt(g, Percent(primary), 22, QuotaText(settings, primary), new RectangleF(134, 48, 78, 30), false, StringAlignment.Center);
            CreamTheme.TextAt(g, PrimaryResetTime(quota), 12, CreamTheme.Muted, new RectangleF(143, 77, 61, 18), false, StringAlignment.Center);
            double? weekly = Value(quota == null ? null : quota.Secondary, settings.UsageDisplay);
            CreamTheme.TextAt(g, I18n.Translate("SevenDay", settings.Language), 14, CreamTheme.Text,
                new RectangleF(27, 125, 31, 20), false, StringAlignment.Near);
            CreamTheme.Progress(g, new RectangleF(61, 131, 106, 11), weekly / 100d, palette.Start, palette.End);
            CreamTheme.TextAt(g, Percent(weekly), 15, QuotaText(settings, weekly), new RectangleF(172, 124, 43, 22), false, StringAlignment.Far);
        }

        private static void Expanded(Graphics g, MonitorSettings settings, RateSnapshot quota, ResetCreditsInfo credits,
            TaskSnapshot tasks, string connection, string creditsState)
        {
            OrbSettings orb = settings.Orb;
            Palette palette = Palette.For(orb.Skin);
            Size logical = OrbGeometry.LogicalSize(true, orb.ShowTaskStatus);
            CreamTheme.Glass(g, new RectangleF(12, 12, 432, logical.Height - 24), 36 * (float)orb.CornerRadiusScale,
                palette.Background, (float)orb.GlassOpacity, (float)orb.GlowStrength, false, palette.Highlight);
            if (orb.ShowTaskStatus)
            {
                CreamTheme.Glass(g, new RectangleF(24, 24, 408, 108), 28 * (float)orb.CornerRadiusScale,
                    Color.White, 1f, 0.2f, true);
                double? progress = tasks.HasProgress ? (double?)tasks.CompletedSteps.Value / tasks.TotalSteps.Value : null;
                Ring(g, new RectangleF(42, 42, 70, 70), progress, 8, palette);
                CreamTheme.TextAt(g, tasks.ProgressText, 22, CreamTheme.Text, new RectangleF(39, 57, 76, 40), false, StringAlignment.Center);
                bool error = tasks.State == "unavailable" || tasks.State == "failed" || tasks.IsStale;
                CreamTheme.TextAt(g, tasks.StatusText(settings.Language), 19, error ? CreamTheme.Danger : CreamTheme.Text,
                    new RectangleF(130, 47, 279, 30), true, StringAlignment.Near);
                CreamTheme.TextAt(g, tasks.DetailText(settings.Language), 12, CreamTheme.Muted,
                    new RectangleF(131, 80, 201, 25), false, StringAlignment.Near);
                CreamTheme.Progress(g, new RectangleF(342, 84, 67, 18), progress,
                    Color.FromArgb(255, 208, 91), Color.FromArgb(255, 188, 57));
            }
            float top = orb.ShowTaskStatus ? 144 : 24;
            int cards = settings.ShowResetCredits ? 3 : 2;
            float width = (408 - (cards - 1) * 12) / cards;
            for (int index = 0; index < 2; index++)
            {
                bool primary = index == 0;
                RectangleF card = new RectangleF(24 + index * (width + 12), top, width, 136);
                CreamTheme.Glass(g, card, 24 * (float)orb.CornerRadiusScale,
                    primary ? palette.PrimaryCard : palette.SecondaryCard, 0.96f, 0.3f, false, palette.Highlight);
                Battery(g, new RectangleF(card.X + (width - 58) / 2, card.Y + 25, 58, 33), primary ? CreamTheme.Blue : CreamTheme.Purple);
                double? value = Value(quota == null ? null : primary ? quota.Primary : quota.Secondary, settings.UsageDisplay);
                CreamTheme.TextAt(g, (primary ? "5h " : "7d ") + Percent(value), 23, QuotaText(settings, value),
                    new RectangleF(card.X + 5, card.Y + 72, width - 10, 35), false, StringAlignment.Center);
                string detail = value.HasValue ? I18n.Translate(connection == "cached" ? "CachedShort" : connection == "error"
                    ? "RefreshFailedShort" : UsageDisplayTools.IsRemaining(settings.UsageDisplay) ? "Remaining" : "Used", settings.Language)
                    : I18n.Translate(connection == "loading" ? "UsageLoading" : connection == "error" ? "ServiceError" : "UsageNotProvided", settings.Language);
                CreamTheme.TextAt(g, detail, 12, CreamTheme.Muted, new RectangleF(card.X + 8, card.Y + 108, width - 16, 20), false, StringAlignment.Center);
            }
            if (settings.ShowResetCredits)
            {
                RectangleF card = new RectangleF(24 + 2 * (width + 12), top, width, 136);
                CreamTheme.Glass(g, card, 24 * (float)orb.CornerRadiusScale,
                    palette.CreditsCard, 0.96f, 0.3f, false, palette.Highlight);
                Ticket(g, new RectangleF(card.X + (width - 54) / 2, card.Y + 25, 54, 36));
                string count = credits == null ? "—" : credits.AvailableCount.ToString(CultureInfo.InvariantCulture);
                CreamTheme.TextAt(g, I18n.Translate("OrbCredits", settings.Language) + " ×" + count, 23, CreamTheme.Text,
                    new RectangleF(card.X + 3, card.Y + 71, width - 6, 34), false, StringAlignment.Center);
                string expiry = credits == null ? I18n.Translate(creditsState == "error" ? "CreditsReadFailed" : "CreditsUnknown", settings.Language) : credits.AvailableCount == 0
                    ? I18n.Translate("CreditsNone", settings.Language) : string.Format(CultureInfo.CurrentCulture,
                        I18n.Translate("CreditsExpire", settings.Language), credits.FormatEarliestExpiry());
                double days = credits != null && credits.EarliestExpiry.HasValue ? (credits.EarliestExpiry.Value - DateTime.Now).TotalDays : 99;
                Color color = days <= 3 ? CreamTheme.Danger : days <= 7 ? CreamTheme.Warning : CreamTheme.Muted;
                if (credits != null && creditsState == "cached") expiry = I18n.Translate("CachedShort", settings.Language) + " · " + expiry;
                CreamTheme.TextAt(g, expiry, 12, color, new RectangleF(card.X + 5, card.Y + 107, width - 10, 22), false, StringAlignment.Center);
            }
        }

        private static void Battery(Graphics g, RectangleF bounds, Color color)
        {
            using (var glow = new SolidBrush(Color.FromArgb(35, color)))
            using (GraphicsPath path = DrawingHelpers.RoundRect(new RectangleF(bounds.X - 5, bounds.Y + 3, bounds.Width + 10, bounds.Height + 9), 9))
                g.FillPath(glow, path);
            using (var terminal = new SolidBrush(color))
                g.FillRectangle(terminal, bounds.Right - 1, bounds.Y + 9, 5, bounds.Height - 18);
            using (GraphicsPath path = DrawingHelpers.RoundRect(bounds, 7))
            using (var gradient = new LinearGradientBrush(bounds, Color.FromArgb((color.R + 255) / 2,
                (color.G + 255) / 2, (color.B + 255) / 2), color, 90f))
            using (var border = new Pen(color, 2))
            { g.FillPath(gradient, path); g.DrawPath(border, path); }
            RectangleF inset = RectangleF.Inflate(bounds, -4, -4);
            using (GraphicsPath path = DrawingHelpers.RoundRect(inset, 4))
            using (var border = new Pen(Color.FromArgb(225, Color.White), 2)) g.DrawPath(border, path);
        }

        private static void Ticket(Graphics g, RectangleF bounds)
        {
            using (GraphicsPath path = DrawingHelpers.RoundRect(bounds, 6))
            using (var gradient = new LinearGradientBrush(bounds, Color.FromArgb(255, 191, 137), CreamTheme.Orange, 90f)) g.FillPath(gradient, path);
            var points = new PointF[10];
            for (int i = 0; i < 10; i++)
            {
                double angle = -Math.PI / 2 + i * Math.PI / 5;
                double radius = i % 2 == 0 ? 10 : 4.5;
                points[i] = new PointF(bounds.X + bounds.Width / 2 + (float)(Math.Cos(angle) * radius),
                    bounds.Y + bounds.Height / 2 + (float)(Math.Sin(angle) * radius));
            }
            using (var star = new SolidBrush(CreamTheme.Card)) g.FillPolygon(star, points);
        }
    }
}

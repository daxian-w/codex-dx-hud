using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace CodexRateMonitorNative
{
    internal sealed class OrbSettings
    {
        public string SizePreset { get; set; }
        public string Skin { get; set; }
        public bool ExpandOnHover { get; set; }
        public int ExpandDelayMs { get; set; }
        public int CollapseDelayMs { get; set; }
        public bool AnimationEnabled { get; set; }
        public int AnimationDurationMs { get; set; }
        public bool ShowTaskStatus { get; set; }
        public double GlassOpacity { get; set; }
        public double GlowStrength { get; set; }
        public double CornerRadiusScale { get; set; }
        public OrbPosition Position { get; set; }

        public OrbSettings()
        {
            SizePreset = "compact";
            Skin = "cream";
            ExpandOnHover = true;
            ExpandDelayMs = 120;
            CollapseDelayMs = 260;
            AnimationEnabled = true;
            AnimationDurationMs = 180;
            ShowTaskStatus = true;
            GlassOpacity = 0.94;
            GlowStrength = 0.75;
            CornerRadiusScale = 1;
            Position = new OrbPosition();
        }

        public void Normalize()
        {
            if (Skin != "cream" && Skin != "glacier" && Skin != "lavender") Skin = "cream";
            if (SizePreset != "tiny" && SizePreset != "small" && SizePreset != "mini" && SizePreset != "compact" && SizePreset != "standard" &&
                SizePreset != "large" && SizePreset != "extraLarge") SizePreset = "compact";
            ExpandDelayMs = Math.Max(0, Math.Min(1000, ExpandDelayMs));
            CollapseDelayMs = Math.Max(100, Math.Min(2000, CollapseDelayMs));
            AnimationDurationMs = Math.Max(80, Math.Min(350, AnimationDurationMs));
            GlassOpacity = FiniteRange(GlassOpacity, 0.85, 1, 0.94);
            GlowStrength = FiniteRange(GlowStrength, 0, 1, 0.75);
            CornerRadiusScale = FiniteRange(CornerRadiusScale, 0.75, 1.25, 1);
            if (Position == null) Position = new OrbPosition();
            Position.Normalize();
        }

        internal static double FiniteRange(double value, double min, double max, double fallback)
        {
            return double.IsNaN(value) || double.IsInfinity(value) ? fallback : Math.Max(min, Math.Min(max, value));
        }

        public OrbSettings Clone()
        {
            var copy = (OrbSettings)MemberwiseClone();
            copy.Position = Position == null ? new OrbPosition() : Position.Clone();
            copy.Normalize();
            return copy;
        }

        // Parse only new settings permissively. One malformed orb field must not
        // discard an otherwise valid legacy account/display configuration.
        public static OrbSettings Parse(Dictionary<string, object> values)
        {
            var result = new OrbSettings();
            if (values == null) return result;
            foreach (var property in typeof(OrbSettings).GetProperties())
            {
                object value = Find(values, property.Name);
                if (value == null) continue;
                try
                {
                    if (property.PropertyType == typeof(OrbPosition))
                    {
                        var positionValues = value as Dictionary<string, object>;
                        if (positionValues != null) result.Position = OrbPosition.Parse(positionValues);
                    }
                    else if (property.PropertyType == typeof(string))
                    {
                        if (value is string) property.SetValue(result, value, null);
                    }
                    else if (property.PropertyType == typeof(bool))
                    {
                        if (value is bool) property.SetValue(result, value, null);
                    }
                    else
                    {
                        if (value is bool || value is string) continue;
                        double numeric = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                        if (double.IsNaN(numeric) || double.IsInfinity(numeric)) continue;
                        if (property.PropertyType == typeof(int))
                        {
                            if (numeric != Math.Truncate(numeric)) continue;
                            property.SetValue(result, (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, numeric)), null);
                        }
                        else property.SetValue(result, numeric, null);
                    }
                }
                catch (Exception) { /* Leave the field's constructor default. */ }
            }
            result.Normalize();
            return result;
        }

        internal static object Find(Dictionary<string, object> values, string key)
        {
            foreach (var entry in values)
                if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase)) return entry.Value;
            return null;
        }
    }

    internal sealed class OrbPosition
    {
        public bool HasSavedPosition { get; set; }
        public string MonitorId { get; set; }
        public double OffsetXDip { get; set; }
        public double OffsetYDip { get; set; }

        public OrbPosition Clone() { return (OrbPosition)MemberwiseClone(); }
        public void Normalize()
        {
            OffsetXDip = OrbSettings.FiniteRange(OffsetXDip, -100000, 100000, 0);
            OffsetYDip = OrbSettings.FiniteRange(OffsetYDip, -100000, 100000, 0);
        }
        public static OrbPosition Parse(Dictionary<string, object> values)
        {
            var result = new OrbPosition();
            object saved = OrbSettings.Find(values, "HasSavedPosition");
            result.HasSavedPosition = saved is bool && (bool)saved;
            result.MonitorId = OrbSettings.Find(values, "MonitorId") as string;
            foreach (string key in new[] { "OffsetXDip", "OffsetYDip" })
            {
                object raw = OrbSettings.Find(values, key);
                if (raw == null || raw is bool || raw is string) continue;
                try
                {
                    double value = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
                    if (key == "OffsetXDip") result.OffsetXDip = value;
                    else result.OffsetYDip = value;
                }
                catch (Exception) { }
            }
            result.Normalize();
            return result;
        }
    }

    internal static class OrbGeometry
    {
        public static double PresetScale(string preset)
        {
            switch (preset)
            {
                case "tiny": return 0.50;
                case "small": return 0.75;
                case "mini": return 0.85;
                case "standard": return 1.15;
                case "large": return 1.35;
                case "extraLarge": return 1.60;
                default: return 1;
            }
        }
        public static Size LogicalSize(bool expanded, bool tasks)
        {
            return expanded ? new Size(456, tasks ? 304 : 184) : new Size(240, 168);
        }
        public static float Scale(OrbSettings settings, int dpi, Rectangle workingArea)
        {
            double scale = PresetScale(settings.SizePreset) * Math.Max(96, dpi) / 96d;
            Size expanded = LogicalSize(true, settings.ShowTaskStatus);
            double margin = 8d * Math.Max(96, dpi) / 96d;
            if (workingArea.Width > 0 && workingArea.Height > 0)
                scale = Math.Min(scale, Math.Min(Math.Max(1, workingArea.Width - margin * 2) / expanded.Width,
                    Math.Max(1, workingArea.Height - margin * 2) / expanded.Height));
            return (float)Math.Max(0.01, scale);
        }
        public static Size PixelSize(bool expanded, bool tasks, float scale)
        {
            Size logical = LogicalSize(expanded, tasks);
            return new Size(Math.Max(1, (int)Math.Round(logical.Width * scale)),
                Math.Max(1, (int)Math.Round(logical.Height * scale)));
        }
        public static Point Clamp(Point anchor, Size size, Rectangle area, int margin)
        {
            return new Point(Math.Max(area.Left + margin, Math.Min(anchor.X, area.Right - size.Width - margin)),
                Math.Max(area.Top + margin, Math.Min(anchor.Y, area.Bottom - size.Height - margin)));
        }
        public static Rectangle ExpandedBounds(Point anchor, Size compact, Size expanded, Rectangle area, int margin)
        {
            bool left = anchor.X + compact.Width / 2 < area.Left + area.Width / 2;
            int x = left ? anchor.X : anchor.X + compact.Width - expanded.Width;
            int y = anchor.Y + expanded.Height <= area.Bottom - margin
                ? anchor.Y : anchor.Y + compact.Height - expanded.Height;
            return new Rectangle(Clamp(new Point(x, y), expanded, area, margin), expanded);
        }
        public static Rectangle ExpandedBoundsFixed(Point anchor, Size compact, Size expanded,
            Rectangle area, int margin, bool right, bool down)
        {
            Point desired = new Point(right ? anchor.X : anchor.X + compact.Width - expanded.Width,
                down ? anchor.Y : anchor.Y + compact.Height - expanded.Height);
            return new Rectangle(Clamp(desired, expanded, area, margin), expanded);
        }
        public static Screen ResolveScreen(OrbPosition position)
        {
            foreach (Screen screen in Screen.AllScreens)
                if (position.HasSavedPosition && screen.DeviceName == position.MonitorId) return screen;
            return Screen.PrimaryScreen ?? Screen.AllScreens[0];
        }
    }
}

using System;
using System.Drawing;
using System.Windows.Forms;

namespace CodexRateMonitorNative
{
    internal sealed partial class AppearanceSettingsForm
    {
        private RadioButton orbModeRadio;
        private GroupBox orbGroup;
        private ComboBox orbSize;
        private ComboBox orbSkin;
        private static readonly string[] OrbSkins = { "cream", "glacier", "lavender" };
        private CheckBox orbHover, orbAnimation, orbTasks, orbExpandedPreview;
        private NumericUpDown orbExpandDelay, orbCollapseDelay, orbDuration, orbGlass, orbGlow, orbRadius;
        private static readonly string[] OrbPresets = { "tiny", "small", "mini", "compact", "standard", "large", "extraLarge" };

        private Control BuildOrbSettings()
        {
            orbGroup = CreateGroup(I18n.T("OrbSettings"));
            var content = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 2 };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var options = new SettingsFlowPanel { Dock = DockStyle.Top, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Padding = new Padding(4, 7, 4, 4) };
            orbHover = OrbCheck("OrbHover");
            orbAnimation = OrbCheck("OrbAnimation");
            orbTasks = OrbCheck("OrbShowTasks");
            orbExpandedPreview = new CheckBox { Text = I18n.T("OrbPreviewExpanded"), AutoSize = true,
                Checked = true, Margin = new Padding(4, 4, 14, 4) };
            orbExpandedPreview.CheckedChanged += delegate { preview.OrbExpanded = orbExpandedPreview.Checked; };
            options.Controls.AddRange(new Control[] { orbHover, orbAnimation, orbTasks, orbExpandedPreview });
            content.Controls.Add(options, 0, 0);
            var fields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 4, RowCount = 6,
                Padding = new Padding(4, 3, 4, 5) };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int i = 0; i < 6; i++) fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            orbSkin = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 178 };
            foreach (string key in new[] { "OrbSkinCream", "OrbSkinGlacier", "OrbSkinLavender" })
                orbSkin.Items.Add(I18n.T(key));
            orbSkin.SelectedIndexChanged += ControlChanged;
            orbSize = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 178 };
            foreach (string key in new[] { "OrbTiny", "OrbSmall", "OrbMini", "OrbCompact", "OrbStandard", "OrbLarge", "OrbExtraLarge" })
                orbSize.Items.Add(I18n.T(key));
            orbSize.SelectedIndexChanged += ControlChanged;
            orbExpandDelay = CreateNumber(0, 1000, 20, 0);
            orbCollapseDelay = CreateNumber(100, 2000, 20, 0);
            orbDuration = CreateNumber(80, 350, 10, 0);
            orbGlass = CreateNumber(85, 100, 1, 0);
            orbGlow = CreateNumber(0, 100, 5, 0);
            orbRadius = CreateNumber(75, 125, 5, 0);
            AddField(fields, 0, 0, I18n.T("OrbSize"), orbSize);
            fields.SetColumnSpan(fields.GetControlFromPosition(1, 0), 3);
            AddField(fields, 0, 1, I18n.T("OrbExpandDelay"), orbExpandDelay);
            AddField(fields, 2, 1, I18n.T("OrbCollapseDelay"), orbCollapseDelay);
            AddField(fields, 0, 2, I18n.T("OrbGlass"), orbGlass, "%");
            AddField(fields, 2, 2, I18n.T("OrbGlow"), orbGlow, "%");
            AddField(fields, 0, 3, I18n.T("OrbRadius"), orbRadius, "%");
            AddField(fields, 2, 3, I18n.T("OrbAnimationDuration"), orbDuration);
            AddField(fields, 0, 4, I18n.T("OrbSkin"), orbSkin);
            fields.SetColumnSpan(fields.GetControlFromPosition(1, 4), 3);
            var reset = CreateSecondaryButton(I18n.T("RestoreDefault"));
            reset.Click += delegate
            {
                OrbPosition position = working.Orb.Position.Clone();
                working.Orb = new OrbSettings { Position = position };
                LoadControls();
            };
            fields.Controls.Add(reset, 1, 5);
            content.Controls.Add(fields, 0, 1);
            orbGroup.Controls.Add(content);
            return orbGroup;
        }
        private CheckBox OrbCheck(string key)
        {
            var check = new CheckBox { Text = I18n.T(key), AutoSize = true, Margin = new Padding(4, 4, 14, 4) };
            check.CheckedChanged += ControlChanged;
            return check;
        }
        private void LoadOrbControls()
        {
            OrbSettings orb = working.Orb;
            orbSize.SelectedIndex = Math.Max(0, Array.IndexOf(OrbPresets, orb.SizePreset));
            orbSkin.SelectedIndex = Math.Max(0, Array.IndexOf(OrbSkins, orb.Skin));
            orbHover.Checked = orb.ExpandOnHover;
            orbAnimation.Checked = orb.AnimationEnabled;
            orbTasks.Checked = orb.ShowTaskStatus;
            orbExpandDelay.Value = orb.ExpandDelayMs;
            orbCollapseDelay.Value = orb.CollapseDelayMs;
            orbDuration.Value = orb.AnimationDurationMs;
            orbGlass.Value = (decimal)(orb.GlassOpacity * 100);
            orbGlow.Value = (decimal)(orb.GlowStrength * 100);
            orbRadius.Value = (decimal)(orb.CornerRadiusScale * 100);
            preview.OrbExpanded = orbExpandedPreview.Checked;
        }
        private void UpdateOrbWorking()
        {
            OrbSettings orb = working.Orb;
            orb.SizePreset = OrbPresets[Math.Max(0, orbSize.SelectedIndex)];
            orb.Skin = OrbSkins[Math.Max(0, orbSkin.SelectedIndex)];
            orb.ExpandOnHover = orbHover.Checked;
            orb.AnimationEnabled = orbAnimation.Checked;
            orb.ShowTaskStatus = orbTasks.Checked;
            orb.ExpandDelayMs = (int)orbExpandDelay.Value;
            orb.CollapseDelayMs = (int)orbCollapseDelay.Value;
            orb.AnimationDurationMs = (int)orbDuration.Value;
            orb.GlassOpacity = (double)orbGlass.Value / 100;
            orb.GlowStrength = (double)orbGlow.Value / 100;
            orb.CornerRadiusScale = (double)orbRadius.Value / 100;
        }
        public void FocusOrbSettings()
        {
            if (orbGroup != null && orbGroup.Visible)
            {
                for (Control parent = orbGroup.Parent; parent != null; parent = parent.Parent)
                {
                    var scroller = parent as ScrollableControl;
                    if (scroller != null && scroller.AutoScroll) { scroller.ScrollControlIntoView(orbGroup); break; }
                }
                orbSize.Focus();
            }
        }
        public void SetPreviewOrbPosition(OrbPosition position) { working.Orb.Position = position.Clone(); }
        public void SetPreviewStripPosition(int x, int y) { working.DesktopX = x; working.DesktopY = y; }
    }
}

using System;

namespace Fun_Dub_Tool_Box.Utilities.Collections
{
    public enum LogoAnchor
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    public sealed class LogoSettings
    {
        public LogoAnchor Anchor { get; set; } = LogoAnchor.BottomRight;
        public double Opacity { get; set; } = 1.0;
        public bool UseManualPlacement { get; set; }
            = false;
        public double ManualX { get; set; }
            = 0.0;
        public double ManualY { get; set; }
            = 0.0;
        public double ScalePercent { get; set; } = 100.0;

        public LogoSettings Clone()
        {
            return new LogoSettings
            {
                Anchor = Anchor,
                Opacity = Opacity,
                UseManualPlacement = UseManualPlacement,
                ManualX = ManualX,
                ManualY = ManualY,
                ScalePercent = ScalePercent
            };
        }

        public void ApplyManualPosition(double x, double y)
        {
            ManualX = x;
            ManualY = y;
            UseManualPlacement = true;
        }

        public void ApplyAnchor(LogoAnchor anchor)
        {
            Anchor = anchor;
            UseManualPlacement = false;
        }

        public void SetOpacityPercent(double percent)
        {
            Opacity = Math.Clamp(percent, 0, 100) / 100.0;
        }
    }
}
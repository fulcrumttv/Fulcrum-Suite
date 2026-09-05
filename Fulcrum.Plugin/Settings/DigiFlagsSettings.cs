using System;
using System.Xml.Serialization;
namespace Fulcrum.Plugin.Settings
{
    [Serializable]
    public sealed class DigiFlagsSettings
    {
        // Existing serialized names are retained for seamless migration from the prototype.
        public double MirrorWidth { get; set; } = 720.0;
        public double BarWidth { get; set; } = 78.0;
        public double BarHeight { get; set; } = 330.0;
        public double Brightness { get; set; } = 1.0;
        public bool AutoHide { get; set; } = true;
        public double IncidentHoldSeconds { get; set; } = 2.5;
        public int LedColumns { get; set; } = 5;

        public bool Enabled { get; set; } = true;
        public bool PreviewMode { get; set; } = false;
        public double HorizontalOffset { get; set; } = 0.0;
        public double VerticalOffset { get; set; } = 0.0;

        [XmlIgnore]
        public double PanelGap { get { return MirrorWidth; } set { MirrorWidth = value; } }
        [XmlIgnore]
        public double PanelWidth { get { return BarWidth; } set { BarWidth = value; } }
        [XmlIgnore]
        public double PanelHeight { get { return BarHeight; } set { BarHeight = value; } }

        public void Normalize()
        {
            MirrorWidth = Clamp(MirrorWidth, 100.0, 3000.0);
            BarWidth = Clamp(BarWidth, 60.0, 120.0);
            BarHeight = Clamp(BarHeight, 220.0, 460.0);
            HorizontalOffset = Clamp(HorizontalOffset, -400.0, 400.0);
            VerticalOffset = Clamp(VerticalOffset, -120.0, 120.0);
            Brightness = Clamp(Brightness, 0.30, 1.0);
            IncidentHoldSeconds = Clamp(IncidentHoldSeconds, 1.0, 6.0);
            // DigiFlags patterns are designed around an odd center column.
            // Snap any legacy/manual value to the supported 5 / 7 / 9 set.
            if (LedColumns <= 6) LedColumns = 5;
            else if (LedColumns <= 8) LedColumns = 7;
            else LedColumns = 9;
        }
        private static double Clamp(double v,double lo,double hi){ return Math.Max(lo,Math.Min(hi,v)); }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Fulcrum.Plugin.Settings;
using SimHub.Plugins;
namespace Fulcrum.Plugin.Publishing
{
    public sealed class DigiFlagsPublisher
    {
        private const string P="Fulcrum.DigiFlags.";
        private const string S="Fulcrum.Settings.DigiFlags.";
        private readonly PluginManager pm;
        private readonly Type type;
        private DateTime incidentUntil=DateTime.MinValue;
        private int incidentDelta;
        private long sequence;
        private bool flasherPreviousRaw;
        private DateTime flasherCycleStartedAt=DateTime.MinValue;
        private long flasherSequence;

        private const double FlasherCycleMilliseconds=900.0;

        public DigiFlagsPublisher(PluginManager pm,Type type){this.pm=pm;this.type=type; Register();}

        public void PublishSettings(DigiFlagsSettings s)
        {
            if(s==null)return;
            s.Normalize();
            SetS("Enabled",s.Enabled);
            SetS("PreviewMode",s.PreviewMode);
            SetS("PanelGap",s.PanelGap);
            SetS("PanelWidth",s.PanelWidth);
            SetS("PanelHeight",s.PanelHeight);
            SetS("HorizontalOffset",s.HorizontalOffset);
            SetS("VerticalOffset",s.VerticalOffset);
            SetS("Brightness",s.Brightness);
            SetS("AutoHide",s.AutoHide);
            SetS("IncidentHoldSeconds",s.IncidentHoldSeconds);
            SetS("LedColumns",s.LedColumns);
            // Backward-compatible aliases used by the v0.1 prototype.
            SetS("MirrorWidth",s.PanelGap);
            SetS("BarWidth",s.PanelWidth);
            SetS("BarHeight",s.PanelHeight);
        }

        public void Update(object rawData,long flags,int newIncidentDelta,DigiFlagsSettings settings)
        {
            if(settings==null)return;

            bool flasherRaw=ReadTelemetryBool(rawData,"dcHeadlightFlash",false);
            DateTime now=DateTime.UtcNow;
            if(flasherRaw && !flasherPreviousRaw)
            {
                flasherCycleStartedAt=now;
                flasherSequence++;
            }
            flasherPreviousRaw=flasherRaw;

            double flasherElapsed=flasherCycleStartedAt==DateTime.MinValue
                ? double.PositiveInfinity
                : (now-flasherCycleStartedAt).TotalMilliseconds;

            if(flasherElapsed<0.0)
            {
                flasherCycleStartedAt=now;
                flasherElapsed=0.0;
            }

            bool flasherActive=
                flasherRaw ||
                (flasherElapsed>=0.0 && flasherElapsed<FlasherCycleMilliseconds);

            double flasherPhaseMs=0.0;
            if(flasherActive && !double.IsInfinity(flasherElapsed))
            {
                flasherPhaseMs=flasherElapsed%FlasherCycleMilliseconds;
                if(flasherPhaseMs<0.0)flasherPhaseMs+=FlasherCycleMilliseconds;
            }

            bool flasherOn=
                flasherActive &&
                (flasherPhaseMs<110.0 ||
                 (flasherPhaseMs>=190.0 && flasherPhaseMs<300.0) ||
                 (flasherPhaseMs>=380.0 && flasherPhaseMs<490.0));

            if(newIncidentDelta>0)
            {
                incidentDelta=newIncidentDelta;
                sequence++;
                incidentUntil=DateTime.UtcNow.AddSeconds(settings.IncidentHoldSeconds);
            }
            bool incident=DateTime.UtcNow<incidentUntil;
            string state="None", color="#00E5EB", anim="Off";
            int severity=0;
            Func<long,bool> H=m=>(flags&m)!=0;

            const long Checkered=0x00000001;
            const long White=0x00000002;
            const long Green=0x00000004;
            const long Yellow=0x00000008;
            const long Red=0x00000010;
            const long Blue=0x00000020;
            const long Debris=0x00000040;
            const long Crossed=0x00000080;
            const long YellowWaving=0x00000100;
            const long OneLap=0x00000200;
            const long GreenHeld=0x00000400;
            const long Caution=0x00004000;
            const long CautionWaving=0x00008000;
            const long Black=0x00010000;
            const long DQ=0x00020000;
            const long Furled=0x00080000;
            const long Repair=0x00100000;
            const long StartReady=0x20000000;
            const long StartSet=0x40000000;
            const long StartGo=0x80000000;

            if(settings.PreviewMode){state="Preview";color="#00E5EB";anim="Preview";severity=0;}
            else if(H(DQ)){state="Disqualified";color="#FFFFFF";anim="TripleX";severity=6;}
            else if(H(Repair)){state="Meatball";color="#FF8A00";anim="Meatball";severity=5;}
            else if(H(Black)){state="Black";color="#FFFFFF";anim="TripleX";severity=5;}
            else if(H(Furled)){state="FurledBlack";color="#FFFFFF";anim="TripleXSlow";severity=4;}
            else if(H(Red)){state="Red";color="#FF2D2D";anim="FullBlink";severity=6;}
            else if(H(StartSet)||H(StartReady)){state="StartRed";color="#FF3030";anim="Solid";severity=4;}
            else if(H(StartGo)){state="StartGo";color="#00E676";anim="FullBlink";severity=2;}
            else if(incident){state=incidentDelta==1?"OffTrack":"Incident";color=incidentDelta==1?"#FFA000":"#FF2D2D";anim=incidentDelta==1?"OffTrack":"Incident";severity=incidentDelta==1?3:5;}
            else if(H(Checkered)){state="Checkered";color="#FFFFFF";anim="Checker";severity=2;}
            else if(H(CautionWaving)){state="CautionWaving";color="#FFD500";anim="CautionWaving";severity=5;}
            else if(H(Caution)){state="Caution";color="#FFD500";anim="Caution";severity=4;}
            else if(H(YellowWaving)){state="YellowWaving";color="#FFD500";anim="YellowWaving";severity=4;}
            else if(H(Yellow)){state="Yellow";color="#FFD500";anim="Solid";severity=3;}
            else if(H(Debris)){state="Debris";color="#FFD500";anim="Debris";severity=3;}
            else if(H(White)){state="White";color="#FFFFFF";anim="FullBlink";severity=2;}
            else if(H(Blue)){state="Blue";color="#2D7BFF";anim="BlueYellow";severity=2;}
            else if(H(OneLap)){state="OneLapToGreen";color="#00E676";anim="OneLap";severity=2;}
            else if(H(GreenHeld)){state="GreenHeld";color="#00E676";anim="Solid";severity=2;}
            else if(H(Green)){state="Green";color="#00E676";anim="FullBlink";severity=1;}
            else if(H(Crossed)){state="Crossed";color="#FFFFFF";anim="Crossed";severity=1;}
            else if(flasherActive){state="Flasher";color="#FFFFFF";anim="TripleFlash";severity=1;}

            bool visible=settings.Enabled && (settings.PreviewMode || state!="None" || !settings.AutoHide);
            Set("State",state);
            Set("Color",color);
            Set("Animation",anim);
            Set("Severity",severity);
            Set("Visible",visible);
            Set("IncidentActive",incident);
            Set("IncidentDelta",incident?incidentDelta:0);
            Set("IncidentSequence",sequence);
            Set("SessionFlagsRaw",flags);
            Set("FlasherRaw",flasherRaw);
            Set("FlasherActive",flasherActive);
            Set("FlasherOn",flasherOn);
            Set("FlasherPhaseMs",flasherPhaseMs);
            Set("FlasherSequence",flasherSequence);
            Set("Phase",(DateTime.UtcNow.TimeOfDay.TotalMilliseconds%1000.0)/1000.0);
        }

        public void ResetRuntime()
        {
            incidentUntil=DateTime.MinValue;
            incidentDelta=0;
            flasherPreviousRaw=false;
            flasherCycleStartedAt=DateTime.MinValue;
            Set("FlasherRaw",false);
            Set("FlasherActive",false);
            Set("FlasherOn",false);
            Set("FlasherPhaseMs",0.0);
        }

        private void Register()
        {
            Add("State","None");Add("Color","#00E5EB");Add("Animation","Off");Add("Severity",0);Add("Visible",false);
            Add("IncidentActive",false);Add("IncidentDelta",0);Add("IncidentSequence",0L);Add("SessionFlagsRaw",0L);Add("Phase",0.0);
            Add("FlasherRaw",false);Add("FlasherActive",false);Add("FlasherOn",false);Add("FlasherPhaseMs",0.0);Add("FlasherSequence",0L);
            AddS("Enabled",true);AddS("PreviewMode",false);AddS("PanelGap",720.0);AddS("PanelWidth",78.0);AddS("PanelHeight",330.0);
            AddS("HorizontalOffset",0.0);AddS("VerticalOffset",0.0);AddS("Brightness",1.0);AddS("AutoHide",true);AddS("IncidentHoldSeconds",2.5);AddS("LedColumns",5);
            AddS("MirrorWidth",720.0);AddS("BarWidth",78.0);AddS("BarHeight",330.0);
        }
        private static bool ReadTelemetryBool(object rawData,string key,bool fallback)
        {
            object telemetry=GetMember(rawData,"CurrentTelemetry");
            if(telemetry==null)telemetry=GetMember(rawData,"Telemetry");
            object value;
            if(!TryReadTelemetryValue(telemetry,key,out value))return fallback;
            if(value==null)return fallback;
            if(value is bool)return (bool)value;
            if(value is byte)return (byte)value!=0;
            if(value is sbyte)return (sbyte)value!=0;
            if(value is short)return (short)value!=0;
            if(value is ushort)return (ushort)value!=0;
            if(value is int)return (int)value!=0;
            if(value is uint)return (uint)value!=0;
            if(value is long)return (long)value!=0;
            if(value is ulong)return (ulong)value!=0;
            string text=Convert.ToString(value);
            if(string.IsNullOrWhiteSpace(text))return fallback;
            bool parsedBool;
            if(bool.TryParse(text,out parsedBool))return parsedBool;
            double parsedNumber;
            return double.TryParse(text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out parsedNumber)
                ? Math.Abs(parsedNumber)>double.Epsilon
                : fallback;
        }

        private static bool TryReadTelemetryValue(object telemetry,string key,out object value)
        {
            value=null;
            if(telemetry==null)return false;

            IDictionary<string,object> generic=telemetry as IDictionary<string,object>;
            if(generic!=null)return generic.TryGetValue(key,out value);

            IReadOnlyDictionary<string,object> readOnly=telemetry as IReadOnlyDictionary<string,object>;
            if(readOnly!=null)return readOnly.TryGetValue(key,out value);

            IDictionary dictionary=telemetry as IDictionary;
            if(dictionary!=null)
            {
                if(dictionary.Contains(key)){value=dictionary[key];return true;}
                foreach(DictionaryEntry entry in dictionary)
                {
                    if(string.Equals(Convert.ToString(entry.Key),key,StringComparison.OrdinalIgnoreCase))
                    {value=entry.Value;return true;}
                }
            }

            IEnumerable enumerable=telemetry as IEnumerable;
            if(enumerable==null)return false;
            foreach(object item in enumerable)
            {
                if(item==null)continue;
                object itemKey=GetMember(item,"Key");
                if(!string.Equals(Convert.ToString(itemKey),key,StringComparison.OrdinalIgnoreCase))continue;
                value=GetMember(item,"Value");
                return true;
            }
            return false;
        }

        private static object GetMember(object source,string name)
        {
            if(source==null)return null;

            IDictionary dictionary=source as IDictionary;
            if(dictionary!=null)
            {
                if(dictionary.Contains(name))return dictionary[name];
                foreach(DictionaryEntry entry in dictionary)
                    if(string.Equals(Convert.ToString(entry.Key),name,StringComparison.OrdinalIgnoreCase))
                        return entry.Value;
            }

            const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.IgnoreCase;
            Type sourceType=source.GetType();
            PropertyInfo property=sourceType.GetProperty(name,flags);
            if(property!=null && property.GetIndexParameters().Length==0)
            {
                try{return property.GetValue(source,null);}catch{return null;}
            }
            FieldInfo field=sourceType.GetField(name,flags);
            if(field!=null)
            {
                try{return field.GetValue(source);}catch{return null;}
            }
            return null;
        }

        private void Add(string n,object v){pm.AddProperty(P+n,type,v,"Fulcrum DigiFlags runtime");}
        private void Set(string n,object v){pm.SetPropertyValue(P+n,type,v);}
        private void AddS(string n,object v){pm.AddProperty(S+n,type,v,"Fulcrum DigiFlags setting");}
        private void SetS(string n,object v){pm.SetPropertyValue(S+n,type,v);}
    }
}

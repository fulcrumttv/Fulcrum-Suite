using System;
using System.IO;
using System.Xml.Serialization;
namespace Fulcrum.Plugin.Settings
{
    internal static class DigiFlagsSettingsStore
    {
        private const string FolderName="FulcrumSuite";
        private const string FileName="digiflags-settings.xml";
        public static DigiFlagsSettings Load()
        {
            try { string p=GetPath(); if(!File.Exists(p)) return new DigiFlagsSettings();
                var ser=new XmlSerializer(typeof(DigiFlagsSettings)); using(var fs=File.OpenRead(p)){ var s=ser.Deserialize(fs) as DigiFlagsSettings ?? new DigiFlagsSettings(); s.Normalize(); return s; } }
            catch { return new DigiFlagsSettings(); }
        }
        public static void Save(DigiFlagsSettings s)
        {
            if(s==null)return; try { s.Normalize(); string p=GetPath(), d=Path.GetDirectoryName(p); if(!Directory.Exists(d))Directory.CreateDirectory(d);
                string t=p+".tmp"; var ser=new XmlSerializer(typeof(DigiFlagsSettings)); using(var fs=File.Create(t))ser.Serialize(fs,s); if(File.Exists(p))File.Delete(p); File.Move(t,p); } catch { }
        }
        private static string GetPath(){ return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"SimHub","PluginsData",FolderName,FileName); }
    }
}

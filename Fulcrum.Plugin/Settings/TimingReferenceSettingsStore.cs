using System;
using System.IO;
using System.Xml.Serialization;

namespace Fulcrum.Plugin.Settings
{
    internal static class TimingReferenceSettingsStore
    {
        private const string FolderName = "FulcrumSuite";
        private const string FileName = "timing-reference-settings.xml";

        public static TimingReferenceSettings Load()
        {
            try
            {
                string path = GetPath();
                if (!File.Exists(path))
                {
                    return new TimingReferenceSettings();
                }

                XmlSerializer serializer = new XmlSerializer(typeof(TimingReferenceSettings));
                using (FileStream stream = File.OpenRead(path))
                {
                    TimingReferenceSettings settings =
                        serializer.Deserialize(stream) as TimingReferenceSettings ??
                        new TimingReferenceSettings();

                    settings.Normalize();
                    return settings;
                }
            }
            catch
            {
                return new TimingReferenceSettings();
            }
        }

        public static void Save(TimingReferenceSettings settings)
        {
            if (settings == null) return;

            try
            {
                settings.Normalize();

                string path = GetPath();
                string directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string temp = path + ".tmp";
                XmlSerializer serializer = new XmlSerializer(typeof(TimingReferenceSettings));
                using (FileStream stream = File.Create(temp))
                {
                    serializer.Serialize(stream, settings);
                }

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(temp, path);
            }
            catch
            {
            }
        }

        private static string GetPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SimHub",
                "PluginsData",
                FolderName,
                FileName);
        }
    }
}

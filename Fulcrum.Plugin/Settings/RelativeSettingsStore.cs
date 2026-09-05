using System;
using System.IO;
using System.Xml.Serialization;

namespace Fulcrum.Plugin.Settings
{
    internal static class RelativeSettingsStore
    {
        private const string FolderName = "FulcrumSuite";
        private const string FileName = "relative-settings.xml";

        public static RelativeOverlaySettings Load()
        {
            string path = GetPath();

            try
            {
                if (!File.Exists(path))
                {
                    return new RelativeOverlaySettings();
                }

                XmlSerializer serializer =
                    new XmlSerializer(typeof(RelativeOverlaySettings));

                using (FileStream stream = File.OpenRead(path))
                {
                    RelativeOverlaySettings settings =
                        serializer.Deserialize(stream) as RelativeOverlaySettings;

                    if (settings == null)
                    {
                        settings = new RelativeOverlaySettings();
                    }

                    settings.Normalize();
                    return settings;
                }
            }
            catch
            {
                return new RelativeOverlaySettings();
            }
        }

        public static void Save(RelativeOverlaySettings settings)
        {
            if (settings == null)
            {
                return;
            }

            try
            {
                settings.Normalize();

                string path = GetPath();
                string directory = Path.GetDirectoryName(path);

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                XmlSerializer serializer =
                    new XmlSerializer(typeof(RelativeOverlaySettings));

                string temporaryPath = path + ".tmp";

                using (FileStream stream = File.Create(temporaryPath))
                {
                    serializer.Serialize(stream, settings);
                }

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(temporaryPath, path);
            }
            catch
            {
                // Settings failures must never interrupt telemetry processing.
            }
        }

        private static string GetPath()
        {
            string applicationData =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData);

            return Path.Combine(
                applicationData,
                "SimHub",
                "PluginsData",
                FolderName,
                FileName);
        }
    }
}

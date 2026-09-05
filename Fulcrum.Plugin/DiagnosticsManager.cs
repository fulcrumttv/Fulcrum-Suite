using System;
using System.IO;
using Fulcrum.Core.Diagnostics;

namespace Fulcrum.Plugin
{
    public class DiagnosticsManager
    {
        private bool reportGenerated;

        public bool ReportGenerated
        {
            get { return reportGenerated; }
        }

        public string LastReportPath
        {
            get;
            private set;
        }

        public string LastError
        {
            get;
            private set;
        }

        public DiagnosticsManager()
        {
            reportGenerated = false;
            LastReportPath = string.Empty;
            LastError = string.Empty;
        }

        public bool TryGenerateReport(
            object rawData,
            string gameName)
        {
            if (reportGenerated)
            {
                return false;
            }

            if (rawData == null)
            {
                LastError =
                    "The raw telemetry object is null.";

                return false;
            }

            try
            {
                string documentsFolder =
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.MyDocuments);

                string diagnosticsFolder = Path.Combine(
                    documentsFolder,
                    "Fulcrum Suite",
                    "Diagnostics");

                Directory.CreateDirectory(
                    diagnosticsFolder);

                string safeGameName =
                    CreateSafeFileName(gameName);

                string fileName =
                    safeGameName +
                    "_Telemetry_" +
                    DateTime.Now.ToString(
                        "yyyy-MM-dd_HH-mm-ss") +
                    ".txt";

                string reportPath = Path.Combine(
                    diagnosticsFolder,
                    fileName);

                string objectReport =
                    ObjectInspector.Inspect(rawData);

                string telemetryReport =
                    TelemetryInspector.Inspect(rawData);

                string completeReport =
                    objectReport +
                    telemetryReport;

                File.WriteAllText(
                    reportPath,
                    completeReport);

                LastReportPath = reportPath;
                LastError = string.Empty;
                reportGenerated = true;

                return true;
            }
            catch (Exception exception)
            {
                LastError = exception.ToString();
                return false;
            }
        }

        public void Reset()
        {
            reportGenerated = false;
            LastReportPath = string.Empty;
            LastError = string.Empty;
        }

        private static string CreateSafeFileName(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "UnknownGame";
            }

            char[] invalidCharacters =
                Path.GetInvalidFileNameChars();

            foreach (
                char invalidCharacter
                in invalidCharacters)
            {
                value = value.Replace(
                    invalidCharacter,
                    '_');
            }

            return value;
        }
    }
}
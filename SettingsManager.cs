using System;
using System.IO;
using System.Xml.Serialization;

namespace PS3_XMB_Tools
{
    public static class SettingsManager
    {
        private static readonly string settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.xml");

        public static void SaveSettings(AppSettings settings)
        {
            var serializer = new XmlSerializer(typeof(AppSettings));
            using (var writer = new StreamWriter(settingsFilePath))
            {
                serializer.Serialize(writer, settings);
            }
        }

        public static AppSettings LoadSettings()
        {
            if (!File.Exists(settingsFilePath))
            {
                return new AppSettings(); // Return default settings if file doesn't exist
            }

            try
            {
                var serializer = new XmlSerializer(typeof(AppSettings));
                using (var reader = new StreamReader(settingsFilePath))
                {
                    return (AppSettings)serializer.Deserialize(reader);
                }
            }
            catch (Exception ex)
            {
                // Log the exception if necessary (e.g., to a log file)
                System.Diagnostics.Debug.WriteLine("Error reading settings file: " + ex.Message);

                // If deserialization fails, delete the corrupt settings file
                if (File.Exists(settingsFilePath))
                {
                    File.Delete(settingsFilePath);
                }

                // Create a new AppSettings instance with default values
                var settings = new AppSettings();
                SaveSettings(settings); // Save the new settings file
                return settings; // Return the new instance with default values
            }
        }
    }
}

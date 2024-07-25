namespace PS3_XMB_Tools
{
    public class AppSettings
    {
        public string ThemeColor { get; set; }
        public SaveDebugLog SaveDebugLogToggle { get; set; }
        public RememberLastTabUsed LastTabUsed { get; set; }
        public string PS3IP { get; set; }
        public string InitialFolderPath { get; set; } // Changed property name

        public AppSettings()
        {
            ThemeColor = "#fc030f"; // Default color as a string
            SaveDebugLogToggle = SaveDebugLog.True;
            LastTabUsed = RememberLastTabUsed.XMBML;
            PS3IP = "PUT PS3 IP HERE"; // Default IP address
            InitialFolderPath = "dev_hdd0/game/NPIA00005/"; // Default initial folder path
        }
    }
}

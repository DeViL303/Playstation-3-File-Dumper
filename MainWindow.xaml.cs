using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ColorPicker;
using HtmlAgilityPack;
using System.IO.Compression;
using FluentFTP;
using System.Net;


namespace PS3_XMB_Tools
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private AppSettings _settings;
        private SolidColorBrush _selectedThemeColor;
        private TaskbarIconUpdater _iconUpdater;
        private Logger _logger;


        public SolidColorBrush SelectedThemeColor
        {
            get { return _selectedThemeColor; }
            set
            {
                if (_selectedThemeColor != value)
                {
                    _selectedThemeColor = value;
                    OnPropertyChanged(nameof(SelectedThemeColor));
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            // Initialize logger
            _logger = new Logger(AppDomain.CurrentDomain.BaseDirectory, "debug.log");

            // Load settings and ensure it's not null
            _settings = SettingsManager.LoadSettings() ?? new AppSettings();
            Color themeColor = (Color)ColorConverter.ConvertFromString(_settings.ThemeColor);
            _selectedThemeColor = new SolidColorBrush(themeColor);
            ApplySettingsToUI();
            _iconUpdater = new TaskbarIconUpdater();
            _iconUpdater.UpdateTaskbarIconWithTint(this, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.png"), themeColor);

            // Initialize PS3IPforFTPTextBox and PS3FolderToDumpTextBox with the saved settings
            PS3IPforFTPTextBox.Text = _settings.PS3IP ?? "PUT PS3 IP HERE";
            PS3FolderToDumpTextBox.Text = _settings.InitialFolderPath ?? "dev_hdd0/game/NPIA00005/";
        }

        public Color ThemeColor
        {
            get => (Color)ColorConverter.ConvertFromString(_settings.ThemeColor);
            set
            {
                if (_settings.ThemeColor != value.ToString())
                {
                    _settings.ThemeColor = value.ToString();
                    SettingsManager.SaveSettings(_settings);
                    OnPropertyChanged(nameof(ThemeColor));
                }
            }
        }

        private void ApplySettingsToUI()
        {
            ThemeColorPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(_settings.ThemeColor);

        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl && e.AddedItems.Count > 0)
            {
                var selectedTab = (TabItem)e.AddedItems[0];
                if (Enum.TryParse<RememberLastTabUsed>(selectedTab.Tag.ToString(), out var lastTabUsed))
                {
                    _settings.LastTabUsed = lastTabUsed;
                    SettingsManager.SaveSettings(_settings);
                }
            }
        }



        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                this.DragMove();
        }

        private void ThemeColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (e.NewValue.HasValue)
            {
                SelectedThemeColor = new SolidColorBrush(e.NewValue.Value);
                _settings.ThemeColor = e.NewValue.Value.ToString();
                SettingsManager.SaveSettings(_settings);
            }
        }

        private void NewColorPicker_ColorChanged(object sender, EventArgs e)
        {
            var colorPicker = sender as StandardColorPicker;
            if (colorPicker != null)
            {
                Color newColor = colorPicker.SelectedColor;
                SelectedThemeColor = new SolidColorBrush(newColor);
                _settings.ThemeColor = newColor.ToString();
                SettingsManager.SaveSettings(_settings);
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dependencies\\icon.png");
                _iconUpdater.UpdateTaskbarIconWithTint(this, iconPath, newColor);
            }
        }

        private void PS3IPforFTPTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.PS3IP = PS3IPforFTPTextBox.Text;
                SettingsManager.SaveSettings(_settings);
            }
        }

        private void PS3FolderToDumpTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.InitialFolderPath = PS3FolderToDumpTextBox.Text;
                SettingsManager.SaveSettings(_settings);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged = delegate { };

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public class DownloadInfo : INotifyPropertyChanged
        {
            private string filePath;
            private string status;
            private string progress;
            private string speed;
            private string totalSize;

            public string FilePath
            {
                get { return filePath; }
                set { filePath = value; OnPropertyChanged(nameof(FilePath)); }
            }

            public string Status
            {
                get { return status; }
                set { status = value; OnPropertyChanged(nameof(Status)); }
            }

            public string Progress
            {
                get { return progress; }
                set { progress = value; OnPropertyChanged(nameof(Progress)); }
            }

            public string Speed
            {
                get { return speed; }
                set { speed = value; OnPropertyChanged(nameof(Speed)); }
            }

            public string TotalSize
            {
                get { return totalSize; }
                set { totalSize = value; OnPropertyChanged(nameof(TotalSize)); }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private ObservableCollection<DownloadInfo> downloadInfos = new ObservableCollection<DownloadInfo>();

        private async void PS3PingButton_Click(object sender, RoutedEventArgs e)
        {
            PingResultTextBlock.Text = "Checking...";
            PingResultTextBlock.Foreground = new SolidColorBrush(Colors.Yellow);

            string ipAddress = PS3IPforFTPTextBox.Text;
            var checker = new PS3Checker(_logger);

            bool isValid = false;
            string message = "";
            bool isFtpSuccessful = false;
            bool isHttpSuccessful = false;

            int retryCount = 0;
            int maxRetries = 6;

            while (retryCount < maxRetries)
            {
                (isValid, message, isFtpSuccessful, isHttpSuccessful) = await checker.CheckPS3Async(ipAddress);

                if (isValid)
                {
                    break; // Exit the loop if the check is successful
                }

                retryCount++;
                if (retryCount < maxRetries)
                {
                    PingResultTextBlock.Text = $"Checking....";
                    await Task.Delay(1000); // Wait for 1 second before retrying
                }
            }

            PingResultTextBlock.Text = message;

            if (isHttpSuccessful)
            {
                PingResultTextBlock.Text += " - HTTP Available";
            }
            else
            {
                PingResultTextBlock.Text += "- HTTP Not Available";
            }

            if (isValid)
            {
                PingResultTextBlock.Foreground = new SolidColorBrush(Colors.LimeGreen);
            }
            else
            {
                PingResultTextBlock.Foreground = new SolidColorBrush(Colors.Red);
            }
        }

        private async void PS3DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (UseFTPCheckBox.IsChecked == true)
            {
                await DownloadOverFTP(PS3IPforFTPTextBox.Text, PS3FolderToDumpTextBox.Text);
                return;
            }

            PingResultTextBlock.Text = "Building Download List - Please Wait...";
            PingResultTextBlock.Foreground = new SolidColorBrush(Colors.Yellow);
            ActivityLog.ItemsSource = downloadInfos;
            downloadInfos.Clear();

            string downloadListPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "downloadlist.txt");
            if (File.Exists(downloadListPath))
            {
                File.Delete(downloadListPath);
            }

            string downloadListTempPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "downloadlist_temp.txt");
            if (File.Exists(downloadListTempPath))
            {
                File.Delete(downloadListTempPath);
            }

            string ipAddress = PS3IPforFTPTextBox.Text;
            string initialFolderPath = PS3FolderToDumpTextBox.Text;

            if (!initialFolderPath.Contains(".") && !initialFolderPath.EndsWith("/"))
            {
                initialFolderPath += "/";
            }

            string initialFolderUrl = $"http://{ipAddress}/{initialFolderPath}";

            await ProcessInitialFolderAsync(initialFolderUrl, downloadListPath);
            var fileCount1 = GetTotalFileCount(downloadListPath);

            await Task.Delay(3000);

            await ProcessInitialFolderAsync(initialFolderUrl, downloadListTempPath);
            var fileCount2 = GetTotalFileCount(downloadListTempPath);

            if (fileCount1 != fileCount2)
            {
                PingResultTextBlock.Text = "Error - Try again";
                PingResultTextBlock.Foreground = new SolidColorBrush(Colors.Red);
                _logger.LogDebugInfo("File counts do not match after rechecking.");
                return;
            }

            PingResultTextBlock.Text = "Downloading Please Wait...";
            PingResultTextBlock.Foreground = new SolidColorBrush(Colors.LimeGreen);

            await FilterAndDownloadFilesAsync(downloadListPath);

            bool allFilesDownloaded = downloadInfos.All(info => info.Status == "Completed");

            string dumpedDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DUMPED");
            if (!Directory.Exists(dumpedDirectory))
            {
                Directory.CreateDirectory(dumpedDirectory);
            }

            string folderPathToZip = Path.Combine(dumpedDirectory, initialFolderPath.TrimEnd('/', '\\'));
            string folderName = Path.GetFileName(folderPathToZip);
            string zipFilePath = Path.Combine(dumpedDirectory, $"{folderName}.zip");

            // Check if there are any files in the folder to be zipped
            bool filesExistToZip = Directory.Exists(folderPathToZip) && Directory.GetFiles(folderPathToZip, "*", SearchOption.AllDirectories).Length > 0;

            if (allFilesDownloaded || filesExistToZip)
            {
                if (ZipResultsCheckBox.IsChecked == true)
                {
                    ZipFolder(folderPathToZip, zipFilePath);
                    PingResultTextBlock.Text = "Download and zipping completed successfully.";
                }
                else
                {
                    PingResultTextBlock.Text = "Download completed successfully.";
                }
                PingResultTextBlock.Foreground = new SolidColorBrush(Colors.LimeGreen);
            }
            else
            {
                PingResultTextBlock.Text = "No Files Downloaded";
                PingResultTextBlock.Foreground = new SolidColorBrush(Colors.Red);
            }
        }

        private void ZipFolder(string folderPath, string zipFilePath)
        {
            if (Directory.Exists(folderPath))
            {
                // Trim the trailing slash or backslash from the folderPath
                folderPath = folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                // Ensure unique zip file path
                string tempZipFilePath = zipFilePath;
                string extension = ".zip";
                int suffix = 1;

                while (File.Exists(tempZipFilePath))
                {
                    tempZipFilePath = Path.Combine(Path.GetDirectoryName(zipFilePath), $"{Path.GetFileNameWithoutExtension(zipFilePath)}_{suffix}{extension}");
                    suffix++;
                }

                // Create the zip file including the folder itself
                ZipFile.CreateFromDirectory(folderPath, tempZipFilePath, CompressionLevel.Fastest, true);
            }
        }


        private int GetTotalFileCount(string downloadListPath)
        {
            if (!File.Exists(downloadListPath))
            {
                return 0;
            }

            var allLines = File.ReadAllLines(downloadListPath);
            return allLines.Count(line => !line.EndsWith("/"));
        }

        private async Task ProcessInitialFolderAsync(string initialFolderUrl, string downloadListPath)
        {
            var folderUrls = await ListFilesInFolderAsync(initialFolderUrl, initialFolderUrl, downloadListPath);
            await ProcessSubfoldersAsync(folderUrls, initialFolderUrl, downloadListPath);
        }

        private async Task ProcessSubfoldersAsync(List<string> folderUrls, string initialFolderUrl, string downloadListPath)
        {
            HashSet<string> processedFolders = new HashSet<string>();

            while (folderUrls.Count > 0)
            {
                string currentFolderUrl = folderUrls[0];
                folderUrls.RemoveAt(0);

                // Avoid processing the same folder multiple times
                if (processedFolders.Contains(currentFolderUrl))
                    continue;

                processedFolders.Add(currentFolderUrl);

                var newFolders = await ListFilesInFolderAsync(currentFolderUrl, initialFolderUrl, downloadListPath);
                folderUrls.AddRange(newFolders);
            }
        }

        private async Task<List<string>> ListFilesInFolderAsync(string folderUrl, string initialFolderUrl, string downloadListPath)
        {
            List<string> newFolders = new List<string>();
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5))) // Increase timeout to 5 seconds
                    {
                        int maxRetries = 5; // Maximum number of retries
                        int retryCount = 0;
                        bool success = false;

                        while (retryCount < maxRetries && !success)
                        {
                            try
                            {
                                HttpResponseMessage response = await client.GetAsync(folderUrl, cts.Token);
                                if (response.IsSuccessStatusCode)
                                {
                                    string responseBody = await response.Content.ReadAsStringAsync();
                                    var filesAndFolders = ParseHtmlForFilesAndFolders(responseBody, folderUrl, initialFolderUrl);

                                    // Add files to download list, excluding those ending with a slash
                                    var filesToAdd = filesAndFolders.files.Where(f => !f.EndsWith("/")).ToList();
                                    await File.AppendAllLinesAsync(downloadListPath, filesToAdd);

                                    // Collect new folders to check
                                    newFolders.AddRange(filesAndFolders.folders);
                                    success = true; // Exit the retry loop on success
                                }
                                else
                                {
                                    _logger.LogDebugInfo($"Failed to retrieve file list from {folderUrl}. Status code: {response.StatusCode}");
                                }
                            }
                            catch (TaskCanceledException)
                            {
                                _logger.LogDebugInfo($"Timeout while listing files in folder {folderUrl}, retrying...");
                                retryCount++;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebugInfo($"Error while listing files in folder {folderUrl}: {ex.Message}, retrying...");
                                retryCount++;
                            }
                        }

                        if (!success)
                        {
                            _logger.LogDebugInfo($"Failed to retrieve file list from {folderUrl} after {maxRetries} retries.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebugInfo($"Error while listing files in folder {folderUrl}: {ex.Message}");
                }
            }

            return newFolders;
        }

        private (List<string> files, List<string> folders) ParseHtmlForFilesAndFolders(string html, string baseUrl, string initialFolderUrl)
        {
            List<string> files = new List<string>();
            List<string> folders = new List<string>();
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);

            var rows = doc.DocumentNode.SelectNodes("//tr");
            if (rows != null)
            {
                foreach (HtmlNode row in rows)
                {
                    HtmlNode link = row.SelectSingleNode(".//a[@href]");

                    if (link != null)
                    {
                        string hrefValue = link.GetAttributeValue("href", string.Empty);

                        // Treat as folder if it does not contain a period or "_INF"
                        if (!string.IsNullOrEmpty(hrefValue) && !hrefValue.Contains(".") && !hrefValue.Contains("_INF") && !hrefValue.Contains("STATE") && !hrefValue.Contains("RESERVED") && !hrefValue.Contains("#"))
                        {
                            string folderUri = new Uri(new Uri(baseUrl), hrefValue + "/").AbsoluteUri;
                            if (folderUri.StartsWith(initialFolderUrl))
                            {
                                folders.Add(folderUri);
                            }
                        }
                        // Treat as file if it contains a period or "_INF"
                        else if (!string.IsNullOrEmpty(hrefValue) && (hrefValue.Contains(".") || hrefValue.Contains("_INF") || hrefValue.Contains("STATE") || hrefValue.Contains("RESERVED")))
                        {
                            string fileUri = new Uri(new Uri(baseUrl), hrefValue).AbsoluteUri;
                            if (fileUri.StartsWith(initialFolderUrl))
                            {
                                files.Add(fileUri);
                            }
                        }
                    }
                }
            }

            return (files, folders);
        }

        private async Task FilterAndDownloadFilesAsync(string downloadListPath)
        {
            if (!File.Exists(downloadListPath))
            {
                _logger.LogDebugInfo("Download list file not found.");
                return;
            }

            // Read the file and filter out URLs ending with a slash
            var allLines = await File.ReadAllLinesAsync(downloadListPath);
            var fileUrls = allLines.Where(line => !line.EndsWith("/")).ToList();

            if (SkipRESERVEDFilesCheckBox.IsChecked == true)
            {
                // Skip RESERVED files
                fileUrls = fileUrls.Where(url => !Path.GetFileName(url).Contains("RESERVED")).ToList();
            }
            else
            {
                // Move RESERVED files to the end of the list
                var nonReservedUrls = fileUrls.Where(url => !Path.GetFileName(url).Contains("RESERVED")).ToList();
                var reservedUrls = fileUrls.Where(url => Path.GetFileName(url).Contains("RESERVED")).ToList();
                fileUrls = nonReservedUrls.Concat(reservedUrls).ToList();
            }

            // Check for existing files
            int existingFileCount = 0;
            var existingFilePaths = new List<string>();
            foreach (var fileUrl in fileUrls)
            {
                var uri = new Uri(fileUrl);
                var filePath = uri.AbsolutePath.Substring(1); // Remove leading slash
                var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DUMPED", filePath);
                if (File.Exists(localPath))
                {
                    existingFileCount++;
                    existingFilePaths.Add(localPath);
                }
            }

            if (existingFileCount > 0)
            {
                var result = CustomMessageBox.Show($"{existingFileCount} files already exist. Do you want to clear them or resume?");
                if (result == CustomMessageBox.CustomMessageBoxResult.Clear)
                {
                    // Clear existing files
                    foreach (var localPath in existingFilePaths)
                    {
                        try
                        {
                            File.Delete(localPath);
                            _logger.LogDebugInfo($"Deleted file: {localPath}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebugInfo($"Failed to delete file {localPath}: {ex.Message}");
                        }
                    }
                }
                else if (result == CustomMessageBox.CustomMessageBoxResult.Resume)
                {
                    // Remove existing files from the download list
                    fileUrls = fileUrls.Where(url => !existingFilePaths.Contains(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DUMPED", new Uri(url).AbsolutePath.Substring(1)))).ToList();
                }
                else if (result == CustomMessageBox.CustomMessageBoxResult.Cancel)
                {
                    return;
                }
            }

            // Write back the modified list to the file
            await File.WriteAllLinesAsync(downloadListPath, fileUrls);

            int totalFiles = fileUrls.Count;
            int completedFiles = 0;

            UpdateProgressText(totalFiles, completedFiles);

            using (HttpClient client = new HttpClient())
            {
                var tasks = new List<Task>();
                var semaphore = new SemaphoreSlim(4); // Allow up to 4 concurrent downloads

                foreach (var fileUrl in fileUrls)
                {
                    await semaphore.WaitAsync(); // Wait if there are already 4 downloads running

                    var downloadInfo = new DownloadInfo { FilePath = fileUrl, Status = "Starting..." };
                    downloadInfos.Add(downloadInfo);
                    AutoScrollToBottom(); // Auto scroll to bottom if at bottom

                    tasks.Add(Task.Run(async () =>
                    {
                        int retryCount = 0;
                        bool success = false;

                        while (retryCount < 10 && !success)
                        {
                            try
                            {
                                var uri = new Uri(fileUrl);
                                var filePath = uri.AbsolutePath.Substring(1); // Remove leading slash
                                var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DUMPED", filePath);

                                Directory.CreateDirectory(Path.GetDirectoryName(localPath));

                                var response = await client.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead);
                                response.EnsureSuccessStatusCode();

                                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                                if (totalBytes >= 0)
                                {
                                    downloadInfo.TotalSize = totalBytes < 1024 * 1024
                                        ? $"{totalBytes / 1024.0:0.00} KB"
                                        : $"{totalBytes / 1024.0 / 1024.0:0.00} MB";
                                }
                                else
                                {
                                    downloadInfo.TotalSize = "Unknown";
                                }
                                var canReportProgress = totalBytes != -1;
                                var startTime = DateTime.UtcNow;

                                using (var contentStream = await response.Content.ReadAsStreamAsync())
                                using (var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                                {
                                    var totalRead = 0L;
                                    var buffer = new byte[8192];
                                    var isMoreToRead = true;

                                    if (totalBytes == 0)
                                    {
                                        totalRead = 1; // Set totalRead to 1 to indicate completion for 0 byte files
                                        downloadInfo.Progress = "100.00%";
                                        ReportProgress(downloadInfo, totalRead, totalBytes, true, startTime);
                                        isMoreToRead = false;
                                    }

                                    do
                                    {
                                        var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                                        if (read == 0)
                                        {
                                            isMoreToRead = false;
                                            ReportProgress(downloadInfo, totalRead, totalBytes, true, startTime);
                                            continue;
                                        }

                                        await fileStream.WriteAsync(buffer, 0, read);
                                        totalRead += read;

                                        if (canReportProgress)
                                        {
                                            ReportProgress(downloadInfo, totalRead, totalBytes, false, startTime);
                                        }
                                    }
                                    while (isMoreToRead);
                                }

                                var actualSize = new FileInfo(localPath).Length;
                                if (totalBytes >= 0 && actualSize != totalBytes)
                                {
                                    throw new Exception($"File size mismatch: expected {totalBytes} bytes, got {actualSize} bytes.");
                                }

                                completedFiles++;
                                UpdateProgressText(totalFiles, completedFiles);
                                _logger.LogDebugInfo($"Downloaded file: {fileUrl}");
                                success = true;
                            }
                            catch (Exception ex)
                            {
                                retryCount++;
                                if (retryCount == 10)
                                {
                                    downloadInfo.Status = "Error";
                                    _logger.LogDebugInfo($"Error downloading file {fileUrl}: {ex.Message}");
                                }
                                else
                                {
                                    downloadInfo.Status = $"Retrying... ({retryCount}/10)";
                                }
                            }
                        }

                        semaphore.Release(); // Release the semaphore
                    }));
                }

                await Task.WhenAll(tasks); // Wait for all downloads to complete
            }
        }






        private void ReportProgress(DownloadInfo downloadInfo, long totalRead, long totalBytes, bool isCompleted, DateTime startTime)
        {
            var percentComplete = totalBytes > 0 ? (double)totalRead / totalBytes * 100 : 100;
            var status = isCompleted ? "Completed" : "Downloading";
            var elapsed = DateTime.UtcNow - startTime;
            var speed = totalRead / elapsed.TotalSeconds / 1024; // Speed in KB/s

            downloadInfo.Status = status;
            downloadInfo.Progress = $"{percentComplete:0.00}%";
            downloadInfo.Speed = $"{speed:0.00} KB/s";
        }


        private void UpdateProgressText(int totalFiles, int completedFiles)
        {
            Dispatcher.Invoke(() =>
            {
                string currentText = PingResultTextBlock.Text;
                int index = currentText.IndexOf(" - Total:");
                if (index > -1)
                {
                    currentText = currentText.Substring(0, index);
                }
                PingResultTextBlock.Text = $"{currentText} - Total: {totalFiles} - Completed: {completedFiles}";
            });
        }

        private void AutoScrollToBottom()
        {
            if (ActivityLog.Items.Count == 0)
                return;

            var border = VisualTreeHelper.GetChild(ActivityLog, 0) as Decorator;
            if (border == null) return;

            var scroll = border.Child as ScrollViewer;
            if (scroll == null) return;

            // Only scroll if already at the bottom
            if (scroll.VerticalOffset == scroll.ScrollableHeight)
            {
                scroll.ScrollToEnd();
            }
        }

        public class DownloadProgress
        {
            public int TotalFiles { get; set; }
            public int CompletedFiles { get; set; }
        }


        private async Task DownloadOverFTP(string ipAddress, string folderPath)
        {
            // Show initial message while preparing
            PingResultTextBlock.Text = "Building Download List - Please Wait...";
            PingResultTextBlock.Foreground = new SolidColorBrush(Colors.Yellow);

            // Give some time to update UI
            await Task.Delay(500); // Adjust delay as necessary

            // Now show the downloading message
            PingResultTextBlock.Text = "Downloading via FTP - Please Wait...";
            PingResultTextBlock.Foreground = new SolidColorBrush(Colors.Yellow);

            ActivityLog.ItemsSource = downloadInfos;
            downloadInfos.Clear();

            string localBasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DUMPED");
            string localPath = Path.Combine(localBasePath, folderPath.Trim('/'));

            _logger.LogDebugInfo($"Creating local directory: {localPath}");
            Directory.CreateDirectory(localPath);

            using (var client = new FtpClient(ipAddress, new NetworkCredential("anonymous", "anonymous")))
            {
                try
                {
                    _logger.LogDebugInfo($"Connecting to FTP server at {ipAddress}");
                    client.Config.ConnectTimeout = 5000; // Set a timeout for the connection
                    client.Config.DataConnectionType = FtpDataConnectionType.PASV; // Use Passive mode

                    client.Connect();
                    _logger.LogDebugInfo("Connected to FTP server");

                    // Count total files
                    var progress = new DownloadProgress
                    {
                        TotalFiles = CountTotalFiles(client, folderPath),
                        CompletedFiles = 0
                    };

                    // Start the recursive download process
                    await DownloadDirectoryRecursively(client, folderPath, localPath, progress);

                    _logger.LogDebugInfo("Download via FTP completed successfully");
                    PingResultTextBlock.Text = "Download via FTP completed successfully.";
                    PingResultTextBlock.Foreground = new SolidColorBrush(Colors.LimeGreen);

                    if (ZipResultsCheckBox.IsChecked == true)
                    {
                        string folderName = Path.GetFileName(localPath);
                        string zipFilePath = Path.Combine(localBasePath, $"{folderName}.zip");

                        _logger.LogDebugInfo($"Starting to zip folder {localPath} to {zipFilePath}");
                        ZipFolder(localPath, zipFilePath);
                        _logger.LogDebugInfo("Zipping completed successfully");

                        PingResultTextBlock.Text = "Download and zipping completed successfully.";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebugInfo($"FTP download failed: {ex.Message}");
                    PingResultTextBlock.Text = "FTP Download failed.";
                    PingResultTextBlock.Foreground = new SolidColorBrush(Colors.Red);
                }
                finally
                {
                    _logger.LogDebugInfo("Disconnecting from FTP server");
                    client.Disconnect();
                    _logger.LogDebugInfo("Disconnected from FTP server");
                }
            }
        }

        private int CountTotalFiles(FtpClient client, string remotePath)
        {
            int totalFiles = 0;
            foreach (FtpListItem item in client.GetListing(remotePath))
            {
                if (SkipRESERVEDFilesCheckBox.IsChecked == true && item.Name.Contains("RESERVED"))
                {
                    continue; // Skip RESERVED files
                }

                if (item.Type == FtpObjectType.Directory)
                {
                    totalFiles += CountTotalFiles(client, item.FullName);
                }
                else if (item.Type == FtpObjectType.File)
                {
                    totalFiles++;
                }
            }
            return totalFiles;
        }

        private async Task DownloadDirectoryRecursively(FtpClient client, string remotePath, string localPath, DownloadProgress progress)
        {
            foreach (FtpListItem item in client.GetListing(remotePath))
            {
                // Skip /RESERVED files if the checkbox is checked
                if (SkipRESERVEDFilesCheckBox.IsChecked == true && item.Name.Contains("RESERVED"))
                {
                    _logger.LogDebugInfo($"Skipping RESERVED file: {item.FullName}");
                    continue;
                }

                string localFilePath = Path.Combine(localPath, item.Name);
                string remoteFilePath = item.FullName;

                if (item.Type == FtpObjectType.Directory)
                {
                    _logger.LogDebugInfo($"Creating local directory: {localFilePath}");
                    Directory.CreateDirectory(localFilePath);
                    await DownloadDirectoryRecursively(client, remoteFilePath, localFilePath, progress);
                }
                else if (item.Type == FtpObjectType.File)
                {
                    var downloadInfo = new DownloadInfo { FilePath = remoteFilePath, Status = "Starting..." };
                    Dispatcher.Invoke(() => downloadInfos.Add(downloadInfo));
                    AutoScrollToBottom(); // Auto scroll to bottom if at bottom

                    int retryCount = 0;
                    bool success = false;

                    while (retryCount < 10 && !success)
                    {
                        try
                        {
                            await DownloadFileWithProgressAsync(client, remoteFilePath, localFilePath, downloadInfo);
                            progress.CompletedFiles++;
                            UpdateProgressText(progress.TotalFiles, progress.CompletedFiles);
                            _logger.LogDebugInfo($"Downloaded file: {remoteFilePath}");
                            success = true;
                        }
                        catch (Exception ex)
                        {
                            retryCount++;
                            if (retryCount == 10)
                            {
                                downloadInfo.Status = "Error";
                                _logger.LogDebugInfo($"Error downloading file {remoteFilePath}: {ex.Message}");
                            }
                            else
                            {
                                downloadInfo.Status = $"Retrying... ({retryCount}/10)";
                                _logger.LogDebugInfo($"Retrying download for file {remoteFilePath} ({retryCount}/10): {ex.Message}");
                            }
                        }
                    }
                }
            }
        }

        private async Task DownloadFileWithProgressAsync(FtpClient client, string remoteFilePath, string localFilePath, DownloadInfo downloadInfo)
        {
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            await Task.Run(() =>
            {
                using (var remoteStream = client.OpenRead(remoteFilePath))
                {
                    var buffer = new byte[8192];
                    long totalBytesRead = 0;
                    int bytesRead;

                    using (var localStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None, buffer.Length, true))
                    {
                        // Handle 0-byte files
                        if (remoteStream.Length == 0)
                        {
                            downloadInfo.Progress = "100.00%";
                            downloadInfo.TotalSize = "0.00 KB";
                            downloadInfo.Speed = "0.00 KB/s";
                            downloadInfo.Status = "Completed";
                            return;
                        }

                        while ((bytesRead = remoteStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            localStream.Write(buffer, 0, bytesRead);
                            totalBytesRead += bytesRead;

                            var percentComplete = (double)totalBytesRead / remoteStream.Length * 100;
                            var elapsed = stopwatch.Elapsed;

                            downloadInfo.Progress = $"{percentComplete:0.00}%";
                            downloadInfo.TotalSize = remoteStream.Length < 1024 * 1024
                                ? $"{remoteStream.Length / 1024.0:0.00} KB"
                                : $"{remoteStream.Length / 1024.0 / 1024.0:0.00} MB";
                            downloadInfo.Speed = $"{totalBytesRead / elapsed.TotalSeconds / 1024:0.00} KB/s";
                            downloadInfo.Status = "Downloading";
                        }
                    }
                }
            });

            stopwatch.Stop();
            downloadInfo.Status = "Completed";
        }




    }
}

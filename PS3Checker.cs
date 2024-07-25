using System;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

public class PS3Checker
{
    private readonly Logger _logger;

    public PS3Checker(Logger logger)
    {
        _logger = logger;
    }

    public async Task<(bool, string, bool, bool)> CheckPS3Async(string ipAddress)
    {
        _logger.LogDebugInfo($"Checking PS3 at IP: {ipAddress}");
        bool isPingSuccessful = await Task.Run(() => PingIP(ipAddress));
        if (!isPingSuccessful)
        {
            _logger.LogDebugInfo($"Ping failed for IP: {ipAddress}");
            return (false, "PS3 Not Found - Check Network Settings", false, false);
        }

        var ftpTask = Task.Run(() => CheckFtpServer(ipAddress));
        var httpTask = Task.Run(() => CheckHttpServer(ipAddress));

        var results = await Task.WhenAll(ftpTask, httpTask);
        bool isFtpSuccessful = results[0];
        bool isHttpSuccessful = results[1];

        if (isFtpSuccessful)
        {
            string fileToCheck = "/dev_flash/vsh/etc/version.txt";
            (bool ftpSuccessful, string versionInfo) = await Task.Run(() => DownloadAndCheckVersion(ipAddress, fileToCheck));

            if (ftpSuccessful)
            {
                _logger.LogDebugInfo($"PS3 Found on FW {versionInfo} - FTP Available");
                return (true, $"Success - PS3 Found on FW {versionInfo} - FTP Available", isFtpSuccessful, isHttpSuccessful);
            }
            else
            {
                _logger.LogDebugInfo($"FTP check failed for IP: {ipAddress}");
                return (true, "IP Valid - FTP Failed - Make sure an FTP Server is running on your PS3", isFtpSuccessful, isHttpSuccessful);
            }
        }

        _logger.LogDebugInfo($"FTP server not found for IP: {ipAddress}");
        return (true, "IP Valid - FTP Server Not Found", isFtpSuccessful, isHttpSuccessful);
    }

    private bool PingIP(string ipAddress)
    {
        try
        {
            using (Ping ping = new Ping())
            {
                PingReply reply = ping.Send(ipAddress, 3000); // Timeout set to 2000ms
                return reply.Status == IPStatus.Success;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebugInfo($"Error pinging IP {ipAddress}: {ex.Message}");
            return false;
        }
    }

    private bool CheckFtpServer(string ipAddress)
    {
        try
        {
            string uri = $"ftp://{ipAddress}";
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(uri);
            request.Method = WebRequestMethods.Ftp.ListDirectory;
            request.Credentials = new NetworkCredential("anonymous", "anonymous");
            request.Timeout = 3000; // Set a short timeout of 2 seconds
            request.EnableSsl = false; // Ignore SSL
            request.UsePassive = true; // Use passive mode

            using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
            {
                return response.StatusCode == FtpStatusCode.OpeningData || response.StatusCode == FtpStatusCode.DataAlreadyOpen;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebugInfo($"Error checking FTP server for IP {ipAddress}: {ex.Message}");
            return false;
        }
    }

    private bool CheckHttpServer(string ipAddress)
    {
        try
        {
            string url = $"http://{ipAddress}/dev_flash/vsh/etc/version.txt";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Timeout = 3000; // Set a short timeout of 2 seconds

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    using (Stream responseStream = response.GetResponseStream())
                    using (StreamReader reader = new StreamReader(responseStream))
                    {
                        string fileContent = reader.ReadToEnd();
                        // You can add any validation of the content if needed
                        _logger.LogDebugInfo($"HTTP check successful for IP {ipAddress}. Content: {fileContent.Substring(0, Math.Min(fileContent.Length, 100))}");
                        return true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebugInfo($"Error checking HTTP server for IP {ipAddress}: {ex.Message}");
        }

        return false;
    }

    private (bool, string) DownloadAndCheckVersion(string ipAddress, string filePath)
    {
        try
        {
            string uri = $"ftp://{ipAddress}{filePath}";
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(uri);
            request.Method = WebRequestMethods.Ftp.DownloadFile;
            request.Credentials = new NetworkCredential("anonymous", "anonymous");
            request.Timeout = 3000; // Set a short timeout of 2 seconds
            request.EnableSsl = false; // Ignore SSL
            request.UsePassive = true; // Use passive mode

            using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
            using (Stream responseStream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(responseStream))
            {
                string fileContent = reader.ReadToEnd();
                string[] lines = fileContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 0 && lines[0].StartsWith("release:"))
                {
                    string version = lines[0].Split(':')[1].Trim();
                    return (true, version.TrimStart('0').TrimEnd('0'));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebugInfo($"Error downloading or reading version file for IP {ipAddress}: {ex.Message}");
        }

        return (false, null);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

public class Logger
{
    private List<string> logBuffer;
    private string logFilePath;
    private Timer logFlushTimer;
    private readonly object logLock = new object();
    private const int FlushInterval = 1000; // Interval in milliseconds

    public Logger(string logDirectory, string logFileName)
    {
        logBuffer = new List<string>();
        logFilePath = Path.Combine(logDirectory, logFileName);
        logFlushTimer = new Timer(_ => FlushLogBuffer(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void LogDebugInfo(string message)
    {
        lock (logLock)
        {
            logBuffer.Add($"{DateTime.Now}: {message}");

            if (logBuffer.Count >= 100)
            {
                FlushLogBuffer();
            }
            else
            {
                logFlushTimer.Change(FlushInterval, Timeout.Infinite);
            }
        }
    }

    private void FlushLogBuffer()
    {
        lock (logLock)
        {
            if (logBuffer.Count > 0)
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter(logFilePath, true))
                    {
                        foreach (var message in logBuffer)
                        {
                            sw.WriteLine(message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Error writing to log file: " + ex.Message);
                }
                logBuffer.Clear();
            }
            logFlushTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }
}

using System.Text;

namespace NovaisFPS.Core;

public sealed class Logger
{
    private readonly object _lock = new();
    private readonly string _logFilePath;

    public Logger(string logFilePath)
    {
        _logFilePath = logFilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath) ?? ".");
    }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message) => Write("ERROR", message);

    public void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
        lock (_lock)
        {
            Console.WriteLine(line);
            File.AppendAllText(_logFilePath, line + Environment.NewLine, Encoding.UTF8);
        }
    }
}



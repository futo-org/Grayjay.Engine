using System;

namespace Grayjay.Engine
{
    public enum LogLevel : int
    {
        None,
        Error,
        Warning,
        Info,
        Verbose,
        Debug
    }

    public static class Logger
    {
        public static Action<LogLevel, string, string, Exception?> LogCallback = (level, tag, message, ex) =>
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string levelStr = level.ToString().ToUpper();
            string logMessage = $"[{timestamp}] [{levelStr}] [{tag}] {message}";
            if (ex != null)
                logMessage += $"\nException: {ex.Message}\nStack Trace: {ex.StackTrace}";
            Console.WriteLine(logMessage);
        };
        public static Func<LogLevel, bool> WillLog = (level) => true;

        internal static void Debug<T>(string message, Exception? ex = null) => LogCallback.Invoke(LogLevel.Debug, nameof(T), message, ex);
        internal static void Verbose<T>(string message, Exception? ex = null) => LogCallback.Invoke(LogLevel.Verbose, nameof(T), message, ex);
        internal static void Info<T>(string message, Exception? ex = null) => LogCallback.Invoke(LogLevel.Info, nameof(T), message, ex);
        internal static void Warning<T>(string message, Exception? ex = null) => LogCallback.Invoke(LogLevel.Warning, nameof(T), message, ex);
        internal static void Error<T>(string message, Exception? ex = null) => LogCallback.Invoke(LogLevel.Error, nameof(T), message, ex);
        internal static void Debug(string tag, string message, Exception? ex = null) => LogCallback.Invoke(LogLevel.Debug, tag, message, ex);
        internal static void Verbose(string tag, string message, Exception? ex = null) => LogCallback.Invoke(LogLevel.Verbose, tag, message, ex);
        internal static void Info(string tag, string message, Exception? ex = null) => LogCallback.Invoke(LogLevel.Info, tag, message, ex);
        internal static void Warning(string tag, string message, Exception? ex = null) => LogCallback.Invoke(LogLevel.Warning, tag, message, ex);
        internal static void Error(string tag, string message, Exception? ex = null) => LogCallback.Invoke(LogLevel.Error, tag, message, ex);
    }
}

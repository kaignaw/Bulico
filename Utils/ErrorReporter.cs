using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Autodesk.Revit.UI;

namespace Bulico
{
    internal static class ErrorReporter
    {
        private const long MaxLogBytes = 2 * 1024 * 1024;
        private const int MaxAttempts = 3;

        public static string LogPath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Bulico",
                    "logs");
                return Path.Combine(dir, "error.log");
            }
        }

        public static void Show(string title, string summary, Exception ex)
        {
            Log(title, ex);
            TaskDialog.Show(title, summary + "\n\n详细信息已写入本地日志：\n" + LogPath);
        }

        public static void Log(string title, Exception ex)
        {
            string entry = string.Format(
                "[{0:yyyy-MM-dd HH:mm:ss}] {1}\r\n{2}\r\n\r\n",
                DateTime.Now,
                title,
                ex);

            // 多个 Revit 实例可能同时写同一个日志文件，先重试再退回实例独立文件。
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                try
                {
                    Write(LogPath, entry);
                    return;
                }
                catch (IOException)
                {
                    Thread.Sleep(50 * (attempt + 1));
                }
                catch (UnauthorizedAccessException)
                {
                    break;
                }
                catch
                {
                    break;
                }
            }

            try
            {
                string fallback = LogPath + "." + Process.GetCurrentProcess().Id;
                Write(fallback, entry);
            }
            catch
            {
                // 日志写入失败不能影响 Revit 命令本身。
            }
        }

        private static void Write(string path, string entry)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(path) && new FileInfo(path).Length > MaxLogBytes)
                File.Move(path, path + ".old", true);

            using (FileStream stream = new FileStream(
                path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            using (StreamWriter writer = new StreamWriter(stream))
            {
                writer.Write(entry);
            }
        }
    }
}

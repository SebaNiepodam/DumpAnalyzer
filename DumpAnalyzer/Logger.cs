using System;
using System.IO;

namespace DumpAnalyzer
{
    public static class Logger
    {
        private static System.IO.StreamWriter _streamWriter;
        public static string logDirectoryPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"");
        private static string _fullLogPath = Path.Combine(logDirectoryPath, @"DumpAnalyzer.log");

        public static void Log(string text)
        {
            if (_streamWriter == null)
                return;
            string log = "[" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "] ";
            //txtWriter.WriteLine("  :");
            //txtWriter.WriteLine("  :{0}", logMessage);
            //txtWriter.WriteLine("-------------------------------");
            log += text;
            System.Diagnostics.Debug.Assert(_streamWriter != null, " Logger not initialized.");
            if (_streamWriter != null)
            {
                _streamWriter.WriteLine(log);
            }
        }

        public static void LogWarning(string text)
        {
            Log("[Warning] " + text);
        }

        public static void LogError(string text)
        {
            Log("[ERROR] " + text);
        }

        public static void Log(string text, string level)
        {
            Log("[" + level + "] " + text);
        }

        public static void Init()
        {
            try
            {
                // check for file                
                FileInfo fileInfo = new FileInfo(_fullLogPath);
                fileInfo.Directory.Create();
                _streamWriter = File.CreateText(_fullLogPath);
                _streamWriter.AutoFlush = true;
                string time = DateTime.Now.ToLongTimeString() + " " + DateTime.Now.ToShortDateString();
                _streamWriter.WriteLine(time + "!!! --- DC Launcher started --- !!!");
                //_streamWriter.WriteLine("Version: " + App.currentVersion);
                _streamWriter.WriteLine("PC Name: " + Environment.MachineName);
                _streamWriter.WriteLine("Culture: " + System.Threading.Thread.CurrentThread.CurrentCulture.ToString());
                //Thread.CurrentThread.CurrentCulture = new CultureInfo("da-DK");
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("Exception on create log file: " + e.ToString());
                _streamWriter = null;
            }
        }

        public static void Close()
        {
            if (_streamWriter != null)
            {
                _streamWriter.Close();
                _streamWriter = null;
            }
        }

        public static void Continue()
        {
            _streamWriter = File.AppendText(_fullLogPath);
        }

        public static string GetLogPath()
        {
            return _fullLogPath;
        }
    }
}

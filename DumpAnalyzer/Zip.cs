using System;
using System.Diagnostics;
using System.IO;

namespace DumpAnalyzer
{
    public static class Zip
    {
        private static readonly string helpersPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"data/helpers");
        private static readonly string zip7zPath = Path.Combine(helpersPath, @"7z\7za.exe");
        
        public static void UnpackFile(string zipPath, string outputPath, string additionalParam1 = @"x ", string additionalParam2 = "")
        {
            if (File.Exists(zip7zPath)) 
            {
                if (!Directory.Exists(outputPath))
                {
                    Directory.CreateDirectory(outputPath);
                }
                
                string args = additionalParam1 + zipPath + @" -o" + outputPath + " " + additionalParam2 + @" -y";
                string output = "";

                Process zipProcess = new Process();
                zipProcess.StartInfo.FileName = zip7zPath;
                zipProcess.StartInfo.Arguments = args;
                zipProcess.StartInfo.RedirectStandardError = true;
                zipProcess.StartInfo.RedirectStandardOutput = true;
                zipProcess.StartInfo.UseShellExecute = false;
                zipProcess.StartInfo.CreateNoWindow = true;
                zipProcess.OutputDataReceived += new DataReceivedEventHandler(
                    (s, e) =>
                    {
                        Console.WriteLine(e.Data);
                        output += e.Data != null ? e.Data.ToString() + "\n" : "NO DATA\n";
                        Logger.Log(output);
                    }
                );
                zipProcess.ErrorDataReceived += new DataReceivedEventHandler((s, e) => { Console.WriteLine(e.Data); });

                zipProcess.Start();
                zipProcess.BeginOutputReadLine();
                zipProcess.WaitForExit();
            }
        }
    }
}
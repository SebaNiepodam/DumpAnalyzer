using System;
using System.IO;
using System.Linq;

namespace DumpAnalyzer
{
    public static class InfoGatherer
    {
        public static string GetInfoFromCrashContextXml(string pathToDirectory)
        {
            string crashConextResult = "CrashContext: \n";
            string filePath = Path.Combine(pathToDirectory, "CrashContext.runtime-xml");
            if (File.Exists(filePath))
            {
                crashConextResult = ReadFile(filePath);
            }

            return crashConextResult;
        }

        public static string GetShortInfoFromCrashContextXml(string pathToDirectory)
        {
            string crashConextResult = "CrashContext: \n";
            string filePath = Path.Combine(pathToDirectory, "CrashContext.runtime-xml");
            if (File.Exists(filePath))
            {
                string readFile = ReadFile(filePath);
                string crashType = "--- Crash Type ---\n" + GetTextBetween(ref readFile, "<CrashType>", "</CrashType>");
                string errorMessage = "\n --- ErrorMessage --- \n" + GetTextBetween(ref readFile, "<ErrorMessage>", "</ErrorMessage>" );
                string callstack = "\n --- Call Stack --- \n" + GetTextBetween(ref readFile, "<CallStack>", "</CallStack>");
                string pCallstack = "\n --- P Call Stack --- \n" + GetTextBetween(ref readFile, "<PCallStack>", "</PCallStack>");
                string ensure = "\n --- IsEnsure --- \n" + GetTextBetween(ref readFile, "<IsEnsure>", "</IsEnsure>");
                string assert = "\n --- IsAssert --- \n" + GetTextBetween(ref readFile, "<IsAssert>", "</IsAssert>");
                string seconds = "\n --- SecondsSinceStart --- \n" + GetTextBetween(ref readFile, "<SecondsSinceStart>", "</SecondsSinceStart>");
                string cpuInfo = GetCpuInfoFromCrashContext(ref readFile);
                crashConextResult = crashType + "\n" + errorMessage + "\n" + pCallstack + "\n" + ensure + "\n" + assert + "\n" + seconds + "\n" + cpuInfo;

                CrashTypeString = crashType;
                ErrorMessageString = errorMessage;
                SecondsSinceStartString = seconds;
            }

            return crashConextResult;
        }

        public static string GetInfoFromCrashReporterLogFile(string pathToDirectory)
        {
            string CrashReporterResult = "Crash reporter log: \n";
            string filePath = Path.Combine(pathToDirectory, "CrashReporter.log");
            if (File.Exists(filePath))
            {
                CrashReporterResult = " crash reporter log exists!\n";
                string readFile = ReadFile(filePath);
                string PCName = "PC Name: " + GetPcName(ref readFile);
                string Culture = "Culture: " + GetCulture(ref readFile);
                string BuildVersion = readFile.Split(new string[] { "Build version:" }, StringSplitOptions.None).Last().Split('\r').First();
                BuildVersion = "Build Version: " + BuildVersion;
                //string AdditionalInfo = readFile.Split(new string[] { "MailInfo]" }, StringSplitOptions.None).Last();
                //AdditionalInfo = AdditionalInfo.Split(new string[] { "] More info:" }, StringSplitOptions.None).First();
                string mailInfo = "Mail: " + GetMail(ref readFile);
                string mailAdditionalInfo = readFile.Split(new string[] { "[MailInfo]:" }, StringSplitOptions.None).Last().Split('\r').First();
                mailAdditionalInfo = "Mail Additional info: " + mailAdditionalInfo;
                CrashReporterResult += PCName + "\n" + Culture + "\n" + BuildVersion + "\n" /* + AdditionalInfo + "\n" */ + mailInfo + "\n" + mailAdditionalInfo + "\n";
            }

            return CrashReporterResult;
        }

        static public string GetPcName(ref string crashReportFile)
        {
            string PCName = crashReportFile.Split(new string[] { "PC Name:" }, StringSplitOptions.None).Last();
            PCName = PCName.Split('\r').First().Trim();

            PcNameString = PCName;

            return PCName;
        }

        static public string GetCulture(ref string crashReportFile)
        {
            string cultureString = crashReportFile.Split(new string[] { "Culture:" }, StringSplitOptions.None).Last();
            cultureString = cultureString.Split('\r').First().Trim();

            return cultureString;
        }


        static public string GetMail(ref string crashReportFile)
        {
            string mailInfo = crashReportFile.Split(new string[] { "[Mail]:" }, StringSplitOptions.None).Last().Split('\r').First();

            MailString = mailInfo;

            return mailInfo;
        }

        public static string GetInfoFromDxDiagFile(string dirName)
        {
            //wczytaj dx diag
            string DXDiagResult = "DX DIAG INFO NOT FOUND: \n";
            if (File.Exists(Path.Combine(dirName, "dxdiag.txt")))
            {
                DXDiagResult = "DC File EXISTS !\n";
                DXDiagResult += InfoGatherer.ReadFile(Path.Combine(dirName, "dxdiag.txt"));
            }

            return DXDiagResult;
        }

        public static string ReadFile(string path)
        {
            string appPath = AppDomain.CurrentDomain.BaseDirectory;
            string lines = null;
            if (File.Exists(path))
            {
                Logger.Log("Trying to read file: " + path);
                try
                {
                    lines = File.ReadAllText(path);
                }
                catch (Exception e)
                {
                    Logger.LogError("Cannot read file: " + path);
                    Logger.LogError(e.ToString());
                }
            }
            else
            {
                Logger.LogWarning("Path: " + path + " not found. Creating new config file.");
            }
            return lines;
        }

        private static string GetLineWithText(ref string fullText, string word)
        {
            string returnString = "";
            string[] lines = fullText.Split('\n');

            for (int i = 0; i < lines.Length; ++i)
            {
                if (lines[i].Contains(word))
                {
                    string temp = lines[i].Trim();
                    if (temp.Substring(0, word.Length) == word)
                        returnString += lines[i].Trim().Substring(word.Length) + "\n";
                }
            }

            returnString = returnString.Replace("\r\n", string.Empty);
            return returnString;
        }

        public static string GetTextBetween(ref string fullText, string startWord, string endWord)
        {
            string returnString = fullText.Split(new string[] { startWord }, StringSplitOptions.None).Last();
            returnString = returnString.Split(new string[] { endWord }, StringSplitOptions.None).First();
            return returnString;
        }

        private static string GetCpuInfoFromCrashContext(ref string readFile)
        {
            string cpuInfostring = "\n --- CPU Info--- \n";
            string cpuBrand = "Cpu: " + GetTextBetween(ref readFile, "<Misc.CPUBrand>", "</Misc.CPUBrand>");
            string cpuCores = "Cores: " + GetTextBetween(ref readFile, "<Misc.NumberOfCores>", "</Misc.NumberOfCores>");
            string cpuCoresHT = "Cores HT: " + GetTextBetween(ref readFile, "<Misc.NumberOfCoresIncludingHyperthreads>", "</Misc.NumberOfCoresIncludingHyperthreads>");
            string cpu64bit = "64 bit: " + GetTextBetween(ref readFile, "<Misc.Is64bitOperatingSystem>", "</Misc.Is64bitOperatingSystem>");
            string cpuVendor = "Vendor: " + GetTextBetween(ref readFile, "<Misc.CPUVendor>", "</Misc.CPUVendor>");
            string gpuBrand = "Gpu: " + GetTextBetween(ref readFile, "<Misc.PrimaryGPUBrand>", "</Misc.PrimaryGPUBrand>");
            string osVersion = "OS: " + GetTextBetween(ref readFile, "<Misc.OSVersionMajor>", "</Misc.OSVersionMajor>");

            cpuInfostring += cpuBrand + "\n" + cpuVendor + "\n" + cpuCores + "\n" + cpuCoresHT + "\n" + cpu64bit + "\n" + gpuBrand + "\n" + osVersion;
            CPU_InfoString = cpuInfostring;
            return cpuInfostring;
        }

        public static string PcNameString = "";
        public static string MailString = "";
        public static string CPU_InfoString = "";
        public static string CrashTypeString = "";
        public static string ErrorMessageString = "";
        public static string SecondsSinceStartString = "";
    }
}

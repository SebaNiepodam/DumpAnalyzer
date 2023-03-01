using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using System.IO;
using System.Net;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace DumpAnalyzer
{
    public partial class DumpAnalyzerWindow : Window
    {
        public class DumpGroup
        {
            public string TopLine;
            public List<List<string>> Callstack;
            public List<string> PathToDump;
            public int Count;

            public DumpGroup(string inTopLine)
            {
                TopLine = inTopLine;
                Callstack = new List<List<string>>();
                PathToDump = new List<string>();
                Count = 1;
            }

            public void Inc()
            {
                SetCount(Count + 1);
            }

            public void SetCount(int newCount)
            {
                Count = newCount;
            }

        }

        public struct CrashInfo
        {
            public string FullCrash;
            public string CallstackTop;
            public string Revision;
            public string FullName;
            public string DumpPath;
            public string CrashContextFull;
            public string CrashContextShort;
            public string InfoFromCrashReporterLog;


            public CrashInfo(string inFullCrash, string inCallstackTop, string inRevision, string inFullName, string inDumpPath, string inCrashContextFull, string inCrashContextShort, string inCrashReporterInfo)
            {
                FullCrash = inFullCrash;
                CallstackTop = inCallstackTop;
                Revision = inRevision;
                FullName = inFullName;
                DumpPath = inDumpPath;
                CrashContextFull = inCrashContextFull;
                CrashContextShort = inCrashContextShort;
                InfoFromCrashReporterLog = inCrashReporterInfo;
            }
        }

        enum WorkingMode
        {
            UncheckedFtp,
            CheckedFtp,
            CheckedVault
        }

        private WorkingMode CurrentWorkingMode;
        List<DumpGroup> DumpGroups = new List<DumpGroup>();

        private List<string> FileNamesOnFtp = new List<string>();
        private List<string> UncheckedFileNamesOnFtp = new List<string>();
        private string DirWithRevision = "";

        private static string _cdb = "cdb.exe";
        private static string dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"data");
        private static string dumpsPath = Path.Combine(dataPath, @"dumps");
        private static string debuggersPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"data/debuggers");

        private static readonly string[] callstackTopExcludedWords = { @"KERNELBASE", @"RaiseException", @"Logf", @"AssertFailed", @"ReportAssert", @"NO-FUNCTION", @"WindowsErrorOutputDevice", @"CheckVerifyFailed", @"lambda" };
        //if (!CallstackFunction.Contains(@"KERNELBASE") && !CallstackFunction.Contains(@"RaiseException") && !CallstackFunction.Contains(@"Logf") && !CallstackFunction.Contains(@"AssertFailed") && !CallstackFunction.Contains(@"NO-FUNCTION") && tempCallstackTop == CallstackTop)

        public static string GetMainDumpPath()
        {
            return dumpsPath;
        }
        private static string symbolsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"data/symbols");
        private IConnection onlineConnection;

        SolidColorBrush BrushRed = new SolidColorBrush(Colors.Red);
        SolidColorBrush BrushGreen = new SolidColorBrush(Colors.Green);

        public DumpAnalyzerWindow()
        {
            InitializeComponent();
            Logger.Init();
            CrashInfo_TextBox.Text = "Tu sie pojawi cool info o crashu!";
        }

        private void CheckSFTPForCrashes_Button_Click(object sender, RoutedEventArgs e)
        {
            CheckSFTPForCrashes();
        }

        private void CheckAnalyzedOnFTP_Button_Click(object sender, RoutedEventArgs e)
        {
            CurrentWorkingMode = WorkingMode.CheckedFtp;
            if (onlineConnection == null)
            {
                onlineConnection = new SFTP();
            }

            List<string> filesOnServer = onlineConnection.GetAnalyzedDumpsFromServer();
            FileNamesOnFtp.Clear();
            file_ListBox.Items.Clear();
            int idx = 0;
            foreach (string File in filesOnServer)
            {
                FileNamesOnFtp.Add(File);
                string Revision = GetRevisionFromFileName(File);
                ListBoxItem lbi = new ListBoxItem
                {
                    Foreground = IsRevisionOK(Revision) ? BrushGreen : BrushRed,
                    Content = $"{idx}_{File}"
                };
                file_ListBox.Items.Add(lbi);
                idx++;
            }
            Console.WriteLine("Directory List Complete, status");

            StatusBar_TextBlock.Text = $"Checking FTP for analyzed dumps finished. Found: {file_ListBox.Items.Count} files.";
        }

        private void CheckAnalyzedOnVault_Button_Click(object sender, RoutedEventArgs e)
        {
            CurrentWorkingMode = WorkingMode.CheckedVault;
            file_ListBox.Items.Clear();
            CrashInfo_ListBox.Items.Clear();
            string vaultDirectoryPath = Properties.Settings.Default.CheckedDumpsVaultPath;
            if (Directory.Exists(vaultDirectoryPath))
            {
                StatusBar_TextBlock.Text = $"Checking Vault for analyzed dumps finished. Found: {file_ListBox.Items.Count} files.";

                string[] analyzedDumps = Directory.GetDirectories(vaultDirectoryPath);
                int idx = 0;
                foreach (string analyzedDump in analyzedDumps)
                {
                    string dumpName = Path.GetFileName(analyzedDump);
                    string Revision = GetRevisionFromFileName(dumpName);
                    ListBoxItem lbi = new ListBoxItem
                    {
                        Foreground = IsRevisionOK(Revision) ? BrushGreen : BrushRed,
                        Content = $"{idx}_{dumpName}"
                    };
                    file_ListBox.Items.Add(lbi);
                    idx++;
                }
            }
        }

        private ConnectionInfo GetConnectionInfo()
        {
            KeyboardInteractiveAuthenticationMethod keybAuth =
                new KeyboardInteractiveAuthenticationMethod(Properties.Settings.Default.FTP_User);

            keybAuth.AuthenticationPrompt += HandleKeyEvent;
            ConnectionInfo connectionInfo = new ConnectionInfo(Properties.Settings.Default.FTP_Host,
                Properties.Settings.Default.FTP_Port,
                Properties.Settings.Default.FTP_User,
                keybAuth
            );
            return connectionInfo;
        }

        private void CheckSFTPForCrashes()
        {
            CurrentWorkingMode = WorkingMode.UncheckedFtp;
            try
            {
                if (onlineConnection == null)
                {
                    onlineConnection = new SFTP();
                }
                List<string> filesOnServer = onlineConnection.GetFilesFromServer();
                FileNamesOnFtp.Clear();
                file_ListBox.Items.Clear();
                int idx = 0;
                foreach (var File in filesOnServer)
                {
                    FileNamesOnFtp.Add(File);
                    string Revision = GetRevisionFromFileName(File);
                    ListBoxItem lbi = new ListBoxItem
                    {
                        Foreground = IsRevisionOK(Revision) ? BrushGreen : BrushRed,
                        Content = $"{idx}_{File}"
                    };
                    file_ListBox.Items.Add(lbi);
                    idx++;
                }
                Console.WriteLine("Directory List Complete, status");

                StatusBar_TextBlock.Text = $"Checking FTP finished. Found: {file_ListBox.Items.Count} files.";
            }
            catch (Exception e)
            {
                Logger.Log($"Error on Check FTP: {e}");
                MessageBox.Show($"Error on Check FTP: {e}", @"Cos nie zabanglało", MessageBoxButton.OK);
            }
        }

        private void AnalyzeCrash_Button_Click(object sender, RoutedEventArgs e)
        {
            if (file_ListBox.SelectedItems.Count == 0)
                return;

            CleanCrashInfos();
            AnalyzeCrashOnFTP();

            // simulate clicking again to clean up a list
            CheckSFTPForCrashes_Button_Click(sender, e);
            Filter_Button_Click(sender, e);
        }

        private void CleanCrashInfos()
        {
            CrashInfo_ListBox.Items.Clear();
            CrashInfos.Clear();
            FilteredCrashInfos.Clear();
            CrashInfosNames.Clear();
            DumpGroups.Clear();
        }

        private void AnalyzeCrashOnFTP()
        {
            try
            {
                CrashInfo_TextBox.Text = "Tu sie pojawi info o crashu.";
                int SelectedFileCount = file_ListBox.SelectedItems.Count;
                Analyze_ProgressBar.Dispatcher.Invoke(() => Analyze_ProgressBar.Value = 0, System.Windows.Threading.DispatcherPriority.Send);
                StatusBar_TextBlock.Text = "Preparing to analyze: ";
                float ProgressBarStep = 100 / SelectedFileCount;

                for (int i = 0; i < SelectedFileCount; ++i)
                {
                    bool bError = false;
                    string fileName = "";
                    string localFileName = "";
                    string dirName = "";
                    string localDirPath = "";
                    string newDirName = "";
                    string CallstackTop = "";
                    try
                    {
                        ListBoxItem lbi = (ListBoxItem)file_ListBox.SelectedItems[i];
                        fileName = lbi.Content.ToString();
                        int fileIdx = fileName.IndexOf('_') + 1;
                        if (fileIdx == 0)
                        {
                            MessageBox.Show("Cosik sie popsulo przy analizie... zla nazwa pliku");
                            continue;
                        }

                        string testNumeric = fileName.Substring(0, fileIdx - 1);
                        bool bIsNumeric = int.TryParse(testNumeric, out int helperIdx);
                        if (bIsNumeric)
                            fileName = fileName.Substring(fileIdx);
                        localFileName = GetFileNameWithoutInvalidChars(fileName);
                        dirName = Path.GetFileNameWithoutExtension(localFileName);

                        string Revision = GetRevisionFromFileName(dirName);

                        if (IsRevisionOK(Revision))
                        {
                            DirWithRevision = Revision.ToString();
                        }
                        else
                            DirWithRevision = "";

                        localDirPath = Path.Combine(GetMainDumpPath(), DirWithRevision, dirName);

                        DownloadFile(fileName, localFileName);
                        UnpackFile(localFileName, localDirPath);
                        // real analyze of dump begins now
                        string cdbCallstack = GenerateCallstackWithCdb(localDirPath);
                        string CrashContextFull = InfoGatherer.GetInfoFromCrashContextXml(localDirPath);
                        string CrashContextShort = InfoGatherer.GetShortInfoFromCrashContextXml(localDirPath);
                        string CallStack = cdbCallstack;
                        CallstackTop = "Unknown";
                        ProcessNewCallstackCDB(ref cdbCallstack, ref CallstackTop);
                        CreateCallstackFile(ref cdbCallstack,  ref CallstackTop, localDirPath, fileName);

                        List<string> FullCallstack = GetFullCallstackFromCDB(ref cdbCallstack);
                        
                        string randomNumber = GetRandomNumberFromFileName(dirName);
                        newDirName = dirName;
                        if (randomNumber.Length > 0)
                            newDirName = dirName.Replace(randomNumber, CallstackTop);
                        else
                            newDirName = dirName.Insert(22, CallstackTop);

                        SetDumpAsChecked(localDirPath, newDirName);
                        RemoveFromFtpAfterAnalyze(fileName);


                        string CrashInfoName = helperIdx + "_" + newDirName;
                        CrashInfosNames.Add(CrashInfoName);
                        CrashInfo_ListBox.Items.Add(CrashInfoName);
                        string dumpPath = dirName;
                        string FullCrashInfo = "Path to unpacked dump: " + dumpPath + " " + "\n" + CallStack;
                        string infoFromCrashReporterLog = InfoGatherer.GetInfoFromCrashReporterLogFile(localDirPath);
                        CrashInfo info = new CrashInfo(cdbCallstack, CallstackTop, Revision, CrashInfoName, dumpPath, CrashContextFull, CrashContextShort, infoFromCrashReporterLog);
                        CrashInfos.Add(info);
                        FilteredCrashInfos.Add(info);
                        AddToDumpGroups(CallstackTop, FullCallstack, dumpPath);
                    }
                    catch (System.IO.PathTooLongException)
                    {
                        MessageBox.Show("Za dluga nazwa pliku (pewnie problem z odpakowaniem zipa) !!!!", "Path Too Long Exception", MessageBoxButton.OK);                                                
                        SelectedFileCount--;
                        --i;
                    }
                    catch (System.Exception ex)
                    {
                        bError = true;
                        Logger.Log("Error on Analyze single crash. \nFilename: " + fileName + "\nLocal File Name: " + localFileName + "\nDirName: " + dirName + "\nNewDirName: " + newDirName + "\nCallstackTop: " + CallstackTop + "\n" + ex.ToString());
                        MessageBox.Show("Error on Analyze single crash. \nFilename: " + fileName + "\nLocal File Name: " + localFileName + "\nDirName: " + dirName + "\nNewDirName: " + newDirName + "\nCallstackTop: " + CallstackTop + "\n" + ex.ToString(), @"Cos nie zabanglało", MessageBoxButton.OK);
                    }
                    finally
                    {
                        if (!bError)
                        {
                            try
                            {
                                if (File.Exists(localFileName))
                                    File.Delete(localFileName);
                            }
                            catch (System.Exception ex)
                            {
                                string msg = "Error on trying to delete: " + Path.Combine(DirWithRevision, localFileName) + "\nDirectory: " + Path.Combine(DirWithRevision, dirName) + "\nError: " + ex.ToString();
                                Logger.Log(msg);
                            }
                        }

                        this.Dispatcher.Invoke(() =>
                        {
                            Analyze_ProgressBar.Value += ProgressBarStep;
                            StatusBar_TextBlock.Text = "Analyzed files: " + (i + 1).ToString() + @"/" + SelectedFileCount.ToString();
                        }, System.Windows.Threading.DispatcherPriority.Background);
                    }
                }

                ShowDumpGroups();

                if (CrashInfo_ListBox.Items.Count > 0)
                    CrashInfo_ListBox.SelectedIndex = 0;
            }
            catch (System.Exception ex)
            {
                Logger.Log($"Error on Analyze: {ex}");
                MessageBox.Show($"Error on Analyze: {ex}", @"Cos nie zabanglało", MessageBoxButton.OK);
            }
        }

        private void RemoveFromFtpAfterAnalyze(string fileToRemove)
        {
            if (Properties.Settings.Default.IsRemoveFromFtpAfterAnalyze)
            {
                onlineConnection.RemoveAfterAnalyze(fileToRemove);
            }
        }

        private void SetDumpAsChecked(string localDirPath, string targetDirPath)
        {

            string directoryName = Path.GetFileName(localDirPath).TrimEnd(Path.DirectorySeparatorChar);
            string targetDirectory = Path.Combine(Properties.Settings.Default.CheckedDumpsFtpPath, targetDirPath);
            //1. upload to ftp
            if (Properties.Settings.Default.IsCopyToFtpEnabled)
            {
                onlineConnection.UploadDirectory(localDirPath, targetDirectory);
            }

            //2. copy to valut
            if (Properties.Settings.Default.IsCopyToVault)
            {
                string finalVaultPath = Path.Combine(Properties.Settings.Default.CheckedDumpsVaultPath, targetDirPath);
                DirectoryCopy(localDirPath, finalVaultPath, false);
            }
        }

        private void DownloadFile(System.String fileName, string localFileName)
        {
            CrashInfo_TextBox.Text = fileName;

            try
            {
                onlineConnection.DownloadFile(fileName, localFileName);
            }
            catch (Exception e)
            {
                Logger.Log("Error on trying to download file from SFTP: " + e.ToString() + " File name: " + fileName);
                MessageBox.Show("Error on Check FTP: " + e.ToString(), @"Cos nie zabanglało", MessageBoxButton.OK);
            }
        }

        private void UnpackFile(System.String zipPath, string dirPath)
        {
            if (Directory.Exists(dirPath))
                DeleteDirectory(dirPath, true);
            if (zipPath.Equals(dirPath))
                dirPath += "_1";

            string fullZipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, zipPath);
            string fullDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dirPath);

            Zip.UnpackFile(fullZipPath, fullDirPath);
        }

        private string GetDumpPath(string dirName)
        {
            string dmpDirectory = dirName;
            Logger.Log("Searching for dmp in: " + dmpDirectory);
            if (Directory.Exists(dmpDirectory))
            {
                string[] files = Directory.GetFiles(dmpDirectory, "*.dmp", SearchOption.AllDirectories);
                if (files.Length > 0)
                {
                    string returnPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, files[0]);
                    return returnPath;
                }
            }
            else
            {
                Logger.LogError("Directory: " + dmpDirectory + " doesn't exists");
                return "NO_DIRECTORY";
            }

            return "NO_FILE";
        }

        private void HandleKeyEvent(object sender, AuthenticationPromptEventArgs e)
        {
            foreach (AuthenticationPrompt prompt in e.Prompts)
            {
                prompt.Response = Properties.Settings.Default.FTP_Password;
            }
        }        

        //copied from StackOverflow
        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            try
            {
                if (!Directory.Exists(sourceDirName))
                {
                    Logger.LogError("The director doesnt exist. " + sourceDirName);
                    return;
                }

                DirectoryInfo dir = new DirectoryInfo(sourceDirName);

                DirectoryInfo[] dirs = dir.GetDirectories();

                // If the destination directory does not exist, create it.
                if (!Directory.Exists(destDirName))
                {
                    Directory.CreateDirectory(destDirName);
                }
                else
                {
                    //Logger.Log("Directory already exists on your Net Drive. Skipping copy.");
                    //MessageBox.Show("Directory already exists on your Net Drive. Skipping copy.", "Directory already exists", MessageBoxButton.OK);
                    //return;
                }


                // Get the file contents of the directory to copy.
                FileInfo[] files = dir.GetFiles();

                foreach (FileInfo file in files)
                {
                    // Create the path to the new copy of the file.
                    string temppath = Path.Combine(destDirName, file.Name);

                    // Copy the file.
                    file.CopyTo(temppath, false);
                }

                // If copySubDirs is true, copy the subdirectories.
                if (copySubDirs)
                {

                    foreach (DirectoryInfo subdir in dirs)
                    {
                        // Create the subdirectory.
                        string temppath = Path.Combine(destDirName, subdir.Name);

                        // Copy the subdirectories.
                        DirectoryCopy(subdir.FullName, temppath, copySubDirs);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError("Cosik sie popsulo w DirectoryCopy: " + e.ToString());
            }
        }

        private List<CrashInfo> CrashInfos = new List<CrashInfo>();
        private List<CrashInfo> FilteredCrashInfos = new List<CrashInfo>();
        private List<string> CrashInfosNames = new List<string>();
        

        private string GetRandomNumberFromFileName(string fileName)
        {
            //fileName = "gd_debug_2020_05_25__08_10_686896181_20200522_0_99_22_r19214.7z";
            string[] separatingStrings = { "__" };
            string randomNumber = fileName.Split(separatingStrings, StringSplitOptions.RemoveEmptyEntries).Last();
            string[] temp = randomNumber.Split('_');
            if (temp.Length > 2)
            {
                randomNumber = temp[2];
                if (randomNumber.Contains('-'))
                {
                    randomNumber = randomNumber.Substring(0, randomNumber.LastIndexOf('-'));
                }
            }

            return randomNumber;
        }

        private void OpenDump_Button_Click(object sender, RoutedEventArgs e)
        {
            switch (CurrentWorkingMode)
            {
                case WorkingMode.UncheckedFtp:
                    OpenLocalDump();
                    break;
                case WorkingMode.CheckedFtp:
                    break;
                case WorkingMode.CheckedVault:
                    OpenVaultDump();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OpenVaultDump()
        {
            if (file_ListBox.SelectedItem != null)
            {
                // 1. Copy from vault to local directory.
                ListBoxItem lbi = (ListBoxItem)file_ListBox.SelectedItem;
                string dumpName = lbi.Content.ToString();
                dumpName = dumpName.Substring(dumpName.IndexOf('_') + 1);
                string vaultDir = Path.Combine(Properties.Settings.Default.CheckedDumpsVaultPath, dumpName);
                string revision = GetRevisionFromFileName(dumpName);
                string dumpPath = "";
                if (Directory.Exists(vaultDir))
                {
                    string destinationDirectory = Path.Combine(GetMainDumpPath(), revision, dumpName);
                    DirectoryCopy(vaultDir, destinationDirectory, false);
                    dumpPath = GetDumpPath(destinationDirectory);
                    OpenDump(dumpPath, revision);
                }
            }

        }

        private void OpenLocalDump()
        {
            // check if *.dmp file is in local directory
            string dirName = FilteredCrashInfos[CrashInfo_ListBox.SelectedIndex].DumpPath;
            string revision = GetRevisionFromFileName(dirName).ToString();
            string filePath = Path.Combine(GetMainDumpPath(), revision, dirName);
            string dumpPath = "";
            if (Directory.Exists(filePath))
            {
                dumpPath = GetDumpPath(filePath);
                OpenDump(dumpPath, revision);
            }
        }

        private void OpenDump(string dumpPath, string revision)
        {
            if (File.Exists(dumpPath))
            {
                MakeSymbolicLinkToPDB(dumpPath, revision);
                ProcessStartInfo startInfo = new ProcessStartInfo(dumpPath);
                string tempCurrDirectory = Environment.CurrentDirectory;
                Environment.CurrentDirectory = Path.GetDirectoryName(dumpPath);
                Process.Start(startInfo);
                Environment.CurrentDirectory = tempCurrDirectory;
            }
        }

        private void MakeSymbolicLinkToPDB(string dumpPath, string revision)
        {
            string PathToPDB = Path.Combine(GetMainDumpPath(), revision);
            string[] pdbFiles = Directory.GetFiles(PathToPDB, "*.pdb", SearchOption.TopDirectoryOnly);
            string targetDirectory = Path.GetDirectoryName(dumpPath);
            if (pdbFiles.Length > 0)
            {
                string fileName = Path.GetFileName(pdbFiles[0]);
                string mklinkCommand = @"/C mklink /h " + targetDirectory + "\\" + fileName + " " + pdbFiles[0];

                Process.Start("cmd.exe", mklinkCommand).WaitForExit();
                Thread.Sleep(1000);
            }
        }

        private void Test_Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void AddToDumpGroups(string CallstackTop, List<string> FullCallstack, string DumpLocations)
        {
            for (int i = 0; i < DumpGroups.Count; ++i)
            {
                DumpGroup CurrDumpGroup = DumpGroups[i];
                if (CurrDumpGroup.TopLine == CallstackTop)
                {
                    CurrDumpGroup.Callstack.Add(FullCallstack);
                    CurrDumpGroup.PathToDump.Add(DumpLocations);
                    CurrDumpGroup.Inc();
                    return;
                }
            }

            DumpGroup NewDumpGroup = new DumpGroup(CallstackTop);            
            NewDumpGroup.Callstack.Add(FullCallstack);
            NewDumpGroup.PathToDump.Add(DumpLocations);

            DumpGroups.Add(NewDumpGroup);
        }

        private void ShowDumpGroups()
        {
            if (file_ListBox.SelectedItems.Count > 3 && DumpGroups.Count > 0)
            {
                DumpGroups.Sort((a, b) => b.Count.CompareTo(a.Count));                
                int TotalCrashes = DumpGroups[0].Count;
                string ResultString = DumpGroups[0].TopLine + "  " + "Count: " + DumpGroups[0].Count + "\n";

                for (int i = 1; i < DumpGroups.Count; ++i)
                {
                    ResultString += DumpGroups[i].TopLine + "  " + "Count: " + DumpGroups[i].Count + "\n";
                    TotalCrashes += DumpGroups[i].Count;
                }
                ResultString += "\n W sumie crashy: " + TotalCrashes.ToString();

                // pobierz rewizje z zaznaczonego pliku
                List<string> revisions = new List<string>();
                for (int i = 0; i < file_ListBox.SelectedItems.Count; ++i)
                {
                    ListBoxItem lbi = (ListBoxItem)file_ListBox.SelectedItems[i];
                    string functionName = lbi.Content.ToString();
                    string revision = GetRevisionFromFileName(functionName).ToString();
                    if (!revisions.Contains(revision))
                        revisions.Add(revision);
                }

                string TitleString = "Podsumowanie z rewizji: " + String.Join(", ", revisions.ToArray());

                MessageBoxResult messegeBoxResult = MessageBox.Show(ResultString, TitleString, MessageBoxButton.OKCancel);
                if (messegeBoxResult == MessageBoxResult.OK)
                {
                    Slack.SendMessage(ResultString, TitleString);
                }
            }
        }

        private string GetFileNameWithoutInvalidChars(string inFileName)
        {
            string invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());

            foreach (char c in invalid)
            {
                inFileName = inFileName.Replace(c.ToString(), "");
            }

            return inFileName;
        }

        private void Filter_CheckBox_Click(object sender, RoutedEventArgs e)
        {
            int idx = 0;

            if (Filter_CheckBox.IsChecked.GetValueOrDefault() == true)
            {
                file_ListBox.Items.Clear();
                for (int i = 0; i < FileNamesOnFtp.Count; ++i)
                {
                    string RandomNum = GetRandomNumberFromFileName(FileNamesOnFtp[i]);
                    bool bIsNumeric = int.TryParse(RandomNum, out int value);
                    if (bIsNumeric)
                    {
                        string Revision = GetRevisionFromFileName(FileNamesOnFtp[i]);

                        string RevisionFromFilter = Filter_TextBox.Text;
                        bool filterOn = Filter_TextBox.Text.Length > 0;

                        if (!filterOn || (RevisionFromFilter == Revision))
                        {
                            ListBoxItem lbi = new ListBoxItem
                            {
                                Foreground = IsRevisionOK(Revision) ? BrushGreen : BrushRed,
                                Content = idx++.ToString() + "_" + FileNamesOnFtp[i]
                            };
                            file_ListBox.Items.Add(lbi);
                        }
                    }
                }
            }
            else
            {
                file_ListBox.Items.Clear();
                for (int i = 0; i < FileNamesOnFtp.Count; ++i)
                {
                    string Revision = GetRevisionFromFileName(FileNamesOnFtp[i]);
                    ListBoxItem lbi = new ListBoxItem
                    {
                        Foreground = IsRevisionOK(Revision) ? BrushGreen : BrushRed,
                        Content = idx++.ToString() + "_" + FileNamesOnFtp[i]
                    };
                    file_ListBox.Items.Add(lbi);
                }
            }
        }

        private void Filter_Button_Click(object sender, RoutedEventArgs e)
        {
            string filterString = Filter_TextBox.Text;
            file_ListBox.Items.Clear();

            for (int i = 0; i < FileNamesOnFtp.Count; ++i)
            {
                if (FileNamesOnFtp[i].ToLower().Contains(filterString.ToLower()))
                {
                    string Revision = GetRevisionFromFileName(FileNamesOnFtp[i]);
                    ListBoxItem lbi = new ListBoxItem
                    {
                        Foreground = IsRevisionOK(Revision) ? BrushGreen : BrushRed,
                        Content = i.ToString() + "_" + FileNamesOnFtp[i]
                    };
                    file_ListBox.Items.Add(lbi);
                }
            }

            FilteredCrashInfos.Clear();
            List<string> LocalCrashInfosNames = new List<string>();
            for (int i = 0; i < CrashInfos.Count; ++i)
            {
                CrashInfo CurrInfo = CrashInfos[i];
                if (CurrInfo.FullName.ToLower().Contains(filterString.ToLower()))
                {
                    FilteredCrashInfos.Add(CurrInfo);
                    LocalCrashInfosNames.Add(CrashInfosNames[i]);
                }
            }

            CrashInfo_ListBox.Items.Clear();
            for (int i = 0; i < FilteredCrashInfos.Count; ++i)
            {
                CrashInfo_ListBox.Items.Add(LocalCrashInfosNames[i]);
            }
        }

        private void Count_Button_Click(object sender, RoutedEventArgs e)
        {
            Dictionary<string, int> resultDict = new Dictionary<string, int>();
            string revision = "Unknown.";
            for (int i = 0; i < file_ListBox.SelectedItems.Count; ++i)
            {
                ListBoxItem lbi = (ListBoxItem)file_ListBox.SelectedItems[i];
                string functionName = lbi.Content.ToString();
                if (revision == "Unknown.")
                {
                    revision = GetRevisionFromFileName(functionName).ToString();
                }

                functionName = GetRandomNumberFromFileName(functionName);
                string[] tempStrings = functionName.Split('_');
                if (tempStrings.Length > 1)
                    functionName = tempStrings[tempStrings.Length - 5];

                if (resultDict.ContainsKey(functionName))
                {
                    resultDict[functionName] = resultDict[functionName] + 1;
                }
                else
                {
                    resultDict.Add(functionName, 1);
                }
            }

            string resultString = "Selected files: " + file_ListBox.SelectedItems.Count.ToString();

            foreach (KeyValuePair<string, int> item in resultDict.OrderByDescending(x => x.Value))
            {
                resultString += "\n" + item.Key.ToString() + " Count: " + item.Value.ToString();
            }

            string titleString = "Podsumowanie z rewizji: " + revision;

            MessageBoxResult messegeBoxResult = MessageBox.Show(resultString, "Podsumowanie", MessageBoxButton.OKCancel);
            if (messegeBoxResult == MessageBoxResult.OK)
            {
                Slack.SendMessage(resultString, titleString);
            }
        }

        private void Filter_TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Filter_Button_Click(sender, e);
            }
        }

        private void CrashInfo_ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool IsSomethingSelected = CrashInfo_ListBox.SelectedItem != null;
            OpenDump_Button.IsEnabled = IsSomethingSelected;

            if (IsSomethingSelected)
            {
                int idx = CrashInfo_ListBox.SelectedIndex;
                if (FilteredCrashInfos.Count > idx)
                {
                    CrashInfo_TextBox.Text = CrashInfo_ListBox.SelectedItem.ToString() + " " + CrashInfo_ListBox.SelectedIndex.ToString();
                    CrashInfo_TextBox.Text += "\n ------------------------" + FilteredCrashInfos[idx].InfoFromCrashReporterLog;
                    CrashInfo_TextBox.Text += "\n ------------------------ Callstack ------------------------ \n";
                    CrashInfo_TextBox.Text += "\n" + FilteredCrashInfos[idx].FullCrash;
                    CrashInfo_TextBox.Text += "\n ------------------------ Callstack End ------------------------ \n";
                    CrashInfo_TextBox.Text += "\n" + FilteredCrashInfos[idx].CrashContextShort;
                    Rename_TextBox.Text = CrashInfo_ListBox.SelectedItem.ToString() + ".7z";
                    CrashContextInfo_TextBox.Text = FilteredCrashInfos[idx].CrashContextFull;
                    ShortCrashContextInfo_TextBox.Text = FilteredCrashInfos[idx].CrashContextShort;
                }
                else
                    MessageBox.Show("CrashInfo jest popsute");
            }
            else
                CrashInfo_TextBox.Text = "NULL";
        }

        private void File_ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool isSomethingSelected = file_ListBox.SelectedItem != null;
            AnalyzeCrash_Button.IsEnabled = isSomethingSelected && CurrentWorkingMode == WorkingMode.UncheckedFtp;
            switch (CurrentWorkingMode)
            {
                case WorkingMode.UncheckedFtp:
                    break;
                case WorkingMode.CheckedFtp:
                    break;
                case WorkingMode.CheckedVault:
                    if (isSomethingSelected)
                        ReadCallstackFromVault();
                    OpenDump_Button.IsEnabled = isSomethingSelected;
                    break;
            }

            if (isSomethingSelected)
            {
                ListBoxItem selectedItem = (ListBoxItem)file_ListBox.SelectedItem;
                string revision = GetRevisionFromFileName(selectedItem.Content.ToString());
                Configure_TextBox.Text = revision;
            }
        }

        private void ReadCallstackFromVault()
        {
            ListBoxItem selectedItem = (ListBoxItem)file_ListBox.SelectedItem;
            string vaultDir = selectedItem.Content.ToString().Substring(selectedItem.Content.ToString().IndexOf('_') + 1);
            vaultDir = Path.Combine(Properties.Settings.Default.CheckedDumpsVaultPath, vaultDir);
            if (Directory.Exists(vaultDir))
            {
                string callstackFilePath = Path.Combine(vaultDir, "callstack.txt");
                if (File.Exists(callstackFilePath))
                {
                    string callstack = InfoGatherer.ReadFile(callstackFilePath);
                    CrashInfo_TextBox.Text = callstack;
                }
            }

        }

        private void CopyInfoToSlack_Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string infoToSend = "";
                string titleToSend = "Unknown Title";

                if (CrashInfo_ListBox.SelectedItem != null)
                {
                    int idx = CrashInfo_ListBox.SelectedIndex;
                    if (FilteredCrashInfos.Count > idx)
                    {
                        titleToSend = FilteredCrashInfos[idx].CallstackTop + "  rev. " + FilteredCrashInfos[idx].Revision.ToString();

                        string s = FilteredCrashInfos[idx].FullCrash;

                        infoToSend += "Name: " + FilteredCrashInfos[idx].FullName.Substring(FilteredCrashInfos[idx].FullName.IndexOf(Properties.Settings.Default.Project_CodenameShort, StringComparison.Ordinal)) + "\n\n";

                        infoToSend += FilteredCrashInfos[idx].InfoFromCrashReporterLog;
                        infoToSend += " \n --------------- CALLSTACK --------------- \n" + FilteredCrashInfos[idx].FullCrash;
                    }
                    else
                        MessageBox.Show("CrashInfo jest popsute");
                }
                else
                    CrashInfo_TextBox.Text = "NULL";

                Slack.SendMessage(infoToSend, titleToSend);
            }
            catch (Exception ex)
            {
                Logger.Log("Error on sending info to slack: " + ex.ToString());
                MessageBox.Show("Error on sending info to slack: " + ex.ToString(), @"Cos nie zabanglało", MessageBoxButton.OK);
            }

        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.WindowState == System.Windows.WindowState.Minimized)
            {
                //FlashTaskbar();
            }
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            // FlashTaskbar();
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            //FlashTaskbarStop();
        }

        private string GetRevisionFromFileName(string fileName)
        {
            try
            {
                int lastIdx = fileName.ToLower().LastIndexOf("-");
                if (lastIdx == -1)
                    return "UNKNOWN";

                string revision = fileName.Substring(lastIdx + 1);
                revision = Path.GetFileNameWithoutExtension(revision);
                return revision;
            }
            catch (System.Exception ex)
            {
                Logger.Log("Error on GetRevisionFromFileName: " + ex.ToString());
            }

            return "UNKNOWN";
        }        

        private bool IsRevisionOK(int Revision)
        {
            return IsRevisionOK(Revision.ToString());
        }

        private bool IsRevisionOK(string RevisionWithVersion)
        {
            string directoryWithPdb = Path.Combine(GetMainDumpPath(), RevisionWithVersion);
            string pdbFile = Path.Combine(directoryWithPdb, $"{Properties.Settings.Default.Project_Codename}.pdb");
            return Directory.Exists(directoryWithPdb) && File.Exists(pdbFile);
        }

        private void DeleteDirectory(string dirToDelete, bool recursive)
        {
            try
            {
                Directory.Delete(dirToDelete, true);
            }
            catch (Exception ex)
            {
                Logger.Log("Nie moge skasowac katalogu: " + dirToDelete + " \nError: " + ex.ToString());

                Process.Start("cmd.exe", @"/C rd /s /q " + dirToDelete);
            }
        }

        private string GenerateCallstackWithCdb(string dirName)
        {
            string dmpPath = GetDumpPath(dirName);
            string cdb_debugger = Path.Combine(debuggersPath, _cdb);
            string revision = GetRevisionFromFileName(dirName);
            string binaryDir = Path.Combine(GetMainDumpPath(), revision.ToString());
            string args = @" -z " + dmpPath + @" -y srv*" + symbolsPath + "*https://msdl.microsoft.com/download/symbols;" + binaryDir + @";" + symbolsPath + @" -c "".ecxr;.lines -e;k;qq""";
            string output = "";
            if (File.Exists(cdb_debugger))
            {
                Process cdbProcess = new Process();
                cdbProcess.StartInfo.FileName = cdb_debugger;
                cdbProcess.StartInfo.Arguments = args;
                cdbProcess.StartInfo.RedirectStandardError = true;
                cdbProcess.StartInfo.RedirectStandardOutput = true;
                cdbProcess.StartInfo.UseShellExecute = false;
                cdbProcess.StartInfo.CreateNoWindow = true;

                cdbProcess.OutputDataReceived += new DataReceivedEventHandler(
                    (s, e) =>
                    {
                        Console.WriteLine(e.Data);
                        output += e.Data != null ? e.Data.ToString() + "\n" : "NO DATA\n";
                    }
                );
                cdbProcess.ErrorDataReceived += new DataReceivedEventHandler((s, e) => { Console.WriteLine(e.Data); });

                cdbProcess.Start();
                cdbProcess.BeginOutputReadLine();
                cdbProcess.WaitForExit();
            }

            return output;
        }

        private void ClearCallstackFromCDB(ref string OutCallstack)
        {
            string tempString = @"  *** Stack trace for last set context - .thread/.cxr resets it";
            int idx1 = OutCallstack.IndexOf(tempString);
            int idx2 = OutCallstack.IndexOf(@"quit:");
            if (idx2 > idx1)
            {
                OutCallstack = OutCallstack.Substring(idx1, idx2 - idx1);
            }

            string[] lines = OutCallstack.Split('\n');
            string TempCallstack = "";
            bool bStart = false;
            string tempCallstackTop = "";

            for (int i = 0; i < lines.Length; ++i)
            {
                if (bStart)
                {
                    if (lines[i].Length > 36)
                    {
                        string CallstackFunction = lines[i].Substring(36);

                        TempCallstack += CallstackFunction + "\n";

                        if (!CallstackFunction.Contains(@"KERNELBASE") && !CallstackFunction.Contains(@"RaiseException") && !CallstackFunction.Contains(@"Logf") && !CallstackFunction.Contains(@"AssertFailed") && !CallstackFunction.Contains(@"NO-FUNCTION"))
                        {
                            tempCallstackTop = CallstackFunction;
                            string[] tabs = tempCallstackTop.Split('!');
                            if (tabs.Length == 2)
                            {
                                tempCallstackTop = tabs[1].Replace("::", "-").Replace("<", "-").Replace(">", "-").Replace("*", "-");
                                tempCallstackTop = tempCallstackTop.Contains('+') ? tempCallstackTop.Split('+').First() : tempCallstackTop.Split('[').First();
                                tempCallstackTop = tempCallstackTop.Trim();
                            }
                        }
                    }
                }

                if (lines[i].Contains("Call Site"))
                    bStart = true;
            }
        }

        private List<string> GetFullCallstackFromCDB(ref string InCallstack)
        {
            string CallStackTop = "Unknown";
            List<string> CallstackLines = new List<string>(InCallstack.Split('\n').Where(x => !string.IsNullOrEmpty(x)));
            CallStackTop = CallstackLines[0];
            CallStackTop = CallStackTop.Split(new string[] { "!" }, StringSplitOptions.RemoveEmptyEntries).Last().Split(new string[] { "()" }, StringSplitOptions.RemoveEmptyEntries).First();
            CallStackTop = CallStackTop.Trim(new char[] { '\n', '\r' });
            CallStackTop = CallStackTop.Replace("::", "-").Replace("<", "-").Replace(">", "-").Replace("*", "-");
            CallStackTop = CallStackTop.Replace("_", "-").Replace("<", "-").Replace(">", "-").Replace("*", "-");
            if ((CallStackTop.Contains("Assert") || CallStackTop.Contains("VCRUNTIME")) && CallstackLines.Count > 1)
            {
                CallStackTop = CallstackLines[1];
                CallStackTop = CallStackTop.Split(new string[] { "!" }, StringSplitOptions.RemoveEmptyEntries).Last().Split(new string[] { "()" }, StringSplitOptions.RemoveEmptyEntries).First();
                CallStackTop = CallStackTop.Trim(new char[] { '\n', '\r' });
                CallStackTop = CallStackTop.Replace("::", "-").Replace("<", "-").Replace(">", "-").Replace("*", "-");
                CallStackTop = CallStackTop.Replace("_", "-").Replace("<", "-").Replace(">", "-").Replace("*", "-");
            }

            if (CallStackTop == "")
                CallStackTop = "Unknown";
            return CallstackLines;
        }        

        private void ProcessNewCallstackCDB(ref string NewCallstack, ref string CallstackTop)
        {
            string tempString = @"  *** Stack trace for last set context - .thread/.cxr resets it";
            tempString = @"Call Site";
            int idx1 = NewCallstack.IndexOf(tempString) + 9;
            int idx2 = NewCallstack.IndexOf(@"quit:");
            if (idx2 > idx1)
            {
                NewCallstack = NewCallstack.Substring(idx1, idx2 - idx1);
            }

            string[] lines = NewCallstack.Split(new char[] {'\n'}, StringSplitOptions.RemoveEmptyEntries);
            string TempCallstack = "";            
            string tempCallstackTop = CallstackTop;

            for (int i = 0; i < lines.Length; ++i)
            {
                if (lines[i].Length > 36)
                {
                    lines[i] = lines[i].Substring(36);
                    string CallstackFunction = lines[i];
                    TempCallstack += $"{i:D2} - {CallstackFunction}\n";

                    //if (stringArray.Any(stringToCheck.Contains))
                    //if (!CallstackFunction.Contains(@"KERNELBASE") && !CallstackFunction.Contains(@"RaiseException") && !CallstackFunction.Contains(@"Logf") && !CallstackFunction.Contains(@"AssertFailed") && !CallstackFunction.Contains(@"NO-FUNCTION") && tempCallstackTop == CallstackTop)
                    if (!callstackTopExcludedWords.Any(CallstackFunction.Contains) && tempCallstackTop == CallstackTop)
                    {
                        tempCallstackTop = CallstackFunction;
                        string[] tabs = tempCallstackTop.Split('!');
                        if (tabs.Length == 2)
                        {
                            CallstackTop = tabs[1].Replace("::", "-").Replace("<", "-").Replace(">", "-").Replace("*", "-");
                            CallstackTop = CallstackTop.Contains('+') ? CallstackTop.Split('+').First() : CallstackTop.Split('[').First();
                            CallstackTop = CallstackTop.Trim();
                        }
                    }
                }
            }

            NewCallstack = TempCallstack.Trim();
        }

        private void CreateCallstackFile(ref string newCallstack, ref string callstackTop, string targetDirectory, string orginalFileName)
        {
            string newFileName = Path.Combine(targetDirectory, "callstack.txt");
            using (StreamWriter sw = File.CreateText(newFileName))
            {
                sw.WriteLine(orginalFileName);
                sw.WriteLine("");
                sw.WriteLine(callstackTop);
                sw.WriteLine("");
                sw.WriteLine(newCallstack);
            }
        }
        private async void Configure_Button_Click(object sender, RoutedEventArgs e)
        {
            string newRevision = Configure_TextBox.Text;
            StatusBar_TextBlock.Text = "Trying to configure revision: " + newRevision + " Downloading PDB - this may take a while";
            StatusBar_TextBlock.InvalidateVisual();
            StatusBar_TextBlock.UpdateLayout();
            StatusBar_TextBlock.UpdateDefaultStyle();            
            await Task.Factory.StartNew((() =>
            {
                ConfigureHelper.FullConfiguration(newRevision);
            }));
            StatusBar_TextBlock.Text = "Completed configuring of revision: " + newRevision;
        }

        private void Settings_Button_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow SettingsWin = new SettingsWindow
            {
                Owner = this
            };
            SettingsWin.ShowDialog();
        }
    }
}
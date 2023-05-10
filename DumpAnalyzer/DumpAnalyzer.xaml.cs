using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Threading;
using Renci.SshNet;
using Renci.SshNet.Common;
using Timer = System.Threading.Timer;

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

        public class UserGroup
        {
            public string UserName;
            public string MachineId;
            public int Count;

            public UserGroup(string inUserName, string inMachineId)
            {
                UserName = inUserName;
                MachineId = inMachineId;
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


            public CrashInfo(string inFullCrash, string inCallstackTop, string inRevision, string inFullName,
                string inDumpPath, string inCrashContextFull, string inCrashContextShort, string inCrashReporterInfo)
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

        private WorkingMode _currentWorkingMode;
        List<DumpGroup> _dumpGroups = new List<DumpGroup>();
        List<UserGroup> _userGroups = new List<UserGroup>();

        private List<string> _fileNamesOnFtp = new List<string>();
        private string _dirWithRevision = "";

        private static string _cdb = "cdb.exe";
        private static string _dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"data");
        private static string _dumpsPath = Path.Combine(_dataPath, @"dumps");
        private static string _debuggersPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"data/debuggers");

        private static readonly string[] CallstackTopExcludedWords =
        {
            @"KERNELBASE", @"RaiseException", @"Logf", @"AssertFailed", @"ReportAssert", @"NO-FUNCTION",
            @"WindowsErrorOutputDevice", @"CheckVerifyFailed", @"lambda"
        };
        //if (!CallstackFunction.Contains(@"KERNELBASE") && !CallstackFunction.Contains(@"RaiseException") && !CallstackFunction.Contains(@"Logf") && !CallstackFunction.Contains(@"AssertFailed") && !CallstackFunction.Contains(@"NO-FUNCTION") && tempCallstackTop == CallstackTop)

        public static string GetMainDumpPath()
        {
            return _dumpsPath;
        }

        private static string _symbolsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"data/symbols");
        private IConnection _onlineConnection;

        SolidColorBrush _brushRed = new SolidColorBrush(Colors.Red);
        SolidColorBrush _brushGreen = new SolidColorBrush(Colors.Green);

        public DumpAnalyzerWindow()
        {
            InitializeComponent();
            Logger.Init();
            CrashInfo_TextBox.Text = "Here will be infomartion about crash.";
        }

        private void CheckSFTPForCrashes_Button_Click(object sender, RoutedEventArgs e)
        {
            {
                CheckSftpForCrashes();
                ColorButtons(CheckFTPForCrashes_Button);
                //var worker = new BackgroundWorker();
                //worker.DoWork += new DoWorkEventHandler(worker_DoWork);
                // worker.RunWorkerAsync();
            }
        }
        
        void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            TimerEvent(this, null);
            //CheckSFTPForCrashes();
            //ColorButtons(sender);
            //StatusBar_TextBlock.Text = "PO KLIKU";
        }

        private void CheckAnalyzedOnFTP_Button_Click(object sender, RoutedEventArgs e)
        {
            _currentWorkingMode = WorkingMode.CheckedFtp;
            if (_onlineConnection == null)
            {
                _onlineConnection = new SFTP();
            }

            List<string> filesOnServer = _onlineConnection.GetAnalyzedDumpsFromServer();
            _fileNamesOnFtp.Clear();
            file_ListBox.Items.Clear();
            int idx = 0;
            foreach (string file in filesOnServer)
            {
                _fileNamesOnFtp.Add(file);
                string revision = GetRevisionFromFileName(file);
                ListBoxItem lbi = new ListBoxItem
                {
                    Foreground = IsRevisionOk(revision) ? _brushGreen : _brushRed,
                    Content = $"{idx}_{file}"
                };
                file_ListBox.Items.Add(lbi);
                idx++;
            }

            Console.WriteLine("Directory List Complete, status");

            StatusBar_TextBlock.Text =
                $"Checking FTP for analyzed dumps finished. Found: {file_ListBox.Items.Count} files.";

            ColorButtons(sender);
        }

        private void CheckAnalyzedOnVault_Button_Click(object sender, RoutedEventArgs e)
        {
            _currentWorkingMode = WorkingMode.CheckedVault;
            file_ListBox.Items.Clear();
            CrashInfo_ListBox.Items.Clear();
            ShortCrashContextInfo_TextBox.Clear();
            string vaultDirectoryPath = Properties.Settings.Default.CheckedDumpsVaultPath;
            if (Directory.Exists(vaultDirectoryPath))
            {
                string[] analyzedDumps = Directory.GetDirectories(vaultDirectoryPath);
                int idx = 0;
                string filterString = Filter_TextBox.Text.ToLower();
                
                StatusBar_TextBlock.Text = $"Checking Vault for analyzed dumps finished. Found: {analyzedDumps.Length} files.";
                ForceUpdateUi();
                
                foreach (string analyzedDump in analyzedDumps)
                {
                    if (analyzedDump.ToLower().Contains(filterString))
                    {
                        string dumpName = Path.GetFileName(analyzedDump);
                        string revision = GetRevisionFromFileName(dumpName);
                        ListBoxItem lbi = new ListBoxItem
                        {
                            Foreground = IsRevisionOk(revision) ? _brushGreen : _brushRed,
                            Content = $"{idx}_{dumpName}"
                        };
                        file_ListBox.Items.Add(lbi);
                        idx++;
                    }
                }
            }

            ColorButtons(sender);
        }

        private void ColorButtons(object sender)
        {
            CheckFTPForCrashes_Button.Foreground = sender == CheckFTPForCrashes_Button ? Brushes.Green : Brushes.Black;
            CheckAnalyzedOnFTP_Button.Foreground = sender == CheckAnalyzedOnFTP_Button ? Brushes.Green : Brushes.Black;
            CheckAnalyzedOnVault_Button.Foreground =
                sender == CheckAnalyzedOnVault_Button ? Brushes.Green : Brushes.Black;
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

        private void CheckSftpForCrashes()
        {
            StatusBar_TextBlock.Text = $"Checking dumps on FTP. It may take a while, please be patient and wait.";
            ForceUpdateUi();
            _currentWorkingMode = WorkingMode.UncheckedFtp;
            try
            {
                if (_onlineConnection == null)
                {
                    _onlineConnection = new SFTP();
                }
                
                Analyze_ProgressBar.Dispatcher.Invoke(() => Analyze_ProgressBar.Value = 55, System.Windows.Threading.DispatcherPriority.Send);
                ForceUpdateUi();
                List<string> filesOnServer = _onlineConnection.GetFilesFromServer();
                
                _fileNamesOnFtp.Clear();
                file_ListBox.Items.Clear();
                int idx = 0;
                foreach (var file in filesOnServer)
                {
                    _fileNamesOnFtp.Add(file);
                    string revision = GetRevisionFromFileName(file);
                    ListBoxItem lbi = new ListBoxItem
                    {
                        Foreground = IsRevisionOk(revision) ? _brushGreen : _brushRed,
                        Content = $"{idx}_{file}"
                    };
                    file_ListBox.Items.Add(lbi);
                    idx++;
                }

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
            _crashInfos.Clear();
            _filteredCrashInfos.Clear();
            _crashInfosNames.Clear();
            _dumpGroups.Clear();
        }

        private void AnalyzeCrashOnFTP()
        {
            try
            {
                CrashInfo_TextBox.Text = "Here will be info about crash.";
                int selectedFileCount = file_ListBox.SelectedItems.Count;
                Analyze_ProgressBar.Dispatcher.Invoke(() => Analyze_ProgressBar.Value = 0,
                    System.Windows.Threading.DispatcherPriority.Send);
                float progressBarStep = 100 / selectedFileCount;

                for (int i = 0; i < selectedFileCount; ++i)
                {
                    bool bError = false;
                    string fileName = "";
                    string localFileName = "";
                    string dirName = "";
                    string localDirPath = "";
                    string newDirName = "";
                    string callstackTop = "";
                    try
                    {
                        ListBoxItem lbi = (ListBoxItem)file_ListBox.SelectedItems[i];
                        fileName = lbi.Content.ToString();
                        int fileIdx = fileName.IndexOf('_') + 1;
                        if (fileIdx == 0)
                        {
                            MessageBox.Show("Something went wrong... probably bad filename");
                            continue;
                        }

                        string testNumeric = fileName.Substring(0, fileIdx - 1);
                        bool bIsNumeric = int.TryParse(testNumeric, out int helperIdx);
                        if (bIsNumeric)
                            fileName = fileName.Substring(fileIdx);
                        localFileName = GetFileNameWithoutInvalidChars(fileName);
                        dirName = Path.GetFileNameWithoutExtension(localFileName);
                        string analyzedFilesBeginString = "Analyzed files: " + (i + 1).ToString() + @"/" + selectedFileCount.ToString() + " - " + localFileName;
                        UpdateStatusBar(analyzedFilesBeginString + $" --- Preparing to analyze.");
                        string revision = GetRevisionFromFileName(dirName);

                        if (IsRevisionOk(revision))
                        {
                            _dirWithRevision = revision.ToString();
                        }
                        else
                            _dirWithRevision = "";

                        localDirPath = Path.Combine(GetMainDumpPath(), _dirWithRevision, dirName);

                        DownloadFile(fileName, localFileName);
                        UnpackFile(localFileName, localDirPath);
                        // real analyze of dump begins now
                        UpdateStatusBar(analyzedFilesBeginString + $" --- Generating callstack");
                        string cdbCallstack = GenerateCallstackWithCdb(localDirPath);
                        string crashContextFull = InfoGatherer.GetInfoFromCrashContextXml(localDirPath);
                        string crashContextShort = InfoGatherer.GetShortInfoFromCrashContextXml(localDirPath);
                        string callStack = cdbCallstack;
                        string userName = InfoGatherer.GetUsernameInfoFromCrashReporter(ref localDirPath);
                        string machineId = InfoGatherer.GetMachineIdInfoFromCrashContext(ref crashContextFull);
                        callstackTop = "Unknown";
                        ProcessNewCallstackCdb(ref cdbCallstack, ref callstackTop);
                        CreateCallstackFile(ref cdbCallstack, ref callstackTop, localDirPath, fileName);

                        List<string> fullCallstack = GetFullCallstackFromCdb(ref cdbCallstack);

                        string randomNumber = GetRandomNumberFromFileName(dirName);
                        newDirName = dirName;
                        if (randomNumber.Length > 0)
                            newDirName = dirName.Replace(randomNumber, callstackTop);
                        else
                            newDirName = dirName.Insert(22, callstackTop);

                        SetDumpAsChecked(localDirPath, newDirName);
                        RemoveFromFtpAfterAnalyze(fileName);

                        string crashInfoName = helperIdx + "_" + newDirName;
                        _crashInfosNames.Add(crashInfoName);
                        CrashInfo_ListBox.Items.Add(crashInfoName);
                        string dumpPath = dirName;
                        string fullCrashInfo = "Path to unpacked dump: " + dumpPath + " " + "\n" + callStack;
                        string infoFromCrashReporterLog = InfoGatherer.GetInfoFromCrashReporterLogFile(localDirPath);
                        CrashInfo info = new CrashInfo(cdbCallstack, callstackTop, revision, crashInfoName, dumpPath,
                            crashContextFull, crashContextShort, infoFromCrashReporterLog);
                        _crashInfos.Add(info);
                        _filteredCrashInfos.Add(info);
                        AddToDumpGroups(callstackTop, fullCallstack, dumpPath);
                        AddToUserGroups(userName, machineId);
                    }
                    catch (System.IO.PathTooLongException)
                    {
                        MessageBox.Show("Za dluga nazwa pliku (pewnie problem z odpakowaniem zipa) !!!!",
                            "Path Too Long Exception", MessageBoxButton.OK);
                        selectedFileCount--;
                        --i;
                    }
                    catch (System.Exception ex)
                    {
                        bError = true;
                        Logger.Log("Error on Analyze single crash. \nFilename: " + fileName + "\nLocal File Name: " +
                                   localFileName + "\nDirName: " + dirName + "\nNewDirName: " + newDirName +
                                   "\nCallstackTop: " + callstackTop + "\n" + ex.ToString());
                        MessageBox.Show(
                            "Error on Analyze single crash. \nFilename: " + fileName + "\nLocal File Name: " +
                            localFileName + "\nDirName: " + dirName + "\nNewDirName: " + newDirName +
                            "\nCallstackTop: " + callstackTop + "\n" + ex.ToString(), @"Cos nie zabanglało",
                            MessageBoxButton.OK);
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
                                string msg = "Error on trying to delete: " +
                                             Path.Combine(_dirWithRevision, localFileName) + "\nDirectory: " +
                                             Path.Combine(_dirWithRevision, dirName) + "\nError: " + ex.ToString();
                                Logger.Log(msg);
                            }
                        }

                        Analyze_ProgressBar.Value += progressBarStep;
                        UpdateStatusBar("Analyzed files: " + (i + 1).ToString() + @"/" + selectedFileCount.ToString() + " - " + localFileName);
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
                _onlineConnection.RemoveAfterAnalyze(fileToRemove);
            }
        }

        private void SetDumpAsChecked(string localDirPath, string targetDirPath)
        {
            string directoryName = Path.GetFileName(localDirPath).TrimEnd(Path.DirectorySeparatorChar);
            string targetDirectory = Path.Combine(Properties.Settings.Default.CheckedDumpsFtpPath, targetDirPath);
            //1. upload to ftp
            if (Properties.Settings.Default.IsCopyToFtpEnabled)
            {
                _onlineConnection.UploadDirectory(localDirPath, targetDirectory);
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
                _onlineConnection.DownloadFile(fileName, localFileName);
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

        private List<CrashInfo> _crashInfos = new List<CrashInfo>();
        private List<CrashInfo> _filteredCrashInfos = new List<CrashInfo>();
        private List<string> _crashInfosNames = new List<string>();


        private string GetRandomNumberFromFileName(string fileName)
        {
            fileName = fileName.Replace('.', '_');
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
            switch (_currentWorkingMode)
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
            string dirName = _filteredCrashInfos[CrashInfo_ListBox.SelectedIndex].DumpPath;
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
                MakeSymbolicLinkToPdbAndExe(dumpPath, revision);
                ProcessStartInfo startInfo = new ProcessStartInfo(dumpPath);
                string tempCurrDirectory = Environment.CurrentDirectory;
                Environment.CurrentDirectory = Path.GetDirectoryName(dumpPath);
                Process.Start(startInfo);
                Environment.CurrentDirectory = tempCurrDirectory;
            }
        }

        private void MakeSymbolicLinkToPdbAndExe(string dumpPath, string revision)
        {
            string pathToPdb = Path.Combine(GetMainDumpPath(), revision);
            //string[] pdbFiles = Directory.GetFiles(pathToPdb, "*.pdb,*.exe", SearchOption.TopDirectoryOnly);
            List<string> pdbFiles = Directory.EnumerateFiles(pathToPdb).Where(file => file.ToLower().EndsWith(".pdb") || file.ToLower().EndsWith(".exe")).ToList();

            string targetDirectory = Path.GetDirectoryName(dumpPath);
            foreach (string pdbFile in pdbFiles)
            {
                string fileName = Path.GetFileName(pdbFile);
                string mklinkCommand = @"/C mklink /h " + targetDirectory + "\\" + fileName + " " + pdbFile;
                Process.Start("cmd.exe", mklinkCommand).WaitForExit();
                Thread.Sleep(300);
            }
        }

        private void Test_Button_Click(object sender, RoutedEventArgs e)
        {
        }

        private void AddToDumpGroups(string callstackTop, List<string> fullCallstack, string dumpLocations)
        {
            for (int i = 0; i < _dumpGroups.Count; ++i)
            {
                DumpGroup currDumpGroup = _dumpGroups[i];
                if (currDumpGroup.TopLine == callstackTop)
                {
                    currDumpGroup.Callstack.Add(fullCallstack);
                    currDumpGroup.PathToDump.Add(dumpLocations);
                    currDumpGroup.Inc();
                    return;
                }
            }

            DumpGroup newDumpGroup = new DumpGroup(callstackTop);
            newDumpGroup.Callstack.Add(fullCallstack);
            newDumpGroup.PathToDump.Add(dumpLocations);

            _dumpGroups.Add(newDumpGroup);
        }

        private void AddToUserGroups(string inUserName, string inMachineId)
        {
            for (int i = 0; i < _userGroups.Count; ++i)
            {
                UserGroup currUserGroup = _userGroups[i];
                if (currUserGroup.MachineId == inMachineId)
                {
                    if (currUserGroup.UserName != inUserName)
                    {
                        currUserGroup.UserName += " " + inUserName;
                    }
                    currUserGroup.Inc();
                    return;
                }
            }

            UserGroup newUserGroup = new UserGroup(inMachineId, inUserName);
            _userGroups.Add(newUserGroup);
        }

        private void ShowDumpGroups()
        {
            if (file_ListBox.SelectedItems.Count > 3 && _dumpGroups.Count > 0)
            {
                _dumpGroups.Sort((a, b) => b.Count.CompareTo(a.Count));
                int totalCrashes = _dumpGroups[0].Count;
                string resultString = _dumpGroups[0].TopLine + "  " + "Count: " + _dumpGroups[0].Count + "\n";

                for (int i = 1; i < _dumpGroups.Count; ++i)
                {
                    resultString += _dumpGroups[i].TopLine + "  " + "Count: " + _dumpGroups[i].Count + "\n";
                    totalCrashes += _dumpGroups[i].Count;
                }

                resultString += "\n Total crashes: " + totalCrashes.ToString();

                // get revision from selected files
                List<string> revisions = new List<string>();
                for (int i = 0; i < file_ListBox.SelectedItems.Count; ++i)
                {
                    ListBoxItem lbi = (ListBoxItem)file_ListBox.SelectedItems[i];
                    string functionName = lbi.Content.ToString();
                    string revision = GetRevisionFromFileName(functionName).ToString();
                    if (!revisions.Contains(revision))
                        revisions.Add(revision);
                }

                string titleString = "Summary from revision: " + String.Join(", ", revisions.ToArray());

                MessageBoxResult messegeBoxResult =
                    MessageBox.Show(resultString, titleString, MessageBoxButton.OKCancel);
                if (messegeBoxResult == MessageBoxResult.OK)
                {
                    Slack.SendMessage(resultString, titleString);
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

            inFileName = inFileName.Replace('.','_').Replace("_7z", ".7z");
            return inFileName;
        }

        private void Filter_CheckBox_Click(object sender, RoutedEventArgs e)
        {
            int idx = 0;

            if (Filter_CheckBox.IsChecked.GetValueOrDefault() == true)
            {
                file_ListBox.Items.Clear();
                for (int i = 0; i < _fileNamesOnFtp.Count; ++i)
                {
                    string randomNum = GetRandomNumberFromFileName(_fileNamesOnFtp[i]);
                    bool bIsNumeric = int.TryParse(randomNum, out int value);
                    if (bIsNumeric)
                    {
                        string revision = GetRevisionFromFileName(_fileNamesOnFtp[i]);

                        string revisionFromFilter = Filter_TextBox.Text;
                        bool filterOn = Filter_TextBox.Text.Length > 0;

                        if (!filterOn || (revisionFromFilter == revision))
                        {
                            ListBoxItem lbi = new ListBoxItem
                            {
                                Foreground = IsRevisionOk(revision) ? _brushGreen : _brushRed,
                                Content = idx++.ToString() + "_" + _fileNamesOnFtp[i]
                            };
                            file_ListBox.Items.Add(lbi);
                        }
                    }
                }
            }
            else
            {
                file_ListBox.Items.Clear();
                for (int i = 0; i < _fileNamesOnFtp.Count; ++i)
                {
                    string revision = GetRevisionFromFileName(_fileNamesOnFtp[i]);
                    ListBoxItem lbi = new ListBoxItem
                    {
                        Foreground = IsRevisionOk(revision) ? _brushGreen : _brushRed,
                        Content = idx++.ToString() + "_" + _fileNamesOnFtp[i]
                    };
                    file_ListBox.Items.Add(lbi);
                }
            }
        }

        private void Filter_Button_Click(object sender, RoutedEventArgs e)
        {
            switch (_currentWorkingMode)
            {
                case WorkingMode.UncheckedFtp:
                    FilterFilesOnFtp();
                    break;
                case WorkingMode.CheckedFtp:
                    break;
                case WorkingMode.CheckedVault:
                    FilterFilesOnLocalDrive();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        void FilterFilesOnFtp()
        {
            string filterString = Filter_TextBox.Text;
            file_ListBox.Items.Clear();

            for (int i = 0; i < _fileNamesOnFtp.Count; ++i)
            {
                if (_fileNamesOnFtp[i].ToLower().Contains(filterString.ToLower()))
                {
                    string revision = GetRevisionFromFileName(_fileNamesOnFtp[i]);
                    ListBoxItem lbi = new ListBoxItem
                    {
                        Foreground = IsRevisionOk(revision) ? _brushGreen : _brushRed,
                        Content = i.ToString() + "_" + _fileNamesOnFtp[i]
                    };
                    file_ListBox.Items.Add(lbi);
                }
            }

            _filteredCrashInfos.Clear();
            List<string> localCrashInfosNames = new List<string>();
            for (int i = 0; i < _crashInfos.Count; ++i)
            {
                CrashInfo currInfo = _crashInfos[i];
                if (currInfo.FullName.ToLower().Contains(filterString.ToLower()))
                {
                    _filteredCrashInfos.Add(currInfo);
                    localCrashInfosNames.Add(_crashInfosNames[i]);
                }
            }

            CrashInfo_ListBox.Items.Clear();
            for (int i = 0; i < _filteredCrashInfos.Count; ++i)
            {
                CrashInfo_ListBox.Items.Add(localCrashInfosNames[i]);
            }
        }

        void FilterFilesOnLocalDrive()
        {
            CheckAnalyzedOnVault_Button_Click(CheckAnalyzedOnVault_Button, null);
        }

        public void UpdateStatusBar(string newStatusBarString)
        {
            StatusBar_TextBlock.Text = newStatusBarString;
            ForceUpdateUi();
        }

        public void ForceUpdateUi()
        {
            //Here update your label, button or any string related object.
            
            //Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate { }));    
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate { }));
        }

        private int _testInt222 = 0;
        public void TimerEvent(object source, ElapsedEventArgs e)
        {
            //var worker = new BackgroundWorker();
            //worker.DoWork += new DoWorkEventHandler(worker_DoWork);
            // worker.RunWorkerAsync();
            StatusBar_TextBlock.Dispatcher.Invoke(() => StatusBar_TextBlock.Text = $"testInt222 = {_testInt222}", System.Windows.Threading.DispatcherPriority.Send);
            //StatusBar_TextBlock.Text = $"testInt222 = {testInt222}";
            ForceUpdateUi();
            _testInt222++;
        }
        
        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            // Updating the Label which displays the current second
            StatusBar_TextBlock.Text = $"testInt222 = {_testInt222}";
            ForceUpdateUi();
            _testInt222++;
            // Forcing the CommandManager to raise the RequerySuggested event
            CommandManager.InvalidateRequerySuggested();
        }

        private void Count_Button_Click(object sender, RoutedEventArgs e)
        {
            Dictionary<string, int> resultDict = new Dictionary<string, int>();
            Dictionary<string, int> uniqueUsersDict = new Dictionary<string, int>();
            string revision = "Unknown.";
            for (int i = 0; i < file_ListBox.SelectedItems.Count; ++i)
            {
                ListBoxItem lbi = (ListBoxItem)file_ListBox.SelectedItems[i];
                string functionName = lbi.Content.ToString();
                string fileName = lbi.Content.ToString();
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

                if (_currentWorkingMode == WorkingMode.CheckedVault)
                {
                    string userName = InfoGatherer.GetUsernameInfoFromCrashReporter(ref fileName);
                    string crashContextFile = InfoGatherer.GetCrashContextFile(ref fileName);
                    string machineId = InfoGatherer.GetMachineIdInfoFromCrashContext(ref crashContextFile);
                    userName += "  - ID: " + machineId + "   "; 
                    if (uniqueUsersDict.ContainsKey(userName))
                    {
                        uniqueUsersDict[userName] += 1;
                    }
                    else
                    {
                        uniqueUsersDict.Add(userName, 1);
                    }
                }
            }

            string resultString = "Selected files: " + file_ListBox.SelectedItems.Count.ToString();

            foreach (KeyValuePair<string, int> item in resultDict.OrderByDescending(x => x.Value))
            {
                resultString += "\n" + item.Key.ToString() + " Count: " + item.Value.ToString();
            }

            resultString += Environment.NewLine + Environment.NewLine + "Unique users:" + Environment.NewLine;
            
            foreach (KeyValuePair<string, int> item in uniqueUsersDict.OrderByDescending(x => x.Value))
            {
                resultString += "\n" + item.Key.ToString() + " Count: " + item.Value.ToString();
            }

            string titleString = "Summary from revision: " + revision;

            MessageBoxResult messegeBoxResult =
                MessageBox.Show(resultString, "Summary", MessageBoxButton.OKCancel);
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
            bool isSomethingSelected = CrashInfo_ListBox.SelectedItem != null;
            OpenDump_Button.IsEnabled = isSomethingSelected;

            if (isSomethingSelected)
            {
                int idx = CrashInfo_ListBox.SelectedIndex;
                if (_filteredCrashInfos.Count > idx)
                {
                    CrashInfo_TextBox.Text = CrashInfo_ListBox.SelectedItem.ToString() + " " +
                                             CrashInfo_ListBox.SelectedIndex.ToString();
                    CrashInfo_TextBox.Text +=
                        "\n ------------------------" + _filteredCrashInfos[idx].InfoFromCrashReporterLog;
                    CrashInfo_TextBox.Text += "\n ------------------------ Callstack ------------------------ \n";
                    CrashInfo_TextBox.Text += "\n" + _filteredCrashInfos[idx].FullCrash;
                    CrashInfo_TextBox.Text += "\n ------------------------ Callstack End ------------------------ \n";
                    CrashInfo_TextBox.Text += "\n" + _filteredCrashInfos[idx].CrashContextShort;
                    Rename_TextBox.Text = CrashInfo_ListBox.SelectedItem.ToString() + ".7z";
                    CrashContextInfo_TextBox.Text = _filteredCrashInfos[idx].CrashContextFull;
                    ShortCrashContextInfo_TextBox.Text = _filteredCrashInfos[idx].CrashContextShort;
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
            AnalyzeCrash_Button.IsEnabled = isSomethingSelected && _currentWorkingMode == WorkingMode.UncheckedFtp;
            switch (_currentWorkingMode)
            {
                case WorkingMode.UncheckedFtp:
                    break;
                case WorkingMode.CheckedFtp:
                    break;
                case WorkingMode.CheckedVault:
                    if (isSomethingSelected)
                    {
                        ReadInfoFromVault();
                    }

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

        private void ReadInfoFromVault()
        {
            ListBoxItem selectedItem = (ListBoxItem)file_ListBox.SelectedItem;
            string vaultDir = selectedItem.Content.ToString()
                .Substring(selectedItem.Content.ToString().IndexOf('_') + 1);
            vaultDir = Path.Combine(Properties.Settings.Default.CheckedDumpsVaultPath, vaultDir);
            if (Directory.Exists(vaultDir))
            {
                // crash context
                string crashContextFull = InfoGatherer.GetInfoFromCrashContextXml(vaultDir);
                string crashContextShort = InfoGatherer.GetShortInfoFromCrashContextXml(vaultDir);
                CrashContextInfo_TextBox.Text = crashContextFull;
                ShortCrashContextInfo_TextBox.Text = crashContextShort;
                // crashreporter log
                string callstackFilePath = Path.Combine(vaultDir, "callstack.txt");
                if (File.Exists(callstackFilePath))
                {
                    string callstack = InfoGatherer.ReadFile(callstackFilePath);
                    string infoFromCrashReporterLogFile = InfoGatherer.GetInfoFromCrashReporterLogFile(vaultDir);

                    CrashInfo_TextBox.Text =
                        "\n ------------------------ Information from crash reporter ------------------------ \n";
                    CrashInfo_TextBox.Text += infoFromCrashReporterLogFile;
                    CrashInfo_TextBox.Text += "\n ------------------------ Callstack ------------------------ \n";
                    CrashInfo_TextBox.Text += callstack;
                    CrashInfo_TextBox.Text += "\n ------------------------ Callstack End ------------------------ \n";
                    CrashInfo_TextBox.Text +=
                        "\n ------------------------ Crash Context Short ------------------------ \n";
                    CrashInfo_TextBox.Text += "\n" + crashContextShort;
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
                    if (_filteredCrashInfos.Count > idx)
                    {
                        titleToSend = _filteredCrashInfos[idx].CallstackTop + "  rev. " +
                                      _filteredCrashInfos[idx].Revision.ToString();

                        string s = _filteredCrashInfos[idx].FullCrash;

                        infoToSend += "Name: " + _filteredCrashInfos[idx].FullName.Substring(_filteredCrashInfos[idx]
                            .FullName.IndexOf(Properties.Settings.Default.Project_CodenameShort,
                                StringComparison.Ordinal)) + "\n\n";

                        infoToSend += _filteredCrashInfos[idx].InfoFromCrashReporterLog;
                        infoToSend += " \n --------------- CALLSTACK --------------- \n" +
                                      _filteredCrashInfos[idx].FullCrash;
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
                MessageBox.Show("Error on sending info to slack: " + ex.ToString(), @"Cos nie zabanglało",
                    MessageBoxButton.OK);
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

        private bool IsRevisionOk(int revision)
        {
            return IsRevisionOk(revision.ToString());
        }

        private bool IsRevisionOk(string revisionWithVersion)
        {
            string directoryWithPdb = Path.Combine(GetMainDumpPath(), revisionWithVersion);
            if (Directory.Exists(directoryWithPdb))
            {
                string[] pdbFiles = Directory.GetFiles(directoryWithPdb, "*.pdb", SearchOption.AllDirectories);
                return pdbFiles.Length > 0;
            }

            return false;
        }

        private void DeleteDirectory(string dirToDelete, bool recursive)
        {
            try
            {
                Directory.Delete(dirToDelete, true);
            }
            catch (Exception ex)
            {
                Logger.Log("Can't delete directory: " + dirToDelete + " \nError: " + ex.ToString());

                Process.Start("cmd.exe", @"/C rd /s /q " + dirToDelete);
            }
        }

        private string GenerateCallstackWithCdb(string dirName)
        {
            string dmpPath = GetDumpPath(dirName);
            string cdbDebugger = Path.Combine(_debuggersPath, _cdb);
            string revision = GetRevisionFromFileName(dirName);
            string binaryDir = Path.Combine(GetMainDumpPath(), revision.ToString());
            string args = @" -z " + dmpPath + @" -y srv*" + _symbolsPath +
                          "*https://msdl.microsoft.com/download/symbols;" + binaryDir + @";" + _symbolsPath +
                          @" -c "".ecxr;.lines -e;k;qq""";
            string output = "";
            if (File.Exists(cdbDebugger))
            {
                Process cdbProcess = new Process();
                cdbProcess.StartInfo.FileName = cdbDebugger;
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
                //cdbProcess.WaitForExit();

                string oldStatusBarString = StatusBar_TextBlock.Text;
                while (!cdbProcess.HasExited)
                {
                    string timerString = " ";
                    for (int i = 0; i < 3; ++i)
                    {
                        timerString += ". ";
                        Thread.Sleep(500);
                        UpdateStatusBar($"{oldStatusBarString}  {timerString}");
                    }
                    
                }
                    
            }

            return output;
        }

        private void ClearCallstackFromCdb(ref string outCallstack)
        {
            string tempString = @"  *** Stack trace for last set context - .thread/.cxr resets it";
            int idx1 = outCallstack.IndexOf(tempString);
            int idx2 = outCallstack.IndexOf(@"quit:");
            if (idx2 > idx1)
            {
                outCallstack = outCallstack.Substring(idx1, idx2 - idx1);
            }

            string[] lines = outCallstack.Split('\n');
            string tempCallstack = "";
            bool bStart = false;
            string tempCallstackTop = "";

            for (int i = 0; i < lines.Length; ++i)
            {
                if (bStart)
                {
                    if (lines[i].Length > 36)
                    {
                        string callstackFunction = lines[i].Substring(36);

                        tempCallstack += callstackFunction + "\n";

                        if (!callstackFunction.Contains(@"KERNELBASE") &&
                            !callstackFunction.Contains(@"RaiseException") && !callstackFunction.Contains(@"Logf") &&
                            !callstackFunction.Contains(@"AssertFailed") && !callstackFunction.Contains(@"NO-FUNCTION"))
                        {
                            tempCallstackTop = callstackFunction;
                            string[] tabs = tempCallstackTop.Split('!');
                            if (tabs.Length == 2)
                            {
                                tempCallstackTop = tabs[1].Replace("::", "-").Replace("<", "-").Replace(">", "-")
                                    .Replace("*", "-");
                                tempCallstackTop = tempCallstackTop.Contains('+')
                                    ? tempCallstackTop.Split('+').First()
                                    : tempCallstackTop.Split('[').First();
                                tempCallstackTop = tempCallstackTop.Trim();
                            }
                        }
                    }
                }

                if (lines[i].Contains("Call Site"))
                    bStart = true;
            }
        }

        private List<string> GetFullCallstackFromCdb(ref string inCallstack)
        {
            string callStackTop = "Unknown";
            List<string> callstackLines =
                new List<string>(inCallstack.Split('\n').Where(x => !string.IsNullOrEmpty(x)));
            callStackTop = callstackLines[0];
            callStackTop = callStackTop.Split(new string[] { "!" }, StringSplitOptions.RemoveEmptyEntries).Last()
                .Split(new string[] { "()" }, StringSplitOptions.RemoveEmptyEntries).First();
            callStackTop = callStackTop.Trim(new char[] { '\n', '\r' });
            callStackTop = callStackTop.Replace("::", "-").Replace("<", "-").Replace(">", "-").Replace("*", "-");
            callStackTop = callStackTop.Replace("_", "-").Replace("<", "-").Replace(">", "-").Replace("*", "-");
            if ((callStackTop.Contains("Assert") || callStackTop.Contains("VCRUNTIME")) && callstackLines.Count > 1)
            {
                callStackTop = callstackLines[1];
                callStackTop = callStackTop.Split(new string[] { "!" }, StringSplitOptions.RemoveEmptyEntries).Last()
                    .Split(new string[] { "()" }, StringSplitOptions.RemoveEmptyEntries).First();
                callStackTop = callStackTop.Trim(new char[] { '\n', '\r' });
                callStackTop = callStackTop.Replace("::", "-").Replace("<", "-").Replace(">", "-").Replace("*", "-");
                callStackTop = callStackTop.Replace("_", "-").Replace("<", "-").Replace(">", "-").Replace("*", "-");
            }

            if (callStackTop == "")
                callStackTop = "Unknown";
            return callstackLines;
        }

        private void ProcessNewCallstackCdb(ref string newCallstack, ref string callstackTop)
        {
            string tempString = @"  *** Stack trace for last set context - .thread/.cxr resets it";
            tempString = @"Call Site";
            int idx1 = newCallstack.IndexOf(tempString) + 9;
            int idx2 = newCallstack.IndexOf(@"quit:");
            if (idx2 > idx1)
            {
                newCallstack = newCallstack.Substring(idx1, idx2 - idx1);
            }

            string[] lines = newCallstack.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string tempCallstack = "";
            string tempCallstackTop = callstackTop;

            for (int i = 0; i < lines.Length; ++i)
            {
                if (lines[i].Length > 36)
                {
                    lines[i] = lines[i].Substring(36);
                    string callstackFunction = lines[i];
                    tempCallstack += $"{i:D2} - {callstackFunction}\n";

                    //if (stringArray.Any(stringToCheck.Contains))
                    //if (!CallstackFunction.Contains(@"KERNELBASE") && !CallstackFunction.Contains(@"RaiseException") && !CallstackFunction.Contains(@"Logf") && !CallstackFunction.Contains(@"AssertFailed") && !CallstackFunction.Contains(@"NO-FUNCTION") && tempCallstackTop == CallstackTop)
                    if (!CallstackTopExcludedWords.Any(callstackFunction.Contains) && tempCallstackTop == callstackTop)
                    {
                        tempCallstackTop = callstackFunction;
                        string[] tabs = tempCallstackTop.Split('!');
                        if (tabs.Length == 2)
                        {
                            callstackTop = tabs[1].Replace("::", "-").Replace("<", "-").Replace(">", "-")
                                .Replace("*", "-");
                            callstackTop = callstackTop.Contains('+')
                                ? callstackTop.Split('+').First()
                                : callstackTop.Split('[').First();
                            callstackTop = callstackTop.Trim();
                        }
                    }
                }
            }

            newCallstack = tempCallstack.Trim();
        }

        private void CreateCallstackFile(ref string newCallstack, ref string callstackTop, string targetDirectory,
            string orginalFileName)
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
            StatusBar_TextBlock.Text = "Trying to configure revision: " + newRevision +
                                       " Downloading PDB - this may take a while";
            StatusBar_TextBlock.InvalidateVisual();
            StatusBar_TextBlock.UpdateLayout();
            StatusBar_TextBlock.UpdateDefaultStyle();
            await Task.Factory.StartNew((() => { ConfigureHelper.FullConfiguration(newRevision); }));
            StatusBar_TextBlock.Text = "Completed configuring of revision: " + newRevision;
        }

        private void Settings_Button_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow settingsWin = new SettingsWindow
            {
                Owner = this
            };
            settingsWin.ShowDialog();
        }
    }
}
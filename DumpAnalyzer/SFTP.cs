using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace DumpAnalyzer
{
    public class SFTP : IConnection
    {
        public void DownloadFile(System.String fileName, string localFileNameFullPath)
        {
            try
            {
                using (SftpClient sftpClient = new SftpClient(GetConnectionInfo()))
                {
                    sftpClient.Connect();
                    System.IO.Stream sftpStream = new System.IO.MemoryStream();
                    sftpClient.DownloadFile(Path.Combine(Properties.Settings.Default.FTP_RootFolder, fileName).Replace('\\', '/'), sftpStream);
                    using (Stream fileStream = File.Create(localFileNameFullPath))
                    {
                        sftpStream.Seek(0, SeekOrigin.Begin);
                        sftpStream.CopyTo(fileStream);
                        fileStream.Close();
                        sftpStream.Close();
                    }

                    sftpClient.Dispose();
                }
            }
            catch (Exception e)
            {
                Logger.Log("Error on trying to download file from SFTP: " + e.ToString() + " File name: " + fileName);
            }
        }

        public List<string> GetFilesFromServer()
        {
            List<string> filesOnServer = new List<string>();
            try
            {
                using (SftpClient sftpClient = new SftpClient(GetConnectionInfo()))
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    sftpClient.Connect();
                    float connectTime = sw.ElapsedMilliseconds;
                    sw.Restart();
                    IEnumerable<SftpFile> ListOfFiles = sftpClient.ListDirectory(Properties.Settings.Default.FTP_RootFolder);
                    float listDirTime = sw.ElapsedMilliseconds;
                    sw.Restart();
                    foreach (SftpFile File in ListOfFiles)
                    {
                        if (File.Name.Contains(Properties.Settings.Default.Project_CodenameShort) && File.Name.Contains(".7z"))
                        {
                            filesOnServer.Add(File.Name);
                        }
                    }
                    float addingToListTime = sw.ElapsedMilliseconds;
                    sw.Stop();
                    Console.WriteLine($@"connect: {connectTime}  List: {listDirTime}  Add: {addingToListTime}");

                    sftpClient.Dispose();

                    //StatusBar_TextBlock.Text = "Checking FTP finished. Found: " + file_ListBox.Items.Count + " files.";
                }
            }
            catch (Exception e)
            {
                Logger.Log("Error on Check FTP: " + e.ToString());
                //MessageBox.Show("Error on Check FTP: " + e.ToString(), @"Cos nie zabanglało", MessageBoxButton.OK);
            }

            return filesOnServer;
        }

        public List<string> GetAnalyzedDumpsFromServer()
        {
            List<string> filesOnServer = new List<string>();
            try
            {
                using (SftpClient sftpClient = new SftpClient(GetConnectionInfo()))
                {
                    sftpClient.Connect();
                    IEnumerable<SftpFile> ListOfFiles = sftpClient.ListDirectory(Properties.Settings.Default.CheckedDumpsFtpPath);
                    foreach (SftpFile File in ListOfFiles)
                    {
                        if (File.Name.Contains(Properties.Settings.Default.Project_CodenameShort))
                        {
                            filesOnServer.Add(File.Name);
                        }
                    }

                    sftpClient.Dispose();
                    Console.WriteLine("Directory List Complete, status");

                    //StatusBar_TextBlock.Text = "Checking FTP finished. Found: " + file_ListBox.Items.Count + " files.";
                }
            }
            catch (Exception e)
            {
                Logger.Log("Error on Check FTP: " + e.ToString());
                //MessageBox.Show("Error on Check FTP: " + e.ToString(), @"Cos nie zabanglało", MessageBoxButton.OK);
            }

            return filesOnServer;
        }
        private ConnectionInfo GetConnectionInfo()
        {
            KeyboardInteractiveAuthenticationMethod keybAuth =
                new KeyboardInteractiveAuthenticationMethod(Properties.Settings.Default.FTP_User);

            keybAuth.AuthenticationPrompt += HandleKeyEvent;

            ConnectionInfo connectionInfo = new ConnectionInfo(Properties.Settings.Default.FTP_Host,
                (int)Properties.Settings.Default.FTP_Port,
                Properties.Settings.Default.FTP_User,
                keybAuth
            );
            return connectionInfo;
        }

        private void HandleKeyEvent(object sender, AuthenticationPromptEventArgs e)
        {
            foreach (AuthenticationPrompt prompt in e.Prompts)
            {
                prompt.Response = Properties.Settings.Default.FTP_Password;
            }
        }

        public bool RenameFile(string fileName, string targetFileName)
        {
            using (SftpClient sftpClient = new SftpClient(GetConnectionInfo()))
            {
                sftpClient.Connect();
                if (sftpClient.IsConnected)
                {
                    sftpClient.RenameFile(fileName, targetFileName);
                    return true;
                }
            }

            return false;
        }

        public void UploadDirectory(string sourcePath, string targetPath)
        {
            try
            {
                if (Directory.Exists(sourcePath))
                {
                    using (SftpClient sftpClient = new SftpClient(GetConnectionInfo()))
                    {
                        sftpClient.Connect();
                        targetPath = targetPath.Replace('\\', '/');
                        CreateTargetPath(ref targetPath, sftpClient);

                        string[] pdbFiles = Directory.GetFiles(sourcePath, "*.*", SearchOption.TopDirectoryOnly);
                        foreach (string currentFile in pdbFiles)
                        {
                            string targetPathForFile = $@"{targetPath}/{Path.GetFileName(currentFile)}";
                            Logger.Log("Creating FileStream object to stream a file");
                            using (FileStream fs = new FileStream(currentFile, FileMode.Open))
                            {
                                sftpClient.BufferSize = 1024;
                                sftpClient.UploadFile(fs, targetPathForFile);
                                Logger.Log("Upload of: " + currentFile + " finished");
                                fs.Close();
                            }
                        }

                        sftpClient.Disconnect();
                        sftpClient.Dispose();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log("Error on trying to upload directory to SFTP: " + e.ToString() + " Source path: " + sourcePath);
            }
        }

        public void RemoveAfterAnalyze(string fileToRemove)
        {
            try
            {
                using (SftpClient sftpClient = new SftpClient(GetConnectionInfo()))
                {
                    sftpClient.Connect();
                    System.IO.Stream sftpStream = new System.IO.MemoryStream();
                    string ftpFilePath = Path.Combine(Properties.Settings.Default.FTP_RootFolder, fileToRemove).Replace('\\', '/');
                    if (sftpClient.Exists(ftpFilePath))
                    {
                        sftpClient.DeleteFile(ftpFilePath);
                    }

                    sftpClient.Dispose();
                }
            }
            catch (Exception e)
            {
                Logger.Log("Error on trying to delete file from SFTP: " + e.ToString() + " File name: " + fileToRemove);
            }
        }

        private void CreateTargetPath(ref string targetPath, SftpClient sftpClient)
        {
            string[] directories = targetPath.Split('/');
            string currPath = "";

            foreach (string currDir in directories)
            {
                currPath += currDir;
                if (!sftpClient.Exists(currPath))
                {
                    sftpClient.CreateDirectory(currPath);
                }
                currPath += "/";
            }
        }
    }
}

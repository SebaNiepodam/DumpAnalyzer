using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DumpAnalyzer
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();

            FTP_Host_TextBox.Text = Properties.Settings.Default.FTP_Host;
            FTP_Port_TextBox.Text = Properties.Settings.Default.FTP_Port.ToString();
            FTP_Username_TextBox.Text = Properties.Settings.Default.FTP_User;
            FTP_Password_TextBox.Text = Properties.Settings.Default.FTP_Password;
            FTP_RootFolder_TextBox.Text = Properties.Settings.Default.FTP_RootFolder;
            SLACK_Channel_TextBox.Text = Properties.Settings.Default.SLACK_Channel;
            PDB_ZipDirectory_TextBox.Text = Properties.Settings.Default.PDB_Zip_Directory;
            PDB_Directory_TextBox.Text = Properties.Settings.Default.PDB_Directory;
            PDB_DirectDirectory_TextBox.Text = Properties.Settings.Default.PDB_Direct_Directory;
            FTP_CheckedDumpsFolder_TextBox.Text = Properties.Settings.Default.CheckedDumpsFtpPath;
            CheckedDumpsVaultPath_TextBox.Text = Properties.Settings.Default.CheckedDumpsVaultPath;
            CopyToFTP_Checkbox.IsChecked = Properties.Settings.Default.IsCopyToFtpEnabled;
            CopyToVault_Checkbox.IsChecked = Properties.Settings.Default.IsCopyToVault;
            ProjectCodenameShort_TextBox.Text = Properties.Settings.Default.Project_CodenameShort;
            ProjectCodename_TextBox.Text = Properties.Settings.Default.Project_Codename;
            RemoveFromFtp_Checkbox.IsChecked = Properties.Settings.Default.IsRemoveFromFtpAfterAnalyze;
        }

        private void Cancel_Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Save_Button_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.FTP_Host = FTP_Host_TextBox.Text;
            Properties.Settings.Default.FTP_Port = int.Parse(FTP_Port_TextBox.Text);
            Properties.Settings.Default.FTP_User = FTP_Username_TextBox.Text;
            Properties.Settings.Default.FTP_Password = FTP_Password_TextBox.Text;
            Properties.Settings.Default.FTP_RootFolder = FTP_RootFolder_TextBox.Text;
            Properties.Settings.Default.SLACK_Channel = SLACK_Channel_TextBox.Text;
            Properties.Settings.Default.PDB_Zip_Directory = PDB_ZipDirectory_TextBox.Text;
            Properties.Settings.Default.PDB_Directory = PDB_Directory_TextBox.Text;
            Properties.Settings.Default.PDB_Direct_Directory = PDB_DirectDirectory_TextBox.Text;
            Properties.Settings.Default.CheckedDumpsFtpPath = FTP_CheckedDumpsFolder_TextBox.Text;
            Properties.Settings.Default.CheckedDumpsVaultPath = CheckedDumpsVaultPath_TextBox.Text;
            Properties.Settings.Default.IsCopyToFtpEnabled = CopyToFTP_Checkbox.IsChecked.HasValue && CopyToFTP_Checkbox.IsChecked.Value;
            Properties.Settings.Default.IsCopyToVault = CopyToVault_Checkbox.IsChecked.HasValue && CopyToVault_Checkbox.IsChecked.Value;
            Properties.Settings.Default.Project_CodenameShort = ProjectCodenameShort_TextBox.Text;
            Properties.Settings.Default.Project_Codename = ProjectCodename_TextBox.Text;
            Properties.Settings.Default.IsRemoveFromFtpAfterAnalyze = RemoveFromFtp_Checkbox.IsChecked.HasValue && RemoveFromFtp_Checkbox.IsChecked.Value;
            Properties.Settings.Default.Save();
            Close();
        }
    }
}

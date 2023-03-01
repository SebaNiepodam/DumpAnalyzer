using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DumpAnalyzer
{
    internal interface IConnection
    {
        List<string> GetFilesFromServer();
        List<string> GetAnalyzedDumpsFromServer();
        void DownloadFile(string fileName, string localFileNameFullPath);
        bool RenameFile(string fileName, string targetFileName);
        void UploadDirectory(string sourcePath, string targetPath);
        void RemoveAfterAnalyze(string fileToRemove);
    }
}

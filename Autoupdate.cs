using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Deployment.Application;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Xplorer
{
    public partial class Autoupdate : Form
    {
        public class ReleaseInfo
        {
            public string Name { get; set; }
            public string DownloadUrl { get; set; }
            public string Body { get; set; }
            public override string ToString()
            {
                return $"Name: {Name}\nDownload URL: {DownloadUrl}\nBody: {Body}";
            }
            public bool CheckIfNewVersion(string oldVersion)
            {
                try
                {
                    // Extract numeric part from member variable
                    string[] parts = Name.Split(' ');
                    if (parts.Length < 2) return false; // Invalid format

                    string newVersion = parts[1]; // Extract version part

                    // Compare versions
                    return CompareVersions(oldVersion, newVersion) < 0;
                }
                catch (Exception ex)
                {
                    throw new Exception("Couldn't compare versions: " + ex.Message);
                }
            }

            private int CompareVersions(string v1, string v2)
            {
                Version version1 = new Version(v1);
                Version version2 = new Version(v2);
                return version1.CompareTo(version2);
            }
        }

        public bool UpdateAvailable { get; set; } = false;
        public ReleaseInfo releaseInfo { get; set; }

        public Autoupdate()
        {
            InitializeComponent();
        }

        private void buttonInstall_Click(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.AppStarting;
            try
            {
                if (DownloadNewVersion())
                {
                    UpdateBinary();
                }
            }
            catch (Exception ex)
            {
                string errorMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                MessageBox.Show("Could not fetch new version: " + errorMessage, "Xplore", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
        }

        private bool DownloadNewVersion()
        {
            try
            {
                string appPath = Application.StartupPath;
                string newVersionPath = Path.Combine(appPath, "newversion.dat");

                // Delete newversion.dat if it exists
                if (File.Exists(newVersionPath))
                {
                    File.Delete(newVersionPath);
                }



                using (HttpClient client = new HttpClient())
                {
                    var response = client.GetAsync(releaseInfo.DownloadUrl).Result;
                    response.EnsureSuccessStatusCode();

                    var data = response.Content.ReadAsByteArrayAsync().Result;
                    string filePath = Path.Combine(Application.StartupPath, "newversion.dat");
                    File.WriteAllBytes(filePath, data);
                }
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception("Error downloading new version: " + ex.Message);
            }
        }

        public void UpdateBinary()
        {
            string appPath = Application.StartupPath;
            string batchFilePath = Path.Combine(appPath, "update.bat");
            string logFile = Path.Combine(appPath, "update_log.txt");
            string procName = Process.GetCurrentProcess().ProcessName + ".exe";

            int pid = Process.GetCurrentProcess().Id;
            string batchContent = "" +
                "@echo off\n" +
                "set \"PROCNAME=" + procName + "\"\n" +
                "set \"LOGFILE=" + logFile + "\"\n" +
                "\n" +
                "echo [%date% %time%] Update script started. Monitoring process: %PROCNAME% >> %LOGFILE%\n" +
                "\n" +
                ":CHECKPROCESS\n" +
                "tasklist /fi \"imagename eq %PROCNAME%\" | findstr /i \"%PROCNAME%\" >nul\n" +
                "if not errorlevel 1 (\n" +
                "    echo [%date% %time%] %PROCNAME% is still running. Waiting... >> %LOGFILE%\n" +
                "    timeout /t 4 /nobreak >nul\n" +
                "    goto CHECKPROCESS\n" +
                ")\n" +
                "\n" +
                "echo [%date% %time%] %PROCNAME% has stopped. Proceeding with update check... >> %LOGFILE%\n" +
                "timeout /t 4 /nobreak >nul\n" +
                "\n" +
                "if exist newversion.dat (\n" +
                "    echo [%date% %time%] New version download found. Replacing %PROCNAME%... >> %LOGFILE%\n" +
                "    del /f /q %PROCNAME% 2>> %LOGFILE%\n" +
                "    if exist %PROCNAME% (\n" +
                "        echo [%date% %time%] ERROR: Failed to delete %PROCNAME% >> %LOGFILE%\n" +
                "        exit /b 1\n" +
                "    )\n" +
                "    rename newversion.dat %PROCNAME% 2>> %LOGFILE%\n" +
                "    if not exist %PROCNAME% (\n" +
                "        echo [%date% %time%] ERROR: Failed to rename newversion.dat to %PROCNAME% >> %LOGFILE%\n" +
                "        exit /b 1\n" +
                "    )\n" +
                "    echo [%date% %time%] Update successful. Starting new %PROCNAME%. >> %LOGFILE%\n" +
                "    start \"\" %PROCNAME% update-success\n" +
                ") else (\n" +
                "    echo [%date% %time%] No new version found. Starting existing %PROCNAME%. >> %LOGFILE%\n" +
                "    start \"\" %PROCNAME%\n" +
                ")\n" +
                "\n" +
                "echo [%date% %time%] Update completed. >> %LOGFILE%\n" +
                "del \"%~f0\"\n";

            File.WriteAllText(batchFilePath, batchContent);

            Process.Start(new ProcessStartInfo
            {
                FileName = batchFilePath,
                Arguments = procName,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            this.Close();
            System.Threading.Thread.Sleep(1000);
            Environment.Exit(0);
        }

        private void buttonClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }
        /// <summary>
        /// TO be called before showdialog
        /// </summary>
        public void CheckForUpdate()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("request");
                    string url = "https://api.github.com/repos/johndoe422/Xplorer/releases/latest";
                    string jsonData = client.GetStringAsync(url).Result;
                    releaseInfo = ParseReleaseInfo(jsonData);
                }

                this.UpdateAvailable = releaseInfo.CheckIfNewVersion(Application.ProductVersion);
            }
            catch (Exception ex)
            {
                string errorMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                MessageBox.Show("Could not check for updates: " + errorMessage, "Xplore", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public static ReleaseInfo ParseReleaseInfo(string jsonData)
        {
            try
            {
                var parser = new JsonParser(jsonData);
                var jsonObject = parser.Parse() as Dictionary<string, object>;

                string name = jsonObject.ContainsKey("name") ? jsonObject["name"].ToString() : string.Empty;
                string body = jsonObject.ContainsKey("body") ? jsonObject["body"].ToString() : string.Empty;
                string downloadUrl = string.Empty;

                if (jsonObject.ContainsKey("assets") && jsonObject["assets"] is List<object> assets && assets.Count > 0)
                {
                    var firstAsset = assets[0] as Dictionary<string, object>;
                    if (firstAsset != null && firstAsset.ContainsKey("browser_download_url"))
                    {
                        downloadUrl = firstAsset["browser_download_url"].ToString();
                    }
                }

                return new ReleaseInfo { Name = name, DownloadUrl = downloadUrl, Body = body };
            }
            catch (Exception ex)
            {
                throw new Exception("Error parsing release info: " + ex.Message);
            }
        }

        private void Autoupdate_Load(object sender, EventArgs e)
        {
            this.labelVersion.Text = releaseInfo.Name;
            this.labelCurrVer.Text = Application.ProductVersion;
            string fixedText = this.releaseInfo.Body.Replace("\\r\\n", Environment.NewLine);
            this.textBoxChanges.Text = fixedText + "\r\n\r\nManual update link:\r\n" + this.releaseInfo.DownloadUrl;

        }
    }
}

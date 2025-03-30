using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Deployment.Application;
using System.Drawing;
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
                // Extract numeric part from member variable
                string[] parts = Name.Split(' ');
                if (parts.Length < 2) return false; // Invalid format

                string newVersion = parts[1]; // Extract version part

                // Compare versions
                return CompareVersions(oldVersion, newVersion) < 0;
            }

            private int CompareVersions(string v1, string v2)
            {
                Version version1 = new Version(v1);
                Version version2 = new Version(v2);
                return version1.CompareTo(version2);
            }
        }

        public bool UpdateAvailable { get; set; } = false;
        public ReleaseInfo releaseInfo  { get; set; }

        public Autoupdate()
        {
            InitializeComponent();
        }

        private void buttonInstall_Click(object sender, EventArgs e)
        {

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
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("request");
                string url = "https://api.github.com/repos/johndoe422/Xplorer/releases/latest";
                string jsonData = client.GetStringAsync(url).Result;
                releaseInfo = ParseReleaseInfo(jsonData);
            }

            this.UpdateAvailable = releaseInfo.CheckIfNewVersion(Application.ProductVersion);
            System.Threading.Thread.Sleep(2000);
        }

        public static ReleaseInfo ParseReleaseInfo(string jsonData)
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

       
    }
}

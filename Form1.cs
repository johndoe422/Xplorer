using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Configuration;
using Microsoft.Win32;



namespace Xplorer
{
    public partial class Form1: Form
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_SMALLICON = 0x000000001;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        // Add this P/Invoke declaration to your form class
        [DllImport("user32.dll")]
        private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

        // Add this P/Invoke declaration
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr ShellExecute(IntPtr hwnd, string lpOperation, string lpFile, string lpParameters, string lpDirectory, ShowCommands nShowCmd);

        private enum ShowCommands : int
        {
            SW_HIDE = 0,
            SW_SHOWNORMAL = 1,
            SW_SHOWMINIMIZED = 2,
            SW_SHOWMAXIMIZED = 3,
            SW_SHOWNOACTIVATE = 4,
            SW_SHOW = 5,
            SW_MINIMIZE = 6,
            SW_SHOWMINNOACTIVE = 7,
            SW_SHOWNA = 8,
            SW_RESTORE = 9,
            SW_SHOWDEFAULT = 10,
            SW_FORCEMINIMIZE = 11
        }

        private bool isCloseEventCancelled = true;
        private int menuOpenCount = 0;
        Icon genericFolderIcon = null;
        private GlobalHotkey globalHotkey;

        // Timer to track idle time and do some memory cleanup
        private Timer idleTimer;

        // Default app settings (config file will override)
        private int maxFolderEntries = 150;
        private bool dontShowAgain = false;
        private bool loadStartmenu = true;

        private enum FolderType
        {
            RegularFolder,
            SpecialFolder
        };

        public enum ExitMenuMode
        {
            Exit,
            Cancel
        };

        private ExitMenuMode exitMode;
        public ExitMenuMode ExitMode
        {
            get
            {
                return exitMode;
            }
            set
            {
                exitMode = value;
                this.exitToolStripMenuItem.Text = exitMode == ExitMenuMode.Cancel ? "&Cancel" : "E&xit";
            }
        }
        

        public Form1()
        {
            InitializeComponent();

            // Initialize Timer 
            idleTimer = new Timer();
            idleTimer.Interval = 4 * 60 * 1000; // 4 minutes in milliseconds
            idleTimer.Tick += IdleTimer_Tick;

            // Load settings
            ReadConfig();

            // Initialize global hotkey
            globalHotkey = new GlobalHotkey(notifyIcon, this);
            // Register Win+Shift+A hotkey
            globalHotkey.RegisterGlobalHotkey(
                GlobalHotkey.MOD_WIN | GlobalHotkey.MOD_SHIFT,
                (uint)Keys.A
            );

            genericFolderIcon = GetFolderIcon();
            PopulateDriveMenuItems();
            PopulateProfileFolders();
        }

        private void IdleTimer_Tick(object sender, EventArgs e)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            this.idleTimer.Stop();
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            ShowStartupBalloonTip();

            // Call AddtoStartup after 4 seconds
            await Task.Delay(3000);
            AddtoStartup();
        }

        private void AddtoStartup()
        {
            if (dontShowAgain)
                return;

            try
            {
                RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (registryKey.GetValue("Xplore") != null)
                {
                    // Already added to startup, but check if exe path in registryKey is correct
                    string registryPath = registryKey.GetValue("Xplore").ToString();
                    string currentExePath = Application.ExecutablePath;

                    // Check if the path in the registry matches the current executable path
                    if (string.Equals(registryPath, currentExePath, StringComparison.OrdinalIgnoreCase))
                    {
                        // Paths match, no need to update or show the dialog
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to check registry: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            AddtoStartup addtoStartup = new AddtoStartup();
            addtoStartup.ShowDialog();

            if (addtoStartup.DoNotShowAgain)
            {
                dontShowAgain = true;
                // set the value in the app config 
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                config.AppSettings.Settings["DontShowAgain"].Value = "true";
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
            }

            if (addtoStartup.AddToStartup)
            {
                try
                {
                    string exePath = Application.ExecutablePath;
                    RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                    registryKey.SetValue("Xplore", exePath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to add to startup: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // Override WndProc to handle global hotkey messages
        protected override void WndProc(ref Message m)
        {
            if (!globalHotkey.ProcessHotkey(ref m))
            {
                base.WndProc(ref m);
            }
        }

        private void ReadConfig()
        {
            CreateDefaultConfigFile();
            try
            {
                if (ConfigurationManager.AppSettings["MaxFolderEntries"] != null)
                    int.TryParse(ConfigurationManager.AppSettings["MaxFolderEntries"], out maxFolderEntries);
                maxFolderEntries = maxFolderEntries > 300 ? 300 : maxFolderEntries;

                if (ConfigurationManager.AppSettings["DontShowAgain"] != null)
                    bool.TryParse(ConfigurationManager.AppSettings["DontShowAgain"], out dontShowAgain);

                if (ConfigurationManager.AppSettings["LoadStartmenu"] != null)
                    bool.TryParse(ConfigurationManager.AppSettings["LoadStartmenu"], out loadStartmenu);
            }
            catch { }
        }

        private void CreateDefaultConfigFile()
        {
            string configPath = Path.Combine(
                Path.GetDirectoryName(Application.ExecutablePath),
                Application.ProductName + ".exe.config"
            );

            if (!File.Exists(configPath))
            {
                File.WriteAllText(configPath, Properties.Resources.DefaultConfigTemplate);
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (ExitMode == ExitMenuMode.Cancel)
            {
                this.contextMenuStripMain.Close();
                return;
            }

            this.isCloseEventCancelled = false;
            this.Close();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = this.isCloseEventCancelled;

            try
            {
                if (!this.isCloseEventCancelled)
                {
                    globalHotkey.UnregisterGlobalHotkey();
                }
            }
            catch (Exception) { }
            
            this.Hide();
        }

        private void notifyIcon_DoubleClick(object sender, EventArgs e)
        {
            this.Show();
        }

        private Icon GetDriveIcon(DriveInfo drive)
        {
            // Use Windows shell to extract the drive icon
            SHFILEINFO shfi = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_SMALLICON;

            IntPtr imageHandle = SHGetFileInfo(drive.Name,
                FILE_ATTRIBUTE_NORMAL,
                ref shfi,
                (uint)Marshal.SizeOf(shfi),
                flags);

            if (shfi.hIcon != IntPtr.Zero)
            {
                Icon driveIcon = (Icon)Icon.FromHandle(shfi.hIcon).Clone();
                DestroyIcon(shfi.hIcon);
                return driveIcon;
            }

            // Fallback to a custom default drive icon method
            return GetDefaultDriveIcon();
        }

        private Icon GetDefaultDriveIcon()
        {
            // Use the system's icon for drives by extracting it from shell32.dll
            IntPtr hIcon = IntPtr.Zero;
            try
            {
                // Extract a standard drive icon from shell32.dll
                // 3 is the index for a typical drive icon in shell32.dll
                hIcon = ExtractIcon(IntPtr.Zero, "shell32.dll", 3);

                if (hIcon != IntPtr.Zero)
                {
                    return Icon.FromHandle(hIcon);
                }
            }
            catch
            {
                // If extraction fails, create a simple placeholder icon
                Bitmap bmp = new Bitmap(16, 16);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.LightGray);
                    g.DrawRectangle(Pens.Gray, 0, 0, 15, 15);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }

            // Last resort fallback
            return new Icon(SystemIcons.Exclamation, 16, 16);
        }


        private ToolStripMenuItem CreateDriveMenuItem(DriveInfo drive)
        {
            // Get the volume label, defaulting to the drive letter if no label exists
            string volumeLabel = string.IsNullOrWhiteSpace(drive.VolumeLabel)
                ? $"Local Disk {drive.Name.TrimEnd('\\')}"
                : drive.VolumeLabel;

            // Create a new menu item with volume label and drive letter
            ToolStripMenuItem driveMenuItem = new ToolStripMenuItem
            {
                Text = $"{volumeLabel} {drive.Name.TrimEnd('\\')}"
            };

            // Set the icon for the menu item
            try
            {
                Icon driveIcon = GetDriveIcon(drive);
                driveMenuItem.Image = driveIcon.ToBitmap();
            }
            catch
            {
                // Fallback to a default icon if needed
                driveMenuItem.Image =  new Icon(SystemIcons.Exclamation, 16, 16).ToBitmap();
            }

            // Add click event to open the drive in File Explorer
            driveMenuItem.MouseDown += (s, e) =>
            {
                if (e.Clicks == 2 && e.Button == MouseButtons.Left)
                {
                    try
                    {
                        ShellExecute(IntPtr.Zero, "open", drive.Name, null, null, ShowCommands.SW_SHOWNORMAL);
                        contextMenuStripMain.Close();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Could not open drive {drive.Name}: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            };

            // Add mouse hover event to dynamically load submenus
            driveMenuItem.MouseHover += (s, e) => PopulateDriveSubmenu(driveMenuItem, drive.Name);

            // Don't have to lazy load initially
            PopulateDriveSubmenu(driveMenuItem, drive.Name);

            return driveMenuItem;
        }

        private void PopulateDriveMenuItems()
        {
            // Find the index of thisPCToolStripMenuItem to insert drives after it
            int insertIndex = contextMenuStripMain.Items.IndexOf(exitToolStripMenuItem);

            try
            {
                // Get all logical drives in the system
                DriveInfo[] drives = DriveInfo.GetDrives()
                    // Optional: Filter to only show Fixed drives if you want
                    .Where(d => d.DriveType == DriveType.Fixed)
                    .ToArray();

                foreach (DriveInfo drive in drives)
                {
                    // Use the modular method to create drive menu item
                    ToolStripMenuItem driveMenuItem = CreateDriveMenuItem(drive);

                    // Insert the drive menu item directly into the context menu strip
                    contextMenuStripMain.Items.Insert(insertIndex, driveMenuItem);
                    insertIndex++; // Increment to maintain order
                }

                // Optional: Add a separator after drive items if needed
                contextMenuStripMain.Items.Insert(insertIndex, new ToolStripSeparator());
            }
            catch (Exception ex)
            {
                // Create an error menu item if drive enumeration fails
                ToolStripMenuItem errorItem = new ToolStripMenuItem($"Error loading drives: {ex.Message}");
                errorItem.Enabled = false;
                contextMenuStripMain.Items.Insert(insertIndex, errorItem);
            }
        }

        private void PopulateProfileFolders()
        {
            //Populate downloads, desktop, pictures, documents, music, videos folders in contextMenuStripMain for easy access
            string[] profileFolders = new string[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
            };
            contextMenuStripMain.Items.Insert(0, new ToolStripSeparator());
            foreach (string folder in profileFolders)
            {
                try
                {
                    ToolStripMenuItem folderMenuItem = CreateFolderMenuItem(folder, FolderType.SpecialFolder);
                    contextMenuStripMain.Items.Insert(0, folderMenuItem);
                }
                catch (Exception ex)
                {
                    ToolStripMenuItem errorItem = new ToolStripMenuItem($"Error loading folder: {ex.Message}");
                    errorItem.Enabled = false;
                    contextMenuStripMain.Items.Add(errorItem);
                }
            }
        }

        private void PopulateDriveSubmenu(ToolStripMenuItem parentMenuItem, string path)
        {
            // Clear existing submenus to prevent duplicate loading
            parentMenuItem.DropDownItems.Clear();

            try
            {
                // Load folders first
                string[] directories = Directory.GetDirectories(path);
                string[] files = Directory.GetFiles(path);

                // Limit the number of items to show in the menu 
                // if number of folders and files together exceed maxFolderEntries,
                // then only load up to maxFolderEntries, give preference to folders
                int totalItems = directories.Length + files.Length;
                bool isLimited = totalItems > maxFolderEntries;
                if (isLimited)
                {
                    int foldersToShow = Math.Min(directories.Length, maxFolderEntries);
                    int filesToShow = Math.Min(files.Length, maxFolderEntries - foldersToShow);

                    directories = directories.Take(foldersToShow).ToArray();
                    files = files.Take(filesToShow).ToArray();
                }
               
                foreach (string dir in directories.OrderBy(d => Path.GetFileName(d)))
                {
                    ToolStripMenuItem folderMenuItem = CreateFolderMenuItem(dir);
                    parentMenuItem.DropDownItems.Add(folderMenuItem);
                }

                foreach (string file in files.OrderBy(f => Path.GetFileName(f)))
                {
                    ToolStripMenuItem fileMenuItem = CreateFileMenuItem(file);
                    parentMenuItem.DropDownItems.Add(fileMenuItem);
                }
                
                // If the entries were limited due to setting,
                // add a last item that reads "More..." to indicate that there are more items
                if (isLimited)
                {
                    ToolStripMenuItem moreMenuItem = new ToolStripMenuItem("More...")
                    {
                        Enabled = false,
                        ForeColor = Color.Gray
                    };
                    parentMenuItem.DropDownItems.Add(moreMenuItem);
                }

                // If no items found, add a "No items" placeholder
                if (parentMenuItem.DropDownItems.Count == 0)
                {
                    ToolStripMenuItem noItemsMenuItem = new ToolStripMenuItem("(Empty)")
                    {
                        Enabled = false
                    };
                    parentMenuItem.DropDownItems.Add(noItemsMenuItem);
                }
            }
            catch (UnauthorizedAccessException)
            {
                ToolStripMenuItem accessDeniedItem = new ToolStripMenuItem("Access Denied")
                {
                    Enabled = false,
                    ForeColor = Color.Red
                };
                parentMenuItem.DropDownItems.Add(accessDeniedItem);
            }
            catch (Exception ex)
            {
                ToolStripMenuItem errorItem = new ToolStripMenuItem($"Error: {ex.Message}")
                {
                    Enabled = false,
                    ForeColor = Color.Red
                };
                parentMenuItem.DropDownItems.Add(errorItem);
            }
        }

        private ToolStripMenuItem CreateFolderMenuItem(string folderPath, FolderType type = FolderType.RegularFolder)
        {
            string folderName = Path.GetFileName(folderPath);
            bool isFolderEmpty = false;

            Icon folderIcon = type == FolderType.SpecialFolder ? FolderIconRetriever.GetFolderIcon(folderPath) : genericFolderIcon;

            ToolStripMenuItem folderMenuItem = new ToolStripMenuItem(folderName)
            {
                Image = folderIcon.ToBitmap(),
                Tag = 0
            };

            try
            {
                string[] directories = Directory.GetDirectories(folderPath);
                string[] files = Directory.GetFiles(folderPath);

                if (directories.Length == 0 && files.Length == 0)
                {
                    folderMenuItem.ForeColor = Color.Gray;
                    isFolderEmpty = true;
                    ToolStripMenuItem dummyItem = new ToolStripMenuItem("(Empty)")
                    {
                        Enabled = false
                    };
                    folderMenuItem.DropDownItems.Add(dummyItem);
                }
                else
                {
                    ToolStripMenuItem dummyItem = new ToolStripMenuItem("Loading...")
                    {
                        Enabled = false
                    };
                    folderMenuItem.DropDownItems.Add(dummyItem);
                }

                // Optional: Check actual folder contents when needed
                if (!isFolderEmpty)
                {
                    folderMenuItem.DropDownOpening += (s, e) =>
                    {
                        if (Convert.ToInt32(folderMenuItem.Tag) == this.menuOpenCount)
                        {
                            // Performance improvement by skipping re-loading contents of the folder again in the same menu opening session
                            return;
                        }

                        folderMenuItem.Tag = this.menuOpenCount;

                        // Clear existing dummy/previous items
                        folderMenuItem.DropDownItems.Clear();
                        try
                        {
                            if (directories.Length != 0 || files.Length != 0)
                            {
                                PopulateDriveSubmenu(folderMenuItem, folderPath);
                            }
                        }
                        catch (UnauthorizedAccessException)
                        {
                            ToolStripMenuItem accessDeniedItem = new ToolStripMenuItem("Access Denied")
                            {
                                Enabled = false,
                                ForeColor = Color.Red
                            };
                            folderMenuItem.DropDownItems.Add(accessDeniedItem);
                        }
                        catch (Exception ex)
                        {
                            ToolStripMenuItem errorItem = new ToolStripMenuItem($"Error: {ex.Message}")
                            {
                                Enabled = false,
                                ForeColor = Color.Red
                            };
                            folderMenuItem.DropDownItems.Add(errorItem);
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                // Handle any initial folder access errors
                ToolStripMenuItem errorItem = new ToolStripMenuItem($"Error: {ex.Message}")
                {
                    Enabled = false,
                    ForeColor = Color.Red
                };
                folderMenuItem.DropDownItems.Add(errorItem);
            }

            // Click event to open folder
            folderMenuItem.MouseDown += (s, e) =>
            {
                if (e.Clicks == 2 && e.Button == MouseButtons.Left)
                {
                    try
                    {
                        ShellExecute(IntPtr.Zero, "open", folderPath, null, null, ShowCommands.SW_SHOWNORMAL);
                        contextMenuStripMain.Close();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Could not open folder: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            };

            return folderMenuItem;
        }

        private ToolStripMenuItem CreateFileMenuItem(string filePath)
        {
            string fileName = Path.GetFileName(filePath);

            ToolStripMenuItem fileMenuItem = new ToolStripMenuItem(fileName)
            {
                Image = GetFileIcon(filePath).ToBitmap()
            };

            fileMenuItem.Click += (s, e) =>
            {

                try
                {
                    System.Diagnostics.Process.Start(filePath);
                    contextMenuStripMain.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not open file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
         
            return fileMenuItem;
        }

        private Icon GetFolderIcon()
        {
            try
            {
                SHFILEINFO shfi = new SHFILEINFO();
                uint flags = SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES;

                IntPtr hIcon = SHGetFileInfo("folder",
                    FILE_ATTRIBUTE_DIRECTORY,
                    ref shfi,
                    (uint)Marshal.SizeOf(shfi),
                    flags);

                if (shfi.hIcon != IntPtr.Zero)
                {
                    Icon folderIcon = (Icon)Icon.FromHandle(shfi.hIcon).Clone();
                    DestroyIcon(shfi.hIcon);
                    return folderIcon;
                }
            }
            catch
            {
                // Fallback methods
                try
                {
                    // Try extracting from shell32.dll
                    IntPtr hIcon = ExtractIcon(IntPtr.Zero, "shell32.dll", 3);
                    if (hIcon != IntPtr.Zero)
                    {
                        return (Icon)Icon.FromHandle(hIcon).Clone();
                    }
                }
                catch { }
            }

            // Last resort
            return SystemIcons.Application;
        }

        private Icon GetFileIcon(string filePath)
        {
            try
            {
                SHFILEINFO shfi = new SHFILEINFO();
                uint flags = SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES;

                IntPtr hIcon = SHGetFileInfo(filePath,
                    FILE_ATTRIBUTE_NORMAL,
                    ref shfi,
                    (uint)Marshal.SizeOf(shfi),
                    flags);

                if (shfi.hIcon != IntPtr.Zero)
                {
                    // Clone the icon to prevent referncing to unmanaged heap after it gets deallocated by DestroyIcon
                    Icon fileIcon = (Icon)Icon.FromHandle(shfi.hIcon).Clone();
                    // Destroy the original icon handle, fix for GDI handle leaks.
                    DestroyIcon(shfi.hIcon);
                    return fileIcon;
                }
            }
            catch { }

            // Fallback icon, but clone it to prevent GDI handles leak anyhow
            return (Icon)SystemIcons.Application.Clone();
        }

        private void notifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            // Bring back exit option on the menu if its cancel (from a hotkey trigger)
            this.ExitMode = ExitMenuMode.Exit;

            if (e.Button == MouseButtons.Left)
            {
                // Instead of initiating the menu on a temp form, this is consistent approach, simulate right click.
                MouseClicker.SimulateRightClick(Cursor.Position.X, Cursor.Position.Y);
            }
        }

        private void ShowStartupBalloonTip()
        {
            notifyIcon.BalloonTipTitle = "Xplore: Quick File System Access";
            notifyIcon.BalloonTipText = "Navigate your file system with ease: " +
                "Left or right click to open file system menu, " +
                "double-click to open folders, and single-click to open files.";
            notifyIcon.BalloonTipIcon = ToolTipIcon.Info;

            // Show the balloon tip for 6 seconds
            notifyIcon.ShowBalloonTip(6000);
        }


        private void contextMenuStripMain_Opened(object sender, EventArgs e)
        {
            this.menuOpenCount++;

            if (this.menuOpenCount % 5 == 0)
            {
                this.idleTimer.Stop();
                this.idleTimer.Start();
            }
            else if (this.idleTimer.Enabled)
            {
                this.idleTimer.Stop();
                this.idleTimer.Start();
            }
        }

    }
}
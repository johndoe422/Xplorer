﻿using System;
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
using System.Reflection;
using System.Threading;
using System.Management;

namespace Xplorer
{
    public partial class Form1 : Form
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
        private System.Windows.Forms.Timer idleTimer;

        // Default app settings (config file will override)
        private int maxFolderEntries = 150;
        private bool dontShowAgain = false;
        private bool loadStartmenu = true;

        // To watch for removable drive insertions and removals
        private ManagementEventWatcher insertWatcher;
        private ManagementEventWatcher removeWatcher;

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

            // Initialize tooltip
            fileInfoTooltip = new ToolTip
            {
                UseAnimation = true,
                UseFading = true,
                AutoPopDelay = 5000,
                InitialDelay = 500,
                ReshowDelay = 100,
                ShowAlways = true
            };

            StartDiskChangeWatchers();

            // Initialize Timer 
            idleTimer = new System.Windows.Forms.Timer();
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
            ReloadBaseMenuItems();
        }

        private void ReloadBaseMenuItems()
        {
            // Clear all items except the exit menu item  
            contextMenuStripMain.Items.Clear();
            contextMenuStripMain.Items.Add(exitToolStripMenuItem);

            // Repopulate the menu items  
            PopulateDriveMenuItems();
            PopulateProfileFolders();
        }

        private void StartDiskChangeWatchers()
        {
            try
            {
                // Watch for removable disk insertion
                insertWatcher = new ManagementEventWatcher(
                    new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 2"));
                insertWatcher.EventArrived += OnDiskInserted;
                insertWatcher.Start();

                // Watch for removable disk removal
                removeWatcher = new ManagementEventWatcher(
                    new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 3"));
                removeWatcher.EventArrived += OnDiskRemoved;
                removeWatcher.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting disk change watchers: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void CloseMenuOnDiskChange()
        {
            // Close the menu if its open
            if (contextMenuStripMain.Visible)
            {
                contextMenuStripMain.Close();
            }
        }

        // Reload the menu items because there may be new removable drives
        private void contextMenuStripMain_Opening(object sender, CancelEventArgs e)
        {
            ReloadBaseMenuItems();
        }

        private void OnDiskInserted(object sender, EventArrivedEventArgs e)
        {
            // Give time for the system to recognize the disk, even 
            Thread.Sleep(1500);
            if (this.InvokeRequired)
            {
                // Ensure the method is executed on the UI thread
                this.BeginInvoke((MethodInvoker)(() => CloseMenuOnDiskChange()));
            }
            else
            {
                // Directly call the method if already on the UI thread
                CloseMenuOnDiskChange();
            }
        }

        private void OnDiskRemoved(object sender, EventArrivedEventArgs e)
        {
            // Give time for the system to unmount
            Thread.Sleep(500);
            if (this.InvokeRequired)
            {
                // Ensure the method is executed on the UI thread
                this.BeginInvoke((MethodInvoker)(() => CloseMenuOnDiskChange()));
            }
            else
            {
                // Directly call the method if already on the UI thread
                CloseMenuOnDiskChange();
            }
        }

        private void IdleTimer_Tick(object sender, EventArgs e)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            this.idleTimer.Stop();
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            notifyIcon.Text += " " + Assembly.GetExecutingAssembly().GetName().Version.ToString();

            ShowStartupBalloonTip();

            await Task.Delay(5000);
            this.Close();

            // Call AddtoStartup after 4 seconds
            await Task.Delay(2000);
            AddtoStartup();

            // Start the update check without blocking UI
            _ = ShowFormIfUpdateAvailableAsync();
        }

        async Task ShowFormIfUpdateAvailableAsync()
        {
            Autoupdate updFrm = new Autoupdate();
            // Run CheckForUpdate() in a background thread
            await Task.Run(() => updFrm.CheckForUpdate());

            if (updFrm.UpdateAvailable)
            {
                updFrm.Show();
                updFrm.BringToFront();
                updFrm.TopMost = false;
            }
        }

        private void AddtoStartup()
        {
            // Remove the registry entry if it exists, since we don't use it anymore
            string keyName = @"Software\Microsoft\Windows\CurrentVersion\Run";
            string valueName = "Xplore";

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(keyName, true))
            {
                if (key != null && key.GetValue(valueName) != null)
                {
                    this.dontShowAgain = false;
                    key.DeleteValue(valueName);
                }
            }

            if (dontShowAgain)
                return;

            try
            {
                // Check if the app is in startup
                if (StartupShortcutCreator.ShortcutExists())
                {
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

                // Add to startup if user chose to do it
                if (addtoStartup.AddToStartup)
                {
                    StartupShortcutCreator.CreateStartupShortcut();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error adding to startup: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Information);

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
                File.WriteAllText(configPath, Xplore.Properties.Resources.DefaultConfigTemplate);
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

                    // Stop the watchers when the form is closing
                    insertWatcher?.Stop();
                    insertWatcher?.Dispose();
                    removeWatcher?.Stop();
                    removeWatcher?.Dispose();
                }
            }
            catch (Exception) { }

            this.Hide();
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
            // Get the volume label based on the drive type
            string volumeLabel = drive.DriveType == DriveType.Removable
                 ? $"{drive.VolumeLabel} (Removable Disk)" : drive.VolumeLabel;

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
                driveMenuItem.Image = new Icon(SystemIcons.Exclamation, 16, 16).ToBitmap();
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

            ToolStripMenuItem dummyItem = new ToolStripMenuItem("Loading...")
            {
                Enabled = false
            };
            driveMenuItem.DropDownItems.Add(dummyItem);

            // Add mouse hover event to dynamically load submenus
            driveMenuItem.DropDownOpening += (s, e) => PopulateDriveSubmenu(driveMenuItem, drive.Name);

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
                    .Where(d => d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Removable)
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
                Image = GetFileIcon(filePath).ToBitmap(),
                Tag = filePath // Store the full path for tooltip use
            };

            fileMenuItem.MouseHover += ShowFileTooltip;
            fileMenuItem.MouseLeave += HideFileTooltip;

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
                    // Clone the icon to prevent refernencing to unmanaged heap after it gets deallocated by DestroyIcon
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
            notifyIcon.BalloonTipText = "Navigate your file system with ease. " +
                "Use hotekey Win+Shift+A or click on Xplore icon in the notification area.";
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

        private void contextMenuStripMain_Closed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            GlobalHotkey.CloseTempForm();
        }

        private ToolTip fileInfoTooltip;
        private Point lastMousePosition;

        private string GetFileMetadataText(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                string fileType = GetFileType(filePath);
                var sb = new StringBuilder();

                sb.AppendLine($"Name: {fileInfo.Name}");
                sb.AppendLine($"Type: {fileType}");
                sb.AppendLine($"Size: {FormatFileSize(fileInfo.Length)}");
                sb.AppendLine($"Created: {fileInfo.CreationTime:g}");
                sb.AppendLine($"Modified: {fileInfo.LastWriteTime:g}");

                return sb.ToString().TrimEnd();
            }
            catch (Exception ex)
            {
                return $"Error getting file info: {ex.Message}";
            }
        }

        private string GetFileType(string filePath)
        {
            try
            {
                SHFILEINFO shfi = new SHFILEINFO();
                uint flags = SHGFI_USEFILEATTRIBUTES;

                IntPtr result = SHGetFileInfo(filePath,
                    FILE_ATTRIBUTE_NORMAL,
                    ref shfi,
                    (uint)Marshal.SizeOf(shfi),
                    flags);

                return !string.IsNullOrEmpty(shfi.szTypeName) ? shfi.szTypeName : Path.GetExtension(filePath);
            }
            catch
            {
                return Path.GetExtension(filePath);
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;

            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }

            return $"{number:n1} {suffixes[counter]}";
        }

        private void ShowFileTooltip(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem menuItem && menuItem.Tag is string filePath)
            {
                // Get current mouse position
                Point mousePos = Control.MousePosition;

                // If mouse hasn't moved significantly, don't reshow the tooltip
                if (lastMousePosition == mousePos) return;
                lastMousePosition = mousePos;

                // Hide any existing tooltip
                fileInfoTooltip.Hide(contextMenuStripMain);

                // Get metadata text
                string tooltipText = GetFileMetadataText(filePath);

                // Calculate best position for tooltip
                Point tooltipPos = CalculateTooltipPosition(mousePos, tooltipText);

                // Show tooltip at calculated position
                fileInfoTooltip.Show(tooltipText, contextMenuStripMain,
                    contextMenuStripMain.PointToClient(tooltipPos), 5000);
            }
        }

        private void HideFileTooltip(object sender, EventArgs e)
        {
            fileInfoTooltip.Hide(contextMenuStripMain);
        }

        /// <summary>
        /// This is needed to avoid the tooltip overlapping with the mouse cursor. 
        /// It will create flicker otherwise
        /// </summary>
        /// <param name="mousePos"></param>
        /// <param name="tooltipText"></param>
        /// <returns></returns>
        private Point CalculateTooltipPosition(Point mousePos, string tooltipText)
        {
            // Estimate tooltip size (rough calculation)
            Size tooltipSize = new Size(
                Math.Max(200, tooltipText.Split('\n').Max(l => l.Length) * 7),
                tooltipText.Split('\n').Length * 15 + 10
            );

            // Get screen working area
            Rectangle screen = Screen.FromPoint(mousePos).WorkingArea;

            // Initial position to the right of cursor
            Point pos = new Point(mousePos.X + 15, mousePos.Y);

            // If tooltip would go off right edge, show it to the left of cursor
            if (pos.X + tooltipSize.Width > screen.Right)
            {
                pos.X = mousePos.X - tooltipSize.Width - 15;
            }

            // If tooltip would go off bottom edge, adjust Y position upward
            if (pos.Y + tooltipSize.Height > screen.Bottom)
            {
                pos.Y = screen.Bottom - tooltipSize.Height - 5;
            }

            return pos;
        }
    }
}
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
        public Form1()
        {
            InitializeComponent();
            PopulateDriveMenuItems();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.isCloseEventCancelled = false;
            this.Close();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = this.isCloseEventCancelled;
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
                return Icon.FromHandle(shfi.hIcon);
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
                Text = $"{volumeLabel} {drive.Name.TrimEnd('\\')}",
                ToolTipText = $"Type: {drive.DriveType}"
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
                        System.Diagnostics.Process.Start("explorer.exe", drive.Name);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Could not open drive {drive.Name}: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            };

            // Add additional drive information to tooltip
            try
            {
                long freeSpaceGB = drive.AvailableFreeSpace / (1024 * 1024 * 1024);
                long totalSpaceGB = drive.TotalSize / (1024 * 1024 * 1024);
                driveMenuItem.ToolTipText += $"\nFree: {freeSpaceGB}GB / {totalSpaceGB}GB";
            }
            catch
            {
                // Ignore errors in space calculation
            }

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

        private void PopulateDriveSubmenu(ToolStripMenuItem parentMenuItem, string path)
        {
            // Clear existing submenus to prevent duplicate loading
            parentMenuItem.DropDownItems.Clear();

            try
            {
                // Load folders first
                string[] directories = Directory.GetDirectories(path);
                foreach (string dir in directories.OrderBy(d => Path.GetFileName(d)))
                {
                    ToolStripMenuItem folderMenuItem = CreateFolderMenuItem(dir);
                    parentMenuItem.DropDownItems.Add(folderMenuItem);
                }

                // Then load files
                string[] files = Directory.GetFiles(path);
                foreach (string file in files.OrderBy(f => Path.GetFileName(f)))
                {
                    ToolStripMenuItem fileMenuItem = CreateFileMenuItem(file);
                    parentMenuItem.DropDownItems.Add(fileMenuItem);
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

        private ToolStripMenuItem CreateFolderMenuItem(string folderPath)
        {
            string folderName = Path.GetFileName(folderPath);

            ToolStripMenuItem folderMenuItem = new ToolStripMenuItem(folderName)
            {
                Image = GetFolderIcon().ToBitmap()
            };

            try
            {
      

                string[] directories = Directory.GetDirectories(folderPath);
                string[] files = Directory.GetFiles(folderPath);

                if (directories.Length == 0 && files.Length == 0)
                {
                    folderMenuItem.ForeColor = Color.Gray;
                    folderMenuItem.Tag = "Empty";
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
                if (folderMenuItem.Tag == null)
                {
                    folderMenuItem.DropDownOpening += (s, e) =>
                    {
                        // Clear existing dummy/previous items
                        folderMenuItem.DropDownItems.Clear();
                        try
                        {
                            if (directories.Length != 0 || files.Length != 0)
                            {
                                // Populate with actual contents
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
                //if (e.Button == MouseButtons.Right)
                //{
                //    FolderContextMenu.ShowContextMenu(folderPath, Cursor.Position.X, Cursor.Position.Y);
                //}
                //else 
                if (e.Clicks == 2 && e.Button == MouseButtons.Left)
                {
                    try
                    {
                        ShellExecute(IntPtr.Zero, "open", folderPath, null, null, ShowCommands.SW_SHOWNORMAL);
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

            // Click event to open file
            fileMenuItem.Click += (s, e) =>
            {
              
                try
                {
                    System.Diagnostics.Process.Start(filePath);
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
                    Icon folderIcon = Icon.FromHandle(shfi.hIcon);
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
                        return Icon.FromHandle(hIcon);
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
                    Icon fileIcon = Icon.FromHandle(shfi.hIcon);
                    return fileIcon;
                }
            }
            catch { }

            // Fallback to system document icon
            return SystemIcons.Application;
        }

    }
}

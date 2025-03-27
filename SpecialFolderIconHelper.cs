using System;
using System.Runtime.InteropServices;
using System.Drawing;

public class FolderIconRetriever
{
    // Required Win32 API constants and structures
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

    // P/Invoke declarations
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

    [DllImport("shell32.dll", EntryPoint = "ExtractIcon", CharSet = CharSet.Auto)]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    // Flag constants
    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_SMALLICON = 0x000000001;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

    /// <summary>
    /// Retrieves the icon for a specific folder path
    /// </summary>
    /// <param name="folderPath">Full path to the folder</param>
    /// <returns>Icon representing the folder</returns>
    public static Icon GetFolderIcon(string folderPath)
    {
        try
        {
            SHFILEINFO shfi = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_SMALLICON;

            // If the folder doesn't exist, use file attributes
            if (!System.IO.Directory.Exists(folderPath))
            {
                flags |= SHGFI_USEFILEATTRIBUTES;
            }

            IntPtr hIcon = SHGetFileInfo(folderPath,
                FILE_ATTRIBUTE_DIRECTORY,
                ref shfi,
                (uint)Marshal.SizeOf(shfi),
                flags);

            if (shfi.hIcon != IntPtr.Zero)
            {
                // Create a copy of the icon to prevent resource disposal
                Icon folderIcon = (Icon)Icon.FromHandle(shfi.hIcon).Clone();

                // Attempt to destroy the original icon handle to prevent resource leaks
                DestroyIcon(shfi.hIcon);

                return folderIcon;
            }
        }
        catch (Exception ex)
        {
            // Log or handle the exception as needed
            System.Diagnostics.Debug.WriteLine($"Error retrieving folder icon: {ex.Message}");
        }

        // Fallback to system application icon if all else fails
        return SystemIcons.Application;
    }

    // Additional P/Invoke to properly destroy icon handle
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
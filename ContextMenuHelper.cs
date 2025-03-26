using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

//TODO: This doesn't work as of now. It's a work in progress.
public class FolderContextMenu
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214e4-0000-0000-c000-000000000046")]
    private interface IContextMenu
    {
        [PreserveSig]
        int QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);

        [PreserveSig]
        int InvokeCommand(ref CMINVOKECOMMANDINFO pici);

        [PreserveSig]
        int QueryInterface(ref Guid riid, out IntPtr ppv);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CMINVOKECOMMANDINFO
    {
        public uint cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public IntPtr lpVerb;
        public IntPtr lpParameters;
        public IntPtr lpDirectory;
        public int nShow;
        public int dwHotKey;
        public IntPtr hIcon;
    }

    [DllImport("shell32.dll")]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

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

    [DllImport("shell32.dll")]
    private static extern int SHGetSpecialFolderLocation(IntPtr hwnd, int csidl, out IntPtr ppidl);

    [DllImport("shell32.dll")]
    private static extern bool SHGetPathFromIDList(IntPtr pidl, System.Text.StringBuilder pszPath);

    [DllImport("shell32.dll")]
    private static extern int SHBindToParent(IntPtr pidl, ref Guid riid, out object ppv, out IntPtr ppidlChild);

    [DllImport("ole32.dll")]
    private static extern int CreateBindCtx(uint reserved, out object ppbc);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    private static extern bool TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hwnd, IntPtr prcRect);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("ole32.dll")]
    private static extern int CoInitialize(IntPtr pvReserved);

    [DllImport("ole32.dll")]
    private static extern void CoUninitialize();

    private const uint CMF_NORMAL = 0x00000000;
    private const uint CMF_EXTENDEDVERBS = 0x00000001;
    private const uint SHGFI_PIDL = 0x000000008;

    public static void ShowContextMenu(string folderPath, int x, int y)
    {
        // Initialize COM
        int comInitResult = CoInitialize(IntPtr.Zero);

        try
        {
            // Validate folder path
            if (string.IsNullOrEmpty(folderPath) || !System.IO.Directory.Exists(folderPath))
            {
                MessageBox.Show("Invalid or non-existent folder path.");
                return;
            }

            // Get the shell folder interface
            object shellFolder;
            IntPtr pidl = IntPtr.Zero;
            IntPtr pidlChild = IntPtr.Zero;

            try
            {
                // Create PIDL for the folder
                pidl = GetPIDLForPath(folderPath);
                if (pidl == IntPtr.Zero)
                {
                    MessageBox.Show("Could not create folder PIDL.");
                    return;
                }

                // Bind to parent
                Guid iidShellFolder = new Guid("000214E6-0000-0000-C000-000000000046");
                int hrBind = SHBindToParent(pidl, ref iidShellFolder, out shellFolder, out pidlChild);
                if (hrBind != 0 || shellFolder == null)
                {
                    MessageBox.Show($"Failed to bind to parent folder. HRESULT: {hrBind}");
                    return;
                }

                // Get context menu
                IntPtr contextMenuPtr = IntPtr.Zero;
                try
                {
                    // Get UI Object
                    Guid IID_IContextMenu = new Guid("000214e4-0000-0000-c000-000000000046");
                    IntPtr[] pidlArray = new IntPtr[] { pidlChild };

                    // Use reflection to call GetUIObjectOf safely
                    Type shellFolderType = shellFolder.GetType();
                    var getUIObjectOfMethod = shellFolderType.GetMethod("GetUIObjectOf");

                    if (getUIObjectOfMethod == null)
                    {
                        MessageBox.Show("Could not find GetUIObjectOf method.");
                        return;
                    }

                    object uiObject = getUIObjectOfMethod.Invoke(shellFolder, new object[]
                    {
                        IntPtr.Zero,  // hwnd
                        1,            // item count
                        pidlArray,    // PIDLs
                        IID_IContextMenu,  // Interface ID
                        IntPtr.Zero   // Reserved
                    });

                    if (uiObject == null)
                    {
                        MessageBox.Show("Failed to get context menu interface.");
                        return;
                    }

                    // Create popup menu
                    IntPtr hMenu = CreatePopupMenu();
                    if (hMenu == IntPtr.Zero)
                    {
                        MessageBox.Show("Failed to create popup menu.");
                        return;
                    }

                    try
                    {
                        // Cast to IContextMenu interface
                        IContextMenu contextMenu = (IContextMenu)uiObject;

                        // Query context menu
                        uint idCmdFirst = 1;
                        int menuResult = contextMenu.QueryContextMenu(
                            hMenu, 0, idCmdFirst, 0xFFFF,
                            CMF_NORMAL | CMF_EXTENDEDVERBS
                        );

                        if (menuResult > 0)
                        {
                            // Show context menu
                            TrackPopupMenu(
                                hMenu,
                                0x0100 | 0x0040,  // TPM_RETURNCMD | TPM_RIGHTBUTTON 
                                x, y,
                                0,
                                Form.ActiveForm.Handle,
                                IntPtr.Zero
                            );
                        }
                        else
                        {
                            MessageBox.Show($"QueryContextMenu failed. Result: {menuResult}");
                        }
                    }
                    finally
                    {
                        // Cleanup menu
                        DestroyMenu(hMenu);
                    }
                }
                finally
                {
                    // Release COM objects
                    if (shellFolder != null)
                    {
                        Marshal.ReleaseComObject(shellFolder);
                    }
                }
            }
            finally
            {
                // Free PIDLs
                if (pidl != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(pidl);
                if (pidlChild != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(pidlChild);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Detailed Error: {ex.Message}\n\nStack Trace: {ex.StackTrace}");
        }
        finally
        {
            // Uninitialize COM if it was successfully initialized
            if (comInitResult == 0)
            {
                CoUninitialize();
            }
        }
    }

    private static IntPtr GetPIDLForPath(string path)
    {
        // Get PIDL for the specified path
        SHFILEINFO shfi = new SHFILEINFO();
        IntPtr pidl = SHGetFileInfo(path, 0, ref shfi, (uint)Marshal.SizeOf(shfi), SHGFI_PIDL);

        return pidl;
    }
}
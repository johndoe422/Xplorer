using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Xplorer;

public class GlobalHotkey
{
    // Import necessary Windows API functions
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // Modifier key constants
    public const uint MOD_ALT = 0x1;
    public const uint MOD_CONTROL = 0x2;
    public const uint MOD_SHIFT = 0x4;
    public const uint MOD_WIN = 0x8;

    // Hotkey message identifier
    private const int WM_HOTKEY = 0x0312;

    private NotifyIcon trayIcon;
    private Form1 mainForm;
    private int _hotkeyId;

    private static Form tempForm = null;

    public GlobalHotkey(NotifyIcon trayIcon, Form1 mainForm)
    {
        this.trayIcon = trayIcon;
        this.mainForm = mainForm;
        _hotkeyId = GetHashCode(); // Unique identifier for the hotkey

        if (tempForm == null)
        {
            tempForm = new Form();

            // Set the form's size to 0 and make it invisible
            tempForm.Size = new Size(0, 0);
            tempForm.StartPosition = FormStartPosition.Manual;
            tempForm.Top = -100;
            tempForm.Left = -100;
            tempForm.ShowInTaskbar = false;
            tempForm.BackColor = Color.Green;
            tempForm.TransparencyKey = Color.Green;
            tempForm.FormBorderStyle = FormBorderStyle.None;
        }
    }

    public bool RegisterGlobalHotkey(uint modifier, uint key)
    {
        return RegisterHotKey(mainForm.Handle, _hotkeyId, modifier, key);
    }

    public bool UnregisterGlobalHotkey()
    {
        return UnregisterHotKey(mainForm.Handle, _hotkeyId);
    }

    // Method to process hotkey message
    public bool ProcessHotkey(ref Message m)
    {
        if (m.Msg == WM_HOTKEY)
        {
            // Check if the hotkey matches your registered hotkey
            if ((int)m.WParam == _hotkeyId)
            {
                // Show context menu at cursor position
                mainForm.ExitMode = Form1.ExitMenuMode.Cancel;
                ShowContextMenu(trayIcon.ContextMenuStrip);
                return true;
            }
        }
        return false;
    }

    public static void ShowContextMenu(ContextMenuStrip contextMenu)
    {
        // Show temporary form to host the context menu
        if (!tempForm.Visible)
            tempForm.Show();
        // Bring the context menu to the foreground and show it at cursor position
        tempForm.Activate();
        contextMenu.Show(tempForm, tempForm.PointToClient(Cursor.Position));
    }
}


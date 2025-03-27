using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

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

    private NotifyIcon _trayIcon;
    private Form _mainForm;
    private int _hotkeyId;

    public GlobalHotkey(NotifyIcon trayIcon, Form mainForm)
    {
        _trayIcon = trayIcon;
        _mainForm = mainForm;
        _hotkeyId = GetHashCode(); // Unique identifier for the hotkey
    }

    public bool RegisterGlobalHotkey(uint modifier, uint key)
    {
        return RegisterHotKey(_mainForm.Handle, _hotkeyId, modifier, key);
    }

    public bool UnregisterGlobalHotkey()
    {
        return UnregisterHotKey(_mainForm.Handle, _hotkeyId);
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
                _trayIcon.ContextMenuStrip.Show(Cursor.Position);
                return true;
            }
        }
        return false;
    }
}


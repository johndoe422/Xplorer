using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class MouseClicker
{
    // Windows API imports for mouse events
    [DllImport("user32.dll")]
    static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);

    [DllImport("user32.dll")]
    static extern bool SetCursorPos(int X, int Y);

    // Constants for mouse events
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;

    // Method 1: Using mouse_event API
    public static void SimulateRightClick(int x, int y)
    {
        // Move cursor to specific location
        SetCursorPos(x, y);

        // Simulate right mouse button down
        mouse_event(MOUSEEVENTF_RIGHTDOWN, x, y, 0, 0);

        // Simulate right mouse button up
        mouse_event(MOUSEEVENTF_RIGHTUP, x, y, 0, 0);
    }

    // Method 2: Using SendInput API for more robust simulation
    [DllImport("user32.dll")]
    static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    struct INPUT
    {
        public uint type;
        public MOUSEKEYBDHARDWAREINPUT data;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct MOUSEKEYBDHARDWAREINPUT
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    public static void SimulateRightClickAdvanced(int x, int y)
    {
        // Move cursor
        SetCursorPos(x, y);

        // Prepare input structures
        INPUT[] inputs = new INPUT[2];

        // Right mouse button down
        inputs[0].type = 0; // INPUT_MOUSE
        inputs[0].data.mi.dwFlags = 0x0008; // MOUSEEVENTF_RIGHTDOWN

        // Right mouse button up
        inputs[1].type = 0; // INPUT_MOUSE
        inputs[1].data.mi.dwFlags = 0x0010; // MOUSEEVENTF_RIGHTUP

        // Send the input
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    // Method 3: Right-click on a specific control
    public static void RightClickOnControl(Control control)
    {
        // Get screen coordinates of the control
        Point screenPoint = control.PointToScreen(new Point(control.Width / 2, control.Height / 2));

        SimulateRightClick(screenPoint.X, screenPoint.Y);
    }

}
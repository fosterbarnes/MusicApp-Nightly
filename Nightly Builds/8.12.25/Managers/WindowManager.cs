using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace MusicApp
{
    public class WindowManager
    {
        // ===========================================
        // WINDOWS API IMPORTS FOR TASKBAR DETECTION
        // ===========================================
        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref RECT pvParam, uint fWinIni);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;
        private const uint SPI_GETWORKAREA = 0x0030;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const int SW_MAXIMIZE = 3;

        // ===========================================
        // WINDOW STATE TRACKING
        // ===========================================
        private bool isCustomMaximized = false;
        private Rect normalWindowBounds;
        private bool normalWindowBoundsRestored = false;
        private Window window;
        private UserControl titleBarPlayer;

        public bool IsCustomMaximized => isCustomMaximized;
        public Rect NormalWindowBounds => normalWindowBounds;

        public WindowManager(Window window, UserControl titleBarPlayer)
        {
            this.window = window;
            this.titleBarPlayer = titleBarPlayer;
        }

        /// <summary>
        /// Initializes the window state tracking
        /// </summary>
        public void InitializeWindowState()
        {
            System.Diagnostics.Debug.WriteLine("WindowManager: InitializeWindowState - Starting window state initialization");
            
            // Store initial window bounds
            normalWindowBounds = new Rect(window.Left, window.Top, window.Width, window.Height);
            normalWindowBoundsRestored = false;
            System.Diagnostics.Debug.WriteLine($"WindowManager: InitializeWindowState - Initial bounds: {normalWindowBounds}, WindowState: {window.WindowState}");
            
            // Check if window starts maximized
            if (window.WindowState == WindowState.Maximized)
            {
                System.Diagnostics.Debug.WriteLine("WindowManager: InitializeWindowState - Window starts maximized, setting custom maximized flag");
                isCustomMaximized = true;
                UpdateWindowStateIcon(WindowState.Maximized);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("WindowManager: InitializeWindowState - Window starts in normal state");
            }
        }

        /// <summary>
        /// Minimizes the window
        /// </summary>
        public void MinimizeWindow()
        {
            System.Diagnostics.Debug.WriteLine("WindowManager: Minimizing window");
            window.WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// Maximizes or restores the window based on current state
        /// </summary>
        public void ToggleMaximize()
        {
            if (isCustomMaximized)
            {
                System.Diagnostics.Debug.WriteLine("WindowManager: ToggleMaximize - Restoring window from maximized state");
                // Restore to normal state
                RestoreWindow();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("WindowManager: ToggleMaximize - Maximizing window to work area");
                // Maximize to work area (excluding taskbar)
                MaximizeToWorkArea();
            }
        }

        /// <summary>
        /// Maximizes the window to the work area (screen area excluding taskbar)
        /// </summary>
        public void MaximizeToWorkArea()
        {
            System.Diagnostics.Debug.WriteLine("WindowManager: MaximizeToWorkArea - Starting custom maximize");
            
            // Store current window bounds for restoration only if they haven't been restored from settings yet
            if (!normalWindowBoundsRestored)
            {
                normalWindowBounds = new Rect(window.Left, window.Top, window.Width, window.Height);
                System.Diagnostics.Debug.WriteLine($"WindowManager: MaximizeToWorkArea - Stored normal bounds: {normalWindowBounds}");
            }
            
            // Get the work area (screen area excluding taskbar)
            RECT workArea = new RECT();
            if (SystemParametersInfo(SPI_GETWORKAREA, 0, ref workArea, 0))
            {
                System.Diagnostics.Debug.WriteLine($"WindowManager: MaximizeToWorkArea - Work area: Left={workArea.Left}, Top={workArea.Top}, Right={workArea.Right}, Bottom={workArea.Bottom}");
                
                // Ensure we're in normal state for custom positioning
                window.WindowState = WindowState.Normal;
                
                // Extend the window by 6 pixels on each side to compensate for gaps
                const int gapCompensation = 6;
                const int topExtraCompensation = 6;
                const int rightExtraCompensation = -12;
                const int bottomExtraCompensation = topExtraCompensation - 18;
                
                window.Left = workArea.Left;
                window.Top = workArea.Top;
                window.Width = (workArea.Right - workArea.Left) + (gapCompensation * 2) + rightExtraCompensation; 
                window.Height = (workArea.Bottom - workArea.Top) + (gapCompensation * 2) + bottomExtraCompensation;
                
                isCustomMaximized = true;
                System.Diagnostics.Debug.WriteLine($"WindowManager: MaximizeToWorkArea - Window positioned to: Left={window.Left}, Top={window.Top}, Width={window.Width}, Height={window.Height}");
                UpdateWindowStateIcon(WindowState.Maximized);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("WindowManager: MaximizeToWorkArea - Fallback to standard maximize");
                // Fallback to standard maximize
                window.WindowState = WindowState.Maximized;
                UpdateWindowStateIcon(WindowState.Maximized);
            }
        }

        /// <summary>
        /// Restores the window to its previous normal state
        /// </summary>
        public void RestoreWindow()
        {
            System.Diagnostics.Debug.WriteLine($"WindowManager: RestoreWindow - Restoring to normal bounds: {normalWindowBounds}");
            
            // Ensure we're in normal state first
            window.WindowState = WindowState.Normal;
            
            // Restore to previous bounds
            window.Left = normalWindowBounds.Left;
            window.Top = normalWindowBounds.Top;
            window.Width = normalWindowBounds.Width;
            window.Height = normalWindowBounds.Height;
            
            isCustomMaximized = false;
            System.Diagnostics.Debug.WriteLine($"WindowManager: RestoreWindow - Window restored to: Left={window.Left}, Top={window.Top}, Width={window.Width}, Height={window.Height}");
            UpdateWindowStateIcon(WindowState.Normal);
        }

        /// <summary>
        /// Checks if the window is currently maximized (either custom or standard)
        /// </summary>
        /// <returns>True if the window is maximized, false otherwise</returns>
        public bool IsWindowMaximized()
        {
            return isCustomMaximized || window.WindowState == WindowState.Maximized;
        }

        /// <summary>
        /// Updates the window state tracking when the window state changes externally
        /// </summary>
        public void OnStateChanged()
        {
            System.Diagnostics.Debug.WriteLine($"WindowManager: OnStateChanged - Window state changed to: {window.WindowState}, isCustomMaximized: {isCustomMaximized}");
            
            // If the window state was changed externally (e.g., by double-clicking title bar),
            // update our custom tracking
            if (window.WindowState == WindowState.Normal && isCustomMaximized)
            {
                System.Diagnostics.Debug.WriteLine("WindowManager: OnStateChanged - Window restored externally, updating tracking");
                isCustomMaximized = false;
                UpdateWindowStateIcon(WindowState.Normal);
            }
            else if (window.WindowState == WindowState.Maximized && !isCustomMaximized)
            {
                System.Diagnostics.Debug.WriteLine("WindowManager: OnStateChanged - Window maximized externally, updating tracking");
                // If window was maximized externally, store current bounds and mark as custom maximized
                // Only update normalWindowBounds if they haven't been restored from settings yet
                if (!normalWindowBoundsRestored)
                {
                    normalWindowBounds = new Rect(window.Left, window.Top, window.Width, window.Height);
                    System.Diagnostics.Debug.WriteLine($"WindowManager: OnStateChanged - Stored new normal bounds: {normalWindowBounds}");
                }
                isCustomMaximized = true;
                UpdateWindowStateIcon(WindowState.Maximized);
            }
        }

        /// <summary>
        /// Handles window location and size changes to update state tracking
        /// </summary>
        public void OnLocationChanged()
        {
            System.Diagnostics.Debug.WriteLine($"WindowManager: OnLocationChanged - Window moved to: Left={window.Left}, Top={window.Top}, Width={window.Width}, Height={window.Height}");
            
            // If the window is moved while custom maximized, it should be restored
            if (isCustomMaximized && window.WindowState != WindowState.Maximized)
            {
                System.Diagnostics.Debug.WriteLine("WindowManager: OnLocationChanged - Window moved while maximized, restoring to normal state");
                isCustomMaximized = false;
                UpdateWindowStateIcon(WindowState.Normal);
            }
        }

        /// <summary>
        /// Closes the window
        /// </summary>
        public void CloseWindow()
        {
            System.Diagnostics.Debug.WriteLine("WindowManager: CloseWindow - Closing window");
            window.Close();
        }

        /// <summary>
        /// Restores window state from settings
        /// </summary>
        public void RestoreWindowState(double width, double height, double left, double top, bool isMaximized)
        {
            System.Diagnostics.Debug.WriteLine($"WindowManager: RestoreWindowState - Restoring from settings: Width={width}, Height={height}, Left={left}, Top={top}, IsMaximized={isMaximized}");
            
            window.Width = width;
            window.Height = height;
            window.Left = left;
            window.Top = top;
            
            // Store these bounds as our normal window bounds
            normalWindowBounds = new Rect(window.Left, window.Top, window.Width, window.Height);
            normalWindowBoundsRestored = true;
            System.Diagnostics.Debug.WriteLine($"WindowManager: RestoreWindowState - Stored normal bounds: {normalWindowBounds}");
            
            if (isMaximized)
            {
                System.Diagnostics.Debug.WriteLine("WindowManager: RestoreWindowState - Window was maximized, applying maximize");
                // Use MaximizeToWorkArea to apply the same gap compensation adjustments
                MaximizeToWorkArea();
            }
        }

        /// <summary>
        /// Resets window state to default values
        /// </summary>
        public void ResetWindowState()
        {
            System.Diagnostics.Debug.WriteLine("WindowManager: ResetWindowState - Resetting to default values");
            window.Width = 1200;
            window.Height = 700;
            window.Left = 100;
            window.Top = 100;
            window.WindowState = WindowState.Normal;
            isCustomMaximized = false;
            System.Diagnostics.Debug.WriteLine($"WindowManager: ResetWindowState - Window reset to: Left={window.Left}, Top={window.Top}, Width={window.Width}, Height={window.Height}");
            UpdateWindowStateIcon(WindowState.Normal);
        }

        /// <summary>
        /// Updates the window state icon in the title bar player
        /// </summary>
        private void UpdateWindowStateIcon(WindowState state)
        {
            System.Diagnostics.Debug.WriteLine($"WindowManager: UpdateWindowStateIcon - Updating icon to: {state}");
            
            // Use reflection to call the UpdateWindowStateIcon method on the titleBarPlayer
            var method = titleBarPlayer.GetType().GetMethod("UpdateWindowStateIcon");
            if (method != null)
            {
                method.Invoke(titleBarPlayer, new object[] { state });
                System.Diagnostics.Debug.WriteLine($"WindowManager: UpdateWindowStateIcon - Icon updated successfully");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"WindowManager: UpdateWindowStateIcon - Warning: UpdateWindowStateIcon method not found on titleBarPlayer");
            }
        }
    }
}

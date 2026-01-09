using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;

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

        // ===========================================
        // WINDOWS API IMPORTS FOR MINIMIZE/RESTORE ANIMATION WORKAROUND
        // ===========================================
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);

        // ===========================================
        // API CONSTANTS FOR MINIMIZE/RESTORE ANIMATION WORKAROUND
        // ===========================================
        internal class ApiCodes
        {
            public const int SC_MINIMIZE = 0xF020;
            public const int SC_CLOSE = 0xF060;
            public const int WM_SYSCOMMAND = 0x0112;
        }

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
        private IntPtr hWnd; // Window handle for API calls
        private DispatcherTimer? saveTimer;
        private bool isDirty = false;
        private bool isRestoring = false; // Flag to prevent bounds updates during restore

        // Event to notify when window state should be saved
        public event EventHandler? WindowStateChanged;

        public bool IsCustomMaximized => isCustomMaximized;
        public Rect NormalWindowBounds => normalWindowBounds;

        public WindowManager(Window window, UserControl titleBarPlayer)
        {
            this.window = window;
            this.titleBarPlayer = titleBarPlayer;
            
            // Get the window handle when the window is loaded
            window.Loaded += Window_Loaded;
        }

        // ===========================================
        // WINDOW LOADED EVENT HANDLER
        // ===========================================
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Get the window handle for API calls
            hWnd = new WindowInteropHelper(window).Handle;
            
            // Add hook to handle system commands for animation workaround
            if (hWnd != IntPtr.Zero)
            {
                HwndSource.FromHwnd(hWnd).AddHook(WindowProc);
            }
        }

        // ===========================================
        // WINDOW PROCEDURE HOOK FOR ANIMATION WORKAROUND
        // ===========================================
        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == ApiCodes.WM_SYSCOMMAND)
            {
                if (wParam.ToInt32() == ApiCodes.SC_MINIMIZE)
                {
                    // Temporarily change window style to enable minimize animation
                    window.WindowStyle = WindowStyle.SingleBorderWindow;
                    window.WindowState = WindowState.Minimized;
                    handled = true;
                }
                else if (wParam.ToInt32() == ApiCodes.SC_CLOSE)
                {
                    // Temporarily change window style to enable close animation
                    window.WindowStyle = WindowStyle.SingleBorderWindow;
                    
                    // Use a dispatcher callback to close the window after the animation starts
                    // This ensures the animation plays before the window actually closes
                    Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, (DispatcherOperationCallback)delegate (object unused)
                    {
                        window.Close();
                        return null;
                    }, null);
                    
                    handled = true;
                }
                // Don't handle SC_RESTORE here - let Windows handle it naturally
                // The OnActivated event in MainWindow will restore the window style
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// Sets the initial window position before the window is shown
        /// This prevents the window from appearing in the center first
        /// </summary>
        public void SetInitialPosition(double left, double top, double width, double height)
        {
            System.Diagnostics.Debug.WriteLine($"WindowManager: SetInitialPosition - Setting initial position: Left={left}, Top={top}, Width={width}, Height={height}");
            
            window.Left = left;
            window.Top = top;
            window.Width = width;
            window.Height = height;
            
            // Store these as initial bounds
            normalWindowBounds = new Rect(left, top, width, height);
            normalWindowBoundsRestored = true;
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
        /// Minimizes the window using the animation workaround
        /// </summary>
        public void MinimizeWindow()
        {
            System.Diagnostics.Debug.WriteLine("WindowManager: Minimizing window with animation workaround");
            
            if (hWnd != IntPtr.Zero)
            {
                // Use the system command to trigger the minimize animation
                SendMessage(hWnd, ApiCodes.WM_SYSCOMMAND, new IntPtr(ApiCodes.SC_MINIMIZE), IntPtr.Zero);
            }
            else
            {
                // Fallback to direct minimize if handle is not available
                System.Diagnostics.Debug.WriteLine("WindowManager: MinimizeWindow - No window handle available, using fallback");
                window.WindowState = WindowState.Minimized;
            }
        }

        /// <summary>
        /// Closes the window using the animation workaround
        /// </summary>
        public void CloseWindowWithAnimation()
        {
            System.Diagnostics.Debug.WriteLine("WindowManager: Closing window with animation workaround");
            
            if (hWnd != IntPtr.Zero)
            {
                // Use the system command to trigger the close animation
                SendMessage(hWnd, ApiCodes.WM_SYSCOMMAND, new IntPtr(ApiCodes.SC_CLOSE), IntPtr.Zero);
            }
            else
            {
                // Fallback to direct close if handle is not available
                System.Diagnostics.Debug.WriteLine("WindowManager: CloseWindowWithAnimation - No window handle available, using fallback");
                window.Close();
            }
        }

        /// <summary>
        /// Maximizes or restores the window based on current state
        /// </summary>
        public void ToggleMaximize()
        {
            System.Diagnostics.Debug.WriteLine($"WindowManager: ToggleMaximize - Current state: isCustomMaximized={isCustomMaximized}, WindowState={window.WindowState}");
            LogCurrentState();
            
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
            
            System.Diagnostics.Debug.WriteLine($"WindowManager: ToggleMaximize - After toggle: isCustomMaximized={isCustomMaximized}, WindowState={window.WindowState}");
            LogCurrentState();
        }

        /// <summary>
        /// Maximizes the window to the work area (screen area excluding taskbar)
        /// </summary>
        public void MaximizeToWorkArea()
        {
            System.Diagnostics.Debug.WriteLine("WindowManager: MaximizeToWorkArea - Starting custom maximize");
            
            // Always store current window bounds for restoration before maximizing
            // This ensures we can restore to the current position, not just the saved position
            normalWindowBounds = new Rect(window.Left, window.Top, window.Width, window.Height);
            System.Diagnostics.Debug.WriteLine($"WindowManager: MaximizeToWorkArea - Stored normal bounds: {normalWindowBounds}");
            
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
            System.Diagnostics.Debug.WriteLine($"WindowManager: RestoreWindow - Starting restore operation");
            System.Diagnostics.Debug.WriteLine($"WindowManager: RestoreWindow - Current state: isCustomMaximized={isCustomMaximized}, WindowState={window.WindowState}");
            System.Diagnostics.Debug.WriteLine($"WindowManager: RestoreWindow - Restoring to normal bounds: {normalWindowBounds}");
            
            // Set the restoring flag to prevent bounds updates during restore
            isRestoring = true;
            
            // Ensure we're in normal state first
            window.WindowState = WindowState.Normal;
            
            // Restore to previous bounds
            window.Left = normalWindowBounds.Left;
            window.Top = normalWindowBounds.Top;
            window.Width = normalWindowBounds.Width;
            window.Height = normalWindowBounds.Height;
            
            // Update the flag BEFORE updating the icon
            isCustomMaximized = false;
            
            System.Diagnostics.Debug.WriteLine($"WindowManager: RestoreWindow - Window restored to: Left={window.Left}, Top={window.Top}, Width={window.Width}, Height={window.Height}");
            System.Diagnostics.Debug.WriteLine($"WindowManager: RestoreWindow - isCustomMaximized set to: {isCustomMaximized}");
            
            // Force update the window state icon
            UpdateWindowStateIcon(WindowState.Normal);
            
            // Double-check the state after restoration
            System.Diagnostics.Debug.WriteLine($"WindowManager: RestoreWindow - Final state check: isCustomMaximized={isCustomMaximized}, WindowState={window.WindowState}");
            
            // Force refresh the icon to ensure it's updated
            ForceRefreshWindowStateIcon();
            
            // Clear the restoring flag after a short delay to allow the restore to complete
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, (DispatcherOperationCallback)delegate (object unused)
            {
                isRestoring = false;
                System.Diagnostics.Debug.WriteLine("WindowManager: RestoreWindow - Restoring flag cleared");
                return null;
            }, null);
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
                // If window was maximized externally, we need to store the current bounds
                // However, since we don't know what the previous normal bounds were,
                // we'll use the saved bounds from settings as a fallback
                // The user can manually restore and reposition if needed
                isCustomMaximized = true;
                UpdateWindowStateIcon(WindowState.Maximized);
            }
            else if (window.WindowState == WindowState.Normal && !isCustomMaximized)
            {
                // Window is in normal state, but check if it's actually visually maximized
                // This can happen when restoring a minimized maximized window
                CheckIfWindowIsVisuallyMaximized();
            }
        }

        /// <summary>
        /// Checks if the window is visually maximized even though WindowState is Normal
        /// This can happen when restoring a minimized maximized window
        /// </summary>
        public void CheckIfWindowIsVisuallyMaximized()
        {
            // Get the work area to compare against
            RECT workArea = new RECT();
            if (SystemParametersInfo(SPI_GETWORKAREA, 0, ref workArea, 0))
            {
                // Calculate the expected maximized dimensions (with our gap compensation)
                const int gapCompensation = 6;
                const int topExtraCompensation = 6;
                const int rightExtraCompensation = -12;
                const int bottomExtraCompensation = topExtraCompensation - 18;
                
                double expectedWidth = (workArea.Right - workArea.Left) + (gapCompensation * 2) + rightExtraCompensation;
                double expectedHeight = (workArea.Bottom - workArea.Top) + (gapCompensation * 2) + bottomExtraCompensation;
                
                // Check if current window dimensions match maximized dimensions (with some tolerance)
                const double tolerance = 5.0; // 5 pixel tolerance for rounding differences
                bool isVisuallyMaximized = Math.Abs(window.Width - expectedWidth) <= tolerance &&
                                         Math.Abs(window.Height - expectedHeight) <= tolerance &&
                                         Math.Abs(window.Left - workArea.Left) <= tolerance &&
                                         Math.Abs(window.Top - workArea.Top) <= tolerance;
                
                if (isVisuallyMaximized && !isCustomMaximized)
                {
                    System.Diagnostics.Debug.WriteLine("WindowManager: CheckIfWindowIsVisuallyMaximized - Window appears to be visually maximized, updating tracking");
                    isCustomMaximized = true;
                    UpdateWindowStateIcon(WindowState.Maximized);
                }
                else if (!isVisuallyMaximized && isCustomMaximized)
                {
                    System.Diagnostics.Debug.WriteLine("WindowManager: CheckIfWindowIsVisuallyMaximized - Window is no longer visually maximized, updating tracking");
                    isCustomMaximized = false;
                    UpdateWindowStateIcon(WindowState.Normal);
                }
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
            
            // Update normal window bounds when window is moved (but not during restore operations)
            if (window.WindowState == WindowState.Normal && !isRestoring)
            {
                normalWindowBounds = new Rect(window.Left, window.Top, window.Width, window.Height);
                System.Diagnostics.Debug.WriteLine($"WindowManager: OnLocationChanged - Updated normal bounds: {normalWindowBounds}");
                
                // Mark as dirty to trigger save
                MarkDirty();
            }
            else if (isRestoring)
            {
                System.Diagnostics.Debug.WriteLine("WindowManager: OnLocationChanged - Skipping bounds update during restore operation");
            }
        }

        /// <summary>
        /// Handles window size changes to update state tracking
        /// </summary>
        public void OnSizeChanged()
        {
            System.Diagnostics.Debug.WriteLine($"WindowManager: OnSizeChanged - Window resized to: Width={window.Width}, Height={window.Height}");
            
            // Update normal window bounds when window is resized (but not during restore operations)
            if (window.WindowState == WindowState.Normal && !isRestoring)
            {
                normalWindowBounds = new Rect(window.Left, window.Top, window.Width, window.Height);
                System.Diagnostics.Debug.WriteLine($"WindowManager: OnSizeChanged - Updated normal bounds: {normalWindowBounds}");
                
                // Mark as dirty to trigger save
                MarkDirty();
            }
            else if (isRestoring)
            {
                System.Diagnostics.Debug.WriteLine("WindowManager: OnSizeChanged - Skipping bounds update during restore operation");
            }
        }

        /// <summary>
        /// Closes the window
        /// </summary>
        public void CloseWindow()
        {
            System.Diagnostics.Debug.WriteLine("WindowManager: CloseWindow - Closing window with animation");
            CloseWindowWithAnimation();
        }

        /// <summary>
        /// Restores window state from settings
        /// </summary>
        public void RestoreWindowState(double width, double height, double left, double top, bool isMaximized)
        {
            System.Diagnostics.Debug.WriteLine($"WindowManager: RestoreWindowState - Restoring from settings: Width={width}, Height={height}, Left={left}, Top={top}, IsMaximized={isMaximized}");
            
            // Only update if the position has changed significantly to prevent unnecessary flicker
            bool positionChanged = Math.Abs(window.Left - left) > 1 || Math.Abs(window.Top - top) > 1;
            bool sizeChanged = Math.Abs(window.Width - width) > 1 || Math.Abs(window.Height - height) > 1;
            
            if (positionChanged || sizeChanged)
            {
                System.Diagnostics.Debug.WriteLine($"WindowManager: RestoreWindowState - Applying changes: Position={positionChanged}, Size={sizeChanged}");
                
                window.Width = width;
                window.Height = height;
                window.Left = left;
                window.Top = top;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("WindowManager: RestoreWindowState - No significant changes, skipping update");
            }
            
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

        /// <summary>
        /// Gets the current window state for saving
        /// </summary>
        public SettingsManager.WindowStateSettings GetCurrentWindowState()
        {
            var state = new SettingsManager.WindowStateSettings
            {
                IsMaximized = isCustomMaximized,
                Width = normalWindowBounds.Width,
                Height = normalWindowBounds.Height,
                Left = normalWindowBounds.Left,
                Top = normalWindowBounds.Top
            };
            
            System.Diagnostics.Debug.WriteLine($"WindowManager: GetCurrentWindowState - Returning state: IsMaximized={state.IsMaximized}, Bounds={state.Left},{state.Top},{state.Width}x{state.Height}");
            return state;
        }

        /// <summary>
        /// Logs the current window state for debugging
        /// </summary>
        public void LogCurrentState()
        {
            System.Diagnostics.Debug.WriteLine($"WindowManager: LogCurrentState - WindowState={window.WindowState}, isCustomMaximized={isCustomMaximized}");
            System.Diagnostics.Debug.WriteLine($"WindowManager: LogCurrentState - Current bounds: Left={window.Left}, Top={window.Top}, Width={window.Width}, Height={window.Height}");
            System.Diagnostics.Debug.WriteLine($"WindowManager: LogCurrentState - Normal bounds: {normalWindowBounds}");
            System.Diagnostics.Debug.WriteLine($"WindowManager: LogCurrentState - normalWindowBoundsRestored={normalWindowBoundsRestored}");
            System.Diagnostics.Debug.WriteLine($"WindowManager: LogCurrentState - isRestoring={isRestoring}");
        }

        /// <summary>
        /// Forces a refresh of the window state icon
        /// </summary>
        public void ForceRefreshWindowStateIcon()
        {
            System.Diagnostics.Debug.WriteLine($"WindowManager: ForceRefreshWindowStateIcon - Current state: isCustomMaximized={isCustomMaximized}");
            
            if (isCustomMaximized)
            {
                System.Diagnostics.Debug.WriteLine("WindowManager: ForceRefreshWindowStateIcon - Updating to maximized icon");
                UpdateWindowStateIcon(WindowState.Maximized);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("WindowManager: ForceRefreshWindowStateIcon - Updating to normal icon");
                UpdateWindowStateIcon(WindowState.Normal);
            }
        }

        /// <summary>
        /// Debug method to test the current window state
        /// </summary>
        public void DebugWindowState()
        {
            System.Diagnostics.Debug.WriteLine("=== WINDOW STATE DEBUG ===");
            LogCurrentState();
            System.Diagnostics.Debug.WriteLine($"IsWindowMaximized() returns: {IsWindowMaximized()}");
            System.Diagnostics.Debug.WriteLine($"Window.ActualWidth: {window.ActualWidth}, Window.ActualHeight: {window.ActualHeight}");
            System.Diagnostics.Debug.WriteLine("=== END DEBUG ===");
        }

        /// <summary>
        /// Marks the window state as dirty and starts the save timer
        /// </summary>
        public void MarkDirty()
        {
            isDirty = true;
            
            // Start the save timer if it's not already running
            if (saveTimer == null)
            {
                saveTimer = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, SaveTimer_Tick, Dispatcher.CurrentDispatcher);
            }
            
            if (saveTimer != null && !saveTimer.IsEnabled)
            {
                saveTimer.Start();
            }
        }

        /// <summary>
        /// Timer callback to save the window state
        /// </summary>
        private void SaveTimer_Tick(object? sender, EventArgs e)
        {
            if (isDirty)
            {
                isDirty = false;
                saveTimer?.Stop();
                
                // Notify that the window state has changed and should be saved
                // This will be handled by the MainWindow
                System.Diagnostics.Debug.WriteLine("WindowManager: SaveTimer_Tick - Window state changed, should be saved");
                WindowStateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}

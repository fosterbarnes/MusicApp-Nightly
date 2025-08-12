using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Data;
using System.Windows.Interop;
using NAudio.Wave;
using ATL;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace MusicApp
{
    public partial class MainWindow : Window
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
        // WINDOWS API IMPORTS FOR RECYCLE BIN
        // ===========================================
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public uint wFunc;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pFrom;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pTo;
            public ushort fFlags;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszProgressTitle;
        }

        private const uint FO_DELETE = 0x0003;
        private const ushort FOF_ALLOWUNDO = 0x0040;
        private const ushort FOF_NOCONFIRMATION = 0x0010;
        private const ushort FOF_SILENT = 0x0004;

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

        // ===========================================
        // DATA COLLECTIONS
        // ===========================================
        private ObservableCollection<MusicTrack> allTracks = new ObservableCollection<MusicTrack>();
        private ObservableCollection<MusicTrack> filteredTracks = new ObservableCollection<MusicTrack>();
        private ObservableCollection<MusicTrack> shuffledTracks = new ObservableCollection<MusicTrack>();
        private ObservableCollection<Playlist> playlists = new ObservableCollection<Playlist>();
        private ObservableCollection<MusicTrack> recentlyPlayed = new ObservableCollection<MusicTrack>();
        
        // ===========================================
        // SETTINGS AND PERSISTENCE
        // ===========================================
        private LibraryManager libraryManager = LibraryManager.Instance;
        private SettingsManager settingsManager = SettingsManager.Instance;
        private SettingsManager.AppSettings appSettings = new SettingsManager.AppSettings();
        
        // ===========================================
        // AUDIO PLAYBACK STATE
        // ===========================================
        private WaveOutEvent? waveOut;
        private AudioFileReader? audioFileReader;
        private int currentTrackIndex = -1;
        private int currentShuffledIndex = -1;
        private MusicTrack? currentTrack;
        private bool isManuallyStopping = false; // Flag to prevent infinite loops during manual stops

        // ===========================================
        // CONSTRUCTOR AND INITIALIZATION
        // ===========================================
        public MainWindow()
        {
            InitializeComponent();
            SetupEventHandlers();
            
            // Load saved data asynchronously
            _ = LoadSavedDataAsync();
            
            // Initialize window state tracking
            InitializeWindowState();
        }

        /// <summary>
        /// Initializes the window state tracking
        /// </summary>
        private void InitializeWindowState()
        {
            // Store initial window bounds
            normalWindowBounds = new Rect(this.Left, this.Top, this.Width, this.Height);
            normalWindowBoundsRestored = false;
            
            // Check if window starts maximized
            if (this.WindowState == WindowState.Maximized)
            {
                isCustomMaximized = true;
                titleBarPlayer.UpdateWindowStateIcon(WindowState.Maximized);
            }
        }

        /// <summary>
        /// Loads all saved data from settings files
        /// </summary>
        private async Task LoadSavedDataAsync()
        {
            try
            {
                // Load general settings (window state only)
                appSettings = await settingsManager.LoadSettingsAsync();
                
                // Load library cache (tracks only)
                var libraryCache = await libraryManager.LoadLibraryCacheAsync();
                
                // Load library folders (music folders and scan times)
                var libraryFolders = await libraryManager.LoadLibraryFoldersAsync();
                
                // Load recently played
                var recentlyPlayedCache = await libraryManager.LoadRecentlyPlayedAsync();
                
                // Load playlists
                var playlistsCache = await libraryManager.LoadPlaylistsAsync();
                
                // Restore window state
                RestoreWindowState();
                
                // Load music from saved folders
                await LoadMusicFromSavedFoldersAsync();
                
                // Restore playlists
                RestorePlaylists(playlistsCache);
                
                // Load sample data if no playlists exist
                LoadSampleData();
                
                // Restore recently played
                RestoreRecentlyPlayed(recentlyPlayedCache);
                
                // Update UI
                UpdateUI();
                
                // Initialize shuffled tracks if shuffle is enabled
                if (titleBarPlayer.IsShuffleEnabled)
                {
                    RegenerateShuffledTracks();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading saved data: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Restores window state from settings
        /// </summary>
        private void RestoreWindowState()
        {
            if (appSettings.WindowState != null)
            {
                this.Width = appSettings.WindowState.Width;
                this.Height = appSettings.WindowState.Height;
                this.Left = appSettings.WindowState.Left;
                this.Top = appSettings.WindowState.Top;
                
                // Store these bounds as our normal window bounds
                normalWindowBounds = new Rect(this.Left, this.Top, this.Width, this.Height);
                normalWindowBoundsRestored = true;
                
                if (appSettings.WindowState.IsMaximized)
                {
                    // Use MaximizeToWorkArea to apply the same gap compensation adjustments
                    MaximizeToWorkArea();
                }
            }
        }

        /// <summary>
        /// Loads music from previously saved folders
        /// </summary>
        private async Task LoadMusicFromSavedFoldersAsync()
        {
            var musicFolders = await libraryManager.GetMusicFoldersAsync();
            if (musicFolders == null || musicFolders.Count == 0)
                return;

            foreach (var folderPath in musicFolders)
            {
                if (Directory.Exists(folderPath))
                {
                    // Check if there are new files in the folder
                    bool hasNewFiles = await libraryManager.HasNewFilesInFolderAsync(folderPath);
                    
                    if (hasNewFiles)
                    {
                        // Load new files from this folder
                        await LoadMusicFromFolderAsync(folderPath, true);
                    }
                    else
                    {
                        // Load from cache
                        await LoadMusicFromCacheAsync(folderPath);
                    }
                }
            }
        }

        /// <summary>
        /// Loads music from cache for a specific folder
        /// </summary>
        private async Task LoadMusicFromCacheAsync(string folderPath)
        {
            try
            {
                var libraryCache = await libraryManager.LoadLibraryCacheAsync();
                var cachedTracks = libraryCache.Tracks.Where(t => t.FilePath.StartsWith(folderPath)).ToList();
                
                foreach (var track in cachedTracks)
                {
                    // Verify file still exists
                    if (File.Exists(track.FilePath))
                    {
                        allTracks.Add(track);
                        filteredTracks.Add(track);
                    }
                }
                
                // Update shuffled tracks if shuffle is enabled
                UpdateShuffledTracks();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading from cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Restores playlists from saved data
        /// </summary>
        private void RestorePlaylists(LibraryManager.PlaylistsCache playlistsCache)
        {
            if (playlistsCache.Playlists != null)
            {
                foreach (var playlist in playlistsCache.Playlists)
                {
                    // Reconstruct tracks for each playlist
                    playlist.ReconstructTracks(allTracks);
                    playlists.Add(playlist);
                }
            }
        }

        /// <summary>
        /// Restores recently played tracks
        /// </summary>
        private void RestoreRecentlyPlayed(LibraryManager.RecentlyPlayedCache recentlyPlayedCache)
        {
            if (recentlyPlayedCache.RecentlyPlayed != null)
            {
                foreach (var item in recentlyPlayedCache.RecentlyPlayed.OrderByDescending(x => x.LastPlayed).Take(20))
                {
                    var track = allTracks.FirstOrDefault(t => t.FilePath == item.FilePath);
                    if (track != null)
                    {
                        recentlyPlayed.Add(track);
                    }
                }
            }
        }

        /// <summary>
        /// Updates the UI after loading data
        /// </summary>
        private void UpdateUI()
        {
            // Refresh the music list
            lstMusic.ItemsSource = null;
            lstMusic.ItemsSource = filteredTracks;
            
            // Refresh playlists
            lstPlaylists.ItemsSource = null;
            lstPlaylists.ItemsSource = playlists;
            
            // Refresh recently played
            lstRecentlyPlayed.ItemsSource = null;
            lstRecentlyPlayed.ItemsSource = recentlyPlayed;
        }

        /// <summary>
        /// Sets up data bindings and event handlers for UI controls
        /// </summary>
        private void SetupEventHandlers()
        {
            // Set the data context for the music list
            lstMusic.ItemsSource = filteredTracks;
            lstPlaylists.ItemsSource = playlists;
            lstRecentlyPlayed.ItemsSource = recentlyPlayed;

            // Wire up title bar player control events
            titleBarPlayer.PlayPauseRequested += TitleBarPlayer_PlayPauseRequested;
            titleBarPlayer.PreviousTrackRequested += TitleBarPlayer_PreviousTrackRequested;
            titleBarPlayer.NextTrackRequested += TitleBarPlayer_NextTrackRequested;
            titleBarPlayer.WindowMinimizeRequested += TitleBarPlayer_WindowMinimizeRequested;
            titleBarPlayer.WindowMaximizeRequested += TitleBarPlayer_WindowMaximizeRequested;
            titleBarPlayer.WindowCloseRequested += TitleBarPlayer_WindowCloseRequested;
            
            // Wire up shuffle state change event
            titleBarPlayer.ShuffleStateChanged += TitleBarPlayer_ShuffleStateChanged;
        }

        /// <summary>
        /// Loads initial sample data for the application (only if no saved data exists)
        /// </summary>
        private void LoadSampleData()
        {
            // Only add sample playlists if no playlists exist
            if (playlists.Count == 0)
            {
                playlists.Add(new Playlist("Favorites", "My favorite songs"));
                playlists.Add(new Playlist("Workout Mix", "High energy songs for workouts"));
                playlists.Add(new Playlist("Chill Vibes", "Relaxing music"));
            }
        }

        #region Shuffle Management

        /// <summary>
        /// Regenerates the shuffled tracks collection and updates current track index
        /// </summary>
        private void RegenerateShuffledTracks()
        {
            try
            {
                Console.WriteLine($"RegenerateShuffledTracks called - filteredTracks.Count: {filteredTracks.Count}");
                
                // Safety check
                if (filteredTracks == null || filteredTracks.Count == 0)
                {
                    Console.WriteLine("No filtered tracks to shuffle");
                    shuffledTracks.Clear();
                    currentShuffledIndex = -1;
                    return;
                }
                
                // Clear existing shuffled tracks
                shuffledTracks.Clear();
                
                // Add all filtered tracks to shuffled collection
                foreach (var track in filteredTracks)
                {
                    if (track != null && !string.IsNullOrEmpty(track.FilePath))
                    {
                        shuffledTracks.Add(track);
                    }
                    else
                    {
                        Console.WriteLine($"Skipping null or invalid track in filteredTracks");
                    }
                }
                
                Console.WriteLine($"Added {shuffledTracks.Count} valid tracks to shuffled queue");
                
                // Only shuffle if we have tracks
                if (shuffledTracks.Count > 1)
                {
                    // Shuffle the tracks using Fisher-Yates algorithm
                    var random = new Random();
                    for (int i = shuffledTracks.Count - 1; i > 0; i--)
                    {
                        int j = random.Next(i + 1);
                        var temp = shuffledTracks[i];
                        shuffledTracks[i] = shuffledTracks[j];
                        shuffledTracks[j] = temp;
                    }
                    Console.WriteLine("Shuffled tracks using Fisher-Yates algorithm");
                }
                else
                {
                    Console.WriteLine("Not enough tracks to shuffle (need at least 2)");
                }
                
                // Update current shuffled index to maintain current track position
                if (currentTrack != null)
                {
                    currentShuffledIndex = shuffledTracks.IndexOf(currentTrack);
                    if (currentShuffledIndex == -1)
                    {
                        // If current track not found in shuffled list, reset to beginning
                        currentShuffledIndex = 0;
                        Console.WriteLine($"Current track not found in shuffled list, resetting index to 0");
                    }
                }
                else
                {
                    currentShuffledIndex = -1;
                }
                
                Console.WriteLine($"Shuffled tracks generated - shuffledTracks.Count: {shuffledTracks.Count}, currentShuffledIndex: {currentShuffledIndex}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in RegenerateShuffledTracks: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                
                // Fallback: clear shuffled tracks and reset index
                try
                {
                    shuffledTracks.Clear();
                    currentShuffledIndex = -1;
                }
                catch (Exception clearEx)
                {
                    Console.WriteLine($"Error clearing shuffled tracks: {clearEx.Message}");
                }
            }
        }

        /// <summary>
        /// Updates the shuffled tracks collection when filtered tracks change
        /// </summary>
        private void UpdateShuffledTracks()
        {
            if (titleBarPlayer.IsShuffleEnabled)
            {
                RegenerateShuffledTracks();
            }
        }

        /// <summary>
        /// Gets the current play queue (either filtered or shuffled based on shuffle state)
        /// </summary>
        private ObservableCollection<MusicTrack> GetCurrentPlayQueue()
        {
            try
            {
                var queue = titleBarPlayer.IsShuffleEnabled ? shuffledTracks : filteredTracks;
                
                // Safety check - ensure we have a valid queue
                if (queue == null)
                {
                    Console.WriteLine("GetCurrentPlayQueue: queue is null, falling back to filteredTracks");
                    queue = filteredTracks;
                }
                
                Console.WriteLine($"GetCurrentPlayQueue - shuffle enabled: {titleBarPlayer.IsShuffleEnabled}, queue count: {queue?.Count ?? 0}");
                return queue ?? new ObservableCollection<MusicTrack>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetCurrentPlayQueue: {ex.Message}");
                return filteredTracks ?? new ObservableCollection<MusicTrack>();
            }
        }

        /// <summary>
        /// Gets the current track index in the current play queue
        /// </summary>
        private int GetCurrentTrackIndex()
        {
            try
            {
                var index = titleBarPlayer.IsShuffleEnabled ? currentShuffledIndex : currentTrackIndex;
                Console.WriteLine($"GetCurrentTrackIndex - shuffle enabled: {titleBarPlayer.IsShuffleEnabled}, index: {index}");
                return index;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetCurrentTrackIndex: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// Sets the current track index in the current play queue
        /// </summary>
        private void SetCurrentTrackIndex(int index)
        {
            if (titleBarPlayer.IsShuffleEnabled)
            {
                currentShuffledIndex = index;
            }
            else
            {
                currentTrackIndex = index;
            }
        }

        /// <summary>
        /// Safely gets a track from the current play queue at the specified index
        /// </summary>
        private MusicTrack? GetTrackFromCurrentQueue(int index)
        {
            try
            {
                var queue = GetCurrentPlayQueue();
                if (queue != null && index >= 0 && index < queue.Count)
                {
                    var track = queue[index];
                    if (track != null && !string.IsNullOrEmpty(track.FilePath))
                    {
                        return track;
                    }
                    else
                    {
                        Console.WriteLine($"Track at index {index} is null or has invalid file path");
                    }
                }
                else
                {
                    Console.WriteLine($"Invalid index {index} or queue is null/empty (count: {queue?.Count ?? 0})");
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting track from queue at index {index}: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Title Bar Player Control Event Handlers

        private void TitleBarPlayer_PlayPauseRequested(object? sender, EventArgs e)
        {
            if (currentTrack == null)
            {
                if (filteredTracks.Count > 0)
                {
                    PlayTrack(filteredTracks[0]);
                }
                return;
            }

            if (titleBarPlayer.IsPlaying)
            {
                PausePlayback();
            }
            else
            {
                ResumePlayback();
            }
        }

        private void TitleBarPlayer_PreviousTrackRequested(object? sender, EventArgs e)
        {
            var currentQueue = GetCurrentPlayQueue();
            var currentIndex = GetCurrentTrackIndex();
            
            Console.WriteLine($"Previous track requested - currentIndex: {currentIndex}, queue count: {currentQueue.Count}");
            
            if (currentIndex > 0)
            {
                var previousTrack = GetTrackFromCurrentQueue(currentIndex - 1);
                if (previousTrack != null)
                {
                    PlayTrack(previousTrack);
                }
                else
                {
                    Console.WriteLine("Previous track is null or invalid, cannot play");
                }
            }
        }

        private void TitleBarPlayer_NextTrackRequested(object? sender, EventArgs e)
        {
            var currentQueue = GetCurrentPlayQueue();
            var currentIndex = GetCurrentTrackIndex();
            
            Console.WriteLine($"Next track requested - currentIndex: {currentIndex}, queue count: {currentQueue.Count}");
            
            if (currentIndex < currentQueue.Count - 1)
            {
                var nextTrack = GetTrackFromCurrentQueue(currentIndex + 1);
                if (nextTrack != null)
                {
                    PlayTrack(nextTrack);
                }
                else
                {
                    Console.WriteLine("Next track is null or has invalid file path, stopping playback");
                    // Clean up and reset to idle state
                    CleanupAudioObjects();
                    currentTrack = null;
                    currentTrackIndex = -1;
                    currentShuffledIndex = -1;
                    titleBarPlayer.SetTrackInfo("No track selected", "", "");
                }
            }
            else
            {
                // If it's the last track, stop playback and reset to idle state
                Console.WriteLine("Reached end of queue, stopping playback and resetting to idle state");
                
                // Clean up audio objects and reset to idle state
                CleanupAudioObjects();
                
                // Reset current track info
                currentTrack = null;
                currentTrackIndex = -1;
                currentShuffledIndex = -1;
                
                // Update title bar player to show no track is playing
                titleBarPlayer.SetTrackInfo("No track selected", "", "");
                
                Console.WriteLine("Playback stopped - app reset to idle state");
            }
        }

        private void TitleBarPlayer_WindowMinimizeRequested(object? sender, EventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void TitleBarPlayer_WindowMaximizeRequested(object? sender, EventArgs e)
        {
            if (isCustomMaximized)
            {
                // Restore to normal state
                RestoreWindow();
            }
            else
            {
                // Maximize to work area (excluding taskbar)
                MaximizeToWorkArea();
            }
        }

        /// <summary>
        /// Maximizes the window to the work area (screen area excluding taskbar)
        /// </summary>
        private void MaximizeToWorkArea()
        {
            // Store current window bounds for restoration only if they haven't been restored from settings yet
            if (!normalWindowBoundsRestored)
            {
                normalWindowBounds = new Rect(this.Left, this.Top, this.Width, this.Height);
            }
            
            // Get the work area (screen area excluding taskbar)
            RECT workArea = new RECT();
            if (SystemParametersInfo(SPI_GETWORKAREA, 0, ref workArea, 0))
            {
                // Ensure we're in normal state for custom positioning
                this.WindowState = WindowState.Normal;
                
                // Extend the window by 6 pixels on each side to compensate for gaps
                const int gapCompensation = 6;
                const int topExtraCompensation = 6;
                const int rightExtraCompensation = -12;
                const int bottomExtraCompensation = topExtraCompensation - 18;
                
                this.Left = workArea.Left;
                this.Top = workArea.Top;
                this.Width = (workArea.Right - workArea.Left) + (gapCompensation * 2) + rightExtraCompensation; 
                this.Height = (workArea.Bottom - workArea.Top) + (gapCompensation * 2) + bottomExtraCompensation;
                
                isCustomMaximized = true;
                titleBarPlayer.UpdateWindowStateIcon(WindowState.Maximized);
            }
            else
            {
                // Fallback to standard maximize
                this.WindowState = WindowState.Maximized;
                titleBarPlayer.UpdateWindowStateIcon(WindowState.Maximized);
            }
        }

        /// <summary>
        /// Restores the window to its previous normal state
        /// </summary>
        private void RestoreWindow()
        {
            // Ensure we're in normal state first
            this.WindowState = WindowState.Normal;
            
            // Restore to previous bounds
            this.Left = normalWindowBounds.Left;
            this.Top = normalWindowBounds.Top;
            this.Width = normalWindowBounds.Width;
            this.Height = normalWindowBounds.Height;
            
            isCustomMaximized = false;
            titleBarPlayer.UpdateWindowStateIcon(WindowState.Normal);
        }

        /// <summary>
        /// Checks if the window is currently maximized (either custom or standard)
        /// </summary>
        /// <returns>True if the window is maximized, false otherwise</returns>
        private bool IsWindowMaximized()
        {
            return isCustomMaximized || this.WindowState == WindowState.Maximized;
        }

        /// <summary>
        /// Updates the window state tracking when the window state changes externally
        /// </summary>
        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            
            // If the window state was changed externally (e.g., by double-clicking title bar),
            // update our custom tracking
            if (this.WindowState == WindowState.Normal && isCustomMaximized)
            {
                isCustomMaximized = false;
                titleBarPlayer.UpdateWindowStateIcon(WindowState.Normal);
            }
            else if (this.WindowState == WindowState.Maximized && !isCustomMaximized)
            {
                // If window was maximized externally, store current bounds and mark as custom maximized
                // Only update normalWindowBounds if they haven't been restored from settings yet
                if (!normalWindowBoundsRestored)
                {
                    normalWindowBounds = new Rect(this.Left, this.Top, this.Width, this.Height);
                }
                isCustomMaximized = true;
                titleBarPlayer.UpdateWindowStateIcon(WindowState.Maximized);
            }
        }

        /// <summary>
        /// Handles window location and size changes to update state tracking
        /// </summary>
        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            
            // If the window is moved while custom maximized, it should be restored
            if (isCustomMaximized && this.WindowState != WindowState.Maximized)
            {
                isCustomMaximized = false;
                titleBarPlayer.UpdateWindowStateIcon(WindowState.Normal);
            }
        }

        private void TitleBarPlayer_WindowCloseRequested(object? sender, EventArgs e)
        {
            this.Close();
        }

        private void TitleBarPlayer_ShuffleStateChanged(object? sender, bool isShuffleEnabled)
        {
            Console.WriteLine($"Shuffle state changed to: {isShuffleEnabled}");
            
            if (isShuffleEnabled)
            {
                // Shuffle was enabled - regenerate shuffled tracks
                Console.WriteLine("Shuffle enabled - regenerating shuffled tracks");
                RegenerateShuffledTracks();
            }
            else
            {
                // Shuffle was disabled - we need to find the current track in the original filtered list
                // and update the currentTrackIndex to maintain the current position
                Console.WriteLine("Shuffle disabled - updating current track index in filtered list");
                if (currentTrack != null)
                {
                    currentTrackIndex = filteredTracks.IndexOf(currentTrack);
                    if (currentTrackIndex == -1)
                    {
                        // If current track not found in filtered list, reset to beginning
                        currentTrackIndex = 0;
                    }
                    Console.WriteLine($"Current track index updated to: {currentTrackIndex}");
                }
            }
        }

        #endregion

        #region Navigation Events

        private void BtnLibrary_Click(object sender, RoutedEventArgs e)
        {
            ShowLibraryView();
        }

        private void BtnPlaylists_Click(object sender, RoutedEventArgs e)
        {
            ShowPlaylistsView();
        }

        private void BtnRecentlyPlayed_Click(object sender, RoutedEventArgs e)
        {
            ShowRecentlyPlayedView();
        }

        private async void BtnAddFolder_Click(object sender, RoutedEventArgs e)
        {
            await AddMusicFolderAsync();
        }

        private async void BtnRescanLibrary_Click(object sender, RoutedEventArgs e)
        {
            await RescanLibraryAsync();
        }

        private async void BtnRemoveFolder_Click(object sender, RoutedEventArgs e)
        {
            await RemoveMusicFolderAsync();
        }

        private void BtnClearSettings_Click(object sender, RoutedEventArgs e)
        {
            ClearSettings();
        }

        #endregion

        #region View Management

        private void ShowLibraryView()
        {
            libraryView.Visibility = Visibility.Visible;
            playlistsView.Visibility = Visibility.Collapsed;
            recentlyPlayedView.Visibility = Visibility.Collapsed;
        }

        private void ShowPlaylistsView()
        {
            libraryView.Visibility = Visibility.Collapsed;
            playlistsView.Visibility = Visibility.Visible;
            recentlyPlayedView.Visibility = Visibility.Collapsed;
        }

        private void ShowRecentlyPlayedView()
        {
            libraryView.Visibility = Visibility.Collapsed;
            playlistsView.Visibility = Visibility.Collapsed;
            recentlyPlayedView.Visibility = Visibility.Visible;
        }

        #endregion

        #region Music Management

        private async Task AddMusicFolderAsync()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select a folder containing music files"
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                await LoadMusicFromFolderAsync(dialog.SelectedPath, true);
            }
        }

        private async Task RescanLibraryAsync()
        {
            try
            {
                var musicFolders = await libraryManager.GetMusicFoldersAsync();
                if (musicFolders == null || musicFolders.Count == 0)
                {
                    System.Windows.MessageBox.Show("No music folders have been added yet.", "No Folders", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    return;
                }

                var totalNewTracks = 0;
                foreach (var folderPath in musicFolders)
                {
                    if (Directory.Exists(folderPath))
                    {
                        // Always re-scan the folder
                        await LoadMusicFromFolderAsync(folderPath, false);
                        totalNewTracks += allTracks.Count(t => t.FilePath.StartsWith(folderPath));
                    }
                }

                UpdateUI();
                System.Windows.MessageBox.Show($"Library re-scanned. Found {totalNewTracks} total tracks across all folders.", "Library Updated", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error re-scanning library: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task RemoveMusicFolderAsync()
        {
            try
            {
                var musicFolders = await libraryManager.GetMusicFoldersAsync();
                if (musicFolders == null || musicFolders.Count == 0)
                {
                    System.Windows.MessageBox.Show("No music folders have been added yet.", "No Folders", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    return;
                }

                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Select a folder to remove from the library",
                    ShowNewFolderButton = false
                };

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var folderToRemove = dialog.SelectedPath;
                    if (musicFolders.Contains(folderToRemove))
                    {
                        // Remove tracks from collections
                        var tracksToRemove = allTracks.Where(t => t.FilePath.StartsWith(folderToRemove)).ToList();
                        foreach (var track in tracksToRemove)
                        {
                            allTracks.Remove(track);
                            filteredTracks.Remove(track);
                            recentlyPlayed.Remove(track);
                        }

                        // Remove from playlists
                        foreach (var playlist in playlists)
                        {
                            var playlistTracksToRemove = playlist.Tracks.Where(t => t.FilePath.StartsWith(folderToRemove)).ToList();
                            foreach (var track in playlistTracksToRemove)
                            {
                                playlist.RemoveTrack(track);
                            }
                        }

                        // Remove from library manager
                        await libraryManager.RemoveMusicFolderAsync(folderToRemove);

                        // Remove from cache
                        await libraryManager.RemoveFolderFromCacheAsync(folderToRemove);

                        // Update UI
                        UpdateUI();

                        System.Windows.MessageBox.Show($"Folder '{folderToRemove}' and {tracksToRemove.Count} tracks removed from library.", "Folder Removed", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    }
                    else
                    {
                        System.Windows.MessageBox.Show($"Folder '{folderToRemove}' not found in library.", "Folder Not Found", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error removing folder: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task LoadMusicFromFolderAsync(string folderPath, bool saveToSettings = false)
        {
            try
            {
                var supportedExtensions = new[] { ".mp3", ".wav", ".flac", ".m4a", ".aac" };
                var musicFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                    .Where(file => supportedExtensions.Contains(Path.GetExtension(file).ToLower()));

                var newTracks = new List<MusicTrack>();
                var existingTracks = allTracks.Where(t => t.FilePath.StartsWith(folderPath)).ToList();

                foreach (var file in musicFiles)
                {
                    try
                    {
                        // Check if track already exists
                        var existingTrack = existingTracks.FirstOrDefault(t => t.FilePath == file);
                        if (existingTrack == null)
                        {
                            var track = LoadMusicTrack(file);
                            if (track != null)
                            {
                                newTracks.Add(track);
                                allTracks.Add(track);
                                filteredTracks.Add(track);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue with other files
                        Console.WriteLine($"Error loading {file}: {ex.Message}");
                    }
                }

                // Save to library manager if requested
                if (saveToSettings)
                {
                    await libraryManager.AddMusicFolderAsync(folderPath);
                }

                // Update library cache
                await UpdateLibraryCacheAsync();

                // Update folder scan time
                await libraryManager.UpdateFolderScanTimeAsync(folderPath);

                // Update shuffled tracks if shuffle is enabled
                UpdateShuffledTracks();

                if (newTracks.Count > 0)
                {
                    System.Windows.MessageBox.Show($"Loaded {newTracks.Count} new music files from the selected folder.", "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    System.Windows.MessageBox.Show("No new music files found in the selected folder.", "No New Files", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading music folder: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task UpdateLibraryCacheAsync()
        {
            try
            {
                var libraryCache = await libraryManager.LoadLibraryCacheAsync();
                libraryCache.Tracks = allTracks.ToList();
                await libraryManager.SaveLibraryCacheAsync(libraryCache);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating library cache: {ex.Message}");
            }
        }

        private MusicTrack? LoadMusicTrack(string filePath)
        {
            try
            {
                var track = new MusicTrack
                {
                    Title = "Unknown Title",
                    Artist = "Unknown Artist",
                    Album = "Unknown Album",
                    DurationTimeSpan = TimeSpan.Zero,
                    Duration = "00:00",
                    FilePath = filePath,
                    TrackNumber = 0,
                    Year = 0,
                    Genre = ""
                };

                // Use ATL.NET to read metadata
                try
                {
                    var atlTrack = new Track(filePath);
                    
                    // Extract metadata from ATL
                    if (!string.IsNullOrEmpty(atlTrack.Title))
                        track.Title = atlTrack.Title;
                    
                    if (!string.IsNullOrEmpty(atlTrack.Artist))
                        track.Artist = atlTrack.Artist;
                    
                    if (!string.IsNullOrEmpty(atlTrack.Album))
                        track.Album = atlTrack.Album;
                    
                    if (atlTrack.TrackNumber.HasValue && atlTrack.TrackNumber.Value > 0)
                        track.TrackNumber = atlTrack.TrackNumber.Value;
                    
                    if (atlTrack.Year.HasValue && atlTrack.Year.Value > 0)
                        track.Year = atlTrack.Year.Value;
                    
                    if (!string.IsNullOrEmpty(atlTrack.Genre))
                        track.Genre = atlTrack.Genre;
                    
                    // Get duration from ATL
                    if (atlTrack.Duration > 0)
                    {
                        track.DurationTimeSpan = TimeSpan.FromSeconds(atlTrack.Duration);
                        track.Duration = track.DurationTimeSpan.ToString(@"mm\:ss");
                    }
                    else
                    {
                        // Fallback to NAudio for duration
                        using var audioFile = new AudioFileReader(filePath);
                        track.DurationTimeSpan = audioFile.TotalTime;
                        track.Duration = audioFile.TotalTime.ToString(@"mm\:ss");
                    }

                    // Check for embedded album art
                    if (atlTrack.EmbeddedPictures != null && atlTrack.EmbeddedPictures.Count > 0)
                    {
                        track.AlbumArtPath = "embedded";
                    }

                    Console.WriteLine($"ATL metadata: Title='{track.Title}', Artist='{track.Artist}', Album='{track.Album}'");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ATL failed for {filePath}: {ex.Message}");
                    
                    // Fallback to NAudio for duration only
                    try
                    {
                        using var audioFile = new AudioFileReader(filePath);
                        track.DurationTimeSpan = audioFile.TotalTime;
                        track.Duration = audioFile.TotalTime.ToString(@"mm\:ss");
                    }
                    catch (Exception audioEx)
                    {
                        Console.WriteLine($"NAudio failed for {filePath}: {audioEx.Message}");
                    }
                }

                return track;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Playback Control

        private void LstMusic_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstMusic.SelectedItem is MusicTrack selectedTrack)
            {
                PlayTrack(selectedTrack);
            }
        }

        private void PlayTrack(MusicTrack track)
        {
            try
            {
                // Safety checks
                if (track == null)
                {
                    Console.WriteLine("PlayTrack called with null track");
                    return;
                }

                if (string.IsNullOrEmpty(track.FilePath))
                {
                    Console.WriteLine($"PlayTrack called with track '{track.Title}' that has no file path");
                    return;
                }

                if (!File.Exists(track.FilePath))
                {
                    Console.WriteLine($"PlayTrack called with track '{track.Title}' but file doesn't exist: {track.FilePath}");
                    return;
                }

                Console.WriteLine($"Playing track: {track.Title} - {track.Artist}");

                // Clean up existing audio objects without triggering PlaybackStopped
                CleanupAudioObjects();

                currentTrack = track;
                
                // Set the current track index in both queues
                currentTrackIndex = filteredTracks.IndexOf(track);
                currentShuffledIndex = shuffledTracks.IndexOf(track);
                
                // If shuffle is enabled and we don't have a valid shuffled index, regenerate shuffled tracks
                if (titleBarPlayer.IsShuffleEnabled && currentShuffledIndex == -1)
                {
                    RegenerateShuffledTracks();
                    currentShuffledIndex = shuffledTracks.IndexOf(track);
                }

                // Load album art if available
                var albumArt = LoadAlbumArt(track);

                // Update title bar player control
                titleBarPlayer.SetTrackInfo(track.Title, track.Artist, track.Album, albumArt);
                titleBarPlayer.SetAudioObjects(waveOut, audioFileReader);

                // Start playback
                audioFileReader = new AudioFileReader(track.FilePath);
                waveOut = new WaveOutEvent();
                waveOut.PlaybackStopped += WaveOut_PlaybackStopped;
                waveOut.Init(audioFileReader);
                waveOut.Play();

                // Update audio objects in control after creation
                titleBarPlayer.SetAudioObjects(waveOut, audioFileReader);
                titleBarPlayer.IsPlaying = true;

                // Add to recently played
                AddToRecentlyPlayed(track);

                // Update playlists view if it's visible
                if (playlistsView.Visibility == Visibility.Visible)
                {
                    UpdatePlaylistsView();
                }

                Console.WriteLine($"Successfully started playing: {track.Title}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error playing track '{track?.Title}': {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                
                // Try to show error to user
                try
                {
                    System.Windows.MessageBox.Show($"Error playing track: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
                catch
                {
                    // If showing message box fails, just log it
                    Console.WriteLine("Failed to show error message box");
                }
                
                // Try to stop playback safely
                try
                {
                    StopPlayback();
                }
                catch (Exception stopEx)
                {
                    Console.WriteLine($"Error stopping playback: {stopEx.Message}");
                }
            }
        }

        private BitmapImage? LoadAlbumArt(MusicTrack track)
        {
            try
            {
                // First try to load embedded album art using ATL.NET
                try
                {
                    var atlTrack = new Track(track.FilePath);
                    var embeddedPictures = atlTrack.EmbeddedPictures;

                    if (embeddedPictures != null && embeddedPictures.Count > 0)
                    {
                        var picture = embeddedPictures[0]; // Get the first picture (usually the album art)
                        var scaledBitmap = CreateHighQualityScaledImage(picture.PictureData);
                        return scaledBitmap; // Successfully loaded embedded art
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading embedded album art for {track.Title}: {ex.Message}");
                }

                // Fallback: Try to find album art in the same directory as the music file
                var directory = Path.GetDirectoryName(track.FilePath);
                if (directory != null)
                {
                    var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
                    var imageFiles = Directory.GetFiles(directory, "*.*")
                        .Where(file => imageExtensions.Contains(Path.GetExtension(file).ToLower()))
                        .ToList();

                    // Look for common album art filenames
                    var albumArtFile = imageFiles.FirstOrDefault(file =>
                    {
                        var fileName = Path.GetFileNameWithoutExtension(file).ToLower();
                        return fileName.Contains("album") || 
                               fileName.Contains("cover") || 
                               fileName.Contains("art") ||
                               fileName.Contains("folder");
                    });

                    // If no specific album art found, use the first image file
                    if (albumArtFile == null && imageFiles.Count > 0)
                    {
                        albumArtFile = imageFiles[0];
                    }

                    if (albumArtFile != null)
                    {
                        var scaledBitmap = CreateHighQualityScaledImageFromFile(albumArtFile);
                        return scaledBitmap;
                    }
                    else
                    {
                        // No album art found, return null
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                // If we can't load album art, return null
                Console.WriteLine($"Error loading album art for {track.Title}: {ex.Message}");
                return null;
            }
        }

        private BitmapImage? CreateHighQualityScaledImage(byte[] imageData)
        {
            try
            {
                using var originalStream = new MemoryStream(imageData);
                using var originalBitmap = new System.Drawing.Bitmap(originalStream);
                
                // Get the target size (assuming the Image control is around 60x60 pixels)
                int targetSize = 120; // Use 2x for high DPI displays
                
                // Calculate new dimensions maintaining aspect ratio
                int originalWidth = originalBitmap.Width;
                int originalHeight = originalBitmap.Height;
                
                double ratio = Math.Min((double)targetSize / originalWidth, (double)targetSize / originalHeight);
                int newWidth = (int)(originalWidth * ratio);
                int newHeight = (int)(originalHeight * ratio);
                
                // Create high-quality scaled bitmap (WPF will handle the rounded corners via clipping)
                using var scaledBitmap = new System.Drawing.Bitmap(newWidth, newHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using var graphics = System.Drawing.Graphics.FromImage(scaledBitmap);
                
                // Set high-quality rendering options
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                
                // Draw the scaled image
                graphics.DrawImage(originalBitmap, 0, 0, newWidth, newHeight);
                
                // Convert to WPF BitmapImage
                var wpfBitmap = new BitmapImage();
                using var stream = new MemoryStream();
                scaledBitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                stream.Position = 0;
                
                wpfBitmap.BeginInit();
                wpfBitmap.CacheOption = BitmapCacheOption.OnLoad;
                wpfBitmap.StreamSource = stream;
                wpfBitmap.EndInit();
                wpfBitmap.Freeze(); // Freeze for better performance
                
                return wpfBitmap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating high-quality scaled image: {ex.Message}");
                return null;
            }
        }

        private BitmapImage? CreateHighQualityScaledImageFromFile(string filePath)
        {
            try
            {
                using var originalBitmap = new System.Drawing.Bitmap(filePath);
                
                // Get the target size (assuming the Image control is around 60x60 pixels)
                int targetSize = 120; // Use 2x for high DPI displays
                
                // Calculate new dimensions maintaining aspect ratio
                int originalWidth = originalBitmap.Width;
                int originalHeight = originalBitmap.Height;
                
                double ratio = Math.Min((double)targetSize / originalWidth, (double)targetSize / originalHeight);
                int newWidth = (int)(originalWidth * ratio);
                int newHeight = (int)(originalHeight * ratio);
                
                // Create high-quality scaled bitmap (WPF will handle the rounded corners via clipping)
                using var scaledBitmap = new System.Drawing.Bitmap(newWidth, newHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using var graphics = System.Drawing.Graphics.FromImage(scaledBitmap);
                
                // Set high-quality rendering options
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                
                // Draw the scaled image
                graphics.DrawImage(originalBitmap, 0, 0, newWidth, newHeight);
                
                // Convert to WPF BitmapImage
                var wpfBitmap = new BitmapImage();
                using var stream = new MemoryStream();
                scaledBitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                stream.Position = 0;
                
                wpfBitmap.BeginInit();
                wpfBitmap.CacheOption = BitmapCacheOption.OnLoad;
                wpfBitmap.StreamSource = stream;
                wpfBitmap.EndInit();
                wpfBitmap.Freeze(); // Freeze for better performance
                
                return wpfBitmap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating high-quality scaled image from file: {ex.Message}");
                return null;
            }
        }



        private void AddToRecentlyPlayed(MusicTrack track)
        {
            // Mark track as played
            track.MarkAsPlayed();

            // Remove if already exists
            var existing = recentlyPlayed.FirstOrDefault(t => t.FilePath == track.FilePath);
            if (existing != null)
            {
                recentlyPlayed.Remove(existing);
            }

            // Add to beginning
            recentlyPlayed.Insert(0, track);

            // Keep only last 20 tracks
            while (recentlyPlayed.Count > 20)
            {
                recentlyPlayed.RemoveAt(recentlyPlayed.Count - 1);
            }
        }



        private void PausePlayback()
        {
            if (waveOut != null)
            {
                waveOut.Pause();
                titleBarPlayer.IsPlaying = false;
            }
        }

        private void ResumePlayback()
        {
            if (waveOut != null)
            {
                waveOut.Play();
                titleBarPlayer.IsPlaying = true;
            }
        }

        /// <summary>
        /// Safely resets the app to idle state (no track playing)
        /// </summary>
        private void ResetToIdleState()
        {
            try
            {
                Console.WriteLine("Resetting app to idle state...");
                
                // Reset current track info
                currentTrack = null;
                currentTrackIndex = -1;
                currentShuffledIndex = -1;
                
                // Update title bar player to show no track is playing
                titleBarPlayer.SetTrackInfo("No track selected", "", "");
                
                Console.WriteLine("App reset to idle state successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error resetting to idle state: {ex.Message}");
            }
        }

        /// <summary>
        /// Safely cleans up audio objects without triggering PlaybackStopped event
        /// </summary>
        private void CleanupAudioObjects()
        {
            try
            {
                Console.WriteLine("Cleaning up audio objects...");
                
                // Set the playing state to false
                titleBarPlayer.IsPlaying = false;
                
                if (waveOut != null)
                {
                    Console.WriteLine("Removing PlaybackStopped event handler and stopping waveOut");
                    // Remove the event handler before stopping to prevent triggering PlaybackStopped
                    waveOut.PlaybackStopped -= WaveOut_PlaybackStopped;
                    waveOut.Stop();
                    waveOut.Dispose();
                    waveOut = null;
                    Console.WriteLine("waveOut disposed");
                }

                if (audioFileReader != null)
                {
                    Console.WriteLine("Disposing audioFileReader");
                    audioFileReader.Dispose();
                    audioFileReader = null;
                    Console.WriteLine("audioFileReader disposed");
                }
                
                Console.WriteLine("Audio objects cleanup completed");
                
                // Reset app state to idle
                ResetToIdleState();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during audio cleanup: {ex.Message}");
            }
        }

        private void StopPlayback()
        {
            // Set flag to indicate we're manually stopping playback
            isManuallyStopping = true;
            
            try
            {
                // Use the safe cleanup method
                CleanupAudioObjects();
            }
            finally
            {
                // Reset flag after a short delay to allow for cleanup
                Task.Delay(100).ContinueWith(_ => isManuallyStopping = false);
            }
        }

        private void WaveOut_PlaybackStopped(object? sender, StoppedEventArgs e)
        {
            try
            {
                // Check if we're manually stopping playback
                if (isManuallyStopping)
                {
                    Console.WriteLine("Playback stopped manually, not advancing to next track");
                    return;
                }

                // Additional safety check: ensure we have valid audio objects
                if (waveOut == null || audioFileReader == null)
                {
                    Console.WriteLine("Audio objects are null, not advancing to next track");
                    return;
                }

                // Additional safety check: ensure the audio objects are still valid (not disposed)
                try
                {
                    var _ = audioFileReader.TotalTime;
                }
                catch (ObjectDisposedException)
                {
                    Console.WriteLine("AudioFileReader was disposed, not advancing to next track");
                    return;
                }
                catch (NullReferenceException)
                {
                    Console.WriteLine("AudioFileReader is null, not advancing to next track");
                    return;
                }

                // This event is raised when the audio playback finishes naturally.
                // Handle the track finished logic directly here
                var currentQueue = GetCurrentPlayQueue();
                var currentIndex = GetCurrentTrackIndex();

                Console.WriteLine($"Track finished naturally - currentIndex: {currentIndex}, queue count: {currentQueue.Count}");

                // Safety checks
                if (currentQueue == null || currentQueue.Count == 0)
                {
                    Console.WriteLine("No tracks in queue, stopping playback");
                    CleanupAudioObjects();
                    return;
                }

                if (currentIndex < 0 || currentIndex >= currentQueue.Count)
                {
                    Console.WriteLine($"Invalid current index: {currentIndex}, resetting to 0");
                    currentIndex = 0;
                }

                if (currentIndex < currentQueue.Count - 1)
                {
                    var nextTrack = GetTrackFromCurrentQueue(currentIndex + 1);
                    if (nextTrack != null)
                    {
                        Console.WriteLine($"Advancing to next track: {nextTrack.Title}");
                        PlayTrack(nextTrack);
                    }
                    else
                    {
                        Console.WriteLine("Next track is null or has invalid file path, stopping playback");
                        // Clean up and reset to idle state
                        CleanupAudioObjects();
                    }
                }
                else
                {
                    // If it's the last track, stop playback and reset to idle state
                    Console.WriteLine("Reached end of queue, stopping playback and resetting to idle state");
                    
                    // Clean up audio objects and reset to idle state
                    CleanupAudioObjects();
                    
                    Console.WriteLine("Playback stopped - app reset to idle state");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in WaveOut_PlaybackStopped: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                
                // Fallback: try to stop playback safely
                try
                {
                    CleanupAudioObjects();
                }
                catch (Exception stopEx)
                {
                    Console.WriteLine($"Error stopping playback: {stopEx.Message}");
                }
            }
        }

        #endregion

        #region Playlist Management

        private void UpdatePlaylistsView()
        {
            // This method can be expanded to show which playlists contain the current track
        }

        #endregion

        #region Settings Management

        private void ClearSettings()
        {
            try
            {
                // Show confirmation dialog
                var result = System.Windows.MessageBox.Show(
                    "This will clear all settings and return the app to a clean state. This action cannot be undone.\n\n" +
                    "The following will be cleared:\n" +
                    "• Music library cache\n" +
                    "• Recently played history\n" +
                    "• Playlists\n" +
                    "• Music folders\n" +
                    "• Window settings\n\n" +
                    "Are you sure you want to continue?",
                    "Clear Settings",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result != System.Windows.MessageBoxResult.Yes)
                {
                    return;
                }

                // Get the AppData\Roaming\MusicApp directory
                var appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MusicApp");

                if (!Directory.Exists(appDataPath))
                {
                    System.Windows.MessageBox.Show("No settings found to clear.", "No Settings", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    return;
                }

                // Find all JSON files in the directory
                var jsonFiles = Directory.GetFiles(appDataPath, "*.json", SearchOption.TopDirectoryOnly);
                
                if (jsonFiles.Length == 0)
                {
                    System.Windows.MessageBox.Show("No settings files found to clear.", "No Settings", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    return;
                }

                // Move files to recycle bin
                int movedFiles = 0;
                foreach (var file in jsonFiles)
                {
                    try
                    {
                        // Use Windows API to move to recycle bin
                        if (MoveToRecycleBin(file))
                        {
                            movedFiles++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error moving file {file} to recycle bin: {ex.Message}");
                    }
                }

                // Clear in-memory collections
                allTracks.Clear();
                filteredTracks.Clear();
                shuffledTracks.Clear();
                playlists.Clear();
                recentlyPlayed.Clear();

                // Reset current track
                currentTrack = null;
                currentTrackIndex = -1;
                currentShuffledIndex = -1;

                // Stop any current playback
                StopPlayback();

                // Clear title bar player track info
                titleBarPlayer.SetTrackInfo("No track selected", "", "");

                // Reset window state to default
                appSettings = new SettingsManager.AppSettings();
                this.Width = 1200;
                this.Height = 700;
                this.Left = 100;
                this.Top = 100;
                this.WindowState = WindowState.Normal;
                isCustomMaximized = false;
                titleBarPlayer.UpdateWindowStateIcon(WindowState.Normal);

                // Update UI
                UpdateUI();

                // Show success message
                System.Windows.MessageBox.Show(
                    $"Successfully cleared {movedFiles} settings files.\n\n" +
                    "The app has been reset to a clean state.",
                    "Settings Cleared",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error clearing settings: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private bool MoveToRecycleBin(string filePath)
        {
            try
            {
                // Use Windows API to move file to recycle bin
                var shf = new SHFILEOPSTRUCT
                {
                    wFunc = FO_DELETE,
                    pFrom = filePath + '\0' + '\0', // Double null-terminated string
                    fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT,
                    fAnyOperationsAborted = false
                };

                int result = SHFileOperation(ref shf);
                return result == 0;
            }
            catch
            {
                // Fallback: try to delete the file directly
                try
                {
                    File.Delete(filePath);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        #endregion





        protected override async void OnClosing(CancelEventArgs e)
        {
            try
            {
                // Get current player settings from the title bar player control
                appSettings.Player = new SettingsManager.PlayerSettings
                {
                    IsShuffleEnabled = titleBarPlayer.IsShuffleEnabled,
                    RepeatMode = titleBarPlayer.RepeatMode
                };

                // Save current window state - always save the normal window bounds, not the current maximized dimensions
                appSettings.WindowState = new SettingsManager.WindowStateSettings
                {
                    IsMaximized = IsWindowMaximized(),
                    Width = normalWindowBounds.Width,
                    Height = normalWindowBounds.Height,
                    Left = normalWindowBounds.Left,
                    Top = normalWindowBounds.Top
                };

                // Save settings
                await settingsManager.SaveSettingsAsync(appSettings);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings on close: {ex.Message}");
            }

            StopPlayback();
            base.OnClosing(e);
        }
    }
}
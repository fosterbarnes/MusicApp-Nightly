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
using System.Windows.Threading;
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



        // ===========================================
        // WINDOW MANAGEMENT
        // ===========================================
        private WindowManager windowManager;

        // ===========================================
        // DATA COLLECTIONS
        // ===========================================
        private ObservableCollection<Song> allTracks = new ObservableCollection<Song>();
        private ObservableCollection<Song> filteredTracks = new ObservableCollection<Song>();
        private ObservableCollection<Song> shuffledTracks = new ObservableCollection<Song>();
        private ObservableCollection<Playlist> playlists = new ObservableCollection<Playlist>();
        private ObservableCollection<Song> recentlyPlayed = new ObservableCollection<Song>();
        
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
        private Song? currentTrack;
        private bool isManuallyStopping = false; // Flag to prevent infinite loops during manual stops
        private bool isManualNavigation = false; // Flag to differentiate between natural progression and manual navigation

        // ===========================================
        // CONSTRUCTOR AND INITIALIZATION
        // ===========================================
        public MainWindow()
        {
            InitializeComponent();
            
            // Initialize window manager
            windowManager = new WindowManager(this, titleBarPlayer);
            
            SetupEventHandlers();
            
            // Load saved data asynchronously
            _ = LoadSavedDataAsync();
            
            // Initialize window state tracking
            windowManager.InitializeWindowState();
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
                windowManager.RestoreWindowState(
                    appSettings.WindowState.Width,
                    appSettings.WindowState.Height,
                    appSettings.WindowState.Left,
                    appSettings.WindowState.Top,
                    appSettings.WindowState.IsMaximized
                );
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
                Console.WriteLine($"After loading from cache - allTracks: {allTracks.Count}, filteredTracks: {filteredTracks.Count}");
                UpdateShuffledTracks();
                
                // Update queue view if it's currently visible
                if (queueView.Visibility == Visibility.Visible)
                {
                    UpdateQueueView();
                }
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
            
            // Refresh queue view if it's visible
            if (queueView.Visibility == Visibility.Visible)
            {
                UpdateQueueView();
            }
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
            
            // Initialize queue view
            lstQueue.ItemsSource = new ObservableCollection<Song>();

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
        /// This should only be called when shuffle is first enabled or when explicitly requested
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
                Console.WriteLine($"Processing {filteredTracks.Count} tracks from filteredTracks");
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
                Console.WriteLine($"Shuffle queue now contains: {string.Join(", ", shuffledTracks.Take(5).Select(t => t.Title))}... (and {shuffledTracks.Count - 5} more)");
                
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
                
                // If we have a current track, ensure it's at the beginning of the shuffled queue
                if (currentTrack != null)
                {
                    // Find the current track in the shuffled list
                    int trackIndex = shuffledTracks.IndexOf(currentTrack);
                    if (trackIndex > 0)
                    {
                        // Move the current track to the beginning
                        var trackToMove = shuffledTracks[trackIndex];
                        shuffledTracks.RemoveAt(trackIndex);
                        shuffledTracks.Insert(0, trackToMove);
                        Console.WriteLine($"Moved current track '{currentTrack.Title}' to beginning of shuffled queue");
                    }
                    else if (trackIndex == 0)
                    {
                        Console.WriteLine($"Current track '{currentTrack.Title}' is already at beginning of shuffled queue");
                    }
                    else
                    {
                        Console.WriteLine($"Current track '{currentTrack.Title}' not found in shuffled list, this shouldn't happen");
                    }
                    
                    // Set the current shuffled index to 0 since the current track is now first
                    currentShuffledIndex = 0;
                }
                else
                {
                    currentShuffledIndex = -1;
                }
                
                Console.WriteLine($"Shuffled tracks generated - shuffledTracks.Count: {shuffledTracks.Count}, currentShuffledIndex: {currentShuffledIndex}");
                Console.WriteLine($"Shuffle queue order: {string.Join(" -> ", shuffledTracks.Take(5).Select(t => t.Title))}...");
                
                // Update queue view if it's currently visible
                if (queueView.Visibility == Visibility.Visible)
                {
                    UpdateQueueView();
                }
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
        /// Ensures the shuffled tracks collection is properly initialized when shuffle is enabled
        /// This method maintains the existing shuffled order if possible, only regenerating when necessary
        /// </summary>
        private void EnsureShuffledTracksInitialized()
        {
            try
            {
                if (!titleBarPlayer.IsShuffleEnabled)
                {
                    return; // Shuffle not enabled, no need to initialize
                }

                Console.WriteLine($"EnsureShuffledTracksInitialized called - shuffledTracks.Count: {shuffledTracks.Count}, currentShuffledIndex: {currentShuffledIndex}");
                
                // If we don't have shuffled tracks or they don't match the current filtered tracks, regenerate
                if (shuffledTracks.Count == 0 || shuffledTracks.Count != filteredTracks.Count)
                {
                    Console.WriteLine("Shuffled tracks count mismatch or empty, regenerating");
                    RegenerateShuffledTracks();
                    return;
                }

                // If current track is not in shuffled tracks, regenerate
                if (currentTrack != null && shuffledTracks.IndexOf(currentTrack) == -1)
                {
                    Console.WriteLine("Current track not found in shuffled tracks, regenerating");
                    RegenerateShuffledTracks();
                    return;
                }

                // If we have a valid current track but no valid shuffled index, find it
                if (currentTrack != null && currentShuffledIndex == -1)
                {
                    currentShuffledIndex = shuffledTracks.IndexOf(currentTrack);
                    if (currentShuffledIndex == -1)
                    {
                        Console.WriteLine("Could not find current track in shuffled tracks, regenerating");
                        RegenerateShuffledTracks();
                    }
                    else
                    {
                        Console.WriteLine($"Found current track in shuffled tracks at index: {currentShuffledIndex}");
                    }
                }

                Console.WriteLine($"Shuffled tracks properly initialized - shuffledTracks.Count: {shuffledTracks.Count}, currentShuffledIndex: {currentShuffledIndex}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in EnsureShuffledTracksInitialized: {ex.Message}");
                // Fallback: regenerate shuffled tracks
                RegenerateShuffledTracks();
            }
        }

        /// <summary>
        /// Updates the shuffled tracks collection when filtered tracks change
        /// This should only be called when the library content changes, not during normal playback
        /// </summary>
        private void UpdateShuffledTracks()
        {
            Console.WriteLine($"UpdateShuffledTracks called - shuffle enabled: {titleBarPlayer.IsShuffleEnabled}, filteredTracks: {filteredTracks.Count}, shuffledTracks: {shuffledTracks.Count}");
            
            if (titleBarPlayer.IsShuffleEnabled)
            {
                // Only regenerate if the library content has actually changed
                if (shuffledTracks.Count != filteredTracks.Count)
                {
                    Console.WriteLine("Library content changed, regenerating shuffled tracks");
                    RegenerateShuffledTracks();
                }
                else
                {
                    Console.WriteLine("Library content unchanged, maintaining existing shuffled order");
                    EnsureShuffledTracksInitialized();
                }
            }
        }

        /// <summary>
        /// Gets the current play queue (either filtered or shuffled based on shuffle state)
        /// </summary>
        private ObservableCollection<Song> GetCurrentPlayQueue()
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
                
                Console.WriteLine($"GetCurrentPlayQueue - shuffle enabled: {titleBarPlayer.IsShuffleEnabled}, queue count: {queue?.Count ?? 0}, filteredTracks: {filteredTracks.Count}, shuffledTracks: {shuffledTracks.Count}");
                
                if (titleBarPlayer.IsShuffleEnabled && shuffledTracks.Count > 0)
                {
                    Console.WriteLine($"Shuffle queue sample: {string.Join(", ", shuffledTracks.Take(3).Select(t => t.Title))}...");
                }
                
                return queue ?? new ObservableCollection<Song>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetCurrentPlayQueue: {ex.Message}");
                return filteredTracks ?? new ObservableCollection<Song>();
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
        private Song? GetTrackFromCurrentQueue(int index)
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
            
            // Set manual navigation flag
            isManualNavigation = true;
            
            // Get current playback position
            var currentPosition = titleBarPlayer.CurrentPosition;
            Console.WriteLine($"Current playback position: {currentPosition.TotalSeconds:F1} seconds");
            
            // Store current playback state to preserve it
            bool wasPlaying = titleBarPlayer.IsPlaying;
            
            // If we're 3 or more seconds into the song, restart the current song
            if (currentPosition.TotalSeconds >= 3.0)
            {
                Console.WriteLine("Restarting current track (3+ seconds elapsed)");
                if (currentTrack != null)
                {
                    // Load the track without starting playback, then restore the previous state
                    LoadTrackWithoutPlayback(currentTrack);
                    // If it was playing before, start playback now
                    if (wasPlaying)
                    {
                        ResumePlayback();
                    }
                }
            }
            // If we're 2 seconds or less into the song, go to previous track
            else if (currentPosition.TotalSeconds <= 2.0 && currentIndex > 0)
            {
                Console.WriteLine("Going to previous track (2 seconds or less elapsed)");
                var previousTrack = GetTrackFromCurrentQueue(currentIndex - 1);
                if (previousTrack != null)
                {
                    // Load the previous track without starting playback, then restore the previous state
                    LoadTrackWithoutPlayback(previousTrack);
                    // If it was playing before, start playback now
                    if (wasPlaying)
                    {
                        ResumePlayback();
                    }
                }
                else
                {
                    Console.WriteLine("Previous track is null or invalid, cannot play");
                    // Update queue view if it's visible
                    if (queueView.Visibility == Visibility.Visible)
                    {
                        UpdateQueueView();
                    }
                }
            }
            // If we're between 2-3 seconds, restart the current song (edge case)
            else
            {
                Console.WriteLine("Restarting current track (between 2-3 seconds elapsed)");
                if (currentTrack != null)
                {
                    // Load the track without starting playback, then restore the previous state
                    LoadTrackWithoutPlayback(currentTrack);
                    // If it was playing before, start playback now
                    if (wasPlaying)
                    {
                        ResumePlayback();
                    }
                }
            }
            
            // Reset manual navigation flag after a short delay
            Task.Delay(100).ContinueWith(_ => isManualNavigation = false);
        }

        private void TitleBarPlayer_NextTrackRequested(object? sender, EventArgs e)
        {
            var currentQueue = GetCurrentPlayQueue();
            var currentIndex = GetCurrentTrackIndex();
            
            Console.WriteLine($"Next track requested - currentIndex: {currentIndex}, queue count: {currentQueue.Count}");
            
            // Set manual navigation flag
            isManualNavigation = true;
            
            if (currentIndex < currentQueue.Count - 1)
            {
                var nextTrack = GetTrackFromCurrentQueue(currentIndex + 1);
                if (nextTrack != null)
                {
                    // Store current playback state to preserve it
                    bool wasPlaying = titleBarPlayer.IsPlaying;
                    
                    // Load the next track without starting playback, then restore the previous state
                    LoadTrackWithoutPlayback(nextTrack);
                    // If it was playing before, start playback now
                    if (wasPlaying)
                    {
                        ResumePlayback();
                    }
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
                    
                    // Update queue view if it's visible
                    if (queueView.Visibility == Visibility.Visible)
                    {
                        UpdateQueueView();
                    }
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
                
                // Update queue view if it's visible
                    if (queueView.Visibility == Visibility.Visible)
                    {
                        UpdateQueueView();
                    }
                
                Console.WriteLine("Playback stopped - app reset to idle state");
            }
            
            // Reset manual navigation flag after a short delay
            Task.Delay(100).ContinueWith(_ => isManualNavigation = false);
        }

        private void TitleBarPlayer_WindowMinimizeRequested(object? sender, EventArgs e)
        {
            windowManager.MinimizeWindow();
        }

        private void TitleBarPlayer_WindowMaximizeRequested(object? sender, EventArgs e)
        {
            windowManager.ToggleMaximize();
        }





        /// <summary>
        /// Updates the window state tracking when the window state changes externally
        /// </summary>
        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            windowManager.OnStateChanged();
        }

        /// <summary>
        /// Handles window activation to restore custom window style after minimize/restore operations
        /// </summary>
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            
            // Restore custom window style after minimize/restore operations
            if (WindowStyle != WindowStyle.None)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, (DispatcherOperationCallback)delegate (object unused)
                {
                    WindowStyle = WindowStyle.None;
                    
                    // After restoring the window style, check if the window is visually maximized
                    // This helps fix the issue where minimize/restore of maximized windows
                    // doesn't properly update the maximize button icon
                    windowManager.CheckIfWindowIsVisuallyMaximized();
                    
                    return null;
                }, null);
            }
        }

        /// <summary>
        /// Handles window location and size changes to update state tracking
        /// </summary>
        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            windowManager.OnLocationChanged();
        }

        private void TitleBarPlayer_WindowCloseRequested(object? sender, EventArgs e)
        {
            windowManager.CloseWindow();
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
            
            // Update queue view if it's currently visible
            if (queueView.Visibility == Visibility.Visible)
            {
                UpdateQueueView();
            }
        }

        #endregion

        #region Navigation Events

        private void BtnLibrary_Click(object sender, RoutedEventArgs e)
        {
            ShowLibraryView();
        }

        private void BtnQueue_Click(object sender, RoutedEventArgs e)
        {
            ShowQueueView();
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
            queueView.Visibility = Visibility.Collapsed;
            playlistsView.Visibility = Visibility.Collapsed;
            recentlyPlayedView.Visibility = Visibility.Collapsed;
        }

        private void ShowQueueView()
        {
            libraryView.Visibility = Visibility.Collapsed;
            queueView.Visibility = Visibility.Visible;
            playlistsView.Visibility = Visibility.Collapsed;
            recentlyPlayedView.Visibility = Visibility.Collapsed;
            
            // Update the queue list with current playing queue
            UpdateQueueView();
        }

        private void ShowPlaylistsView()
        {
            libraryView.Visibility = Visibility.Collapsed;
            queueView.Visibility = Visibility.Collapsed;
            playlistsView.Visibility = Visibility.Visible;
            recentlyPlayedView.Visibility = Visibility.Collapsed;
        }

        private void ShowRecentlyPlayedView()
        {
            libraryView.Visibility = Visibility.Collapsed;
            queueView.Visibility = Visibility.Collapsed;
            playlistsView.Visibility = Visibility.Collapsed;
            recentlyPlayedView.Visibility = Visibility.Visible;
        }

        #endregion

        #region Queue Management

        /// <summary>
        /// Updates the queue view with the current playing queue
        /// </summary>
        private void UpdateQueueView()
        {
            try
            {
                Console.WriteLine("UpdateQueueView called");
                
                var queueView = BuildQueueView();
                Console.WriteLine($"Built queue view with {queueView?.Count ?? 0} songs");
                
                if (queueView != null && queueView.Count > 0)
                {
                    Console.WriteLine($"Setting lstQueue.ItemsSource to queue with {queueView.Count} songs");
                    lstQueue.ItemsSource = queueView;
                }
                else
                {
                    Console.WriteLine("Queue view is empty, setting lstQueue.ItemsSource to empty collection");
                    lstQueue.ItemsSource = new ObservableCollection<Song>();
                }
                
                // Log the current state
                var currentQueue = GetCurrentPlayQueue();
                Console.WriteLine($"Current queue state - filteredTracks: {filteredTracks.Count}, shuffledTracks: {shuffledTracks.Count}, currentTrack: {currentTrack?.Title ?? "None"}, currentIndex: {GetCurrentTrackIndex()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating queue view: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                lstQueue.ItemsSource = new ObservableCollection<Song>();
            }
        }

        /// <summary>
        /// Builds a proper queue view with currently playing song at the top
        /// </summary>
        private ObservableCollection<Song> BuildQueueView()
        {
            try
            {
                Console.WriteLine("BuildQueueView called");
                var queueView = new ObservableCollection<Song>();
                
                // Get the current play queue
                var currentQueue = GetCurrentPlayQueue();
                var currentIndex = GetCurrentTrackIndex();
                
                Console.WriteLine($"BuildQueueView - currentQueue: {currentQueue?.Count ?? 0}, currentIndex: {currentIndex}, currentTrack: {currentTrack?.Title ?? "None"}");
                
                if (currentQueue == null || currentQueue.Count == 0)
                {
                    // No queue available, show empty queue
                    Console.WriteLine("BuildQueueView - No queue available, returning empty queue");
                    return queueView;
                }

                if (currentTrack != null && currentIndex >= 0)
                {
                    Console.WriteLine($"BuildQueueView - Building queue with current track: {currentTrack.Title} at index {currentIndex}");
                    
                    if (titleBarPlayer.IsShuffleEnabled)
                    {
                        // For shuffle mode, show current track at top, then remaining tracks in shuffled order
                        // First add the current track
                        queueView.Add(currentTrack);
                        Console.WriteLine($"BuildQueueView - Added current shuffled track at top: {currentTrack.Title}");
                        
                        // Then add remaining tracks from current position onwards (skip previously played)
                        if (currentIndex < currentQueue.Count - 1)
                        {
                            for (int i = currentIndex + 1; i < currentQueue.Count; i++)
                            {
                                var track = currentQueue[i];
                                if (track != null && !string.IsNullOrEmpty(track.FilePath))
                                {
                                    queueView.Add(track);
                                    Console.WriteLine($"BuildQueueView - Added remaining shuffled track: {track.Title} at position {i}");
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine("BuildQueueView - No remaining tracks after current track in shuffle mode");
                        }
                    }
                    else
                    {
                        // For normal mode, show current track at top followed by remaining tracks
                        // Add the currently playing song at the top
                        queueView.Add(currentTrack);

                        // Add the remaining songs in order (from current position + 1 to end)
                        // Only add if there are actually songs after the current one
                        if (currentIndex < currentQueue.Count - 1)
                        {
                            for (int i = currentIndex + 1; i < currentQueue.Count; i++)
                            {
                                var track = currentQueue[i];
                                if (track != null && !string.IsNullOrEmpty(track.FilePath))
                                {
                                    queueView.Add(track);
                                    Console.WriteLine($"BuildQueueView - Added remaining track: {track.Title}");
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine("BuildQueueView - No remaining tracks after current track");
                        }
                    }
                }
                else
                {
                    // No current track playing, show empty queue when app is in idle state
                    Console.WriteLine("BuildQueueView - No current track playing, returning empty queue (app is idle)");
                    return queueView;
                }

                Console.WriteLine($"Built queue view - current track: {currentTrack?.Title ?? "None"} (index {currentIndex}), total songs: {queueView.Count}, queue size: {currentQueue.Count}");
                return queueView;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error building queue view: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ObservableCollection<Song>();
            }
        }

        /// <summary>
        /// Gets the actual playback queue (the songs that will actually be played)
        /// This is different from the full library - it represents the current play session
        /// </summary>
        private ObservableCollection<Song> GetActualPlaybackQueue()
        {
            try
            {
                // If we have a current track, the playback queue should only include
                // songs from the current track's position to the end of the current queue
                var currentQueue = GetCurrentPlayQueue();
                var currentIndex = GetCurrentTrackIndex();
                
                if (currentQueue == null || currentQueue.Count == 0 || currentIndex < 0)
                {
                    return new ObservableCollection<Song>();
                }

                // Create a new collection with only the songs that will actually be played
                var playbackQueue = new ObservableCollection<Song>();
                
                // Add songs from current position to end (these are the songs that will actually play)
                for (int i = currentIndex; i < currentQueue.Count; i++)
                {
                    var track = currentQueue[i];
                    if (track != null && !string.IsNullOrEmpty(track.FilePath))
                    {
                        playbackQueue.Add(track);
                    }
                }

                Console.WriteLine($"GetActualPlaybackQueue - current index: {currentIndex}, total queue: {currentQueue.Count}, playback queue: {playbackQueue.Count}");
                return playbackQueue;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting actual playback queue: {ex.Message}");
                return new ObservableCollection<Song>();
            }
        }

        /// <summary>
        /// Event handler for queue list selection changes
        /// </summary>
        private void LstQueue_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstQueue.SelectedItem is Song selectedSong)
            {
                // When selecting from queue view, jump to the selected track without regenerating shuffle queue
                var currentQueue = GetCurrentPlayQueue();
                if (currentQueue != null)
                {
                    int queueIndex = currentQueue.IndexOf(selectedSong);
                    if (queueIndex >= 0)
                    {
                        Console.WriteLine($"Queue selection: jumping to track '{selectedSong.Title}' at index {queueIndex} in existing queue");
                        
                        // Set manual navigation flag to prevent shuffle queue regeneration
                        isManualNavigation = true;
                        
                        // Start playing the selected track immediately
                        PlayTrack(selectedSong);
                        
                        // Clear the selection to avoid confusion
                        lstQueue.SelectedItem = null;
                    }
                }
            }
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

                        // Update shuffled tracks if shuffle is enabled
                        UpdateShuffledTracks();

                        // Update queue view if it's currently visible
                        if (queueView.Visibility == Visibility.Visible)
                        {
                            UpdateQueueView();
                        }

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

                var newTracks = new List<Song>();
                var existingTracks = allTracks.Where(t => t.FilePath.StartsWith(folderPath)).ToList();

                foreach (var file in musicFiles)
                {
                    try
                    {
                        // Check if track already exists
                        var existingTrack = existingTracks.FirstOrDefault(t => t.FilePath == file);
                        if (existingTrack == null)
                        {
                            var track = LoadSong(file);
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
                Console.WriteLine($"After loading from folder - allTracks: {allTracks.Count}, filteredTracks: {filteredTracks.Count}");
                UpdateShuffledTracks();

                // Update queue view if it's currently visible
                if (queueView.Visibility == Visibility.Visible)
                {
                    UpdateQueueView();
                }

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

        private Song? LoadSong(string filePath)
        {
            try
            {
                var track = new Song
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
            if (lstMusic.SelectedItem is Song selectedTrack)
            {
                // When selecting from library view, always regenerate shuffle queue if shuffle is enabled
                PlayTrack(selectedTrack);
            }
        }

        private void PlayTrack(Song track)
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
                // We need to do this manually to avoid resetting the current track
                try
                {
                    Console.WriteLine("Cleaning up audio objects...");
                    
                    // Set the playing state to false temporarily
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
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during audio cleanup: {ex.Message}");
                }

                currentTrack = track;
                
                // Set the current track index in both queues
                currentTrackIndex = filteredTracks.IndexOf(track);
                currentShuffledIndex = shuffledTracks.IndexOf(track);
                
                // If shuffle is enabled and this is NOT manual navigation, regenerate shuffled queue
                // Manual navigation (skip forward/backward) should maintain the existing shuffled order
                if (titleBarPlayer.IsShuffleEnabled && !isManualNavigation)
                {
                    Console.WriteLine("Shuffle enabled and not manual navigation - regenerating shuffled queue");
                    RegenerateShuffledTracks();
                    currentShuffledIndex = shuffledTracks.IndexOf(track);
                }
                else if (titleBarPlayer.IsShuffleEnabled && isManualNavigation)
                {
                    Console.WriteLine("Shuffle enabled but manual navigation - maintaining existing shuffled queue");
                    // Just update the index to the new track position
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

                // Update queue view if it's visible
                if (queueView.Visibility == Visibility.Visible)
                {
                    UpdateQueueView();
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

        /// <summary>
        /// Loads a track without starting playback - useful for track navigation while preserving play state
        /// </summary>
        private void LoadTrackWithoutPlayback(Song track)
        {
            try
            {
                // Safety checks
                if (track == null)
                {
                    Console.WriteLine("LoadTrackWithoutPlayback called with null track");
                    return;
                }

                if (string.IsNullOrEmpty(track.FilePath))
                {
                    Console.WriteLine($"LoadTrackWithoutPlayback called with track '{track.Title}' that has no file path");
                    return;
                }

                if (!File.Exists(track.FilePath))
                {
                    Console.WriteLine($"LoadTrackWithoutPlayback called with track '{track.Title}' but file doesn't exist: {track.FilePath}");
                    return;
                }

                Console.WriteLine($"Loading track without playback: {track.Title} - {track.Artist}");

                // Store current playback state
                bool wasPlaying = titleBarPlayer.IsPlaying;

                // Clean up existing audio objects without triggering PlaybackStopped
                CleanupAudioObjects();

                currentTrack = track;
                
                // Set the current track index in both queues
                currentTrackIndex = filteredTracks.IndexOf(track);
                currentShuffledIndex = shuffledTracks.IndexOf(track);
                
                // If shuffle is enabled and this is NOT manual navigation, regenerate shuffled queue
                // Manual navigation (skip forward/backward) should maintain the existing shuffled order
                if (titleBarPlayer.IsShuffleEnabled && !isManualNavigation)
                {
                    Console.WriteLine("Shuffle enabled and not manual navigation - regenerating shuffled queue");
                    RegenerateShuffledTracks();
                    currentShuffledIndex = shuffledTracks.IndexOf(track);
                }
                else if (titleBarPlayer.IsShuffleEnabled && isManualNavigation)
                {
                    Console.WriteLine("Shuffle enabled but manual navigation - maintaining existing shuffled queue");
                    // Just update the index to the new track position
                    currentShuffledIndex = shuffledTracks.IndexOf(track);
                }

                // Load album art if available
                var albumArt = LoadAlbumArt(track);

                // Update title bar player control
                titleBarPlayer.SetTrackInfo(track.Title, track.Artist, track.Album, albumArt);

                // Create audio objects but don't start playback
                audioFileReader = new AudioFileReader(track.FilePath);
                waveOut = new WaveOutEvent();
                waveOut.PlaybackStopped += WaveOut_PlaybackStopped;
                waveOut.Init(audioFileReader);

                // Update audio objects in control
                titleBarPlayer.SetAudioObjects(waveOut, audioFileReader);
                
                // Restore the previous playback state
                titleBarPlayer.IsPlaying = wasPlaying;

                // Add to recently played
                AddToRecentlyPlayed(track);

                // Update playlists view if it's visible
                if (playlistsView.Visibility == Visibility.Visible)
                {
                    UpdatePlaylistsView();
                }

                // Update queue view if it's visible
                if (queueView.Visibility == Visibility.Visible)
                {
                    UpdateQueueView();
                }

                Console.WriteLine($"Successfully loaded track without playback: {track.Title}, playback state: {titleBarPlayer.IsPlaying}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading track '{track?.Title}' without playback: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                
                // Try to show error to user
                try
                {
                    System.Windows.MessageBox.Show($"Error loading track: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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

        private BitmapImage? LoadAlbumArt(Song track)
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



        private void AddToRecentlyPlayed(Song track)
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
                
                // Update queue view if it's visible
                if (queueView.Visibility == Visibility.Visible)
                {
                    UpdateQueueView();
                }
                
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
                        
                        // Update queue view if it's visible
                        if (queueView.Visibility == Visibility.Visible)
                        {
                            UpdateQueueView();
                        }
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
                windowManager.ResetWindowState();

                // Update queue view if it's visible
                if (queueView.Visibility == Visibility.Visible)
                {
                    UpdateQueueView();
                }

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
                    IsMaximized = windowManager.IsWindowMaximized(),
                    Width = windowManager.NormalWindowBounds.Width,
                    Height = windowManager.NormalWindowBounds.Height,
                    Left = windowManager.NormalWindowBounds.Left,
                    Top = windowManager.NormalWindowBounds.Top
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
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
using NAudio.Wave;
using ATL;



namespace MusicApp
{
    public partial class MainWindow : Window
    {
        // ===========================================
        // DATA COLLECTIONS
        // ===========================================
        private ObservableCollection<MusicTrack> allTracks = new ObservableCollection<MusicTrack>();
        private ObservableCollection<MusicTrack> filteredTracks = new ObservableCollection<MusicTrack>();
        private ObservableCollection<Playlist> playlists = new ObservableCollection<Playlist>();
        private ObservableCollection<MusicTrack> recentlyPlayed = new ObservableCollection<MusicTrack>();
        
        // ===========================================
        // AUDIO PLAYBACK STATE
        // ===========================================
        private WaveOutEvent? waveOut;
        private AudioFileReader? audioFileReader;
        private int currentTrackIndex = -1;
        private MusicTrack? currentTrack;

        // ===========================================
        // CONSTRUCTOR AND INITIALIZATION
        // ===========================================
        public MainWindow()
        {
            InitializeComponent();
            SetupEventHandlers();
            LoadSampleData();
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
        }

        /// <summary>
        /// Loads initial sample data for the application
        /// </summary>
        private void LoadSampleData()
        {
            // Add some sample playlists
            playlists.Add(new Playlist("Favorites", "My favorite songs"));
            playlists.Add(new Playlist("Workout Mix", "High energy songs for workouts"));
            playlists.Add(new Playlist("Chill Vibes", "Relaxing music"));
        }

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
            if (currentTrackIndex > 0)
            {
                PlayTrack(filteredTracks[currentTrackIndex - 1]);
            }
        }

        private void TitleBarPlayer_NextTrackRequested(object? sender, EventArgs e)
        {
            if (currentTrackIndex < filteredTracks.Count - 1)
            {
                PlayTrack(filteredTracks[currentTrackIndex + 1]);
            }
        }

        private void TitleBarPlayer_WindowMinimizeRequested(object? sender, EventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void TitleBarPlayer_WindowMaximizeRequested(object? sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                titleBarPlayer.UpdateWindowStateIcon(WindowState.Normal);
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                titleBarPlayer.UpdateWindowStateIcon(WindowState.Maximized);
            }
        }

        private void TitleBarPlayer_WindowCloseRequested(object? sender, EventArgs e)
        {
            this.Close();
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

        private void BtnAddFolder_Click(object sender, RoutedEventArgs e)
        {
            AddMusicFolder();
        }

        #endregion

        #region View Management

        private void ShowLibraryView()
        {
            txtHeader.Text = "Music Library";
            libraryView.Visibility = Visibility.Visible;
            playlistsView.Visibility = Visibility.Collapsed;
            recentlyPlayedView.Visibility = Visibility.Collapsed;
        }

        private void ShowPlaylistsView()
        {
            txtHeader.Text = "Playlists";
            libraryView.Visibility = Visibility.Collapsed;
            playlistsView.Visibility = Visibility.Visible;
            recentlyPlayedView.Visibility = Visibility.Collapsed;
        }

        private void ShowRecentlyPlayedView()
        {
            txtHeader.Text = "Recently Played";
            libraryView.Visibility = Visibility.Collapsed;
            playlistsView.Visibility = Visibility.Collapsed;
            recentlyPlayedView.Visibility = Visibility.Visible;
        }

        #endregion

        #region Music Management

        private void AddMusicFolder()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select a folder containing music files"
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                LoadMusicFromFolder(dialog.SelectedPath);
            }
        }

        private void LoadMusicFromFolder(string folderPath)
        {
            try
            {
                var supportedExtensions = new[] { ".mp3", ".wav", ".flac", ".m4a", ".aac" };
                var musicFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                    .Where(file => supportedExtensions.Contains(Path.GetExtension(file).ToLower()));

                foreach (var file in musicFiles)
                {
                    try
                    {
                        var track = LoadMusicTrack(file);
                        if (track != null)
                        {
                            allTracks.Add(track);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue with other files
                        Console.WriteLine($"Error loading {file}: {ex.Message}");
                    }
                }

                // Update filtered tracks
                UpdateFilteredTracks();
                System.Windows.MessageBox.Show($"Loaded {allTracks.Count} music files from the selected folder.", "Success");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading music folder: {ex.Message}", "Error");
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

        private void UpdateFilteredTracks()
        {
            var searchText = txtSearch.Text.ToLower();
            var filtered = allTracks.Where(track =>
                track.Title.ToLower().Contains(searchText) ||
                track.Artist.ToLower().Contains(searchText) ||
                track.Album.ToLower().Contains(searchText)
            ).ToList();

            filteredTracks.Clear();
            foreach (var track in filtered)
            {
                filteredTracks.Add(track);
            }
        }

        #endregion

        #region Search and Filtering

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateFilteredTracks();
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
                StopPlayback();

                currentTrack = track;
                currentTrackIndex = filteredTracks.IndexOf(track);

                // Load album art if available
                var albumArt = LoadAlbumArt(track);

                // Update title bar player control
                titleBarPlayer.SetTrackInfo(track.Title, track.Artist, albumArt);
                titleBarPlayer.SetAudioObjects(waveOut, audioFileReader);

                // Start playback
                audioFileReader = new AudioFileReader(track.FilePath);
                waveOut = new WaveOutEvent();
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
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error playing track: {ex.Message}", "Error");
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

        private void StopPlayback()
        {
            // Set playing state to false before disposing audio objects
            titleBarPlayer.IsPlaying = false;
            
            if (waveOut != null)
            {
                waveOut.Stop();
                waveOut.Dispose();
                waveOut = null;
            }

            if (audioFileReader != null)
            {
                audioFileReader.Dispose();
                audioFileReader = null;
            }
        }



        #endregion

        #region Playlist Management

        private void UpdatePlaylistsView()
        {
            // This method can be expanded to show which playlists contain the current track
        }

        #endregion





        protected override void OnClosing(CancelEventArgs e)
        {
            StopPlayback();
            base.OnClosing(e);
        }
    }
}
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using NAudio.Wave;

namespace MusicApp.Controls
{
    public partial class TitleBarPlayerControl : System.Windows.Controls.UserControl
    {
        // ===========================================
        // EVENTS
        // ===========================================
        public event EventHandler? PlayPauseRequested;
        public event EventHandler? PreviousTrackRequested;
        public event EventHandler? NextTrackRequested;
        public event EventHandler? WindowMinimizeRequested;
        public event EventHandler? WindowMaximizeRequested;
        public event EventHandler? WindowCloseRequested;
        public event EventHandler<double>? VolumeChanged;

        // ===========================================
        // AUDIO PLAYBACK STATE
        // ===========================================
        private WaveOutEvent? waveOut;
        private AudioFileReader? audioFileReader;
        private bool isPlaying = false;
        private bool isMuted = false;
        private double previousVolume = 50;
        private DispatcherTimer? seekBarTimer;
        private TimeSpan totalDuration;
        private TimeSpan pausedPosition;
        private bool isUpdatingAudioObjects = false;

        // ===========================================
        // PROPERTIES
        // ===========================================
        public bool IsPlaying
        {
            get => isPlaying;
            set
            {
                isPlaying = value;
                UpdatePlayPauseIcon();
                UpdateSeekBarTimer();
            }
        }

        public double Volume
        {
            get => sliderVolume.Value;
            set
            {
                sliderVolume.Value = Math.Max(0, Math.Min(100, value));
                if (waveOut != null && !isMuted)
                {
                    waveOut.Volume = (float)(sliderVolume.Value / 100.0);
                }
            }
        }

        public bool IsMuted
        {
            get => isMuted;
            set
            {
                isMuted = value;
                UpdateVolumeIcon();
            }
        }

        // ===========================================
        // CONSTRUCTOR
        // ===========================================
        public TitleBarPlayerControl()
        {
            InitializeComponent();
            this.Loaded += TitleBarPlayerControl_Loaded;
            this.Unloaded += TitleBarPlayerControl_Unloaded;
            InitializeSeekBarTimer();
        }

        // ===========================================
        // WINDOW CONTROL EVENTS
        // ===========================================
        private void TitleBarPlayerControl_Loaded(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.SizeChanged += Window_SizeChanged;
                UpdateSongInfoWidth(); // Initial update
            }
        }

        private void TitleBarPlayerControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // Clean up timer when control is unloaded
            if (seekBarTimer != null)
            {
                seekBarTimer.Stop();
                seekBarTimer.Tick -= SeekBarTimer_Tick;
                seekBarTimer = null;
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateSongInfoWidth();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                // Check if this is a double-click
                if (e.ClickCount == 2)
                {
                    // Double-click toggles maximize/restore
                    WindowMaximizeRequested?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    // Single click for drag
                    Window.GetWindow(this)?.DragMove();
                }
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowMinimizeRequested?.Invoke(this, EventArgs.Empty);
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            WindowMaximizeRequested?.Invoke(this, EventArgs.Empty);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            WindowCloseRequested?.Invoke(this, EventArgs.Empty);
        }

        // ===========================================
        // PLAYBACK CONTROL EVENTS
        // ===========================================
        private void BtnPlayPause_Click(object sender, RoutedEventArgs e)
        {
            PlayPauseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void BtnPrevious_Click(object sender, RoutedEventArgs e)
        {
            PreviousTrackRequested?.Invoke(this, EventArgs.Empty);
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            NextTrackRequested?.Invoke(this, EventArgs.Empty);
        }

        // ===========================================
        // VOLUME CONTROL EVENTS
        // ===========================================
        private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (waveOut != null && !isMuted)
            {
                waveOut.Volume = (float)(sliderVolume.Value / 100.0);
            }
            VolumeChanged?.Invoke(this, sliderVolume.Value);
        }

        private void IconVolume_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (isMuted)
            {
                // Unmute
                isMuted = false;
                iconVolume.Kind = PackIconKind.VolumeHigh;
                sliderVolume.Value = previousVolume;
                if (waveOut != null)
                {
                    waveOut.Volume = (float)(previousVolume / 100.0);
                }
            }
            else
            {
                // Mute
                isMuted = true;
                previousVolume = sliderVolume.Value;
                iconVolume.Kind = PackIconKind.VolumeOff;
                sliderVolume.Value = 0;
                if (waveOut != null)
                {
                    waveOut.Volume = 0;
                }
            }
        }

        // ===========================================
        // PUBLIC METHODS
        // ===========================================
        
        /// <summary>
        /// Sets the current track information displayed in the title bar
        /// </summary>
        public void SetTrackInfo(string title, string artist, BitmapImage? albumArt = null)
        {
            if (txtCurrentTrack != null)
                txtCurrentTrack.Text = title ?? "No track selected";
            if (txtCurrentArtist != null)
                txtCurrentArtist.Text = artist ?? "";
            
            if (imgAlbumArt != null)
            {
                if (albumArt != null)
                {
                    imgAlbumArt.Source = albumArt;
                }
                else
                {
                    imgAlbumArt.Source = null;
                }
            }
        }

        /// <summary>
        /// Sets the audio playback objects for volume control
        /// </summary>
        public void SetAudioObjects(WaveOutEvent? waveOut, AudioFileReader? audioFileReader)
        {
            // Set flag to prevent race conditions
            isUpdatingAudioObjects = true;
            
            try
            {
                // Stop the seek bar timer before changing audio objects
                if (seekBarTimer != null)
                {
                    seekBarTimer.Stop();
                }
                
                this.waveOut = waveOut;
                this.audioFileReader = audioFileReader;
                
                // Update seek bar with initial values
                if (audioFileReader != null)
                {
                    try
                    {
                        totalDuration = audioFileReader.TotalTime;
                        pausedPosition = TimeSpan.Zero;
                        UpdateSeekBar(TimeSpan.Zero, totalDuration);
                    }
                    catch (ObjectDisposedException)
                    {
                        // AudioFileReader was disposed, reset seek bar
                        totalDuration = TimeSpan.Zero;
                        pausedPosition = TimeSpan.Zero;
                        UpdateSeekBar(TimeSpan.Zero, TimeSpan.Zero);
                    }
                }
                else
                {
                    // Reset seek bar when no audio file is available
                    totalDuration = TimeSpan.Zero;
                    pausedPosition = TimeSpan.Zero;
                    UpdateSeekBar(TimeSpan.Zero, TimeSpan.Zero);
                }
            }
            finally
            {
                // Clear flag when done
                isUpdatingAudioObjects = false;
            }
        }

        /// <summary>
        /// Updates the window state icon (maximize/restore)
        /// </summary>
        public void UpdateWindowStateIcon(WindowState state)
        {
            if (state == WindowState.Maximized)
            {
                iconMaximize.Kind = PackIconKind.WindowRestore;
            }
            else
            {
                iconMaximize.Kind = PackIconKind.WindowMaximize;
            }
        }

        // ===========================================
        // PRIVATE HELPER METHODS
        // ===========================================
        private void UpdatePlayPauseIcon()
        {
            iconPlayPause.Kind = isPlaying ? PackIconKind.Pause : PackIconKind.Play;
        }

        private void UpdateVolumeIcon()
        {
            iconVolume.Kind = isMuted ? PackIconKind.VolumeOff : PackIconKind.VolumeHigh;
        }

        /// <summary>
        /// Updates the song info section width and position based on the current window width
        /// </summary>
        private void UpdateSongInfoWidth()
        {
            var window = Window.GetWindow(this);
            if (window == null) return;

            double windowWidth = window.ActualWidth;
            double calculatedWidth = CalculateResponsiveWidth(windowWidth);
            
            // Find the song info border and update its width and position
            var songInfoBorder = this.FindName("songInfoBorder") as Border;
            if (songInfoBorder != null)
            {
                songInfoBorder.Width = calculatedWidth;
                UpdateSongInfoPosition(windowWidth, calculatedWidth);
            }
        }

        /// <summary>
        /// Updates the song info section position based on window width
        /// </summary>
        private void UpdateSongInfoPosition(double windowWidth, double songInfoWidth)
        {
            const double PIN_WINDOW_WIDTH = 1039;
            
            var songInfoBorder = this.FindName("songInfoBorder") as Border;
            if (songInfoBorder == null) return;
            
            if (windowWidth < PIN_WINDOW_WIDTH)
            {
                // Pin the song info at the position it would be at 1039px window width
                double pinnedPosition = CalculateSongInfoPosition(PIN_WINDOW_WIDTH, songInfoWidth);
                songInfoBorder.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                songInfoBorder.Margin = new Thickness(pinnedPosition, 5, 0, 0);
            }
            else
            {
                // Use normal centering
                songInfoBorder.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
                songInfoBorder.Margin = new Thickness(0, 5, 0, 0);
            }
        }

        /// <summary>
        /// Calculates the left position of the song info section for a given window width
        /// </summary>
        private double CalculateSongInfoPosition(double windowWidth, double songInfoWidth)
        {
            // Calculate the center position of the window
            double windowCenter = windowWidth / 2;
            // Calculate the left edge of the song info section when centered
            return windowCenter - (songInfoWidth / 2);
        }

        /// <summary>
        /// Calculates the responsive width based on window size
        /// </summary>
        /// <param name="windowWidth">Current window width in pixels</param>
        /// <returns>Calculated width for the song info section</returns>
        private double CalculateResponsiveWidth(double windowWidth)
        {
            const double MIN_WIDTH = 300;
            const double MAX_WIDTH = 600;
            const double MIN_WINDOW_WIDTH = 1039;
            const double MAX_WINDOW_WIDTH = 1600; // You can adjust this value

            if (windowWidth <= MIN_WINDOW_WIDTH)
            {
                return MIN_WIDTH;
            }
            else if (windowWidth >= MAX_WINDOW_WIDTH)
            {
                return MAX_WIDTH;
            }
            else
            {
                // Linear interpolation between min and max
                double windowRange = MAX_WINDOW_WIDTH - MIN_WINDOW_WIDTH;
                double widthRange = MAX_WIDTH - MIN_WIDTH;
                double progress = (windowWidth - MIN_WINDOW_WIDTH) / windowRange;
                return MIN_WIDTH + (widthRange * progress);
            }
        }

        /// <summary>
        /// Updates the seek bar with current time and total duration
        /// </summary>
        /// <param name="currentTime">Current playback position</param>
        /// <param name="totalTime">Total duration of the track</param>
        public void UpdateSeekBar(TimeSpan currentTime, TimeSpan totalTime)
        {
            // Update time displays
            if (txtCurrentTime != null)
                txtCurrentTime.Text = FormatTimeSpan(currentTime);
            if (txtTotalDuration != null)
                txtTotalDuration.Text = FormatTimeSpan(totalTime);
            
            // Update progress bar
            if (progressFill != null)
            {
                if (totalTime.TotalSeconds > 0)
                {
                    double progress = currentTime.TotalSeconds / totalTime.TotalSeconds;
                    double progressWidth = 200 * progress; // 200 is the width of the progress bar
                    progressFill.Width = Math.Max(0, Math.Min(200, progressWidth));
                }
                else
                {
                    progressFill.Width = 0;
                }
            }
        }

        /// <summary>
        /// Handles seeking when user clicks on the progress bar
        /// </summary>
        /// <param name="clickPosition">X position of click relative to progress bar (0-200)</param>
        public void SeekToPosition(double clickPosition)
        {
            if (audioFileReader == null || totalDuration.TotalSeconds <= 0) return;

            try
            {
                // Calculate the target position based on click
                double progress = Math.Max(0, Math.Min(1, clickPosition / 200));
                TimeSpan targetPosition = TimeSpan.FromSeconds(progress * totalDuration.TotalSeconds);
                
                // Seek to the target position
                audioFileReader.CurrentTime = targetPosition;
                
                // Update the seek bar immediately
                UpdateSeekBar(targetPosition, totalDuration);
            }
            catch (ObjectDisposedException)
            {
                // AudioFileReader was disposed, ignore the seek operation
                Console.WriteLine("Cannot seek: AudioFileReader has been disposed");
            }
            catch (Exception ex)
            {
                // Log any other errors
                Console.WriteLine($"Error during seek operation: {ex.Message}");
            }
        }

        /// <summary>
        /// Formats a TimeSpan to MM:SS format
        /// </summary>
        /// <param name="timeSpan">TimeSpan to format</param>
        /// <returns>Formatted time string</returns>
        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            return $"{(int)timeSpan.TotalMinutes}:{timeSpan.Seconds:D2}";
        }

        /// <summary>
        /// Initializes the seek bar timer
        /// </summary>
        private void InitializeSeekBarTimer()
        {
            seekBarTimer = new DispatcherTimer();
            seekBarTimer.Interval = TimeSpan.FromSeconds(1);
            seekBarTimer.Tick += SeekBarTimer_Tick;
        }

        /// <summary>
        /// Updates the seek bar timer based on playback state
        /// </summary>
        private void UpdateSeekBarTimer()
        {
            // Don't update timer if we're currently updating audio objects
            if (isUpdatingAudioObjects) return;
            
            if (seekBarTimer == null) return;

            // Always stop the timer first
            seekBarTimer.Stop();

            try
            {
                if (isPlaying && audioFileReader != null)
                {
                    // Test if audioFileReader is still valid by accessing a property
                    try
                    {
                        var _ = audioFileReader.TotalTime;
                        seekBarTimer.Start();
                    }
                    catch (ObjectDisposedException)
                    {
                        // AudioFileReader was disposed, don't start timer
                        Console.WriteLine("AudioFileReader was disposed when trying to start timer");
                    }
                }
                else
                {
                    // Store current position when pausing
                    if (audioFileReader != null)
                    {
                        try
                        {
                            pausedPosition = audioFileReader.CurrentTime;
                        }
                        catch (ObjectDisposedException)
                        {
                            // AudioFileReader was disposed, ignore
                        }
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // AudioFileReader was disposed, timer is already stopped
                Console.WriteLine("AudioFileReader was disposed during timer update");
            }
            catch (Exception ex)
            {
                // Log any other errors, timer is already stopped
                Console.WriteLine($"Error updating seek bar timer: {ex.Message}");
            }
        }

        /// <summary>
        /// Timer tick event to update seek bar
        /// </summary>
        private void SeekBarTimer_Tick(object? sender, EventArgs e)
        {
            // Don't process timer ticks if we're updating audio objects
            if (isUpdatingAudioObjects) return;
            
            // Immediately stop the timer if we're not playing or have no audio reader
            if (!isPlaying || audioFileReader == null)
            {
                if (seekBarTimer != null)
                {
                    seekBarTimer.Stop();
                }
                return;
            }

            try
            {
                // Update the seek bar with current time
                UpdateSeekBar(audioFileReader.CurrentTime, totalDuration);
            }
            catch (ObjectDisposedException)
            {
                // AudioFileReader was disposed, stop the timer
                Console.WriteLine("AudioFileReader was disposed during timer tick");
                if (seekBarTimer != null)
                {
                    seekBarTimer.Stop();
                }
            }
            catch (Exception ex)
            {
                // Log any other errors and stop the timer
                Console.WriteLine($"Error in seek bar timer: {ex.Message}");
                if (seekBarTimer != null)
                {
                    seekBarTimer.Stop();
                }
            }
        }


    }
} 
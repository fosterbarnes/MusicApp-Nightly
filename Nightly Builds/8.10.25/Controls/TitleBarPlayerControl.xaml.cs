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
            
            // Update seek bar width after layout is complete
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateSeekBarWidth));
            
            // Debug: Check initial volume state
            System.Diagnostics.Debug.WriteLine($"Control loaded - Volume slider value: {sliderVolume.Value}");
            System.Diagnostics.Debug.WriteLine($"Control loaded - isMuted: {isMuted}");
            System.Diagnostics.Debug.WriteLine($"Control loaded - waveOut is null: {waveOut == null}");
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
            UpdateSeekBarWidth();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Don't handle window dragging if the click is on the seek bar
            if (e.Source == seekBarBackground || seekBarBackground.IsMouseOver)
            {
                System.Diagnostics.Debug.WriteLine("TitleBar_MouseLeftButtonDown - click is on seek bar, ignoring window drag");
                return;
            }
            
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
                    System.Diagnostics.Debug.WriteLine("TitleBar_MouseLeftButtonDown - starting window drag");
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
            System.Diagnostics.Debug.WriteLine($"Volume slider changed to: {sliderVolume.Value}");
            System.Diagnostics.Debug.WriteLine($"waveOut is null: {waveOut == null}");
            System.Diagnostics.Debug.WriteLine($"isMuted: {isMuted}");
            
            if (waveOut != null && !isMuted)
            {
                waveOut.Volume = (float)(sliderVolume.Value / 100.0);
                System.Diagnostics.Debug.WriteLine($"Set waveOut.Volume to: {waveOut.Volume}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Cannot set volume - waveOut is null or muted");
            }
            
            VolumeChanged?.Invoke(this, sliderVolume.Value);
        }

        private void IconVolume_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (isMuted)
            {
                // Unmute
                System.Diagnostics.Debug.WriteLine($"Unmuting - setting volume to: {previousVolume}");
                isMuted = false;
                iconVolume.Kind = PackIconKind.VolumeHigh;
                sliderVolume.Value = previousVolume;
                if (waveOut != null)
                {
                    waveOut.Volume = (float)(previousVolume / 100.0);
                    System.Diagnostics.Debug.WriteLine($"Set waveOut.Volume to: {waveOut.Volume}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Cannot unmute - waveOut is null");
                }
            }
            else
            {
                // Mute
                System.Diagnostics.Debug.WriteLine($"Muting - previous volume was: {sliderVolume.Value}");
                isMuted = true;
                previousVolume = sliderVolume.Value;
                iconVolume.Kind = PackIconKind.VolumeOff;
                sliderVolume.Value = 0;
                if (waveOut != null)
                {
                    waveOut.Volume = 0;
                    System.Diagnostics.Debug.WriteLine("Set waveOut.Volume to 0 (muted)");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Cannot mute - waveOut is null");
                }
            }
        }

        // ===========================================
        // PUBLIC METHODS
        // ===========================================
        
        /// <summary>
        /// Sets the current track information displayed in the title bar
        /// </summary>
        public void SetTrackInfo(string title, string artist, string? album = null, BitmapImage? albumArt = null)
        {
            if (txtCurrentTrack != null)
                txtCurrentTrack.Text = title ?? "No track selected";
            if (txtCurrentArtist != null)
                txtCurrentArtist.Text = artist ?? "";
            if (txtCurrentAlbum != null)
                txtCurrentAlbum.Text = album ?? "";
            
            // Show/hide dash separator and album based on whether album info exists
            if (txtDashSeparator != null)
                txtDashSeparator.Visibility = !string.IsNullOrEmpty(album) ? Visibility.Visible : Visibility.Collapsed;
            if (txtCurrentAlbum != null)
                txtCurrentAlbum.Visibility = !string.IsNullOrEmpty(album) ? Visibility.Visible : Visibility.Collapsed;
            
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
                
                // Initialize volume on the waveOut object
                if (waveOut != null && !isMuted)
                {
                    waveOut.Volume = (float)(sliderVolume.Value / 100.0);
                    System.Diagnostics.Debug.WriteLine($"Initialized waveOut volume to: {waveOut.Volume}");
                }
                
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
        /// Updates the current seek bar width for accurate seeking calculations
        /// </summary>
        private void UpdateSeekBarWidth()
        {
            if (seekBarBackground != null)
            {
                currentSeekBarWidth = seekBarBackground.ActualWidth;
                System.Diagnostics.Debug.WriteLine($"Seek bar width updated: {currentSeekBarWidth}");
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

            // Update total duration for seeking calculations
            totalDuration = totalTime;
            
            // Update progress bar
            if (progressFill != null)
            {
                if (totalTime.TotalSeconds > 0)
                {
                    double progress = currentTime.TotalSeconds / totalTime.TotalSeconds;
                    // Use the current seek bar width instead of hardcoded 200
                    double progressWidth = currentSeekBarWidth * progress;
                    progressFill.Width = Math.Max(0, Math.Min(currentSeekBarWidth, progressWidth));
                }
                else
                {
                    progressFill.Width = 0;
                }
            }
            
            // Update the stored seek bar width for accurate seeking
            UpdateSeekBarWidth();
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

        // ===========================================
        // SEEK BAR INTERACTION EVENTS
        // ===========================================
        private bool isDragging = false;
        private double currentSeekBarWidth = 0;
        private bool wasMutedBeforeDrag = false;
        private double volumeBeforeDrag = 100;
        private bool isMouseDown = false;
        private System.Windows.Point lastValidMousePosition;
        private DateTime lastMouseDownTime;

        /// <summary>
        /// Handles mouse left button down on the seek bar
        /// </summary>
        private void SeekBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"SeekBar_MouseLeftButtonDown called - Source: {e.Source}, OriginalSource: {e.OriginalSource}");
            
            if (audioFileReader == null || totalDuration.TotalSeconds <= 0) return;

            // Calculate and seek to the clicked position relative to the seek bar background
            System.Windows.Point clickPoint = e.GetPosition(seekBarBackground);
            double clickPosition = clickPoint.X;
            
            System.Diagnostics.Debug.WriteLine($"Mouse down - raw clickPoint: {clickPoint}, clickPosition: {clickPosition}, seek bar width: {currentSeekBarWidth}");
            
            // Clamp the position to valid bounds instead of rejecting it
            if (clickPosition < 0)
            {
                clickPosition = 0;
                System.Diagnostics.Debug.WriteLine($"Clamped negative click position to 0");
            }
            else if (clickPosition > currentSeekBarWidth)
            {
                clickPosition = currentSeekBarWidth;
                System.Diagnostics.Debug.WriteLine($"Clamped click position to max width: {currentSeekBarWidth}");
            }
            
            // Store the valid position for later use
            lastValidMousePosition = clickPoint;
            isMouseDown = true;
            lastMouseDownTime = DateTime.Now;
            
            // Now seek to the clamped position
            System.Diagnostics.Debug.WriteLine($"Seeking to clamped click position: {clickPosition}");
            SeekToPosition(clickPosition);
            
            // Store current audio state before starting drag
            wasMutedBeforeDrag = isMuted;
            volumeBeforeDrag = sliderVolume.Value;
            
            // Mute audio during drag to prevent jarring playback
            if (waveOut != null && !isMuted)
            {
                System.Diagnostics.Debug.WriteLine($"Muting audio during drag - previous volume: {sliderVolume.Value}");
                waveOut.Volume = 0;
            }
            
            // Start dragging
            isDragging = true;
            seekBarBackground.CaptureMouse();
            System.Diagnostics.Debug.WriteLine($"Drag started - mouse captured, isDragging: {isDragging}");
            
            // Verify mouse capture was successful
            if (seekBarBackground.IsMouseCaptured)
            {
                System.Diagnostics.Debug.WriteLine("Mouse capture successful - seek bar has mouse capture");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("WARNING: Mouse capture failed - seek bar does not have mouse capture");
            }
            
            // Mark the event as handled to prevent it from bubbling up to the title bar
            e.Handled = true;
            System.Diagnostics.Debug.WriteLine("SeekBar_MouseLeftButtonDown - event marked as handled");
        }

        /// <summary>
        /// Handles mouse movement while dragging on the seek bar
        /// </summary>
        private void SeekBar_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!isDragging || audioFileReader == null || totalDuration.TotalSeconds <= 0) return;

            // Ignore mouse move events that happen too soon after mouse down (prevents invalid coordinates)
            TimeSpan timeSinceMouseDown = DateTime.Now - lastMouseDownTime;
            if (timeSinceMouseDown.TotalMilliseconds < 50)
            {
                System.Diagnostics.Debug.WriteLine($"Ignoring mouse move too soon after mouse down: {timeSinceMouseDown.TotalMilliseconds}ms");
                return;
            }

            // Get the current mouse position relative to the seek bar background
            System.Windows.Point currentPoint = e.GetPosition(seekBarBackground);
            double currentPosition = currentPoint.X;
            
            System.Diagnostics.Debug.WriteLine($"Mouse move during drag - raw position: {currentPosition}, seek bar width: {currentSeekBarWidth}");
            
            // Check if this is a valid position (within reasonable bounds)
            if (currentPosition < -100 || currentPosition > currentSeekBarWidth + 100)
            {
                System.Diagnostics.Debug.WriteLine($"Invalid mouse position detected: {currentPosition}, using last valid position");
                // Use the last valid position instead of seeking to an invalid one
                return;
            }
            
            // Clamp the position to valid bounds
            if (currentPosition < 0)
            {
                currentPosition = 0;
                System.Diagnostics.Debug.WriteLine($"Clamped negative position to 0");
            }
            else if (currentPosition > currentSeekBarWidth)
            {
                currentPosition = currentSeekBarWidth;
                System.Diagnostics.Debug.WriteLine($"Clamped position to max width: {currentSeekBarWidth}");
            }
            
            // Store this as the last valid position
            lastValidMousePosition = currentPoint;
            
            // Now seek to the clamped position
            System.Diagnostics.Debug.WriteLine($"Seeking to clamped position: {currentPosition}");
            SeekToPosition(currentPosition);
        }

        /// <summary>
        /// Handles mouse left button up on the seek bar
        /// </summary>
        private void SeekBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"SeekBar_MouseLeftButtonUp called - isDragging: {isDragging}, isMouseDown: {isMouseDown}");
            
            if (isDragging)
            {
                System.Diagnostics.Debug.WriteLine("SeekBar_MouseLeftButtonUp - ending drag operation");
                
                // Restore audio state after drag ends
                if (waveOut != null)
                {
                    if (wasMutedBeforeDrag)
                    {
                        // Was muted before, keep muted
                        System.Diagnostics.Debug.WriteLine("Drag ended - keeping audio muted (was muted before)");
                        waveOut.Volume = 0;
                    }
                    else
                    {
                        // Was not muted, restore previous volume
                        float restoredVolume = (float)(volumeBeforeDrag / 100.0);
                        waveOut.Volume = restoredVolume;
                        System.Diagnostics.Debug.WriteLine($"Drag ended - restored volume to: {restoredVolume}");
                    }
                }
                
                isDragging = false;
                isMouseDown = false;
                seekBarBackground.ReleaseMouseCapture();
                
                System.Diagnostics.Debug.WriteLine("Drag operation ended - mouse capture released");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("SeekBar_MouseLeftButtonUp called but not dragging - ignoring");
            }
        }

        /// <summary>
        /// Handles when the mouse leaves the seek bar area during dragging
        /// </summary>
        private void SeekBar_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (isDragging)
            {
                System.Diagnostics.Debug.WriteLine("Mouse left seek bar area during drag - continuing drag operation");
                // Don't end the drag, just continue tracking
            }
        }

        /// <summary>
        /// Global mouse up handler to catch when mouse is released outside the seek bar
        /// </summary>
        private void UserControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Only handle if we're actually dragging and the mouse is not over the seek bar
            if (isDragging && !seekBarBackground.IsMouseOver)
            {
                System.Diagnostics.Debug.WriteLine("Global mouse up detected outside seek bar during drag - ending drag operation");
                // End the drag operation
                if (waveOut != null)
                {
                    if (wasMutedBeforeDrag)
                    {
                        System.Diagnostics.Debug.WriteLine("Global mouse up - keeping audio muted (was muted before)");
                        waveOut.Volume = 0;
                    }
                    else
                    {
                        float restoredVolume = (float)(volumeBeforeDrag / 100.0);
                        waveOut.Volume = restoredVolume;
                        System.Diagnostics.Debug.WriteLine($"Global mouse up - restored volume to: {restoredVolume}");
                    }
                }
                
                isDragging = false;
                isMouseDown = false;
                seekBarBackground.ReleaseMouseCapture();
                System.Diagnostics.Debug.WriteLine("Drag operation ended via global mouse up");
            }
        }

        /// <summary>
        /// Updates the existing SeekToPosition method to work with dynamic seek bar width
        /// </summary>
        /// <param name="clickPosition">X position of click relative to seek bar</param>
        public void SeekToPosition(double clickPosition)
        {
            if (audioFileReader == null || totalDuration.TotalSeconds <= 0) return;

            try
            {
                // Use the stored seek bar width that gets updated dynamically
                double seekBarWidth = currentSeekBarWidth;
                
                // Debug output to see what values we're getting
                System.Diagnostics.Debug.WriteLine($"=== SEEK DEBUG INFO ===");
                System.Diagnostics.Debug.WriteLine($"clickPosition: {clickPosition}");
                System.Diagnostics.Debug.WriteLine($"currentSeekBarWidth: {currentSeekBarWidth}");
                System.Diagnostics.Debug.WriteLine($"totalDuration: {totalDuration.TotalSeconds}");
                System.Diagnostics.Debug.WriteLine($"audioFileReader.CurrentTime before seek: {audioFileReader.CurrentTime}");
                
                if (seekBarWidth <= 0)
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: Seek bar width is 0 or negative, cannot seek");
                    System.Diagnostics.Debug.WriteLine("Trying to get width from seekBarBackground.ActualWidth...");
                    seekBarWidth = seekBarBackground?.ActualWidth ?? 0;
                    System.Diagnostics.Debug.WriteLine($"seekBarBackground.ActualWidth: {seekBarWidth}");
                    
                    if (seekBarWidth <= 0)
                    {
                        System.Diagnostics.Debug.WriteLine("ERROR: Still cannot get valid width, aborting seek");
                        return;
                    }
                }

                // Ensure clickPosition is within bounds
                if (clickPosition < 0) 
                {
                    System.Diagnostics.Debug.WriteLine($"WARNING: clickPosition {clickPosition} < 0, clamping to 0");
                    clickPosition = 0;
                }
                if (clickPosition > seekBarWidth) 
                {
                    System.Diagnostics.Debug.WriteLine($"WARNING: clickPosition {clickPosition} > seekBarWidth {seekBarWidth}, clamping to seekBarWidth");
                    clickPosition = seekBarWidth;
                }

                // Calculate the target position based on click (0-1 range)
                double progress = clickPosition / seekBarWidth;
                System.Diagnostics.Debug.WriteLine($"Calculated progress: {progress}");
                
                // Ensure progress is between 0 and 1
                progress = Math.Max(0, Math.Min(1, progress));
                System.Diagnostics.Debug.WriteLine($"Clamped progress: {progress}");
                
                TimeSpan targetPosition = TimeSpan.FromSeconds(progress * totalDuration.TotalSeconds);
                System.Diagnostics.Debug.WriteLine($"Target position: {targetPosition}");
                System.Diagnostics.Debug.WriteLine($"=== END SEEK DEBUG ===");
                
                // Seek to the target position
                audioFileReader.CurrentTime = targetPosition;
                
                // Update the seek bar immediately
                UpdateSeekBar(targetPosition, totalDuration);
            }
            catch (ObjectDisposedException)
            {
                // AudioFileReader was disposed, ignore the seek operation
                System.Diagnostics.Debug.WriteLine("Cannot seek: AudioFileReader has been disposed");
            }
            catch (Exception ex)
            {
                // Log any other errors
                System.Diagnostics.Debug.WriteLine($"Error during seek operation: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

    }
} 
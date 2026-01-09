using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using NAudio.Wave;
using MusicApp;

namespace MusicApp.TitleBarWithPlayerControls
{
    public partial class TitleBar : System.Windows.Controls.UserControl
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
        public event EventHandler<bool>? ShuffleStateChanged;

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
        // PLAYER SETTINGS STATE
        // ===========================================
        private bool isShuffleEnabled = false;
        private SettingsManager.RepeatMode repeatMode = SettingsManager.RepeatMode.Off;

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

                // Safety check: if we're stopping playback, ensure the seek bar timer is stopped
                if (!isPlaying)
                {
                    StopSeekBarTimer();

                    // Additional safety: if we're stopping and have no valid audio objects, reset to initial state
                    if (!AreAudioObjectsValid())
                    {
                        ResetToInitialState();
                    }
                }
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

        public bool IsShuffleEnabled
        {
            get => isShuffleEnabled;
            set
            {
                if (isShuffleEnabled != value)
                {
                    isShuffleEnabled = value;
                    UpdateShuffleIcon();
                    ShuffleStateChanged?.Invoke(this, isShuffleEnabled);
                }
            }
        }

        public SettingsManager.RepeatMode RepeatMode
        {
            get => repeatMode;
            set
            {
                repeatMode = value;
                UpdateRepeatIcon();
            }
        }

        public bool IsRepeatEnabled
        {
            get => repeatMode != SettingsManager.RepeatMode.Off;
        }

        /// <summary>
        /// Gets the current playback position
        /// </summary>
        public TimeSpan CurrentPosition
        {
            get
            {
                try
                {
                    if (AreAudioObjectsValid() && audioFileReader != null)
                    {
                        return audioFileReader.CurrentTime;
                    }
                    return pausedPosition;
                }
                catch
                {
                    // If we encounter an error getting the current position, reset to initial state
                    Console.WriteLine("Error getting current position - resetting to initial state");
                    ResetToInitialState();
                    return TimeSpan.Zero;
                }
            }
        }

        // ===========================================
        // CONSTRUCTOR
        // ===========================================
        public TitleBar()
        {
            InitializeComponent();
            this.Loaded += TitleBar_Loaded;
            this.Unloaded += TitleBar_Unloaded;
            InitializeSeekBarTimer();

            // Initialize search box placeholder text
            if (txtSearch != null)
            {
                txtSearch.Text = "Search";
                txtSearch.Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)); // #CCCCCC
                txtSearch.GotFocus += TxtSearch_GotFocus;
                txtSearch.LostFocus += TxtSearch_LostFocus;
            }
        }

        private void TxtSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            if (txtSearch != null && txtSearch.Text == "Search")
            {
                txtSearch.Text = "";
                txtSearch.Foreground = new SolidColorBrush(Colors.White);
            }
        }

        private void TxtSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            if (txtSearch != null && string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                txtSearch.Text = "Search";
                txtSearch.Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)); // #CCCCCC
            }
        }

        // ===========================================
        // WINDOW CONTROL EVENTS
        // ===========================================
        private async void TitleBar_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = Window.GetWindow(this);
                if (window != null)
                {
                    window.SizeChanged += Window_SizeChanged;
                    UpdateSongInfoWidth(); // Initial update
                }

                // Update seek bar width after layout is complete
                _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => {
                    UpdateSeekBarWidth();
                    UpdateGradientMask(); // Ensure gradient mask is positioned correctly after layout
                }));

                // Load player settings
                await LoadPlayerSettingsAsync();

                // Debug: Check initial volume state
                System.Diagnostics.Debug.WriteLine($"Control loaded - Volume slider value: {sliderVolume.Value}");
                System.Diagnostics.Debug.WriteLine($"Control loaded - isMuted: {isMuted}");
                System.Diagnostics.Debug.WriteLine($"Control loaded - waveOut is null: {waveOut == null}");
            }
            catch (Exception ex)
            {
                // Log any errors and reset to initial state as a safety measure
                System.Diagnostics.Debug.WriteLine($"Error in TitleBar_Loaded: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine("Error in TitleBar_Loaded - resetting to initial state");
                ResetToInitialState();
            }
        }

        private void TitleBar_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Clean up timer when control is unloaded
                if (seekBarTimer != null)
                {
                    seekBarTimer.Stop();
                    seekBarTimer.Tick -= SeekBarTimer_Tick;
                    seekBarTimer = null;
                }
            }
            catch (Exception ex)
            {
                // Log any errors and reset to initial state as a safety measure
                System.Diagnostics.Debug.WriteLine($"Error in TitleBar_Unloaded: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine("Error in TitleBar_Unloaded - resetting to initial state");
                ResetToInitialState();
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                UpdateSongInfoWidth();
                UpdateSeekBarWidth();
                UpdateGradientMask(); // Update gradient mask position and size

                // Immediately update the progress bar with the new width to prevent visual lag
                if (audioFileReader != null && totalDuration.TotalSeconds > 0)
                {
                    try
                    {
                        // Update the progress bar using the current playback position and new width
                        if (progressFill != null)
                        {
                            double progress = audioFileReader.CurrentTime.TotalSeconds / totalDuration.TotalSeconds;
                            double progressWidth = currentSeekBarWidth * progress;
                            progressFill.Width = Math.Max(0, Math.Min(currentSeekBarWidth, progressWidth));
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // AudioFileReader was disposed, ignore
                    }
                    catch (NullReferenceException)
                    {
                        // AudioFileReader is null, ignore
                    }
                }
            }
            catch (Exception ex)
            {
                // Log any errors and reset to initial state as a safety measure
                System.Diagnostics.Debug.WriteLine($"Error in Window_SizeChanged: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine("Error in Window_SizeChanged - resetting to initial state");
                ResetToInitialState();
            }
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
            System.Diagnostics.Debug.WriteLine("TitleBar: Maximize button clicked!");
            System.Diagnostics.Debug.WriteLine($"TitleBar: Current icon state: {iconMaximize.Kind}");
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
        // SHUFFLE AND REPEAT BUTTON EVENTS
        // ===========================================
        private async void BtnShuffle_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Shuffle button clicked!");
            e.Handled = true; // Prevent event bubbling to parent

            // Animate button to pressed state
            AnimateButtonPress(btnShuffleTransform);

            // Toggle shuffle state
            IsShuffleEnabled = !IsShuffleEnabled;

            // Save the new state to settings
            await SettingsManager.Instance.SetShuffleStateAsync(IsShuffleEnabled);

            System.Diagnostics.Debug.WriteLine($"Shuffle state changed to: {IsShuffleEnabled}");
        }

        private async void BtnRepeat_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Repeat button clicked!");
            e.Handled = true; // Prevent event bubbling to parent

            // Animate button to pressed state
            AnimateButtonPress(btnRepeatTransform);

            // Cycle through repeat states: Off -> All -> One -> Off
            var newMode = repeatMode switch
            {
                SettingsManager.RepeatMode.Off => SettingsManager.RepeatMode.All,
                SettingsManager.RepeatMode.All => SettingsManager.RepeatMode.One,
                SettingsManager.RepeatMode.One => SettingsManager.RepeatMode.Off,
                _ => SettingsManager.RepeatMode.Off
            };

            RepeatMode = newMode;

            // Save the new state to settings
            await SettingsManager.Instance.SetRepeatModeAsync(newMode);

            System.Diagnostics.Debug.WriteLine($"Repeat mode changed to: {newMode} ({(int)newMode})");
        }

        // ===========================================
        // BUTTON ANIMATION HELPER METHODS
        // ===========================================
        private void AnimateButtonPress(ScaleTransform transform)
        {
            if (transform != null)
            {
                var animation = new System.Windows.Media.Animation.DoubleAnimation
                {
                    To = 0.90,
                    Duration = TimeSpan.FromMilliseconds(50),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                };

                transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
                transform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
            }
        }

        private void AnimateButtonRelease(ScaleTransform transform)
        {
            if (transform != null)
            {
                var animation = new System.Windows.Media.Animation.DoubleAnimation
                {
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(100),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                };

                transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
                transform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
            }
        }

        private void AnimateFillExpand(System.Windows.FrameworkElement fillElement)
        {
            if (fillElement != null)
            {
                var animation = new System.Windows.Media.Animation.DoubleAnimation
                {
                    To = 24.0,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                };

                fillElement.BeginAnimation(WidthProperty, animation);
                fillElement.BeginAnimation(HeightProperty, animation);
            }
        }

        private void AnimateFillContract(System.Windows.FrameworkElement fillElement)
        {
            if (fillElement != null)
            {
                var animation = new System.Windows.Media.Animation.DoubleAnimation
                {
                    To = 0.0,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                };

                fillElement.BeginAnimation(WidthProperty, animation);
                fillElement.BeginAnimation(HeightProperty, animation);
            }
        }

        // ===========================================
        // QUEUE BUTTON EVENTS
        // ===========================================
        private void BtnQueue_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Queue button clicked!");
            e.Handled = true; // Prevent event bubbling to parent

            // Animate button to pressed state
            AnimateButtonPress(btnQueueTransform);

            // TODO: Add queue functionality later
            // This is just a placeholder as requested
        }

        private void BtnQueue_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // Prevent event bubbling to parent

            // Animate button back to normal size
            AnimateButtonRelease(btnQueueTransform);
        }

        // Additional mouse event handlers to prevent window dragging
        private void BtnShuffle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // Prevent event bubbling

            // Animate button back to normal size
            AnimateButtonRelease(btnShuffleTransform);
        }

        private void BtnShuffle_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            e.Handled = true; // Prevent event bubbling
        }

        private void BtnRepeat_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // Prevent event bubbling

            // Animate button back to normal size
            AnimateButtonRelease(btnRepeatTransform);
        }

        private void BtnRepeat_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            e.Handled = true; // Prevent event bubbling
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

                // If we're clearing audio objects, reset to initial state like a fresh launch
                if (waveOut == null || audioFileReader == null)
                {
                    ResetToInitialState();
                }
                else
                {
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
                }
            }
            catch (Exception ex)
            {
                // Log any errors and reset to initial state as a safety measure
                Console.WriteLine($"Error in SetAudioObjects: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Console.WriteLine("Error in SetAudioObjects - resetting to initial state");
                ResetToInitialState();
            }
            finally
            {
                // Clear flag when done
                isUpdatingAudioObjects = false;
            }
        }

        /// <summary>
        /// Resets the TitleBar to its initial state like a fresh launch
        /// This is called when audio objects are cleared (queue ends, new track selected)
        /// </summary>
        public void ResetToInitialState()
        {
            try
            {
                Console.WriteLine("Resetting TitleBar to initial state - clearing all audio-related state");

                // Stop and clear the seek bar timer
                if (seekBarTimer != null)
                {
                    seekBarTimer.Stop();
                    Console.WriteLine("Seek bar timer stopped during reset");
                }

                // Reset all audio-related state variables
                totalDuration = TimeSpan.Zero;
                pausedPosition = TimeSpan.Zero;
                isPlaying = false;

                // Reset drag-related state
                isDragging = false;
                isMouseDown = false;
                dragTargetPosition = TimeSpan.Zero;

                // Reset seek bar display to initial state
                UpdateSeekBar(TimeSpan.Zero, TimeSpan.Zero);

                // Update play/pause icon to show play state
                UpdatePlayPauseIcon();

                // Ensure the timer is completely stopped
                StopSeekBarTimer();

                Console.WriteLine("TitleBar reset to initial state completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during TitleBar reset: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");

                // If we encounter an error during reset, try to at least stop the timer
                try
                {
                    if (seekBarTimer != null)
                    {
                        seekBarTimer.Stop();
                        Console.WriteLine("Emergency timer stop during reset error");
                    }
                }
                catch
                {
                    Console.WriteLine("Failed to stop timer during reset error");
                }
            }
        }

        /// <summary>
        /// Immediately stops the seek bar timer and resets related state
        /// </summary>
        private void StopSeekBarTimer()
        {
            try
            {
                if (seekBarTimer != null)
                {
                    seekBarTimer.Stop();
                    Console.WriteLine("Seek bar timer immediately stopped");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping seek bar timer: {ex.Message}");

                // If we encounter an error stopping the timer, reset to initial state as a safety measure
                Console.WriteLine("Error in StopSeekBarTimer - resetting to initial state");
                ResetToInitialState();
            }
        }

        /// <summary>
        /// Updates the window state icon (maximize/restore)
        /// </summary>
        public void UpdateWindowStateIcon(WindowState state)
        {
            System.Diagnostics.Debug.WriteLine($"TitleBar: UpdateWindowStateIcon called with state: {state}");

            if (state == WindowState.Maximized)
            {
                iconMaximize.Kind = PackIconKind.WindowRestore;
                System.Diagnostics.Debug.WriteLine("TitleBar: Icon set to WindowRestore (restore button)");
            }
            else
            {
                iconMaximize.Kind = PackIconKind.WindowMaximize;
                System.Diagnostics.Debug.WriteLine("TitleBar: Icon set to WindowMaximize (maximize button)");
            }

            System.Diagnostics.Debug.WriteLine($"TitleBar: Final icon state: {iconMaximize.Kind}");
        }

        /// <summary>
        /// Gets the current window state icon for debugging
        /// </summary>
        public PackIconKind GetCurrentWindowStateIcon()
        {
            return iconMaximize.Kind;
        }

        // ===========================================
        // PRIVATE HELPER METHODS
        // ===========================================
        private async Task LoadPlayerSettingsAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Loading player settings...");

                IsShuffleEnabled = await SettingsManager.Instance.GetShuffleStateAsync();
                RepeatMode = await SettingsManager.Instance.GetRepeatModeAsync();

                System.Diagnostics.Debug.WriteLine($"Raw settings loaded - Shuffle: {IsShuffleEnabled}, Repeat: {RepeatMode}");

                // Update icons to reflect loaded state
                UpdateShuffleIcon();
                UpdateRepeatIcon();

                // Notify that shuffle state has been loaded (this will trigger MainWindow to initialize shuffled tracks)
                ShuffleStateChanged?.Invoke(this, IsShuffleEnabled);

                System.Diagnostics.Debug.WriteLine($"Player settings loaded and icons updated - Shuffle: {IsShuffleEnabled}, Repeat: {RepeatMode}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading player settings: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                // If we encounter an error loading settings, reset to initial state as a safety measure
                System.Diagnostics.Debug.WriteLine("Error in LoadPlayerSettingsAsync - resetting to initial state");
                ResetToInitialState();
            }
        }

        private void UpdatePlayPauseIcon()
        {
            try
            {
                iconPlayPause.Kind = isPlaying ? PackIconKind.Pause : PackIconKind.Play;
            }
            catch (Exception ex)
            {
                // Log any errors and reset to initial state as a safety measure
                System.Diagnostics.Debug.WriteLine($"Error in UpdatePlayPauseIcon: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine("Error in UpdatePlayPauseIcon - resetting to initial state");
                ResetToInitialState();
            }
        }

        private void UpdateVolumeIcon()
        {
            try
            {
                iconVolume.Kind = isMuted ? PackIconKind.VolumeOff : PackIconKind.VolumeHigh;
            }
            catch (Exception ex)
            {
                // Log any errors and reset to initial state as a safety measure
                System.Diagnostics.Debug.WriteLine($"Error in UpdateVolumeIcon: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine("Error in UpdateVolumeIcon - resetting to initial state");
                ResetToInitialState();
            }
        }

        private void UpdateShuffleIcon()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"UpdateShuffleIcon called - IsShuffleEnabled: {IsShuffleEnabled}");

                if (btnShuffle != null && btnShuffleFill != null)
                {
                    var icon = btnShuffle.FindName("iconShuffle") as PackIcon;
                    if (icon != null)
                    {
                        if (IsShuffleEnabled)
                        {
                            icon.Kind = PackIconKind.ShuffleVariant;
                            // Animate the fill expanding from center
                            AnimateFillExpand(btnShuffleFill);

                            System.Diagnostics.Debug.WriteLine("Shuffle icon updated to ACTIVE state (purple background)");
                        }
                        else
                        {
                            icon.Kind = PackIconKind.ShuffleVariant;
                            // Animate the fill contracting to center
                            AnimateFillContract(btnShuffleFill);

                            System.Diagnostics.Debug.WriteLine("Shuffle icon updated to INACTIVE state (transparent background)");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("ERROR: Could not find iconShuffle in btnShuffle");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: btnShuffle or btnShuffleFill is null");
                }
            }
            catch (Exception ex)
            {
                // Log any errors and reset to initial state as a safety measure
                System.Diagnostics.Debug.WriteLine($"Error in UpdateShuffleIcon: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine("Error in UpdateShuffleIcon - resetting to initial state");
                ResetToInitialState();
            }
        }

        private void UpdateRepeatIcon()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"UpdateRepeatIcon called - RepeatMode: {RepeatMode}");

                if (btnRepeat != null && btnRepeatFill != null)
                {
                    var icon = btnRepeat.FindName("iconRepeat") as System.Windows.Controls.Image;
                    if (icon != null)
                    {
                        switch (RepeatMode)
                        {
                            case SettingsManager.RepeatMode.Off:
                                // Use standard repeat icon for OFF state
                                icon.Source = System.Windows.Application.Current.Resources["RepeatStandardIcon"] as System.Windows.Media.DrawingImage;
                                // Animate the fill contracting to center
                                AnimateFillContract(btnRepeatFill);

                                ResetRepeatIconTransform(); // Reset any transform from previous state
                                System.Diagnostics.Debug.WriteLine("Repeat icon updated to OFF state (transparent background)");
                                break;

                            case SettingsManager.RepeatMode.All:
                                // Use standard repeat icon for ALL state
                                icon.Source = System.Windows.Application.Current.Resources["RepeatStandardIcon"] as System.Windows.Media.DrawingImage;
                                // Animate the fill expanding from center
                                AnimateFillExpand(btnRepeatFill);

                                ResetRepeatIconTransform(); // Reset any transform from previous state
                                System.Diagnostics.Debug.WriteLine("Repeat icon updated to ALL state (purple background)");
                                break;

                            case SettingsManager.RepeatMode.One:
                                // Use custom XAML icon for repeat one state
                                try
                                {
                                    System.Diagnostics.Debug.WriteLine("Attempting to load custom repeat one icon...");

                                    // Check if the resource exists
                                    if (System.Windows.Application.Current.Resources.Contains("RepeatOneIcon"))
                                    {
                                        var customIcon = System.Windows.Application.Current.Resources["RepeatOneIcon"] as System.Windows.Media.DrawingImage;
                                        if (customIcon != null)
                                        {
                                            icon.Source = customIcon;

                                            // Apply vertical offset to center the repeat icon (not the "1")
                                            var transform = icon.RenderTransform as TranslateTransform;
                                            if (transform != null)
                                            {
                                                transform.Y = -1.5; // Shift up by 1.5 pixels to center the repeat icon
                                            }

                                            System.Diagnostics.Debug.WriteLine("Repeat icon updated to ONE state with custom XAML icon and vertical offset");
                                        }
                                        else
                                        {
                                            System.Diagnostics.Debug.WriteLine("Custom icon resource found but is null, using fallback");
                                            icon.Source = System.Windows.Application.Current.Resources["RepeatStandardIcon"] as System.Windows.Media.DrawingImage;
                                            ResetRepeatIconTransform();
                                        }
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine("Custom icon resource not found in Application.Resources, using fallback");
                                        icon.Source = System.Windows.Application.Current.Resources["RepeatStandardIcon"] as System.Windows.Media.DrawingImage;
                                        ResetRepeatIconTransform();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // Fallback to standard icon if there's any error
                                    System.Diagnostics.Debug.WriteLine($"Error loading custom icon: {ex.Message}");
                                    System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                                    icon.Source = System.Windows.Application.Current.Resources["RepeatStandardIcon"] as System.Windows.Media.DrawingImage;
                                    ResetRepeatIconTransform();
                                }

                                // Animate the fill expanding from center
                                AnimateFillExpand(btnRepeatFill);
                                break;
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("ERROR: Could not find iconRepeat in btnRepeat");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: btnRepeat or btnRepeatFill is null");
                }
            }
            catch (Exception ex)
            {
                // Log any errors and reset to initial state as a safety measure
                System.Diagnostics.Debug.WriteLine($"Error in UpdateRepeatIcon: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine("Error in UpdateRepeatIcon - resetting to initial state");
                ResetToInitialState();
            }
        }

        /// <summary>
        /// Resets the repeat icon transform to its default position
        /// </summary>
        private void ResetRepeatIconTransform()
        {
            var icon = btnRepeat?.FindName("iconRepeat") as System.Windows.Controls.Image;
            if (icon?.RenderTransform is TranslateTransform transform)
            {
                transform.Y = 0; // Reset to default position
            }
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

            // Update the gradient mask position and width
            UpdateGradientMask();
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
                songInfoBorder.Margin = new Thickness(pinnedPosition, 5, 0, 5);
            }
            else
            {
                // Use normal centering
                songInfoBorder.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
                songInfoBorder.Margin = new Thickness(0, 5, 0, 5);
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
        /// Updates the gradient mask position and width to prevent text from extending under shuffle/repeat buttons
        /// </summary>
        private void UpdateGradientMask()
        {
            if (textGradientMask == null) return;

            var window = Window.GetWindow(this);
            if (window == null) return;

            // The shuffle button is positioned at the right edge of the song info section
            // We need to position the mask so it covers the area where text extends under the buttons
            // The mask should start from the left edge of the shuffle button area and extend leftward

            // Fixed width for the gradient transition (from transparent to background color)
            double maskWidth = 120; // Increased width to extend to the right edge
            double maskHeight = 38; // Height set to 38px as requested

            // Position the mask at the very top with no margin to cover the entire text area
            // This ensures it covers both the song title and artist text completely
            double maskTopMargin = 0; // No top margin to start from the very top

            // Update the mask position and size
            textGradientMask.Width = maskWidth;
            textGradientMask.Height = maskHeight;
            textGradientMask.Margin = new Thickness(0, maskTopMargin, 0, 0);
        }

        /// <summary>
        /// Updates the seek bar with current time and total duration
        /// </summary>
        /// <param name="currentTime">Current playback position</param>
        /// <param name="totalTime">Total duration of the track</param>
        public void UpdateSeekBar(TimeSpan currentTime, TimeSpan totalTime)
        {
            try
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

                // Note: Seek bar width is now only updated when window size changes
                // This prevents unnecessary updates every second during playback
            }
            catch (Exception ex)
            {
                // Log any errors and reset to initial state as a safety measure
                Console.WriteLine($"Error in UpdateSeekBar: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Console.WriteLine("Error in UpdateSeekBar - resetting to initial state");
                ResetToInitialState();
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
            try
            {
                seekBarTimer = new DispatcherTimer();
                seekBarTimer.Interval = TimeSpan.FromSeconds(1);
                seekBarTimer.Tick += SeekBarTimer_Tick;
            }
            catch (Exception ex)
            {
                // Log any errors and reset to initial state as a safety measure
                Console.WriteLine($"Error in InitializeSeekBarTimer: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Console.WriteLine("Error in InitializeSeekBarTimer - resetting to initial state");
                ResetToInitialState();
            }
        }

        /// <summary>
        /// Safely checks if audio objects are valid and can be accessed
        /// </summary>
        private bool AreAudioObjectsValid()
        {
            try
            {
                // Check if audioFileReader is null
                if (audioFileReader == null)
                {
                    return false;
                }

                // Test if audioFileReader is still valid by accessing a property
                var _ = audioFileReader.TotalTime;
                return true;
            }
            catch (ObjectDisposedException)
            {
                Console.WriteLine("AudioFileReader was disposed during validity check");
                return false;
            }
            catch (NullReferenceException)
            {
                Console.WriteLine("AudioFileReader is null during validity check");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking audio object validity: {ex.Message}");

                // If we encounter an error checking validity, reset to initial state as a safety measure
                Console.WriteLine("Error in AreAudioObjectsValid - resetting to initial state");
                ResetToInitialState();

                return false;
            }
        }

        /// <summary>
        /// Updates the seek bar timer based on current playback state
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
                if (isPlaying && AreAudioObjectsValid())
                {
                    // Audio objects are valid, start the timer
                    seekBarTimer.Start();
                    Console.WriteLine("Seek bar timer started - audio objects are valid");
                }
                else
                {
                    // Store current position when pausing or when audio objects are invalid
                    if (AreAudioObjectsValid() && audioFileReader != null)
                    {
                        try
                        {
                            pausedPosition = audioFileReader.CurrentTime;
                            Console.WriteLine($"Stored paused position: {pausedPosition}");
                        }
                        catch (Exception ex)
                        {
                            // Any error, reset paused position
                            Console.WriteLine($"Error getting current time: {ex.Message}");
                            pausedPosition = TimeSpan.Zero;
                        }
                    }
                    else
                    {
                        // Audio objects are invalid, reset paused position
                        pausedPosition = TimeSpan.Zero;
                        Console.WriteLine("Audio objects invalid - reset paused position to zero");

                        // Additional safety: if audio objects are invalid and we're not playing, reset to initial state
                        if (!isPlaying)
                        {
                            Console.WriteLine("Audio objects invalid and not playing - resetting to initial state");
                            ResetToInitialState();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log any errors, timer is already stopped
                Console.WriteLine($"Error updating seek bar timer: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");

                // If we encounter an error, reset to initial state as a safety measure
                Console.WriteLine("Error in UpdateSeekBarTimer - resetting to initial state");
                ResetToInitialState();
            }
        }

        /// <summary>
        /// Timer tick event to update seek bar
        /// </summary>
        private void SeekBarTimer_Tick(object? sender, EventArgs e)
        {
            // Don't process timer ticks if we're updating audio objects
            if (isUpdatingAudioObjects) return;

            // Don't update the seekbar while the user is dragging
            if (isDragging) return;

            // Immediately stop the timer if we're not playing or have no audio reader
            if (!isPlaying || !AreAudioObjectsValid())
            {
                if (seekBarTimer != null)
                {
                    seekBarTimer.Stop();
                    Console.WriteLine("Seek bar timer stopped - not playing or audio objects invalid");
                }

                // Additional safety: if we're not playing and audio objects are invalid, reset to initial state
                if (!isPlaying && !AreAudioObjectsValid())
                {
                    Console.WriteLine("Timer stopped due to invalid state - resetting to initial state");
                    ResetToInitialState();
                }

                return;
            }

            try
            {
                // Update the seek bar with current time
                if (audioFileReader != null)
                {
                    UpdateSeekBar(audioFileReader.CurrentTime, totalDuration);
                }
                else
                {
                    // audioFileReader became null, stop the timer
                    Console.WriteLine("audioFileReader became null during timer tick");
                    if (seekBarTimer != null)
                    {
                        seekBarTimer.Stop();
                    }
                }
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
            catch (NullReferenceException)
            {
                // AudioFileReader is null, stop the timer
                Console.WriteLine("AudioFileReader is null during timer tick");
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

                // If we encounter an error, reset to initial state as a safety measure
                Console.WriteLine("Error in SeekBarTimer_Tick - resetting to initial state");
                ResetToInitialState();
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
        private TimeSpan dragTargetPosition; // Store the visual position user sees while dragging

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

            // Calculate the target position based on click (0-1 range)
            double progress = clickPosition / currentSeekBarWidth;
            progress = Math.Max(0, Math.Min(1, progress));
            dragTargetPosition = TimeSpan.FromSeconds(progress * totalDuration.TotalSeconds);

            // Don't seek immediately - just update the visual display
            // The actual seek will happen when the user releases the mouse
            System.Diagnostics.Debug.WriteLine($"Drag started at position: {clickPosition}, target time: {dragTargetPosition}");

            // Immediately update the seekbar display to show the new position
            if (audioFileReader != null && totalDuration.TotalSeconds > 0)
            {
                try
                {
                    UpdateSeekBar(dragTargetPosition, totalDuration);
                }
                catch (ObjectDisposedException)
                {
                    // AudioFileReader was disposed, ignore
                }
                catch (NullReferenceException)
                {
                    // AudioFileReader is null, ignore
                }
            }

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

            // Calculate the target position based on current mouse position (0-1 range)
            double progress = currentPosition / currentSeekBarWidth;
            progress = Math.Max(0, Math.Min(1, progress));
            dragTargetPosition = TimeSpan.FromSeconds(progress * totalDuration.TotalSeconds);

            // Don't seek immediately - just update the visual display
            // The actual seek will happen when the user releases the mouse
            System.Diagnostics.Debug.WriteLine($"Dragging to position: {currentPosition}, target time: {dragTargetPosition}");

            // Immediately update the seekbar display to show the new position during drag
            if (audioFileReader != null && totalDuration.TotalSeconds > 0)
            {
                try
                {
                    UpdateSeekBar(dragTargetPosition, totalDuration);
                }
                catch (ObjectDisposedException)
                {
                    // AudioFileReader was disposed, ignore
                }
                catch (NullReferenceException)
                {
                    // AudioFileReader is null, ignore
                }
            }
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

                // Jump to the stored drag target position when drag ends
                if (audioFileReader != null && totalDuration.TotalSeconds > 0)
                {
                    try
                    {
                        // Additional null check for safety
                        if (audioFileReader != null)
                        {
                            // Set the audio to the exact position the user was seeing during drag
                            audioFileReader.CurrentTime = dragTargetPosition;
                            // Update the seekbar display to show the final position
                            UpdateSeekBar(dragTargetPosition, totalDuration);
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // AudioFileReader was disposed, ignore
                    }
                    catch (NullReferenceException)
                    {
                        // AudioFileReader is null, ignore
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

                // Jump to the stored drag target position when drag ends
                if (audioFileReader != null && totalDuration.TotalSeconds > 0)
                {
                    try
                    {
                        // Additional null check for safety
                        if (audioFileReader != null)
                        {
                            // Set the audio to the exact position the user was seeing during drag
                            audioFileReader.CurrentTime = dragTargetPosition;
                            // Update the seekbar display to show the final position
                            UpdateSeekBar(dragTargetPosition, totalDuration);
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // AudioFileReader was disposed, ignore
                    }
                    catch (NullReferenceException)
                    {
                        // AudioFileReader is null, ignore
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
                // Additional null check for safety
                if (audioFileReader == null)
                {
                    System.Diagnostics.Debug.WriteLine("Cannot seek: audioFileReader is null");
                    return;
                }

                // Use the stored seek bar width that gets updated dynamically
                double seekBarWidth = currentSeekBarWidth;

                // Debug output to see what values we're getting
                System.Diagnostics.Debug.WriteLine($"=== SEEK DEBUG INFO ===");
                System.Diagnostics.Debug.WriteLine($"clickPosition: {clickPosition}");
                System.Diagnostics.Debug.WriteLine($"currentSeekBarWidth: {currentSeekBarWidth}");
                System.Diagnostics.Debug.WriteLine($"totalDuration: {totalDuration.TotalSeconds}");

                // Additional null check before accessing CurrentTime
                if (audioFileReader != null)
                {
                    System.Diagnostics.Debug.WriteLine($"audioFileReader.CurrentTime before seek: {audioFileReader.CurrentTime}");
                }

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

                // Additional null check before seeking
                if (audioFileReader != null)
                {
                    // Seek to the target position
                    audioFileReader.CurrentTime = targetPosition;

                    // Update the seek bar immediately
                    UpdateSeekBar(targetPosition, totalDuration);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Cannot seek: audioFileReader became null during seek operation");
                }
            }
            catch (ObjectDisposedException)
            {
                // AudioFileReader was disposed, ignore the seek operation
                System.Diagnostics.Debug.WriteLine("Cannot seek: AudioFileReader has been disposed");
            }
            catch (NullReferenceException)
            {
                // AudioFileReader is null, ignore the seek operation
                System.Diagnostics.Debug.WriteLine("Cannot seek: AudioFileReader is null");
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
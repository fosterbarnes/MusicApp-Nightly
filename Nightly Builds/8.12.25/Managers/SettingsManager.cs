using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace MusicApp
{
    public class SettingsManager
    {
        private static readonly string AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "MusicApp");
        
        private static readonly string SettingsFilePath = Path.Combine(AppDataPath, "settings.json");

        public class AppSettings
        {
            public WindowStateSettings WindowState { get; set; } = new WindowStateSettings();
            public PlayerSettings Player { get; set; } = new PlayerSettings();
        }

        public class PlayerSettings
        {
            public bool IsShuffleEnabled { get; set; } = false;
            public RepeatMode RepeatMode { get; set; } = RepeatMode.Off;
        }

        public enum RepeatMode
        {
            Off = 0,
            All = 1,
            One = 2
        }

        public class WindowStateSettings
        {
            public bool IsMaximized { get; set; } = false;
            public double Width { get; set; } = 1200;
            public double Height { get; set; } = 700;
            public double Left { get; set; } = 100;
            public double Top { get; set; } = 100;
        }

        private static SettingsManager? _instance;
        private static readonly object _lock = new object();

        public static SettingsManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new SettingsManager();
                    }
                }
                return _instance;
            }
        }

        private SettingsManager()
        {
            EnsureAppDataDirectoryExists();
        }

        private void EnsureAppDataDirectoryExists()
        {
            if (!Directory.Exists(AppDataPath))
            {
                Directory.CreateDirectory(AppDataPath);
            }
        }

        #region Settings Management

        public async Task<AppSettings> LoadSettingsAsync()
        {
            try
            {
                Console.WriteLine($"LoadSettingsAsync - Settings file exists: {File.Exists(SettingsFilePath)}");
                Console.WriteLine($"LoadSettingsAsync - Settings file path: {SettingsFilePath}");
                
                if (File.Exists(SettingsFilePath))
                {
                    var json = await File.ReadAllTextAsync(SettingsFilePath);
                    Console.WriteLine($"LoadSettingsAsync - JSON content: {json}");
                    
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, options);
                    Console.WriteLine($"LoadSettingsAsync - Deserialized settings is null: {settings == null}");
                    
                    // Ensure all properties are properly initialized
                    if (settings != null)
                    {
                        Console.WriteLine($"LoadSettingsAsync - Player is null: {settings.Player == null}");
                        Console.WriteLine($"LoadSettingsAsync - WindowState is null: {settings.WindowState == null}");
                        
                        settings.Player ??= new PlayerSettings();
                        settings.WindowState ??= new WindowStateSettings();
                        
                        Console.WriteLine($"LoadSettingsAsync - After null coalescing - Player.IsShuffleEnabled: {settings.Player.IsShuffleEnabled}");
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            
            // Return new settings with proper initialization
            Console.WriteLine("LoadSettingsAsync - Returning new AppSettings with defaults");
            return new AppSettings
            {
                Player = new PlayerSettings(),
                WindowState = new WindowStateSettings()
            };
        }

        public async Task SaveSettingsAsync(AppSettings settings)
        {
            try
            {
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var json = JsonSerializer.Serialize(settings, options);
                await File.WriteAllTextAsync(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        #endregion

        #region Player Settings

        public async Task<bool> GetShuffleStateAsync()
        {
            var settings = await LoadSettingsAsync();
            Console.WriteLine($"GetShuffleStateAsync - Player is null: {settings.Player == null}");
            if (settings.Player != null)
            {
                Console.WriteLine($"GetShuffleStateAsync - Player.IsShuffleEnabled: {settings.Player.IsShuffleEnabled}");
            }
            return settings.Player?.IsShuffleEnabled ?? false;
        }

        public async Task SetShuffleStateAsync(bool isEnabled)
        {
            Console.WriteLine($"SetShuffleStateAsync called with value: {isEnabled}");
            var settings = await LoadSettingsAsync();
            Console.WriteLine($"Loaded settings - Player is null: {settings.Player == null}");
            if (settings.Player != null)
            {
                settings.Player.IsShuffleEnabled = isEnabled;
                Console.WriteLine($"Set Player.IsShuffleEnabled to: {settings.Player.IsShuffleEnabled}");
                await SaveSettingsAsync(settings);
                Console.WriteLine($"Settings saved successfully");
            }
        }

        public async Task<RepeatMode> GetRepeatModeAsync()
        {
            var settings = await LoadSettingsAsync();
            return settings.Player?.RepeatMode ?? RepeatMode.Off;
        }

        public async Task SetRepeatModeAsync(RepeatMode repeatMode)
        {
            var settings = await LoadSettingsAsync();
            if (settings.Player != null)
            {
                settings.Player.RepeatMode = repeatMode;
                await SaveSettingsAsync(settings);
            }
        }

        #endregion
    }
}

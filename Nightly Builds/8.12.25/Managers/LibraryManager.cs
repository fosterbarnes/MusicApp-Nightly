using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace MusicApp
{
    public class LibraryManager
    {
        private static readonly string AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "MusicApp");
        
        private static readonly string LibraryCacheFilePath = Path.Combine(AppDataPath, "library.json");
        private static readonly string RecentlyPlayedFilePath = Path.Combine(AppDataPath, "recentlyPlayed.json");
        private static readonly string LibraryFoldersFilePath = Path.Combine(AppDataPath, "libraryFolders.json");
        private static readonly string PlaylistsFilePath = Path.Combine(AppDataPath, "playlists.json");

        public class LibraryCache
        {
            public List<Song> Tracks { get; set; } = new List<Song>();
        }

        public class LibraryFolders
        {
            public List<string> MusicFolders { get; set; } = new List<string>();
            public Dictionary<string, DateTime> FolderLastScanned { get; set; } = new Dictionary<string, DateTime>();
        }

        public class RecentlyPlayedCache
        {
            public List<RecentlyPlayedItem> RecentlyPlayed { get; set; } = new List<RecentlyPlayedItem>();
        }

        public class RecentlyPlayedItem
        {
            public string FilePath { get; set; } = "";
            public DateTime LastPlayed { get; set; } = DateTime.Now;
        }

        public class PlaylistsCache
        {
            public List<Playlist> Playlists { get; set; } = new List<Playlist>();
        }

        private static LibraryManager? _instance;
        private static readonly object _lock = new object();

        public static LibraryManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new LibraryManager();
                    }
                }
                return _instance;
            }
        }

        private LibraryManager()
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

        #region Library Cache Management

        public async Task<LibraryCache> LoadLibraryCacheAsync()
        {
            try
            {
                if (File.Exists(LibraryCacheFilePath))
                {
                    var json = await File.ReadAllTextAsync(LibraryCacheFilePath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var cache = JsonSerializer.Deserialize<LibraryCache>(json, options);
                    return cache ?? new LibraryCache();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading library cache: {ex.Message}");
            }
            return new LibraryCache();
        }

        public async Task SaveLibraryCacheAsync(LibraryCache cache)
        {
            try
            {
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var json = JsonSerializer.Serialize(cache, options);
                await File.WriteAllTextAsync(LibraryCacheFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving library cache: {ex.Message}");
            }
        }

        #endregion

        #region Recently Played Management

        public async Task<RecentlyPlayedCache> LoadRecentlyPlayedAsync()
        {
            try
            {
                if (File.Exists(RecentlyPlayedFilePath))
                {
                    var json = await File.ReadAllTextAsync(RecentlyPlayedFilePath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var recentlyPlayed = JsonSerializer.Deserialize<RecentlyPlayedCache>(json, options);
                    return recentlyPlayed ?? new RecentlyPlayedCache();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading recently played: {ex.Message}");
            }
            return new RecentlyPlayedCache();
        }

        public async Task SaveRecentlyPlayedAsync(RecentlyPlayedCache recentlyPlayed)
        {
            try
            {
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var json = JsonSerializer.Serialize(recentlyPlayed, options);
                await File.WriteAllTextAsync(RecentlyPlayedFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving recently played: {ex.Message}");
            }
        }

        #endregion

        #region Library Folders Management

        public async Task<LibraryFolders> LoadLibraryFoldersAsync()
        {
            try
            {
                if (File.Exists(LibraryFoldersFilePath))
                {
                    var json = await File.ReadAllTextAsync(LibraryFoldersFilePath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var folders = JsonSerializer.Deserialize<LibraryFolders>(json, options);
                    return folders ?? new LibraryFolders();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading library folders: {ex.Message}");
            }
            return new LibraryFolders();
        }

        public async Task SaveLibraryFoldersAsync(LibraryFolders folders)
        {
            try
            {
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var json = JsonSerializer.Serialize(folders, options);
                await File.WriteAllTextAsync(LibraryFoldersFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving library folders: {ex.Message}");
            }
        }

        #endregion

        #region Playlists Management

        public async Task<PlaylistsCache> LoadPlaylistsAsync()
        {
            try
            {
                if (File.Exists(PlaylistsFilePath))
                {
                    var json = await File.ReadAllTextAsync(PlaylistsFilePath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var playlists = JsonSerializer.Deserialize<PlaylistsCache>(json, options);
                    return playlists ?? new PlaylistsCache();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading playlists: {ex.Message}");
            }
            return new PlaylistsCache();
        }

        public async Task SavePlaylistsAsync(PlaylistsCache playlists)
        {
            try
            {
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var json = JsonSerializer.Serialize(playlists, options);
                await File.WriteAllTextAsync(PlaylistsFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving playlists: {ex.Message}");
            }
        }

        #endregion

        #region Music Folders Management

        public async Task<List<string>> GetMusicFoldersAsync()
        {
            var folders = await LoadLibraryFoldersAsync();
            return folders.MusicFolders ?? new List<string>();
        }

        public async Task AddMusicFolderAsync(string folderPath)
        {
            var folders = await LoadLibraryFoldersAsync();
            if (!folders.MusicFolders.Contains(folderPath))
            {
                folders.MusicFolders.Add(folderPath);
                await SaveLibraryFoldersAsync(folders);
            }
        }

        public async Task RemoveMusicFolderAsync(string folderPath)
        {
            var folders = await LoadLibraryFoldersAsync();
            if (folders.MusicFolders.Contains(folderPath))
            {
                folders.MusicFolders.Remove(folderPath);
                await SaveLibraryFoldersAsync(folders);
            }
        }

        #endregion

        #region Utility Methods

        public async Task<bool> HasNewFilesInFolderAsync(string folderPath)
        {
            try
            {
                var folders = await LoadLibraryFoldersAsync();
                if (!folders.FolderLastScanned.ContainsKey(folderPath))
                    return true;

                var lastScanned = folders.FolderLastScanned[folderPath];
                var directoryInfo = new DirectoryInfo(folderPath);
                
                // Check if any files have been modified since last scan
                var supportedExtensions = new[] { ".mp3", ".wav", ".flac", ".m4a", ".aac" };
                var musicFiles = directoryInfo.GetFiles("*.*", SearchOption.AllDirectories)
                    .Where(file => supportedExtensions.Contains(file.Extension.ToLower()));

                return musicFiles.Any(file => file.LastWriteTime > lastScanned);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking for new files: {ex.Message}");
                return true; // Assume there are new files if we can't check
            }
        }

        public async Task UpdateFolderScanTimeAsync(string folderPath)
        {
            try
            {
                var folders = await LoadLibraryFoldersAsync();
                folders.FolderLastScanned[folderPath] = DateTime.Now;
                await SaveLibraryFoldersAsync(folders);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating folder scan time: {ex.Message}");
            }
        }

        public async Task RemoveFolderFromCacheAsync(string folderPath)
        {
            try
            {
                var folders = await LoadLibraryFoldersAsync();
                
                // Remove folder from scan times
                if (folders.FolderLastScanned.ContainsKey(folderPath))
                {
                    folders.FolderLastScanned.Remove(folderPath);
                }
                
                await SaveLibraryFoldersAsync(folders);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing folder from cache: {ex.Message}");
            }
        }

        #endregion
    }
}

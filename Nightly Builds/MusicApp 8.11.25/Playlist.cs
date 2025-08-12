using System;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Linq;

namespace MusicApp
{
    public class Playlist
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        
        [JsonIgnore]
        public ObservableCollection<MusicTrack> Tracks { get; set; } = new ObservableCollection<MusicTrack>();
        
        // For serialization - we'll store the file paths and reconstruct the tracks
        public List<string> TrackFilePaths { get; set; } = new List<string>();
        
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime LastModified { get; set; } = DateTime.Now;

        public Playlist()
        {
        }

        public Playlist(string name, string description = "")
        {
            Name = name;
            Description = description;
        }

        public void AddTrack(MusicTrack track)
        {
            if (!Tracks.Any(t => t.FilePath == track.FilePath))
            {
                Tracks.Add(track);
                TrackFilePaths.Add(track.FilePath);
                LastModified = DateTime.Now;
            }
        }

        public void RemoveTrack(MusicTrack track)
        {
            Tracks.Remove(track);
            TrackFilePaths.Remove(track.FilePath);
            LastModified = DateTime.Now;
        }

        public void Clear()
        {
            Tracks.Clear();
            TrackFilePaths.Clear();
            LastModified = DateTime.Now;
        }

        // Method to reconstruct tracks from file paths
        public void ReconstructTracks(IEnumerable<MusicTrack> availableTracks)
        {
            Tracks.Clear();
            foreach (var filePath in TrackFilePaths)
            {
                var track = availableTracks.FirstOrDefault(t => t.FilePath == filePath);
                if (track != null)
                {
                    Tracks.Add(track);
                }
            }
        }
    }
} 
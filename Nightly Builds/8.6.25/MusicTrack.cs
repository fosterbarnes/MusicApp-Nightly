using System;
using System.Text.Json.Serialization;

namespace MusicApp
{
    public class MusicTrack
    {
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        public string Album { get; set; } = "";
        public string Duration { get; set; } = "";
        public string FilePath { get; set; } = "";
        
        [JsonIgnore]
        public TimeSpan DurationTimeSpan { get; set; }
        
        public string AlbumArtPath { get; set; } = "";
        public int TrackNumber { get; set; }
        public int Year { get; set; }
        public string Genre { get; set; } = "";

        // Additional fields for future categorization
        public string Composer { get; set; } = "";
        public string AlbumArtist { get; set; } = "";
        public string DiscNumber { get; set; } = "";
        public string Bitrate { get; set; } = "";
        public string SampleRate { get; set; } = "";
        public long FileSize { get; set; }
        public DateTime DateAdded { get; set; } = DateTime.Now;
        public DateTime LastPlayed { get; set; } = DateTime.MinValue;
        public int PlayCount { get; set; } = 0;

        public override string ToString()
        {
            return $"{Title} - {Artist}";
        }

        // Helper method to update play count and last played
        public void MarkAsPlayed()
        {
            PlayCount++;
            LastPlayed = DateTime.Now;
        }
    }
} 
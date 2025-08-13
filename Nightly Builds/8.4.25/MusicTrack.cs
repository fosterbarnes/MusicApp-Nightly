using System;

namespace MusicApp
{
    public class MusicTrack
    {
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        public string Album { get; set; } = "";
        public string Duration { get; set; } = "";
        public string FilePath { get; set; } = "";
        public TimeSpan DurationTimeSpan { get; set; }
        public string AlbumArtPath { get; set; } = "";
        public int TrackNumber { get; set; }
        public int Year { get; set; }
        public string Genre { get; set; } = "";

        public override string ToString()
        {
            return $"{Title} - {Artist}";
        }
    }
} 
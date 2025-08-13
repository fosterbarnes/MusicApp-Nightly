using System.Collections.ObjectModel;

namespace MusicApp
{
    public class Playlist
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public ObservableCollection<MusicTrack> Tracks { get; set; } = new ObservableCollection<MusicTrack>();
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
            Tracks.Add(track);
            LastModified = DateTime.Now;
        }

        public void RemoveTrack(MusicTrack track)
        {
            Tracks.Remove(track);
            LastModified = DateTime.Now;
        }

        public void Clear()
        {
            Tracks.Clear();
            LastModified = DateTime.Now;
        }
    }
} 
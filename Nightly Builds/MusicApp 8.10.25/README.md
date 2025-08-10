# Music App - iTunes-like WPF Application

A modern, beautiful music player built with WPF and C# that provides an iTunes-like experience for Windows.

## Features

### 🎵 Music Library Management
- **Add Music Folders**: Import your entire music collection by selecting folders
- **Supported Formats**: MP3, WAV, FLAC, M4A, AAC
- **Smart Metadata Extraction**: 
  - Reads embedded ID3 tags when available
  - Falls back to filename parsing (Artist - Title format)
  - Uses directory structure for album and artist information
  - Extracts Time using NAudio
- **Album Art Display**: 
  - Looks for embedded album artwork in music files
  - Searches for image files in the same directory (cover.jpg, album.png, etc.)
  - Supports JPG, PNG, BMP, and GIF formats

### 🎧 Playback Controls
- **Play/Pause**: Control playback with a beautiful Material Design interface
- **Previous/Next**: Navigate between tracks easily
- **Volume Control**: Adjust volume with a slider
- **Track Information**: See current track details in the player bar

### 🔍 Search and Navigation
- **Real-time Search**: Search through your music library by title, artist, or album
- **Sidebar Navigation**: Easy access to different sections
- **Recently Played**: Track your listening history

### 📋 Playlist Management
- **Create Playlists**: Organize your music into custom playlists
- **Sample Playlists**: Pre-loaded with "Favorites", "Workout Mix", and "Chill Vibes"
- **Playlist Viewing**: Browse and manage your playlists

### 🎨 Modern UI
- **Material Design**: Beautiful, modern interface using Material Design principles
- **Dark Theme**: Easy on the eyes with a dark color scheme
- **Responsive Layout**: Adapts to different window sizes
- **Professional Look**: Clean, iTunes-inspired design

## Getting Started

### Prerequisites
- Visual Studio 2022
- .NET 8.0 SDK
- Windows 10/11

### Installation
1. Open the solution in Visual Studio 2022
2. Restore NuGet packages (Visual Studio will do this automatically)
3. Build the solution (Ctrl+Shift+B)
4. Run the application (F5)

### First Steps
1. **Add Your Music**: Click "Add Music Folder" in the sidebar to import your music collection
2. **Browse Your Library**: Use the "Music Library" section to view all your tracks
3. **Search**: Use the search bar to find specific songs, artists, or albums
4. **Play Music**: Click on any track to start playing
5. **Create Playlists**: Use the "Playlists" section to organize your music

## Project Structure

```
MusicApp/
├── MainWindow.xaml          # Main UI layout
├── MainWindow.xaml.cs       # Main application logic
├── MusicTrack.cs           # Data model for music tracks
├── Playlist.cs             # Data model for playlists
├── App.xaml                # Application resources
└── MusicApp.csproj         # Project configuration
```

## Key Components

### Data Models
- **MusicTrack**: Represents a single music track with metadata
- **Playlist**: Represents a collection of music tracks

### UI Sections
- **Sidebar**: Navigation between different views
- **Main Content**: Displays music library, playlists, or recently played
- **Player Bar**: Shows current track and playback controls

### Libraries Used
- **NAudio**: Audio playback and processing
- **MaterialDesignThemes**: Modern UI components
- **Custom Metadata Extraction**: Smart fallback system for reading music metadata

## Customization

### Adding New Features
The modular design makes it easy to add new features:
- Add new navigation buttons in the sidebar
- Create new data models for additional functionality
- Extend the UI with new views and controls

### Styling
The app uses Material Design theming:
- Colors can be customized in the XAML resources
- Icons can be changed using Material Design icons
- Layout can be modified in the XAML files

## Troubleshooting

### Common Issues
1. **No Audio**: Check your system volume and audio drivers
2. **Files Not Loading**: Ensure music files are in supported formats
3. **Missing Metadata**: Some files may not have complete metadata tags

### Performance Tips
- Large music libraries may take time to load initially
- Search is optimized for real-time filtering
- Album art loading is handled efficiently

## Future Enhancements

Potential features to add:
- **Equalizer**: Audio equalization controls
- **Crossfade**: Smooth transitions between tracks
- **Shuffle/Repeat**: Playback mode controls
- **Mini Player**: Compact player mode
- **Keyboard Shortcuts**: Media key support
- **Last.fm Integration**: Scrobbling support
- **Cloud Storage**: Integration with cloud music services

## Contributing

This is a learning project that demonstrates:
- WPF application development
- Material Design implementation
- Audio processing with NAudio
- File system operations
- Data binding and MVVM patterns

Feel free to use this as a starting point for your own music applications!

## License

This project is for educational purposes. Feel free to modify and extend it for your own use. 
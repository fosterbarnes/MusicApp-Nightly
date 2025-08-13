# MusicApp - Modern WPF Music Player

A sophisticated, feature-rich music player built with WPF and C# that provides a modern, iTunes-inspired experience for Windows. Built with .NET 8.0 and featuring a custom title bar, advanced audio playback, and comprehensive music library management.

## 🎵 Core Features

### 🎧 Advanced Audio Playback
- **Multi-format Support**: MP3, WAV, FLAC, M4A, AAC using NAudio and ATL libraries
- **Smart Metadata Extraction**: 
  - Primary: Embedded ID3 tags and metadata
  - Fallback: Filename parsing (Artist - Title format)
  - Directory structure analysis for album/artist information
  - Audio duration extraction using NAudio
- **Professional Audio Engine**: Built on NAudio for high-quality playback
- **Seek Bar**: Visual progress tracking with real-time updates

### 🎛️ Player Controls & Features
- **Play/Pause**: Intuitive playback control
- **Previous/Next**: Track navigation with visual feedback
- **Volume Control**: Slider-based volume with mute toggle
- **Shuffle Mode**: Random track selection with visual state indication
- **Repeat Modes**: Off, All tracks, Single track repeat
- **Seek Bar**: Click-to-seek functionality with visual feedback

### 📚 Music Library Management
- **Folder Import**: Add entire music collections by selecting folders
- **Smart Caching**: Persistent library cache for fast loading
- **Metadata Display**: Title, Artist, Album, Duration, Track Number, Year, Genre
- **Album Art Support**: Embedded artwork and directory-based image files
- **File Management**: Recycle bin integration for safe file deletion

### 📋 Playlist System
- **Custom Playlists**: Create and manage personal music collections
- **Persistent Storage**: JSON-based playlist persistence
- **Track Management**: Add, remove, and reorder tracks
- **Sample Data**: Pre-loaded example playlists for new users

### 🎨 Modern UI & Experience
- **Custom Title Bar**: Full control over window appearance and behavior
- **Material Design**: Beautiful, modern interface using Material Design principles
- **Dark Theme**: Professional dark color scheme (#1E1E1E background)
- **Responsive Layout**: Adapts to different window sizes with minimum dimensions
- **Window State Management**: Maximize, minimize, and restore with custom logic

## 🏗️ Project Architecture

### Current Project Structure
```
MusicApp/
├── Models/                    # Data models
│   ├── Song.cs              # Music track representation
│   └── Playlist.cs          # Playlist data structure
├── Managers/                 # Business logic managers
│   ├── LibraryManager.cs    # Music library and caching
│   ├── SettingsManager.cs   # Application settings
│   └── WindowManager.cs     # Window state management
├── TitleBarWithPlayerControls/  # Custom title bar
│   ├── TitleBar.xaml        # Title bar UI layout
│   └── TitleBar.xaml.cs     # Title bar logic
├── Converters/               # WPF value converters
│   ├── ValueConverters.cs   # Slider and progress converters
│   └── README.md            # Converter documentation
├── Resources/                # Application resources
│   ├── icon/                # Application icon (.ico)
│   └── svg/                 # Custom SVG icons
├── MainWindow.xaml           # Main application window
├── MainWindow.xaml.cs        # Main application logic
├── Styles.xaml               # Global styles and themes
├── App.xaml                  # Application resources
└── MusicApp.csproj          # Project configuration
```

### Key Components

#### Data Models
- **Song**: Comprehensive music track representation with 20+ properties
- **Playlist**: Flexible playlist system with track management

#### Managers (Singleton Pattern)
- **LibraryManager**: Handles music library, caching, and file operations
- **SettingsManager**: Manages application settings and player preferences
- **WindowManager**: Controls window state, positioning, and custom maximize behavior

#### Custom Title Bar
- **Integrated Player Controls**: Playback controls built into the title bar
- **Window Management**: Custom minimize, maximize, and close functionality
- **State Persistence**: Remembers window position and state

## 🚀 Getting Started

### Prerequisites
- **Visual Studio 2022** (recommended) or VS Code
- **.NET 8.0 SDK**
- **Windows 10/11**

### Installation & Setup
1. **Clone/Download** the project
2. **Open** `MusicApp.sln` in Visual Studio
3. **Restore** NuGet packages (automatic in VS)
4. **Build** the solution (Ctrl+Shift+B)
5. **Run** the application (F5)

### First Launch Experience
1. **Add Music**: Use "Add Music Folder" to import your collection
2. **Browse Library**: Navigate through your imported music
3. **Create Playlists**: Organize tracks into custom collections
4. **Customize**: Adjust volume, enable shuffle/repeat modes

## 🔧 Technical Implementation

### Audio Processing
- **NAudio 2.2.1**: Professional audio playback engine
- **ATL Core 7.2.0**: Advanced metadata extraction
- **WaveOutEvent**: High-quality audio output
- **AudioFileReader**: Efficient file reading and seeking

### UI Framework
- **WPF (.NET 8.0)**: Modern Windows presentation framework
- **Material Design**: Professional UI components and theming
- **Custom Controls**: Tailored user experience
- **Data Binding**: MVVM-inspired architecture

### Data Persistence
- **JSON Storage**: Human-readable configuration files
- **AppData Location**: `%AppData%\MusicApp\`
- **Caching System**: Fast library loading and metadata storage
- **Settings Persistence**: Window state, player preferences, library cache

### Performance Features
- **Asynchronous Operations**: Non-blocking file operations
- **Smart Caching**: Avoids re-scanning unchanged folders
- **Lazy Loading**: Efficient memory usage
- **Background Processing**: UI remains responsive during operations

## 🎯 Current Capabilities

### ✅ Implemented Features
- Complete audio playback system
- Music library management with caching
- Custom title bar with integrated controls
- Playlist creation and management
- Shuffle and repeat modes
- Volume control and mute
- Seek bar functionality
- Window state persistence
- File deletion with recycle bin
- Recently played tracking
- Sample data for new users

### 🔄 In Progress/Planned
- Equalizer controls
- Crossfade between tracks
- Mini player mode
- Keyboard shortcuts
- Cloud storage integration
- Last.fm scrobbling

## 🛠️ Development & Customization

### Adding New Features
The modular architecture makes extension straightforward:
- **New Managers**: Add to `Managers/` folder following singleton pattern
- **UI Controls**: Extend existing XAML or create new custom controls
- **Data Models**: Add new models to `Models/` folder
- **Converters**: Implement new value converters in `Converters/`

### Styling & Theming
- **Global Styles**: Defined in `Styles.xaml`
- **Material Design**: Easily customizable color schemes
- **Custom Icons**: SVG-based icon system in `Resources/svg/`
- **Responsive Design**: Adapts to different screen sizes

### Code Quality Features
- **Async/Await**: Modern asynchronous programming patterns
- **Error Handling**: Comprehensive exception handling
- **Logging**: Debug output for troubleshooting
- **Memory Management**: Proper disposal of audio resources

## 🐛 Troubleshooting

### Common Issues
1. **No Audio Output**: Check system volume and audio drivers
2. **Files Not Loading**: Verify supported audio formats
3. **Missing Metadata**: Some files may have incomplete tags
4. **Performance Issues**: Large libraries may take time to cache initially

### Performance Optimization
- **Library Caching**: First scan creates persistent cache
- **Smart Scanning**: Only rescans folders with new files
- **Background Processing**: UI remains responsive during operations
- **Memory Efficiency**: Lazy loading of audio resources

## 📚 Learning Resources

This project demonstrates:
- **WPF Application Development**: Modern Windows UI development
- **Audio Processing**: Professional audio library integration
- **Custom Controls**: Building tailored user experiences
- **Data Persistence**: JSON-based configuration management
- **Async Programming**: Modern C# asynchronous patterns
- **Singleton Pattern**: Efficient resource management
- **Material Design**: Professional UI/UX implementation

## 📄 License & Usage

This project is designed for educational purposes and personal use. Feel free to:
- **Study** the code and architecture
- **Modify** for your own projects
- **Extend** with new features
- **Learn** from the implementation patterns

## 🤝 Contributing

While this is primarily a learning project, suggestions and improvements are welcome:
- **Bug Reports**: Document issues with reproduction steps
- **Feature Ideas**: Suggest new functionality
- **Code Improvements**: Propose optimizations or refactoring
- **Documentation**: Help improve this README or add code comments

---

**Built with ❤️ using WPF, C#, and .NET 8.0** 
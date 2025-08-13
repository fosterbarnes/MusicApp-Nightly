# ![Icon](https://i.postimg.cc/d3c9vxzF/Music-App-Icon24x24.png) MusicApp - An Offline Music Player ![Icon](https://i.postimg.cc/d3c9vxzF/Music-App-Icon24x24.png)

![MusicAppScreenshot](https://i.postimg.cc/DzN7dWWm/Music-App-TB5-Ff-AUlm-R.png)

MusicApp is in very early development. This repo mainly exists as an archive/backup of my daily progress. If you somehow stumble upon this repo, feel free to try it out but don't expect a complete app. Bugs are expected.

I hate streaming services. I have tried SO many music player apps like Foobar2000,
Musicbee, AIMP, Clementine, Strawberry, etc. and just don't like them. No disrespect to the creators but they're just not for me. I tolerate iTunes, and while it is functional and has a UI that I find more functional than the alternatives, it's very out of date, sluggish overall and (for some reason????) makes my twitch stream lag when I play music with it lmao (I'm a twitch streamer).

To be honest, this app is made so I can use as my daily music player. HOWEVER, if you agree with one or more of the previous statements, this app may also be for you too lol. It's made for Windows with WPF in C#, for this reason, Linux/macOS versions are not currently planned. My main concern is efficiency for my personal daily driver OS (Windows 10) not cross compatibility. The thought of making such a detailed and clean UI in Rust (my cross compat. language of choice) gives me goosebumps and shivers, ergo: WPF in C#, using XAML for styling.

If you want to use it, download the latest [release.zip](https://github.com/fosterbarnes/MusicApp-Nightly/releases/latest), unzip, then run MusicApp.exe

## Implemented Features

- Working audio playback with various file types. Lossless support
- Working music library settings that save when the app closes.
- Ability to remove music library folders and clear settings.
- Auto-rescan music library folders when the app launches.
- Combined title/media control bar. Includes:
  - Reverse, play/pause and skip buttons.
  - Volume control slider
  - Currently playing track viewport with working seek bar and song info. Clicking and dragging takes the user to the selected time.
  - Currently playing track section auto-centers and resizes based on window size.
  - Minimize, maximize, and close buttons
- Ability to add a music library folder
- Basic playlist menu
- Basic recently played menu
- Shuffle and repeat buttons
- Queue view to see all songs in the current queue

## Planned Features

#### General/Playback

- Settings menu:
  - EQ
  - Multiple audio backends
  - Themes/colors
  - Cross-fading between songs
  - Volume normalization
  - Sample rate
- Ability to edit metadata
- Visualizer
- Playlist import support
- _POSSIBLE_ iTunes library import support. I need to look into whether that's legal or not lmao
- Audio file converting/compressing
- Album art scraper
- Optional metadata correction/cleanup
- Robust queuing system/menu. I like to make "on the fly" playlists with my queues, so it must be as seamless and robust as possible
- "Like" system and liked tracks menu
- Keyboard shortcuts for actions like "play/pause", "skip" "volume up/down" etc. These should work whether or not the app window is focused
- Mini-player window that can be open in addition to the main window, or as a replacement to the main window
- Support for multiple libraries
- Option to add "Add to MusicApp" to windows right-click context menu

#### Menu/UI

- Separate artist, album, songs, recently added and genre menus/lists
  - Large thumbnails for album and recently added menus with a "drop-down" view when clicked
  - List view for songs and genre menus
  - Combined list/thumbnail view for artists menu. Artists will be displayed in a list, their albums will be sorted and shown with large thumbnails similar to the dropdown when clicked in album view
- Light and dark mode options. Default is dark mode
- Multiple color themes (1st priority being mint green for my girlfriend), this will also change the icon

#### Title Bar

- Allow the user to click the artist or album name from the currently playing song view. Clicking will open the respective item in the library
- Move search to the title bar

#### Integration

- Last.fm support
- Possible media server integration (primarily emby/jellyfin because that's what I use)

#### Backend/Boring Stuff

- Automatic updates integrated with GitHub releases
- Installer
- Option for portable version

## Support

If you have any issues, create an issue from the [Issues](https://github.com/fosterbarnes/rustitles/issues) tab and I will get back to you as quickly as possible.

If you'd like to support me, follow me on twitch:
[https://www.twitch.tv/fosterbarnes](https://www.twitch.tv/fosterbarnes)

or if you're feeling generous drop a donation:
[https://coff.ee/fosterbarnes](https://coff.ee/fosterbarnes)

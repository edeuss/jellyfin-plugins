# Jellyfin.Plugin.Animated.Music

A Jellyfin plugin that adds support for animated covers and vertical video backgrounds for music albums.

## Features

- **Animated Covers**: Support for animated GIF, MP4, WebM, MOV, and AVI files as album covers
- **Vertical Backgrounds**: Support for vertical video backgrounds for music albums
- **Configurable**: Customizable filename patterns and file size limits
- **Web Interface**: Easy-to-use configuration page in the Jellyfin dashboard
- **API Endpoints**: REST API endpoints to access animated files programmatically
- **Web Interface Integration**: Automatic display of animated covers on album details pages

## Installation

### Building from Source

1. Clone this repository
2. Build the project using .NET 8.0:

   ```bash
   dotnet build --configuration Release
   ```

3. Copy the built files to your Jellyfin plugins directory
4. Restart Jellyfin Server

## Usage

### File Structure

Place your animated files in your music album folders with the following naming convention:

```
Music/
├── Artist Name/
│   ├── Album Name/
│   │   ├── 01 - Track 1.mp3
│   │   ├── 02 - Track 2.mp3
│   │   ├── cover-animated.gif          # Animated cover
│   │   ├── vertical-background.mp4     # Vertical background
│   │   └── cover.jpg                   # Static cover (optional)
│   └── Another Album/
│       ├── 01 - Track 1.mp3
│       ├── cover-animated.webm         # Different format
│       └── vertical-background.mov     # Different format
```

### Supported File Formats

- **Animated Covers**: `.gif`, `.mp4`, `.webm`, `.mov`, `.avi`
- **Vertical Backgrounds**: `.gif`, `.mp4`, `.webm`, `.mov`, `.avi`

### Configuration

Access the plugin configuration through the Jellyfin dashboard:

1. Go to **Dashboard** → **Plugins**
2. Find **Animated Music** in the list
3. Click **Configure**

#### Configuration Options

- **Enable Animated Covers**: Toggle animated cover support
- **Enable Vertical Backgrounds**: Toggle vertical background support
- **Animated Cover Filename Pattern**: Base filename for animated covers (default: `cover-animated`)
- **Vertical Background Filename Pattern**: Base filename for vertical backgrounds (default: `vertical-background`)
- **Maximum File Size (MB)**: Maximum allowed file size for animated files (default: 50MB)
- **Supported Formats**: Comma-separated list of supported file extensions

## API Endpoints

The plugin provides REST API endpoints to access animated files programmatically:

### Get Animated Cover

```
GET /AnimatedMusic/Album/{albumId}/AnimatedCover
```

Returns the animated cover file for the specified album.

### Get Vertical Background

```
GET /AnimatedMusic/Album/{albumId}/VerticalBackground
```

Returns the vertical background file for the specified album.

### Get Animated Info

```
GET /AnimatedMusic/Album/{albumId}/Info
```

Returns information about available animated files for the album, including:

- Whether animated cover and background are available
- Direct URLs to access the files
- Album information

### Example Usage

#### Get animated cover

```bash
curl "http://your-jellyfin-server/AnimatedMusic/Album/d5861930-8da6-499c-b7dd-235c60703f64/AnimatedCover"
```

#### Get vertical background

```bash
curl "http://your-jellyfin-server/AnimatedMusic/Album/d5861930-8da6-499c-b7dd-235c60703f64/VerticalBackground"
```

#### Get info about available files

```bash
curl "http://your-jellyfin-server/AnimatedMusic/Album/d5861930-8da6-499c-b7dd-235c60703f64/Info"
```

#### Example Response (Info endpoint)

```json
{
  "albumId": "d5861930-8da6-499c-b7dd-235c60703f64",
  "hasAnimatedCover": true,
  "hasVerticalBackground": true,
  "animatedCoverUrl": "/AnimatedMusic/Album/d5861930-8da6-499c-b7dd-235c60703f64/AnimatedCover",
  "verticalBackgroundUrl": "/AnimatedMusic/Album/d5861930-8da6-499c-b7dd-235c60703f64/VerticalBackground"
}
```

### Notes

- All endpoints respect the plugin configuration settings
- Files are served with appropriate MIME types (image/gif, video/mp4, etc.)
- Album IDs must be valid GUIDs
- Returns 404 if files are not found or features are disabled

### Building

```bash
# Restore dependencies
dotnet restore

# Build in Release mode
dotnet build --configuration Release

# Run tests (if any)
dotnet test
```

## Troubleshooting

### Logs

Check the Jellyfin server logs for plugin-related messages. Look for entries containing "Animated Music" or "Jellyfin.Plugin.Animated.Music".

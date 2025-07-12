# Jellyfin.Plugin.Animated.Music

A Jellyfin plugin that adds support for animated covers and vertical video backgrounds for music albums through an REST API for clients to use.

## Features

- **Animated Covers**: Support for animated GIF, MP4, WebM, MOV, and AVI files as album covers (square album cover video, replaces the image album cover)
- **Vertical Backgrounds**: Support for vertical video backgrounds for music albums or tracks (a vertical background is a 9:16 aspect ratio video designed to fill the entire screen with playback controls overlaid at the bottom)

## Installation

### Building from Source

1. Clone this repository
2. Build the project using .NET 8.0:

   ```bash
   sh build.sh
   ```

3. Copy the built .zip to your Jellyfin plugins directory
4. Restart Jellyfin Server

### From Catalog page

1. Navigate to the dashboard plugins catalog page.
2. Click on the gear icon.
3. Click on the + (plus) icon.
4. Add this Repository URL

   ```text
   https://raw.githubusercontent.com/edeuss/jellyfin-plugins/refs/heads/main/manifest.json
   ```

5. Navigate back to the catalog page.
6. Find "Animated Music"
7. Install

## Usage

### File Structure

Place your animated files in your music album folders with the following naming convention:

```
Music/
├── Artist Name/
│   ├── Album Name/
│   │   ├── 01 - Track 1.mp3
│   │   ├── 02 - Track 2.mp3
│   │   ├── cover-animated.gif                      # Animated cover
│   │   ├── vertical-background.mp4                 # Album vertical background
│   │   ├── vertical-background-01 - Track 1.mp4    # Track-specific vertical background
│   │   ├── vertical-background-02 - Track 2.webm   # Track-specific vertical background
│   │   └── cover.jpg                   
│   └── Another Album/
│       ├── 01 - Track 1.mp3
│       ├── cover-animated.webm                     # Different format
│       ├── vertical-background.mov                 # Album vertical background
│       └── vertical-background-01 - Track 1.mov    # Track-specific vertical background
```

### Supported File Formats

- **Animated Covers**: `.gif`, `.mp4`, `.webm`, `.mov`, `.avi`
- **Vertical Backgrounds**: `.gif`, `.mp4`, `.webm`, `.mov`, `.avi`

## API Endpoints

The plugin provides REST API endpoints to access animated files programmatically:

### Get Album Animated Cover

```text
GET /AnimatedMusic/Album/{albumId}/AnimatedCover
```

Returns the animated cover file for the specified album.

### Get Album Vertical Background

```text
GET /AnimatedMusic/Album/{albumId}/VerticalBackground
```

Returns the vertical background file for the specified album.

### Get Album Animated Info

```text
GET /AnimatedMusic/Album/{albumId}/Info
```

```json
{
  "albumId": "d5861930-8da6-499c-b7dd-235c60703f64",
  "hasAnimatedCover": true,
  "hasVerticalBackground": true,
  "animatedCoverUrl": "/AnimatedMusic/Album/d5861930-8da6-499c-b7dd-235c60703f64/AnimatedCover",
  "verticalBackgroundUrl": "/AnimatedMusic/Album/d5861930-8da6-499c-b7dd-235c60703f64/VerticalBackground"
}
```

### Get Track Vertical Background

```text
GET /AnimatedMusic/Track/{trackId}/VerticalBackground
```

### Get Track Animated Info

```text
GET /AnimatedMusic/Track/{trackId}/Info
```

### Notes

- Files are served with appropriate MIME types (image/gif, video/mp4, etc.)
- Album/Track IDs must be valid GUIDs
- Returns 404 if files are not found

## Troubleshooting

### Logs

Check the Jellyfin server logs for plugin-related messages. Look for entries containing "Animated Music" or "Jellyfin.Plugin.Animated.Music".

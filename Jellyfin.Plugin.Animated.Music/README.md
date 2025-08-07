# Jellyfin.Plugin.Animated.Music

A Jellyfin plugin that adds support for animated covers and vertical video backgrounds for music albums through an REST API for clients to use.

## Features

- **Animated Covers**: Support for animated GIF, MP4, WebM, MOV, and AVI files as album covers (square album cover video, replaces the image album cover)
- **Animated Cover Previews**: Support for static image previews of the first frame of animated covers (JPG, PNG, WebP)
- **Tall Animated Covers**: Support for tall aspect ratio animated covers for special layouts
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

```txt
Music/
├── Artist Name/
│ ├── Album Name/
│ │ ├── 01 - Track 1.mp3
│ │ ├── 02 - Track 2.mp3
│ │ ├── cover-animated.gif # Animated cover
│ │ ├── cover-animated-preview.jpg # Preview image (first frame)
│ │ ├── cover-animated-tall.mp4 # Tall animated cover
│ │ ├── cover-animated-tall-preview.png # Tall preview image (first frame)
│ │ ├── vertical-background.mp4 # Album vertical background
│ │ ├── vertical-background-01 - Track 1.mp4 # Track-specific vertical background
│ │ ├── vertical-background-02 - Track 2.webm # Track-specific vertical background
│ │ └── cover.jpg
```

### Supported File Formats

- **Animated Covers**: `.gif`, `.mp4`, `.webm`, `.mov`, `.avi`
- **Animated Cover Previews**: `.jpg`, `.jpeg`, `.png`, `.webp`
- **Tall Animated Covers**: `.gif`, `.mp4`, `.webm`, `.mov`, `.avi`
- **Tall Animated Cover Previews**: `.jpg`, `.jpeg`, `.png`, `.webp`
- **Vertical Backgrounds**: `.gif`, `.mp4`, `.webm`, `.mov`, `.avi`

## API Endpoints

The plugin provides REST API endpoints to access animated files programmatically:

### Get Album Animated Cover

```text
GET /AnimatedMusic/Album/{albumId}/AnimatedCover
```

Returns the animated cover file for the specified album.

### Get Album Animated Cover Preview

```text
GET /AnimatedMusic/Album/{albumId}/AnimatedCoverPreview
```

Returns the animated cover preview image for the specified album.

### Get Album Tall Animated Cover

```text
GET /AnimatedMusic/Album/{albumId}/AnimatedCoverTall
```

Returns the tall animated cover file for the specified album.

### Get Album Tall Animated Cover Preview

```text
GET /AnimatedMusic/Album/{albumId}/AnimatedCoverTallPreview
```

Returns the tall animated cover file for the specified album.

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
  "AlbumId": "d5861930-8da6-499c-b7dd-235c60703f64",
  "HasAnimatedCover": true,
  "HasAnimatedCoverPreview": true,
  "HasAnimatedCoverTall": true,
  "HasVerticalBackground": true,
  "AnimatedCoverUrl": "/AnimatedMusic/Album/d5861930-8da6-499c-b7dd-235c60703f64/AnimatedCover",
  "AnimatedCoverPreviewUrl": "/AnimatedMusic/Album/d5861930-8da6-499c-b7dd-235c60703f64/AnimatedCoverPreview",
  "AnimatedCoverTallUrl": "/AnimatedMusic/Album/d5861930-8da6-499c-b7dd-235c60703f64/AnimatedCoverTall",
  "AnimatedCoverTallPreviewUrl": "/AnimatedMusic/Album/d5861930-8da6-499c-b7dd-235c60703f64/AnimatedCoverTallPreview",
  "VerticalBackgroundUrl": "/AnimatedMusic/Album/d5861930-8da6-499c-b7dd-235c60703f64/VerticalBackground"
}
```

### Get Track Animated Cover

```text
GET /AnimatedMusic/Track/{trackId}/AnimatedCover
```

Returns the animated cover file for the specified tracks album cover.

### Get Track Animated Cover Preview

```text
GET /AnimatedMusic/Track/{trackId}/AnimatedCoverPreview
```

Returns the animated cover preview image for the specified track.

### Get Track Tall Animated Cover

```text
GET /AnimatedMusic/Track/{trackId}/AnimatedCoverTall
```

Returns the tall animated cover file for the specified track.

### Get Track Tall Animated Cover Preview

```text
GET /AnimatedMusic/Track/{trackId}/AnimatedCoverTallPreview
```

Returns the tall animated cover file for the specified track.

### Get Track Vertical Background

```text
GET /AnimatedMusic/Track/{trackId}/VerticalBackground
```

Returns the vertical background file for the specified track.

### Get Track Animated Info

```text
GET /AnimatedMusic/Track/{trackId}/Info
```

```json
{
  "TrackId": "d5861930-8da6-499c-b7dd-235c60703f64",
  "HasAnimatedCover": true,
  "HasAnimatedCoverPreview": true,
  "HasAnimatedCoverTall": true,
  "HasVerticalBackground": true,
  "HasTrackSpecificVerticalBackground": true,
  "AnimatedCoverUrl": "/AnimatedMusic/Album/d5861930-8da6-499c-b7dd-235c60703f64/AnimatedCover",
  "AnimatedCoverPreviewUrl": "/AnimatedMusic/Album/d5861930-8da6-499c-b7dd-235c60703f64/AnimatedCoverPreview",
  "AnimatedCoverTallUrl": "/AnimatedMusic/Album/d5861930-8da6-499c-b7dd-235c60703f64/AnimatedCoverTall",
  "VerticalBackgroundUrl": "/AnimatedMusic/Track/{trackId}/VerticalBackground"
}
```

### Notes

- Files are served with appropriate MIME types (image/gif, video/mp4, etc.)
- Album/Track IDs must be valid GUIDs
- Returns 404 if files are not found

## Troubleshooting

### Logs

Check the Jellyfin server logs for plugin-related messages. Look for entries containing "Animated Music" or "Jellyfin.Plugin.Animated.Music".

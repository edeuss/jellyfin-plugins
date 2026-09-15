# Jellyfin.Plugin.Animated.Music

A Jellyfin plugin that adds animated covers and vertical video backgrounds for music albums through a REST API.

Requires **Jellyfin 12.0 or 12.1**.

## Features

- **Animated covers**: GIF, MP4, WebM, MOV, and AVI as square album covers
- **Tall animated covers**: the same formats at a tall aspect ratio
- **Vertical backgrounds**: 9:16 videos for full-screen playback with controls overlaid
- **Previews**: first-frame JPEG generated from the video (optional sidecar images still work if you already have them)
- **Track-specific files**: covers and backgrounds named after a track file, with album-level fallback

## Installation

### Building from source

1. Clone this repository
2. Build with the .NET 10 SDK:

   ```bash
   sh build.sh
   ```

3. Copy the built `.zip` to your Jellyfin plugins directory
4. Restart Jellyfin Server

### From the catalog

1. Open the dashboard plugins catalog page
2. Click the gear icon, then the plus icon
3. Add this repository URL:

   ```text
   https://raw.githubusercontent.com/edeuss/jellyfin-plugins/refs/heads/main/manifest.json
   ```

4. Find **Animated Music** and install

## File layout

Place animated files in the album folder. Preview images are optional.

```txt
Music/
├── Artist Name/
│ └── Album Name/
│     ├── 01 - Track 1.mp3
│     ├── 02 - Track 2.mp3
│     ├── cover-animated.mp4
│     ├── cover-animated-tall.webm
│     ├── vertical-background.mp4
│     ├── vertical-background-01 - Track 1.mp4
│     └── cover.jpg
```

### Names

| File | Role |
|------|------|
| `cover-animated.*` | Square animated cover |
| `cover-animated-tall.*` | Tall animated cover |
| `vertical-background.*` | Album vertical background |
| `cover-animated-{trackStem}.*` | Track-specific cover |
| `cover-animated-tall-{trackStem}.*` | Track-specific tall cover |
| `vertical-background-{trackStem}.*` | Track-specific vertical background |
| `cover-animated-preview.*` | Optional still (otherwise extracted from the video) |
| `cover-animated-tall-preview.*` | Optional tall still |
| `vertical-background-preview.*` | Optional background still |

`{trackStem}` is the track file name without extension, for example `01 - Track 1`.

Preferred video extensions, in order: `.mp4`, `.webm`, `.gif`, `.mov`, `.avi`.

Optional preview stills: `.jpg`, `.jpeg`, `.png`, `.webp`.

## How to find videos

### Animated covers

Apple Music animated album covers can be downloaded with the [Animated Music tool](https://deuss.dev/tools/animated-music).

### Vertical backgrounds

Spotify Canvas videos can be downloaded from a track link via [canvasdownloader.com](https://www.canvasdownloader.com).

## API

All routes require a logged-in Jellyfin user. Album and track IDs are GUIDs. Media responses support HTTP Range and HEAD. Missing items or files return 404.

### Status

```text
GET /AnimatedMusic
```

```json
{
  "pluginName": "Animated Music",
  "version": "2.0.0",
  "status": "Available"
}
```

### Album info

```text
GET /AnimatedMusic/Albums/{albumId}
```

```json
{
  "albumId": "d5861930-8da6-499c-b7dd-235c60703f64",
  "cover": {
    "available": true,
    "url": "/AnimatedMusic/Albums/d5861930-8da6-499c-b7dd-235c60703f64/Cover",
    "previewUrl": "/AnimatedMusic/Albums/d5861930-8da6-499c-b7dd-235c60703f64/Cover/Preview",
    "mimeType": "video/mp4",
    "fileName": "cover-animated.mp4",
    "fileSize": 1234567
  },
  "tallCover": {
    "available": false,
    "url": null,
    "previewUrl": null,
    "mimeType": null,
    "fileName": null,
    "fileSize": null
  },
  "verticalBackground": {
    "available": true,
    "url": "/AnimatedMusic/Albums/d5861930-8da6-499c-b7dd-235c60703f64/VerticalBackground",
    "previewUrl": "/AnimatedMusic/Albums/d5861930-8da6-499c-b7dd-235c60703f64/VerticalBackground/Preview",
    "mimeType": "video/webm",
    "fileName": "vertical-background.webm",
    "fileSize": 999
  }
}
```

`previewUrl` is set whenever the video exists. The plugin extracts the first frame with FFmpeg and caches it. A sidecar still is used when present.

### Album files

```text
GET /AnimatedMusic/Albums/{albumId}/Cover
GET /AnimatedMusic/Albums/{albumId}/Cover/Preview
GET /AnimatedMusic/Albums/{albumId}/TallCover
GET /AnimatedMusic/Albums/{albumId}/TallCover/Preview
GET /AnimatedMusic/Albums/{albumId}/VerticalBackground
GET /AnimatedMusic/Albums/{albumId}/VerticalBackground/Preview
```

### Track info

```text
GET /AnimatedMusic/Tracks/{trackId}
```

Track-specific files are preferred; otherwise the album file is used.

```json
{
  "trackId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
  "albumId": "d5861930-8da6-499c-b7dd-235c60703f64",
  "trackFileName": "01 - Track 1",
  "cover": {
    "available": true,
    "url": "/AnimatedMusic/Tracks/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/Cover",
    "previewUrl": "/AnimatedMusic/Tracks/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/Cover/Preview",
    "mimeType": "video/mp4",
    "fileName": "cover-animated.mp4",
    "fileSize": 1234567,
    "trackSpecific": false
  },
  "tallCover": {
    "available": false,
    "url": null,
    "previewUrl": null,
    "mimeType": null,
    "fileName": null,
    "fileSize": null
  },
  "verticalBackground": {
    "available": true,
    "url": "/AnimatedMusic/Tracks/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/VerticalBackground",
    "previewUrl": "/AnimatedMusic/Tracks/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/VerticalBackground/Preview",
    "mimeType": "video/mp4",
    "fileName": "vertical-background-01 - Track 1.mp4",
    "fileSize": 888,
    "trackSpecific": true
  }
}
```

### Track files

```text
GET /AnimatedMusic/Tracks/{trackId}/Cover
GET /AnimatedMusic/Tracks/{trackId}/Cover/Preview
GET /AnimatedMusic/Tracks/{trackId}/TallCover
GET /AnimatedMusic/Tracks/{trackId}/TallCover/Preview
GET /AnimatedMusic/Tracks/{trackId}/VerticalBackground
GET /AnimatedMusic/Tracks/{trackId}/VerticalBackground/Preview
```

## Troubleshooting

Check the Jellyfin server logs for messages from `Animated Music` or `Jellyfin.Plugin.Animated.Music`. Preview extraction needs FFmpeg configured on the server.

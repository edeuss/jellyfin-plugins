# jellyfin-plugins

Custom Jellyfin plugins and a plugin catalog manifest for installing them from the dashboard.

## Plugins

| Plugin | Folder | Description |
| ------ | ------ | ----------- |
| [Animated Music](Jellyfin.Plugin.Animated.Music/) | `Jellyfin.Plugin.Animated.Music` | Animated album covers and vertical video backgrounds for music (REST API for clients). See the [plugin README](Jellyfin.Plugin.Animated.Music/README.md) for file layout and API details. |

## Install from the catalog

1. Open **Dashboard** → **Plugins** → **Catalog**.
2. Click the gear icon, then the **+** icon to add a repository.
3. Paste one of these manifest URLs:

   **Stable**

   ```text
   https://raw.githubusercontent.com/edeuss/jellyfin-plugins/refs/heads/main/manifest.json
   ```

   **Pre-release** (newer builds; may be less stable)

   ```text
   https://raw.githubusercontent.com/edeuss/jellyfin-plugins/refs/heads/main/manifest-prerelease.json
   ```

4. Save, refresh the catalog if needed, then install the plugin you want.
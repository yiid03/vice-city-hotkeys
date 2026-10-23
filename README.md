# Vice City Hotkeys

Type classic GTA Vice City cheat codes with a single hotkey. A small Windows utility with customizable keys, editable codes, and English / Turkish language selection.

## Download and install

Open this repository's **Releases** section and download **ViceCityHotkeys-1.0.0-Setup.exe**. Run setup, choose English or Turkish, and follow the installation steps. Installation is per user and does not require administrator privileges.

Prefer no installation? Download **ViceCityHotkeys-1.0.0-Portable.zip**, extract it, and open `ViceCityHotkeys.exe`. Keep the `.exe.config` file next to it.

**Requirements:** Windows 10 or 11, .NET Framework 4.8 or later, and the original PC version of GTA Vice City. The Definitive Edition is not supported. The game executable must be named `gta-vc.exe` or `gta_vc.exe`.

## How to use

1. Open Vice City Hotkeys and leave it running.
2. Switch to Vice City and resume gameplay.
3. Tap and release a hotkey. Wait for the code to finish before typing or using another shortcut.

| Default key | Action | Code |
| --- | --- | --- |
| F6 | Full health | `ASPIRINE` |
| F7 | Full armor | `PRECIOUSPROTECTION` |
| F8 | Weapon set 1 | `THUGSTOOLS` |
| F9 | Clear wanted level | `LEAVEMEALONE` |
| F10 | Weapon set 2 | `PROFESSIONALTOOLS` |
| F11 | Weapon set 3 | `NUTTERTOOLS` |
| F12 | Spawn tank | `PANZER` |

Choose a different F1–F12 key and edit a code, then click **Save changes**. Each row needs a unique key; codes accept 1–40 letters from A to Z. The action labels describe the default codes; if you replace a code, its label does not change.

Use the **Language** selector to switch between English and Türkçe immediately. The language is remembered automatically. Other edits take effect when saved.

Hotkeys are intercepted only while classic Vice City is the foreground app. Holding Shift, Ctrl, Alt, or the Windows key prevents a new code from starting. Switching windows or pressing a modifier cancels typing. Closing the utility stops all hotkeys. Do not use shortcuts in the game's menus or text fields; the utility cannot distinguish them from gameplay.

## Troubleshooting

- **The game misses letters:** increase **Key delay (ms)** to 70–100 and save. Each character uses a key-down and a key-up interval; longer codes can take several seconds.
- **Nothing happens:** leave menus, tap and release the key, and check **Hotkeys enabled**. If the game runs as administrator, launch the utility as administrator too.
- **Laptop media keys activate instead:** use your keyboard's Fn lock or the appropriate Fn combination.
- **Another mod uses the same key:** change the hotkey in the utility or the mod.
- **Unfamiliar executable warning:** the distributed binaries are unsigned. You can inspect the source and build them yourself; release checksums identify the files but are not a digital signature.

Settings are stored in `%LOCALAPPDATA%\ViceCityHotkeys\settings.xml`. Uninstall through Windows **Installed apps**; preferences are retained. Delete that settings file manually if you want to reset preferences. Settings from the earlier `ViceCityKisayol` portable utility are imported on first launch when available.

## Privacy and behavior

The application has no network requests, accounts, analytics, or key logs. It examines function-key events to detect configured shortcuts and sends letter scan codes to the foreground game. It does not patch the game, read or write game memory, or modify game files.

## Build from source

The project uses Windows Forms and the C# compiler included with .NET Framework. No NuGet packages are required.

```powershell
# Application, tests, portable archive, and installer (Inno Setup 6 required):
powershell -NoProfile -File .\build.ps1 -InnoCompiler "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

# Application, tests, and portable archive only:
powershell -NoProfile -File .\build.ps1 -SkipInstaller
```

Build output is written to `build/` and distributable files to `dist/`. SHA-256 checksums are generated in `dist/SHA256SUMS.txt`.

The self-tests cover settings validation, XML persistence and recovery, English/Turkish switching, default key selection, native input layout, and rejection of non-game windows. These checks do **not** establish in-game compatibility. The initial release has not been tested against a running copy of Vice City; please include your game version and Windows version when reporting an issue.

## Releases

The GitHub Actions workflow builds and tests the application on Windows. Pushing a `v1.0.0` tag creates a release containing the installer, portable archive, and checksums. Maintainers need repository write permission to publish a tag.

## Credits and license

Cheat-code reference: [Grand Theft Wiki](https://www.grandtheftwiki.com/GTA_Vice_City_Cheats_%28PC%29). Installer built with [Inno Setup](https://jrsoftware.org/isinfo.php). Source is available under the [MIT license](LICENSE).

This is an independent fan utility and is not affiliated with or endorsed by Rockstar Games or Take-Two Interactive. GTA Vice City and related names belong to their respective owners.


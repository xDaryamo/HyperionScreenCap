# Hyperion Screen Capture (Windows Grabber)

<p>
<a href="https://github.com/xDaryamo/HyperionScreenCap/releases"><img src="https://img.shields.io/github/v/release/xDaryamo/HyperionScreenCap"/></a>
<a href="https://github.com/xDaryamo/HyperionScreenCap/releases"><img src="https://img.shields.io/github/release-date/xDaryamo/HyperionScreenCap"/></a>
<a href="https://github.com/xDaryamo/HyperionScreenCap/actions/workflows/release.yml"><img src="https://github.com/xDaryamo/HyperionScreenCap/actions/workflows/release.yml/badge.svg"/></a>
</p>

Windows screen capture program for the [Hyperion](https://github.com/hyperion-project/hyperion.ng) open-source Ambilight project.

It captures each monitor with DirectX 11, downscales the image and sends it to the FlatBuffers interface of one or more Hyperion servers.

This is a fork. The code comes from [hanselb](https://github.com/hanselb/HyperionScreenCap), [RickDB](https://github.com/RickDB/HyperionScreenCap), [sabaatworld](https://github.com/sabaatworld/HyperionScreenCap) and [ctrl-shift-win-b](https://github.com/ctrl-shift-win-b/HyperionScreenCap). See [Credits](#credits) for who wrote what.

## What this fork changes

I run two monitors, one of them rotated, each with its own LED strip and its own Hyperion instance. These are the changes that setup needed.

- Capture keeps retrying when a Hyperion server is unreachable. Before, it gave up after 90 attempts and stayed off until you restarted it by hand.
- Rotated monitors are captured in their desktop orientation. Before, a portrait monitor produced an image that was half black.
- Each task is bound to its monitor's device name (`\\.\DISPLAY1`) and not to its index. Turning one monitor off no longer makes another task capture the wrong screen.
- A task pauses while its monitor is powered off and resumes when it comes back. The state is read over DDC/CI every 2 seconds.
- Every device-loss error (UAC prompt, session disconnect, device removed or reset) recreates the capture device and retries every 3 seconds.
- Every server configured in a task is connected. Before, the loop stopped at the first server that was already connected.
- DirectX 9 capture, the ProtoBuffer client and the update checker are gone, with the libraries they needed. The package list went from 34 to 14 and the exe from 14 MB to 3 MB.
- log4net and Newtonsoft.Json were updated to versions without known vulnerabilities.
- Every push to `master` is built by GitHub Actions. A `fix` or `feat` commit publishes a new release with the exe attached.

## Requirements

Windows 10 version 1903 or later, or Windows 11. Both include .NET Framework 4.8, so there is nothing to install.

## Download

Download `HyperionScreenCap.exe` from the [latest release](https://github.com/xDaryamo/HyperionScreenCap/releases/latest) and run it. There is no installer.

Keep the exe in a fixed folder. Windows stores the settings per exe path, so moving it starts from an empty configuration.

## Configuration

Right-click the tray icon and open the setup window. Add one capture task per monitor and point each one at its Hyperion server (FlatBuffers port, 19400 by default). The help tab of the setup window describes every parameter.

If your LED controller drops out of live mode on a static screen, lower **Frame capture timeout**. With 100 ms the program resends the last frame 10 times a second when nothing on screen changes. The default is 1250 ms. WLED leaves live mode after 2.5 seconds without data, so at that rate two lost packets in a row are enough to blank the strip.

The REST API is still there and can start or stop the capture remotely. Enable it in the setup window.

## HDR Support (DirectX 11)

When Windows is running in HDR mode, the DirectX 11 capture path automatically detects the FP16 (scRGB) frame format and applies tone mapping before sending pixels to Hyperion. This prevents clipping and colour shift that would occur if the raw HDR values were sent directly.

### Requirements

- DirectX 11 capture method selected
- Windows HDR enabled for the target display
- Application manifest declaring PerMonitorV2 DPI awareness (included)

### Configuration

The HDR tone mapping settings are exposed in the capture configuration dialog under **HDR Tone Mapping**:

| Setting | Description |
|---|---|
| Enable HDR Tone Mapping | Activates FP16 capture and tone mapping. Enabled automatically when HDR is detected. |
| Tone Mapping Method | Algorithm used to compress HDR luminance into SDR range. **Reinhard is recommended.** |
| Peak Luminance (nits) | The maximum brightness of your display (e.g. 1000 for a typical HDR TV). |
| SDR White Level (nits) | The brightness Windows uses for SDR content in HDR mode (default 200). Match the value set in Windows HDR settings. |
| Saturation | Colour saturation multiplier applied after tone mapping (1.0 = neutral). |

### Tone Mapping Methods

| Method | Characteristic |
|---|---|
| **Reinhard** (recommended) | Smooth, perceptually natural roll-off. Works well for Ambilight use. |
| Reinhard Extended | Like Reinhard but preserves more mid-tone contrast. Uses Peak Brightness setting. |
| ACES | Cinema-standard curve; aggressive shoulder and toe. |
| Clip | Hard clip at white point. Saturates highlights. |

### SDR White Level

Windows boosts the brightness of SDR application content when HDR is active. The default boost is typically 200 nits. If the Ambilight colours look washed out or too dim, adjust **SDR White Level** to match the value shown in Windows Settings > Display > HDR > SDR content brightness.

## Setup Window

The screenshots below come from upstream and still show the DirectX 9 options that this fork removed.

### Manage screen capture configurations and other application level settings.

![Setup Window General Tab](Screenshots/1.png)

### Define the capture settings and one or more Hyperion server destinations.

![Setup Window Edit Capture Configuration](Screenshots/2.png)

### Use the help tab to understand how to configure screen captures.

![Setup Window Help Tab](Screenshots/3.png)

## DirectX11 4K @ 60Hz HDR Demo

Video by sabaatworld.

[![Hyperion TV Ambient Light 4K 60Hz](https://img.youtube.com/vi/gY6-J97fXKc/0.jpg)](https://www.youtube.com/watch?v=gY6-J97fXKc "Hyperion TV Ambient Light 4K 60Hz")

## Building

Open `HyperionScreenCap.sln` in Visual Studio 2022, or build from a command prompt with the Build Tools and the .NET desktop workload installed:

```
msbuild HyperionScreenCap.sln -t:Restore -p:RestorePackagesConfig=true
msbuild HyperionScreenCap.sln -p:Configuration=Release
```

## Credits

This program is the work of several people, each building on the previous fork.

- [hanselb](https://github.com/hanselb/HyperionScreenCap) wrote the original program.
- [RickDB](https://github.com/RickDB/HyperionScreenCap) added ProtoBuffer support, the setup window and the REST API.
- [sabaatworld](https://github.com/sabaatworld/HyperionScreenCap) (Sabaat Ahmad) added DirectX 11 capture, FlatBuffers support, multiple displays and multiple Hyperion servers. His version has its own [forum thread](https://hyperion-project.org/threads/1018).
- [ctrl-shift-win-b](https://github.com/ctrl-shift-win-b/HyperionScreenCap) (Timo Birnschein) added HDR capture with tone mapping, recovery after sleep and wake, and the fix for the FlatBuffers priority lost after a reconnect. The HDR section above is his.
- [SouljaVR](https://github.com/SouljaVR/HyperionScreenCap) showed how to recover from UAC prompts and fullscreen games. The device-loss handling here follows his idea.

Icons made by [mynamepong](https://www.flaticon.com/authors/mynamepong), [Good Ware](https://www.flaticon.com/authors/good-ware), [Freepik](https://www.flaticon.com/authors/freepik)
and [Kiranshastry](https://www.flaticon.com/authors/kiranshastry) from [www.flaticon.com](https://www.flaticon.com/)

## License

MIT, same as upstream. See [LICENSE.txt](LICENSE.txt).

# Hyperion Screen Capture (Windows Grabber)

<p>
<a href="https://github.com/sabaatworld/HyperionScreenCap/releases"><img src="https://img.shields.io/github/v/release/sabaatworld/HyperionScreenCap?include_prereleases"/></a>
<a href="https://github.com/sabaatworld/HyperionScreenCap/releases"><img src="https://img.shields.io/github/release-date/sabaatworld/HyperionScreenCap"/></a>
<a href="https://github.com/sabaatworld/HyperionScreenCap/releases"><img src="https://img.shields.io/github/downloads/sabaatworld/HyperionScreenCap/total?color=ffa500"/></a>
</p>

Windows screen capture program for the [Hyperion](https://github.com/tvdzwan/hyperion) open-source Ambilight project.

The program uses DirectX 9/11 to capture the screen, resize it and send it to the FlatBuffer or ProtoBuffer interface of Hyperion.

**Changelog and official forum thread:** https://hyperion-project.org/threads/1018

## Dependencies

**The following dependencies need to be installed manually**

[Microsoft DotNet 4.0](https://www.microsoft.com/en-us/download/details.aspx?id=17718)

[DirectX End-User Runtime](https://www.microsoft.com/en-us/download/details.aspx?displaylang=en&id=35)

**If screen capture is not working, install the following dependencies as well**

[Visual C++ Redistributable for Visual Studio 2012](https://www.microsoft.com/en-us/download/details.aspx?id=30679)

[Microsoft Visual C++ 2010 Service Pack 1](https://www.microsoft.com/en-us/download/details.aspx?id=26999)

[Microsoft Visual C++ 2008 Service Pack 1](https://www.microsoft.com/en-us/download/details.aspx?id=26368)

## Download

Simply download and install [SetupHyperionScreenCapture.exe](https://github.com/sabaatworld/HyperionScreenCap/releases) from the latest release.

## Configuration

The application can be configured using the setup window which can be accessed by right clicking on the system tray icon. The defaults for most of the settings should work out of the box.
Description of the configuration parameters can be found on the help tab of the setup window.

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
| Reinhard Extended | Like Reinhard but preserves more mid-tone contrast. |
| Uncharted 2 (Filmic) | Filmic S-curve with lifted blacks; more contrast. |
| ACES | Cinema-standard curve; aggressive shoulder and toe. |
| Clamp | Hard clip at white point. Saturates highlights. |

### SDR White Level

Windows boosts the brightness of SDR application content when HDR is active. The default boost is typically 200 nits. If the Ambilight colours look washed out or too dim, adjust **SDR White Level** to match the value shown in Windows Settings > Display > HDR > SDR content brightness.

## Setup Window

### Manage screen capture configurations and other application level settings.

![Setup Window General Tab](Screenshots/1.png)


### Select between DirectX11 & DirectX9 capture methods and define one or more Hyperion server destinations.

![Setup Window Edit Capture Configuration](Screenshots/2.png)

### Use the help tab to understand how to configure screen captures.

![Setup Window Help Tab](Screenshots/3.png)

## DirectX11 4K @ 60Hz HDR Demo

[![Hyperion TV Ambient Light 4K 60Hz](https://img.youtube.com/vi/gY6-J97fXKc/0.jpg)](https://www.youtube.com/watch?v=gY6-J97fXKc "Hyperion TV Ambient Light 4K 60Hz")

## Credits

Icons made by [mynamepong](https://www.flaticon.com/authors/mynamepong), [Good Ware](https://www.flaticon.com/authors/good-ware), [Freepik](https://www.flaticon.com/authors/freepik)
and [Kiranshastry](https://www.flaticon.com/authors/kiranshastry) from [www.flaticon.com](https://www.flaticon.com/)

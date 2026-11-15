# Galaxy Smartphone Battery Check v1.4.8

**Windows 10/11 • C# / .NET 8 • ADB • Unofficial battery diagnostics**

Galaxy Battery Check is an independent Windows utility that retrieves supported battery information from Samsung Galaxy smartphones using Android Debug Bridge (ADB). It performs read-only diagnostics; root access is not required.

## Highlights
- New C#/.NET 8 Windows Forms application and command-line interface, replacing the legacy Python/Tkinter implementation.
- Automatic device detection and multi-device selection.
- Displays BSOH and ASOC separately, prioritizing BSOH and labeling ASOC as a secondary reference.
- Shows charge level, battery temperature, cycle count, available capacity information, and estimates where supported.
- Korean, English, and Japanese user interfaces.
- JSON export and portable Windows distribution.

## Download and run
Download the **Windows x64 portable ZIP** from this release's **Assets** section. Extract the *entire* archive, then open `run-gui.cmd` or `win-x64/GalaxyBatteryCheck.exe`.

Install Google's official [Android SDK Platform-Tools](https://developer.android.com/tools/releases/platform-tools), enable USB debugging on your Galaxy device, and approve the device authorization dialog. Your phone must expose the requested battery fields; some models and firmware revisions do not.

For a runtime-included self-contained distribution, no separate .NET Desktop Runtime installation is needed. Building from source requires the .NET 8 SDK on Windows.

## File integrity and VirusTotal
These two VirusTotal reports refer to the **original separately supplied v1.4.8 artifacts** and are *not* applicable to a differently compiled or repackaged release asset:

| Original artifact | SHA-256 | VirusTotal |
| --- | --- | --- |
| `GalaxyBatteryCheck-v1.4.8.zip` | `5ed4ecc8c519bc06f378a004ad5e1acc4e31433524a7167d559ed1d64fe67263` | [ZIP report](https://www.virustotal.com/gui/file/5ed4ecc8c519bc06f378a004ad5e1acc4e31433524a7167d559ed1d64fe67263) |
| `GalaxyBatteryCheck.exe` (in original ZIP) | `b2e996cf093cdc826b8fe8768ce2e96dfd8c7ec4b8d3d0b7025b7f2f9a79695d` | [EXE report](https://www.virustotal.com/gui/file/b2e996cf093cdc826b8fe8768ce2e96dfd8c7ec4b8d3d0b7025b7f2f9a79695d) |

**Automated builds:** An automatically built ZIP has its own independent SHA-256, provided in a matching `.sha256` release asset. Do not attribute the original VirusTotal results to the automated build. A VirusTotal report is not a guarantee that software is malware-free.

## Copyright and distribution restrictions
**No open-source license is granted.** Unauthorized modification, redistribution, mirroring, reuploading, commercial reuse, or repackaging requires prior written permission from the copyright holder. Applicable legal exceptions and GitHub's Terms of Service remain effective. Third-party components retain their own licenses. See [LICENSE](https://github.com/M26Pershing90mm/Galaxy-Smartphone-Battery-Check/blob/main/LICENSE).

## Important notice
This program is **not** developed, endorsed, sponsored, or certified by Samsung Electronics. BSOH, ASOC, color indicators, and estimated full-charge capacity should not be treated as official battery diagnostics or replacement criteria. For definitive testing, consult an authorized Samsung service provider.

**Developer:** Sakai · [Project repository](https://github.com/M26Pershing90mm/Galaxy-Smartphone-Battery-Check) · [Website](https://wezard4u.tistory.com/)

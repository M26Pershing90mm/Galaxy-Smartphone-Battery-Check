# Galaxy Battery Check v1.4.8 — Release notes

**Tag:** `v1.4.8`  
**Release title:** `Galaxy Battery Check v1.4.8 — Windows Battery Diagnostics`  
**Distribution:** Upload the original `GalaxyBatteryCheck-v1.4.8.zip` as a Release asset. Do not present a modified ZIP as the verified original.

## 한국어

Samsung Galaxy 기기의 배터리 정보를 ADB를 통해 읽기 전용으로 조회하는 Windows 프로그램입니다. v1.4.8은 C# / .NET 8 Windows Forms GUI 및 CLI를 사용합니다. BSOH·ASOC, 배터리 온도, 충전 사이클 등 기기가 제공하는 정보를 보여주며, 일부 항목은 기기/펌웨어에 따라 확인할 수 없습니다. 루팅은 필요하지 않으며 삼성전자의 공식 진단 프로그램이 아닙니다.

**설치·실행:** 배포 ZIP 전체를 압축 해제하고 `run-gui.cmd` 또는 `win-x64/GalaxyBatteryCheck.exe`를 실행하세요. 공식 Google Platform-Tools의 ADB와 USB 디버깅 승인이 필요합니다. JSON을 공유하기 전에는 기기 식별정보를 확인하세요.

**라이선스:** 별도 오픈소스 라이선스 없음. **저작권자 사전 서면 허가 없는 수정 및 재배포 금지.** 세부 조건은 저장소 `LICENSE` 참고(법률상 예외 및 GitHub 약관 준수).

## English

An unofficial, read-only Windows battery information utility for Samsung Galaxy devices over ADB. Version 1.4.8 uses C# / .NET 8 Windows Forms and offers GUI and CLI. Battery data availability varies by device and firmware. No root is required; this is not an official Samsung diagnostic product.

**Running:** Extract the complete release ZIP, run `run-gui.cmd` or `win-x64/GalaxyBatteryCheck.exe`, install official Google Platform-Tools, and authorize USB debugging. Review device identifiers before sharing JSON exports.

**Copyright:** No open-source license is granted. **Unauthorized modification and redistribution are prohibited without prior written permission**, subject to applicable legal exceptions and GitHub's Terms of Service. See `LICENSE`.

## SHA-256 and VirusTotal

| Artifact | SHA-256 | VirusTotal |
| --- | --- | --- |
| Original `GalaxyBatteryCheck-v1.4.8.zip` | `5ed4ecc8c519bc06f378a004ad5e1acc4e31433524a7167d559ed1d64fe67263` | https://www.virustotal.com/gui/file/5ed4ecc8c519bc06f378a004ad5e1acc4e31433524a7167d559ed1d64fe67263 |
| `win-x64/GalaxyBatteryCheck.exe` | `b2e996cf093cdc826b8fe8768ce2e96dfd8c7ec4b8d3d0b7025b7f2f9a79695d` | https://www.virustotal.com/gui/file/b2e996cf093cdc826b8fe8768ce2e96dfd8c7ec4b8d3d0b7025b7f2f9a79695d |

These hashes identify only the exact original files. Neither hash matching nor a VirusTotal report alone establishes that software is malware-free. Rebuilds and repackaged ZIPs require new hashes.
# Galaxy Battery Check 1.4.8 — Windows 실행 안내

## 실행

1. `GalaxyBatteryCheck-1.4.8-win-x64-portable.zip`을 원하는 폴더에 **모두 압축 해제**합니다.
2. `run-gui.cmd`를 더블클릭합니다. 직접 실행하려면 `win-x64/GalaxyBatteryCheck.exe`를 실행합니다.
3. Windows x64용이며 .NET 8.0.34 런타임을 포함합니다. 별도 .NET 설치가 필요하지 않습니다. ARM64/x86용 결과물은 이번에 검증하지 않았습니다.

## ADB와 휴대전화

- ADB는 패키지에 포함하지 않았습니다. [Google 공식 Platform-Tools](https://developer.android.com/tools/releases/platform-tools) Windows ZIP을 받아 해제합니다.
- `win-x64/platform-tools/adb.exe` 구조가 되도록 **platform-tools 폴더 전체**를 복사합니다. DLL도 필요하므로 adb.exe만 복사하지 마세요. 또는 GUI의 찾아보기로 기존 adb.exe를 지정합니다.
- 휴대전화 개발자 옵션에서 USB 디버깅을 켜고 데이터 전송이 가능한 USB 케이블로 연결합니다. 잠금을 해제하고 PC의 RSA 디버깅 허용 창을 승인합니다. [Android 공식 설명](https://developer.android.com/tools/adb)
- Windows가 ADB 기기를 인식하지 못하면 해당 기기 제조사의 USB 드라이버가 필요할 수 있습니다. [기기 연결 공식 안내](https://developer.android.com/studio/run/device)
- GUI는 약 4초 간격으로 연결을 확인하고 새 기기를 자동 조회합니다. 여러 대이면 기기를 선택합니다. 다시 읽으려면 배터리 조회를 누릅니다.
- `unauthorized`는 휴대전화 승인 상태, `offline`은 연결 상태를 확인합니다. ADB를 찾지 못하면 찾아보기로 지정합니다.

실물 갤럭시가 연결되지 않아 기기별 배터리 조회는 검증하지 못했습니다. BSOH, ASOC, 사이클, 정격 용량은 기기가 제공하고 읽기 권한이 있는 값만 표시합니다. 미제공 항목은 확인 불가입니다. 루팅이나 기기 설정 변경 명령은 소스에 없습니다. 배터리 수명 색상은 비공식 참고 기준이고 완충 용량은 간이 추정입니다.

## 명령줄

압축 해제한 폴더에서 다음을 실행합니다.

```bat
run-cli.cmd --language ko --help
run-cli.cmd --language ko --json --redact-serial --save report.json
run-cli.cmd --language ko --adb "C:\platform-tools\adb.exe"
```

GUI와 CLI 모두 실행 파일 옆의 platform-tools 폴더에서 ADB를 찾습니다. CLI는 명시한 --adb 경로를 우선합니다. GUI에서 저장한 JSON에는 기기 시리얼이 포함됩니다. CLI는 --redact-serial로 JSON 시리얼을 가릴 수 있습니다.

## 소스 재빌드

수정 소스 ZIP은 실행 패키지와 별도입니다. C#/.NET 8 SDK가 필요하며 외부 NuGet 라이브러리 의존성은 없습니다. `global.json`은 8.0.100 이상 8.0 SDK 기능 대역을 선택합니다. .NET 10 SDK만으로는 이 설정을 충족하지 않습니다.

```bat
build.cmd -SelfContained -Language ko
```

빌드 스크립트는 오프라인 방식입니다. 선택한 플랫폼의 참조/AppHost/런타임 팩이 로컬 SDK 또는 NuGet 캐시에 있어야 합니다. 없으면 NU1100 등으로 실패합니다. 이번 결과물은 준비되어 있던 로컬 .NET SDK 8.0.428 및 8.0.34 런타임 패키지로 생성했습니다. SDK와 캐시 자체는 소스 ZIP에 포함하지 않았습니다.

런타임 포함 배포 방식은 [Microsoft 배포 문서](https://learn.microsoft.com/en-us/dotnet/core/deploying/)를 참고하세요. 프로그램 폴더의 DLL·JSON·런타임 파일을 지우면 실행되지 않을 수 있습니다.

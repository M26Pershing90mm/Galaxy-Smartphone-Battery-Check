param(
    [ValidateSet('win-x64','win-arm64','win-x86')]
    [string]$Runtime = 'win-x64',
    [switch]$SelfContained,
    [ValidateSet('auto','ko','en','ja')]
    [string]$Language = 'auto'
)
$ErrorActionPreference = 'Stop'
if ($Language -eq 'auto') {
    $Language = [System.Globalization.CultureInfo]::CurrentUICulture.TwoLetterISOLanguageName
    if ($Language -notin @('ko','en','ja')) { $Language = 'en' }
}
$messages = @{
    ko = @{
        sdk = '.NET 8 SDK가 없습니다. Desktop Runtime만으로는 소스를 빌드할 수 없습니다. 실행만 하려면 포터블 배포본을 사용하세요.'
        build = '빌드에 실패했습니다. 위의 오류를 확인하세요. 오프라인 빌드에는 로컬 SDK와 대상 플랫폼 참조 팩이 필요합니다.'
        tests = '테스트가 실패했습니다. 위의 오류를 확인하세요.'
        publish = '배포 파일 생성에 실패했습니다. 선택한 플랫폼 또는 SelfContained에 필요한 팩이 로컬에 있는지 확인하세요.'
        success = '오프라인 빌드 완료'
    }
    en = @{
        sdk = '.NET 8 SDK was not found. Desktop Runtime alone cannot build source code. Use the portable release to run the app.'
        build = 'Build failed. Review the errors above. Offline builds require local SDK and target reference packs.'
        tests = 'Tests failed. Review the errors above.'
        publish = 'Publish failed. Required platform or SelfContained packs must be available locally.'
        success = 'Offline build completed'
    }
    ja = @{
        sdk = '.NET 8 SDKがありません。Desktop Runtimeだけではソースをビルドできません。実行する場合はポータブル配布版を使用してください。'
        build = 'ビルドに失敗しました。上のエラーを確認してください。オフラインビルドにはローカルのSDKと対象プラットフォームの参照パックが必要です。'
        tests = 'テストが失敗しました。上のエラーを確認してください。'
        publish = '配布ファイルを作成できません。対象プラットフォームまたはSelfContainedに必要なパックがローカルにあるか確認してください。'
        success = 'オフラインビルドが完了しました'
    }
}
$text = $messages[$Language]
Push-Location $PSScriptRoot
try {
    $dotnet = Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $dotnet) { throw $text.sdk }
    $sdkList = & $dotnet.Source --list-sdks
    if ($LASTEXITCODE -ne 0 -or -not ($sdkList -match '^8\.')) { throw $text.sdk }
    & $dotnet.Source build GalaxyBatteryCheck.sln -c Release -m:1 -p:UseSharedCompilation=false --configfile (Join-Path $PSScriptRoot 'NuGet.Config') -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { throw $text.build }
    & $dotnet.Source run --project GalaxyBatteryCheck.Core.Tests -c Release --no-build --no-restore
    if ($LASTEXITCODE -ne 0) { throw $text.tests }
    $standalone = $SelfContained.IsPresent.ToString().ToLowerInvariant()
    $output = Join-Path $PSScriptRoot $Runtime
    & $dotnet.Source publish GalaxyBatteryCheck.Gui -c Release -m:1 -p:UseSharedCompilation=false -r $Runtime --self-contained $standalone -p:NuGetAudit=false --configfile (Join-Path $PSScriptRoot 'NuGet.Config') -o $output
    if ($LASTEXITCODE -ne 0) { throw $text.publish }
    & $dotnet.Source publish GalaxyBatteryCheck.Cli -c Release -m:1 -p:UseSharedCompilation=false -r $Runtime --self-contained $standalone -p:NuGetAudit=false --configfile (Join-Path $PSScriptRoot 'NuGet.Config') -o $output
    if ($LASTEXITCODE -ne 0) { throw $text.publish }
    Write-Host ($text.success + ': ' + $output)
    exit 0
}
catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
finally { Pop-Location }




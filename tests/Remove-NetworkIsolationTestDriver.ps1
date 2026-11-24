$ErrorActionPreference='Stop'
$taskOutput=Join-Path $PSScriptRoot 'output\network-isolation'
$taskResult=Join-Path $taskOutput 'cleanup-result.json'
$taskOwnership=New-Object Threading.Mutex($false,'Global\CallriftIsolationDriverOwnership')
$taskOwns=$false
try {
    try { $taskOwns=$taskOwnership.WaitOne(0) } catch [Threading.AbandonedMutexException] { $taskOwns=$true }
    if (!$taskOwns) { throw 'An active Callrift session still owns the driver.' }
    if (@(Get-NetAdapter -Name 'Callrift Discord' -ErrorAction SilentlyContinue).Count) { throw 'The private adapter is still active; cleanup refused.' }
    $taskBase=[IO.Path]::GetFullPath((Join-Path $env:ProgramFiles 'Callrift Network'))
    $taskDirectory=[IO.Path]::GetFullPath((Join-Path $taskBase '1.0.5.0'))
    $taskDriver=[IO.Path]::GetFullPath((Join-Path $taskDirectory 'ProtonVPN.CalloutDriver.sys'))
    if (!$taskDriver.StartsWith($taskBase+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected cleanup target.' }
    foreach ($taskPath in @($taskBase,$taskDirectory,$taskDriver)) {
        if ((Test-Path -LiteralPath $taskPath) -and ((Get-Item -LiteralPath $taskPath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Cleanup refuses reparse points.' }
    }
    $taskService=Get-CimInstance Win32_SystemDriver -Filter "Name='CallriftDiscordIsolation'"
    if ($taskService) {
        $taskRegistered=$taskService.PathName
        if ($taskRegistered.StartsWith('\??\')) { $taskRegistered=$taskRegistered.Substring(4) }
        if ($taskRegistered -ine $taskDriver -or $taskService.StartMode -ne 'Manual' -or $taskService.State -ne 'Stopped') { throw 'The test driver service is not in the expected stopped state.' }
        & sc.exe delete CallriftDiscordIsolation | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'The test service could not be removed.' }
    }
    if (Test-Path -LiteralPath $taskDriver) {
        if ((Get-FileHash -LiteralPath $taskDriver -Algorithm SHA256).Hash -ne 'ba788ffdc1bd95dd4aa0eccb1b43a5e512eae09fb2d8d623e7266bde65d79e00') { throw 'Unexpected driver file; not deleted.' }
        Remove-Item -LiteralPath $taskDriver -Force
    }
    foreach ($taskPath in @($taskDirectory,$taskBase)) {
        if ((Test-Path -LiteralPath $taskPath) -and @(Get-ChildItem -LiteralPath $taskPath -Force).Count -eq 0) { [IO.Directory]::Delete($taskPath,$false) }
    }
    $taskWfpPath=Join-Path $taskOutput 'cleanup-wfp-state.xml'
    & netsh.exe wfp show state ('file='+$taskWfpPath) | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not verify WFP cleanup.' }
    # netsh emits two XML root elements on some Windows builds. Match only our
    # exact display-name prefix rather than parsing it as a single XML document.
    $taskWfpText=[IO.File]::ReadAllText($taskWfpPath)
    $taskRemaining=[regex]::Matches($taskWfpText,'<name>Callrift:[^<]*</name>').Count
    Remove-Item -LiteralPath $taskWfpPath
    $taskReport=[ordered]@{
        ServiceRemoved=!(Get-CimInstance Win32_SystemDriver -Filter "Name='CallriftDiscordIsolation'")
        DriverFileRemoved=!(Test-Path -LiteralPath $taskDriver)
        AdapterRemoved=@(Get-NetAdapter -Name 'Callrift Discord' -ErrorAction SilentlyContinue).Count -eq 0
        WfpObjectsRemaining=$taskRemaining
        FinishedAt=[DateTimeOffset]::Now.ToString('o')
    }
    $taskReport | ConvertTo-Json | Set-Content -LiteralPath $taskResult -Encoding UTF8
    if (!$taskReport.ServiceRemoved -or !$taskReport.DriverFileRemoved -or !$taskReport.AdapterRemoved -or $taskRemaining -ne 0) { exit 1 }
} catch {
    @{Passed=$false;ErrorType=$_.Exception.GetType().Name;ErrorId=$_.FullyQualifiedErrorId} | ConvertTo-Json | Set-Content -LiteralPath $taskResult -Encoding UTF8
    exit 1
} finally {
    if ($taskOwns) { $taskOwnership.ReleaseMutex() }
    $taskOwnership.Dispose()
}

param([switch]$UseProton,[switch]$DiagnosticCapture,[switch]$DisableTunnelForwarding,[ValidateRange(8,600)][int]$DurationSeconds=8,[switch]$TestIPv6)
$ErrorActionPreference = 'Stop'
$taskDirectory = Join-Path $PSScriptRoot 'output\network-isolation'
$taskLog = Join-Path $taskDirectory 'integration-console.txt'
$taskResult = Join-Path $taskDirectory 'integration-result.json'
trap {
    $_.Exception.Message | Set-Content -LiteralPath (Join-Path $taskDirectory 'wrapper-error.txt') -Encoding UTF8
    [IO.File]::WriteAllText((Join-Path $taskDirectory 'completed.txt'),'1')
    exit 1
}
# Remove only the rule created by the earlier system-stack integration attempt.
# The gVisor stack does not create this persistent inbound firewall exception.
$taskEngine=[IO.Path]::GetFullPath((Join-Path $taskDirectory 'engine\sing-box.exe'))
Get-NetFirewallRule -DisplayName ('sing-tun ('+$taskEngine+')') -ErrorAction SilentlyContinue | ForEach-Object {
    $taskRule=$_
    if (@($taskRule | Get-NetFirewallApplicationFilter | Where-Object Program -eq $taskEngine).Count -eq 1) { $taskRule | Remove-NetFirewallRule }
}
foreach ($taskName in @('completed.txt','integration-result.json','integration-engine.log','active.ready','active.ack','wrapper-error.txt','integration-route-choice.json','integration-selected-sockets.json')) {
    $taskPath=Join-Path $taskDirectory $taskName
    if (Test-Path -LiteralPath $taskPath) { Remove-Item -LiteralPath $taskPath }
}
function Get-NetworkSnapshot {
    [ordered]@{
        Routes = @((Get-NetRoute -ErrorAction Stop | Where-Object InterfaceAlias -ne 'Callrift Discord' | Select-Object InterfaceIndex,DestinationPrefix,NextHop,RouteMetric,Protocol | Sort-Object InterfaceIndex,DestinationPrefix,NextHop) | ConvertTo-Json -Compress)
        Dns = @((Get-DnsClientServerAddress -ErrorAction Stop | Where-Object InterfaceAlias -ne 'Callrift Discord' | Select-Object InterfaceIndex,AddressFamily,ServerAddresses | Sort-Object InterfaceIndex,AddressFamily) | ConvertTo-Json -Compress)
        Interfaces = @((Get-NetIPInterface -ErrorAction Stop | Where-Object InterfaceAlias -ne 'Callrift Discord' | Select-Object InterfaceIndex,AddressFamily,InterfaceMetric,AutomaticMetric,Forwarding,WeakHostSend,WeakHostReceive,NlMtu | Sort-Object InterfaceIndex,AddressFamily) | ConvertTo-Json -Compress)
        Firewall = @((Get-NetFirewallRule -ErrorAction Stop | Select-Object Name,Enabled,Direction,Action | Sort-Object Name) | ConvertTo-Json -Compress)
    }
}
$taskBefore = Get-NetworkSnapshot
$taskDuring = $null
$taskTraceStarted=$false
$taskProcess=$null
function Read-TestRoute {
    try { @(Find-NetRoute -LocalIPAddress '198.18.83.1' -RemoteIPAddress '1.1.1.1' -ErrorAction Stop | Select-Object InterfaceAlias,InterfaceIndex,DestinationPrefix,NextHop,RouteMetric) }
    catch { [pscustomobject]@{LookupError=$_.Exception.Message} }
}
try {
    $taskStart = @{FilePath=(Join-Path $taskDirectory 'NetworkIsolationTests.exe'); WindowStyle='Hidden'; PassThru=$true; RedirectStandardOutput=$taskLog; RedirectStandardError=(Join-Path $taskDirectory 'integration-stderr.txt')}
    $taskArgs=@('--duration='+$DurationSeconds)
    if ($UseProton) { $taskArgs+='--proton' }
    if ($TestIPv6) { $taskArgs+='--ipv6' }
    $taskStart.ArgumentList=$taskArgs
    $taskProcess=Start-Process @taskStart
    while (!$taskProcess.WaitForExit(250)) {
        if (!$taskDuring -and (Test-Path -LiteralPath (Join-Path $taskDirectory 'active.ready'))) {
            $taskRouteBefore=Read-TestRoute
            if ($DisableTunnelForwarding) { Set-NetIPInterface -InterfaceAlias 'Callrift Discord' -AddressFamily IPv4 -Forwarding Disabled -PolicyStore ActiveStore }
            [ordered]@{Before=$taskRouteBefore;After=(Read-TestRoute);Tunnel=@(Get-NetIPInterface -InterfaceAlias 'Callrift Discord' | Select-Object AddressFamily,Forwarding,WeakHostSend,WeakHostReceive,IgnoreDefaultRoutes,ConnectionState)} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskDirectory 'integration-route-choice.json') -Encoding UTF8
            $taskDuring=Get-NetworkSnapshot
            Get-NetRoute -InterfaceAlias 'Callrift Discord' | Select-Object DestinationPrefix,NextHop,RouteMetric,InterfaceIndex | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskDirectory 'integration-routes.json')
            if ($DiagnosticCapture) {
                $taskTracePath=Join-Path $taskDirectory 'integration-packets.etl'
                $taskTraceStart=& netsh.exe trace start capture=yes report=no persistent=no ('tracefile='+$taskTracePath) maxsize=4 filemode=circular correlation=no Ethernet.Type=IPv4 IPv4.Address=198.18.83.1 PacketTruncateBytes=64
                $taskTraceStarted=$LASTEXITCODE -eq 0 -and ($taskTraceStart -join "`n").Contains($taskTracePath)
                $taskTraceStart | Set-Content -LiteralPath (Join-Path $taskDirectory 'integration-trace-status.txt')
            }
            [IO.File]::WriteAllText((Join-Path $taskDirectory 'active.ack'),'')
        }
    }
    $taskExit = $taskProcess.ExitCode
} catch {
    $_.Exception.Message | Set-Content -LiteralPath (Join-Path $taskDirectory 'wrapper-error.txt') -Encoding UTF8
    $taskExit = 1
} finally {
    # Always allow the harness to reach its own cleanup before inspecting the network.
    if ($taskProcess) {
        if (!$taskProcess.HasExited) {
            [IO.File]::WriteAllText((Join-Path $taskDirectory 'active.ack'),'')
            $taskProcess.WaitForExit()
        }
        $taskProcess.Dispose()
    }
    if($taskTraceStarted) { & netsh.exe trace stop | Out-File -LiteralPath (Join-Path $taskDirectory 'integration-trace-status.txt') -Append }
    $taskAfter = Get-NetworkSnapshot
    $taskReport = if (Test-Path -LiteralPath $taskResult) { Get-Content -LiteralPath $taskResult -Raw -Encoding UTF8 | ConvertFrom-Json } else { [pscustomobject]@{Passed=$false} }
    foreach ($taskKind in @('Routes','Dns','Interfaces','Firewall')) {
        $taskSame = ($taskBefore[$taskKind] -join '') -ceq ($taskAfter[$taskKind] -join '')
        $taskReport | Add-Member -NotePropertyName ($taskKind + 'Unchanged') -NotePropertyValue $taskSame -Force
        if (!$taskSame) { $taskExit=1; $taskReport.Passed=$false }
        if ($taskDuring) {
            $taskSameDuring = ($taskBefore[$taskKind] -join '') -ceq ($taskDuring[$taskKind] -join '')
            $taskReport | Add-Member -NotePropertyName ($taskKind + 'UnchangedDuring') -NotePropertyValue $taskSameDuring -Force
            if (!$taskSameDuring) { $taskExit=1; $taskReport.Passed=$false }
        }
    }
    $taskAdapterGone=@(Get-NetAdapter -Name 'Callrift Discord' -ErrorAction SilentlyContinue).Count -eq 0
    $taskReport | Add-Member -NotePropertyName 'TunnelAdapterRemoved' -NotePropertyValue $taskAdapterGone -Force
    $taskService=Get-Service -Name 'CallriftDiscordIsolation' -ErrorAction SilentlyContinue
    $taskDriverStopped=(!$taskService -or $taskService.Status -eq 'Stopped')
    $taskReport | Add-Member -NotePropertyName 'DriverStopped' -NotePropertyValue $taskDriverStopped -Force
    if (!$taskAdapterGone -or !$taskDriverStopped) { $taskExit=1; $taskReport.Passed=$false }
    if(!$taskReport.Passed) { $taskExit=1 } elseif($null -eq $taskExit) { $taskExit=0 }
    [IO.File]::WriteAllText($taskResult,($taskReport | ConvertTo-Json -Depth 4),(New-Object Text.UTF8Encoding($false)))
    [IO.File]::WriteAllText((Join-Path $taskDirectory 'completed.txt'),[string]$taskExit)
}
exit $taskExit

param([ValidateRange(1,30)][int]$MaximumMinutes=15)
$ErrorActionPreference='Stop'
$taskDirectory=Join-Path $PSScriptRoot 'output\discord-trial'
$taskBefore=Get-Content -LiteralPath (Join-Path $taskDirectory 'before.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$taskProbe=Join-Path $PSScriptRoot 'output\network-isolation\DirectProbe.exe'
$taskChildren=@()
$taskSamples=@()
$taskStop=Join-Path $taskDirectory 'observer.stop'
$taskV4=Join-Path $taskDirectory 'native4.json'
$taskV6=Join-Path $taskDirectory 'native6.json'
$taskTunnels=@('198.18.83.1','fd7a:ca11:81f7::1')
$taskStarted=[DateTimeOffset]::Now
function Read-Probe([string]$Path) {
    try {
        $taskSample=Get-Content -LiteralPath $Path -Raw -ErrorAction Stop | ConvertFrom-Json
        [ordered]@{Tcp=$taskSample.Tcp;Udp=$taskSample.Udp;Failures=$taskSample.Failures;BoundToTunnel=$taskSample.BoundToTunnel}
    } catch { $null }
}
function Read-UnchangedNetwork {
    $taskCurrent=[ordered]@{
        Routes=@(Get-NetRoute | Where-Object InterfaceAlias -ne 'Callrift Discord' | Select-Object InterfaceIndex,DestinationPrefix,NextHop,RouteMetric,Protocol | Sort-Object InterfaceIndex,DestinationPrefix,NextHop)
        Dns=@(Get-DnsClientServerAddress | Where-Object InterfaceAlias -ne 'Callrift Discord' | Select-Object InterfaceIndex,AddressFamily,ServerAddresses | Sort-Object InterfaceIndex,AddressFamily)
        Interfaces=@(Get-NetIPInterface | Where-Object InterfaceAlias -ne 'Callrift Discord' | Select-Object InterfaceIndex,AddressFamily,InterfaceMetric,AutomaticMetric,Forwarding,WeakHostSend,WeakHostReceive,NlMtu | Sort-Object InterfaceIndex,AddressFamily)
    }
    $taskComparison=[ordered]@{}
    foreach($taskKind in @('Routes','Dns','Interfaces')) {
        $taskComparison[$taskKind+'Unchanged']=($taskCurrent[$taskKind] | ConvertTo-Json -Depth 5 -Compress) -ceq ($taskBefore.$taskKind | ConvertTo-Json -Depth 5 -Compress)
    }
    $taskComparison
}
try {
    foreach($taskPath in @($taskStop,$taskV4,$taskV6,($taskV4+'.stop'),($taskV6+'.stop'))) { if(Test-Path -LiteralPath $taskPath) { Remove-Item -LiteralPath $taskPath } }
    foreach($taskPair in @(@('--continuity',$taskV4),@('--continuity6',$taskV6))) {
        $taskChildren+=Start-Process -FilePath $taskProbe -ArgumentList @($taskPair[0],('"'+$taskPair[1]+'"')) -WindowStyle Hidden -PassThru
    }
    while(!(Test-Path -LiteralPath $taskStop) -and ([DateTimeOffset]::Now-$taskStarted).TotalMinutes -lt $MaximumMinutes) {
        $taskDiscord=@(Get-Process -Name Discord,DiscordPTB,DiscordCanary,DiscordDevelopment -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
        $taskTcp=@(Get-NetTCPConnection -State Established -ErrorAction SilentlyContinue)
        $taskUdp=@(Get-NetUDPEndpoint -ErrorAction SilentlyContinue)
        $taskNic=Get-NetAdapter -Name 'Callrift Discord' -ErrorAction SilentlyContinue
        $taskStats=if($taskNic) { Get-NetAdapterStatistics -Name 'Callrift Discord' } else { $null }
        $taskNow=[ordered]@{
            At=[DateTimeOffset]::Now.ToString('o')
            TunnelPresent=!!$taskNic
            DiscordTcpInTunnel=@($taskTcp | Where-Object { $_.OwningProcess -in $taskDiscord -and $_.LocalAddress -in $taskTunnels }).Count
            DiscordExternalTcpDirect=@($taskTcp | Where-Object { $_.OwningProcess -in $taskDiscord -and $_.LocalAddress -notin $taskTunnels -and ![Net.IPAddress]::IsLoopback([Net.IPAddress]::Parse($_.RemoteAddress)) }).Count
            DiscordUdpBindingsInTunnel=@($taskUdp | Where-Object { $_.OwningProcess -in $taskDiscord -and $_.LocalAddress -in $taskTunnels }).Count
            OtherAppsTcpInTunnel=@($taskTcp | Where-Object { $_.OwningProcess -notin $taskDiscord -and $_.LocalAddress -in $taskTunnels }).Count
            # UDP endpoint enumeration describes bindings, not transmitted packets.
            # Discovery/ICE services can enumerate and bind every adapter. WFP guards
            # authorization of non-selected applications' traffic on the private IP.
            OtherUdpBindingsInTunnel=@($taskUdp | Where-Object { $_.OwningProcess -notin $taskDiscord -and $_.LocalAddress -in $taskTunnels }).Count
            TunnelBytesSent=if($taskStats){$taskStats.SentBytes}else{0}
            TunnelBytesReceived=if($taskStats){$taskStats.ReceivedBytes}else{0}
            NativeIPv4=(Read-Probe $taskV4)
            NativeIPv6=(Read-Probe $taskV6)
            Network=(Read-UnchangedNetwork)
        }
        $taskSamples+=,$taskNow
        $taskNow | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskDirectory 'latest.json') -Encoding UTF8
        Start-Sleep -Seconds 5
    }
} catch {
    @{ErrorType=$_.Exception.GetType().Name;ErrorId=$_.FullyQualifiedErrorId} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskDirectory 'observer-error.json') -Encoding UTF8
} finally {
    foreach($taskPath in @($taskV4,$taskV6)) { [IO.File]::WriteAllText($taskPath+'.stop','') }
    foreach($taskProcess in $taskChildren) { try { if(!$taskProcess.WaitForExit(10000)) { $taskProcess.Kill(); $taskProcess.WaitForExit(2000) | Out-Null } } finally { $taskProcess.Dispose() } }
    [ordered]@{StartedAt=$taskStarted.ToString('o');FinishedAt=[DateTimeOffset]::Now.ToString('o');Samples=$taskSamples;NativeIPv4=(Read-Probe $taskV4);NativeIPv6=(Read-Probe $taskV6);Scope='Read-only live Discord observation and independent TCP/UDP continuity probes. No packet payloads, destinations, keys or public IPs are recorded.'} | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $taskDirectory 'observation.json') -Encoding UTF8
    foreach($taskPath in @($taskV4,$taskV6,($taskV4+'.stop'),($taskV6+'.stop'))) { if(Test-Path -LiteralPath $taskPath) { Remove-Item -LiteralPath $taskPath } }
}

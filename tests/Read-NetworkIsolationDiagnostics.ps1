param([int]$ProbeId,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
$taskRoute=try { @(Find-NetRoute -LocalIPAddress '198.18.83.1' -RemoteIPAddress '1.1.1.1' -ErrorAction Stop | Select-Object InterfaceAlias,DestinationPrefix,NextHop,RouteMetric) } catch { [pscustomobject]@{LookupError=$_.Exception.Message} }
[ordered]@{
    Sockets=@(Get-NetTCPConnection -OwningProcess $ProbeId -ErrorAction SilentlyContinue | Select-Object LocalAddress,LocalPort,RemoteAddress,RemotePort,State)
    SelectedRoute=$taskRoute
    Neighbors=@(Get-NetNeighbor -InterfaceAlias 'Callrift Discord' | Select-Object IPAddress,State)
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'integration-selected-sockets.json') -Encoding UTF8

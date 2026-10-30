# Shared helpers for scenarios. Dot-sourced after ArcaneInput.psm1 is imported.
# Observation comes from the game's telemetry file; every action is an OS input event.

if (-not (Test-Path variable:RouteFile)) { $RouteFile = 'route.json' }
$script:Route = Get-Content (Join-Path $PSScriptRoot $RouteFile) -Raw | ConvertFrom-Json
$script:HeldMove = @{}
$script:LeftHeld = $false

function Get-Snap {
    param([string]$Directory = $OutDir)
    $path = Join-Path $Directory 'telemetry.jsonl'
    for ($attempt = 0; $attempt -lt 50; $attempt++) {
        if (Test-Path -LiteralPath $path) {
            $stream = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
            try {
                $length = $stream.Length
                $start = [Math]::Max(0, $length - 32768)
                $null = $stream.Seek($start, [IO.SeekOrigin]::Begin)
                $buffer = New-Object byte[] ($length - $start)
                $null = $stream.Read($buffer, 0, $buffer.Length)
            } finally { $stream.Dispose() }
            $lines = ([Text.Encoding]::UTF8.GetString($buffer) -split "`n") | Where-Object { $_.Trim().StartsWith('{') -and $_.Trim().EndsWith('}') }
            if ($lines) { return (@($lines)[-1] | ConvertFrom-Json) }
        }
        Start-Sleep -Milliseconds 100
    }
    throw 'No telemetry from the game.'
}

function Wait-Snap {
    param([scriptblock]$Until, [int]$Seconds = 10, [string]$What = 'condition')
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        $snap = Get-Snap
        if (& $Until $snap) { return $snap }
        Start-Sleep -Milliseconds 100
    }
    throw "Timed out waiting for $What"
}

function Assert-That {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw "Assertion failed: $Message" }
    Write-ArcaneLog "PASS $Message"
}

function ConvertTo-Client {
    param($Snap, [double]$X, [double]$Y)
    $ppu = $Snap.screenH / (2 * $Snap.viewHalfHeight)
    $cx = [int][Math]::Round(($X - $Snap.camX) * $ppu + $Snap.screenW / 2)
    $cy = [int][Math]::Round($Snap.screenH / 2 - ($Y - $Snap.camY) * $ppu)
    [pscustomobject]@{ X = [Math]::Max(4, [Math]::Min($Snap.screenW - 5, $cx)); Y = [Math]::Max(4, [Math]::Min($Snap.screenH - 5, $cy)) }
}

function Set-MoveKeys {
    param([double]$DX, [double]$DY)
    $want = @{}
    $length = [Math]::Sqrt($DX * $DX + $DY * $DY)
    if ($length -gt 0.05) {
        $nx = $DX / $length; $ny = $DY / $length
        if ($nx -gt 0.38) { $want['D'] = $true }
        if ($nx -lt -0.38) { $want['A'] = $true }
        if ($ny -gt 0.38) { $want['W'] = $true }
        if ($ny -lt -0.38) { $want['S'] = $true }
    }
    foreach ($key in @($script:HeldMove.Keys)) { if (-not $want.ContainsKey($key)) { Key-Up -Key $key; $script:HeldMove.Remove($key) } }
    foreach ($key in $want.Keys) { if (-not $script:HeldMove.ContainsKey($key)) { Key-Down -Key $key; $script:HeldMove[$key] = $true } }
}

function Stop-Moving { Set-MoveKeys 0 0 }

function Set-LeftHeld {
    param([bool]$Held)
    if ($Held -and -not $script:LeftHeld) { Mouse-Down -Button Left; $script:LeftHeld = $true }
    if (-not $Held -and $script:LeftHeld) { Mouse-Up -Button Left; $script:LeftHeld = $false }
}

function Release-All { Stop-Moving; Set-LeftHeld $false }

function Get-RouteDistance {
    param([string]$Goal, [double]$X, [double]$Y)
    $width = $script:Route.width; $height = $script:Route.height
    $col = [int][Math]::Floor($X + $width / 2); $row = [int][Math]::Floor($height / 2 - $Y)
    if ($col -lt 0 -or $row -lt 0 -or $col -ge $width -or $row -ge $height) { return -1 }
    return $script:Route.goals.$Goal.distance[$row * $width + $col]
}

# Direction toward the neighbouring cell that is closer to the goal.
function Get-RouteStep {
    param([string]$Goal, [double]$X, [double]$Y)
    $width = $script:Route.width; $height = $script:Route.height
    $map = $script:Route.goals.$Goal.distance
    $col = [int][Math]::Floor($X + $width / 2); $row = [int][Math]::Floor($height / 2 - $Y)
    $here = $map[$row * $width + $col]
    if ($here -eq 0) { return $null }
    $best = $null; $bestDistance = [int]::MaxValue
    foreach ($dr in -1, 0, 1) {
        foreach ($dc in -1, 0, 1) {
            if ($dr -eq 0 -and $dc -eq 0) { continue }
            $c = $col + $dc; $r = $row + $dr
            if ($c -lt 0 -or $r -lt 0 -or $c -ge $width -or $r -ge $height) { continue }
            $d = $map[$r * $width + $c]
            if ($d -lt 0 -or $d -ge $bestDistance) { continue }
            if ($here -ge 0 -and $d -ge $here) { continue }
            $best = @($c, $r); $bestDistance = $d
        }
    }
    if (-not $best) {
        # Off the map's walkable cells (pushed by a body): head for the goal directly.
        $t = $script:Route.goals.$Goal.target
        return [pscustomobject]@{ DX = $t[0] - $X; DY = $t[1] - $Y }
    }
    $tx = -$width / 2 + $best[0] + 0.5; $ty = $height / 2 - $best[1] - 0.5
    [pscustomobject]@{ DX = $tx - $X; DY = $ty - $Y }
}

function Advance-Dialogue {
    param([int]$MaxLines = 12)
    for ($i = 0; $i -lt $MaxLines; $i++) {
        $snap = Get-Snap
        if ($snap.ui -ne 'dialogue') { return $snap }
        Key-Tap -Key F
        Start-Sleep -Milliseconds 260
    }
    return (Get-Snap)
}

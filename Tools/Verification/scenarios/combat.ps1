# Walking and fighting with OS input, shared by the playthrough scenarios.
# Dot-sourced after common.ps1. $script:WalkGoal names a route goal whose map marks every walkable cell.
if (-not (Test-Path variable:script:WalkGoal)) { $script:WalkGoal = 'orin' }

$script:Started = [DateTime]::UtcNow
$script:Deaths = 0
$script:LastDodge = [DateTime]::MinValue
$script:LastSkill = @{ E = [DateTime]::MinValue; Q = [DateTime]::MinValue; '1' = [DateTime]::MinValue }
$script:WasDead = $false
$script:Shots = 0

function Assert-Budget { if (([DateTime]::UtcNow - $script:Started).TotalMinutes -gt 25) { throw 'Playthrough took longer than 25 minutes.' } }

function Get-Threats($snap) {
    @($snap.enemyList | Where-Object {
        $d = [Math]::Sqrt(($_.x - $snap.x) * ($_.x - $snap.x) + ($_.y - $snap.y) * ($_.y - $snap.y))
        $_.alerted -and $d -lt 9
    })
}

function Handle-Interrupts($snap) {
    if ($snap.ui -eq 'dialogue') { Release-All; Advance-Dialogue | Out-Null; return $true }
    if ($snap.ui -eq 'ending') { Release-All; return $true }
    if ($snap.ui -ne 'play') { Key-Tap -Key Escape; Start-Sleep -Milliseconds 200; return $true }
    if ($snap.dead) {
        Release-All
        if (-not $script:WasDead) { $script:Deaths++; Write-ArcaneLog "INFO Mira was defeated ($script:Deaths); waiting for the camp respawn" ; Save-ClientCapture -Path (Join-Path $OutDir ("down-{0}.png" -f $script:Deaths)) }
        $script:WasDead = $true
        Start-Sleep -Milliseconds 300
        return $true
    }
    $script:WasDead = $false
    return $false
}

function Fight-Step($snap, $threats) {
    $target = $threats | Sort-Object { ($_.x - $snap.x) * ($_.x - $snap.x) + ($_.y - $snap.y) * ($_.y - $snap.y) } | Select-Object -First 1
    $dx = $target.x - $snap.x; $dy = $target.y - $snap.y
    $distance = [Math]::Sqrt($dx * $dx + $dy * $dy)
    $aim = ConvertTo-Client $snap $target.x ($target.y + 0.45)
    Mouse-MoveClient -X $aim.X -Y $aim.Y
    Set-LeftHeld $true
    $nx = $dx / [Math]::Max($distance, 0.01); $ny = $dy / [Math]::Max($distance, 0.01)
    $keep = if ($target.boss) { 4.2 } else { 3.6 }
    if ($distance -lt $keep - 0.8) { $mx = -$nx; $my = -$ny }
    elseif ($distance -gt $keep + 1.6) { $mx = $nx; $my = $ny }
    else { $mx = -$ny * 0.9 - $nx * 0.2; $my = $nx * 0.9 - $ny * 0.2 }
    # Avoid backing into blocked cells: if the step leads off the walkable map, strafe the other way.
    if ((Get-RouteDistance $script:WalkGoal ($snap.x + $mx * 0.8) ($snap.y + $my * 0.8)) -lt 0) { $mx = $ny; $my = -$nx }
    Set-MoveKeys $mx $my
    $now = [DateTime]::UtcNow
    $warned = @($threats | Where-Object { $_.telegraphing -and [Math]::Sqrt(($_.x - $snap.x) * ($_.x - $snap.x) + ($_.y - $snap.y) * ($_.y - $snap.y)) -lt 4.5 })
    if ($warned.Count -gt 0 -and ($now - $script:LastDodge).TotalSeconds -gt 2.1) {
        Key-Tap -Key Space
        $script:LastDodge = $now
        Write-ArcaneLog 'INFO dodge on a warned attack'
    }
    if ($snap.hp -lt $snap.maxHp * 0.5 -and $snap.potions -gt 0) { Key-Tap -Key R }
    foreach ($key in 'E', 'Q', '1') {
        $index = @{ E = 1; Q = 2; '1' = 3 }[$key]
        if (($now - $script:LastSkill[$key]).TotalSeconds -gt 3 -and $snap.mana -gt 35) {
            Key-Tap -Key $key
            $script:LastSkill[$key] = $now
        }
    }
}

# Walks toward a goal, fighting whatever engages on the way. Returns when standing in the goal area.
function Go-To([string]$Goal) {
    Write-ArcaneLog "INFO walking to $Goal"
    $lastProgress = [DateTime]::UtcNow; $lastX = 0; $lastY = 0
    while ($true) {
        Assert-Budget
        $snap = Get-Snap
        if (Handle-Interrupts $snap) { continue }
        $threats = @(Get-Threats $snap)
        if ($threats.Count -gt 0) { Fight-Step $snap $threats; Start-Sleep -Milliseconds 80; continue }
        Set-LeftHeld $false
        $step = Get-RouteStep $Goal $snap.x $snap.y
        if (-not $step) { Stop-Moving; return $snap }
        Set-MoveKeys $step.DX $step.DY
        $aim = ConvertTo-Client $snap ($snap.x + $step.DX * 2) ($snap.y + $step.DY * 2)
        Mouse-MoveClient -X $aim.X -Y $aim.Y
        $moved = [Math]::Abs($snap.x - $lastX) + [Math]::Abs($snap.y - $lastY)
        if ($moved -gt 0.3) { $lastProgress = [DateTime]::UtcNow; $lastX = $snap.x; $lastY = $snap.y }
        elseif (([DateTime]::UtcNow - $lastProgress).TotalSeconds -gt 2.5) {
            Write-ArcaneLog "INFO stuck near ($([Math]::Round($snap.x,1)), $([Math]::Round($snap.y,1))); side-stepping"
            Set-MoveKeys (-$step.DY) $step.DX
            Start-Sleep -Milliseconds 400
            $lastProgress = [DateTime]::UtcNow
        }
        Start-Sleep -Milliseconds 70
    }
}

function Clear-Area([double]$Radius = 9) {
    while ($true) {
        Assert-Budget
        $snap = Get-Snap
        if (Handle-Interrupts $snap) { continue }
        $near = @($snap.enemyList | Where-Object { [Math]::Sqrt(($_.x - $snap.x) * ($_.x - $snap.x) + ($_.y - $snap.y) * ($_.y - $snap.y)) -lt $Radius })
        if ($near.Count -eq 0) { Release-All; return $snap }
        foreach ($e in $near) { $e.alerted = $true }
        Fight-Step $snap $near
        Start-Sleep -Milliseconds 80
    }
}

function Investigate([string]$Goal, [string]$Capture) {
    while ($true) {
        $snap = Go-To $Goal
        $snap = Clear-Area 7
        $snap = Get-Snap
        if ((Get-RouteDistance $Goal $snap.x $snap.y) -ne 0) { continue }
        Release-All
        Start-Sleep -Milliseconds 250
        $snap = Get-Snap
        if ($snap.interaction -eq '') { Write-ArcaneLog "WARN no prompt at $Goal (x=$($snap.x), y=$($snap.y))"; Key-Hold -Key W -Milliseconds 200; continue }
        Write-ArcaneLog "INFO prompt at ${Goal}: $($snap.interaction)"
        Save-ClientCapture -Path (Join-Path $OutDir "$Capture-prompt.png")
        Key-Tap -Key F
        Start-Sleep -Milliseconds 400
        if ((Get-Snap).ui -eq 'dialogue') {
            Save-ClientCapture -Path (Join-Path $OutDir "$Capture-dialogue.png")
            Advance-Dialogue | Out-Null
            return (Get-Snap)
        }
    }
}


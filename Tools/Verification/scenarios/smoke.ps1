# Dot-sourced by Run-Scenario.ps1; OutDir is the current run directory.
Start-Sleep -Seconds 4
$initialTelemetry = Get-LastTelemetry -OutDir $OutDir
$hasTelemetry = $null -ne $initialTelemetry
if (-not $hasTelemetry) { Write-ArcaneLog 'WARN No telemetry: old build; skipping movement, cooldown and pause assertions. OS input and screen captures still run.' }
if ($hasTelemetry) {
    # A new game opens Orin's briefing; advance it with F like a player would.
    for ($line = 0; $line -lt 8 -and (Get-LastTelemetry -OutDir $OutDir).ui -eq 'dialogue'; $line++) { Key-Tap -Key F; Start-Sleep -Milliseconds 300 }
}

function Get-SmokeSnapshot {
    if (-not $hasTelemetry) { return $null }
    $snapshot = Get-LastTelemetry -OutDir $OutDir
    if ($null -eq $snapshot) { throw 'Telemetry disappeared during the scenario.' }
    if ($snapshot.dead) { throw 'Player is dead; smoke scenario cannot verify movement or casting.' }
    return $snapshot
}
function Assert-SmokeCondition {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw "Assertion failed: $Message" }
    Write-ArcaneLog "PASS $Message"
}
function Assert-SmokeCooldown {
    param($Before, [int]$Index, [string]$Name)
    if (-not $hasTelemetry) { return }
    $samples = @(Read-Telemetry -OutDir $OutDir | Where-Object { $_.frame -gt $Before.frame })
    $jumps = @($samples | Where-Object { $_.ready[$Index] -gt 0 -and $_.ready[$Index] -gt $Before.ready[$Index] })
    Assert-SmokeCondition ($jumps.Count -gt 0) "$Name remaining cooldown increased above zero"
}
Save-ClientCapture -Path (Join-Path $OutDir '00-start.png')
$directions = @(
    @{Key='D'; Axis='x'; Sign=1; Capture='01-right.png'},
    @{Key='W'; Axis='y'; Sign=1; Capture='02-up.png'},
    @{Key='A'; Axis='x'; Sign=-1; Capture='03-left.png'},
    @{Key='S'; Axis='y'; Sign=-1; Capture='04-down.png'}
)
foreach ($direction in $directions) {
    $before = Get-SmokeSnapshot
    Key-Hold -Key $direction.Key -Milliseconds 700
    Start-Sleep -Milliseconds 300
    Save-ClientCapture -Path (Join-Path $OutDir $direction.Capture)
    if ($hasTelemetry) {
        $after = Get-SmokeSnapshot
        Assert-SmokeCondition ($after.frame -gt $before.frame) "Fresh telemetry after $($direction.Key)"
        $delta = $after.($direction.Axis) - $before.($direction.Axis)
        Assert-SmokeCondition (($delta * $direction.Sign) -gt 0.01) "$($direction.Key) changed $($direction.Axis) in expected sign (delta=$delta)"
    }
}
$points = @(
    @{X=900; Y=360; Capture='05-aim-right.png'},
    @{X=640; Y=160; Capture='06-aim-up.png'},
    @{X=380; Y=360; Capture='07-aim-left.png'},
    @{X=640; Y=560; Capture='08-aim-down.png'}
)
foreach ($point in $points) {
    Mouse-MoveClient -X $point.X -Y $point.Y
    Start-Sleep -Milliseconds 300
    Save-ClientCapture -Path (Join-Path $OutDir $point.Capture)
}
$beforeBolt = Get-SmokeSnapshot
Mouse-Hold -Button Left -Milliseconds 600
Start-Sleep -Milliseconds 250
Save-ClientCapture -Path (Join-Path $OutDir '09-bolt.png')
Assert-SmokeCooldown $beforeBolt 0 'Bolt'
$beforeDash = Get-SmokeSnapshot
try {
    Key-Down -Key D
    Start-Sleep -Milliseconds 100
    Key-Tap -Key Space
    Start-Sleep -Milliseconds 300
} finally { Key-Up -Key D }
Save-ClientCapture -Path (Join-Path $OutDir '10-dash.png')
Assert-SmokeCooldown $beforeDash 6 'Dash'
$beforePause = Get-SmokeSnapshot
Key-Tap -Key Escape
Start-Sleep -Milliseconds 350
Save-ClientCapture -Path (Join-Path $OutDir '11-paused.png')
if ($hasTelemetry) {
    $paused = Get-SmokeSnapshot
    Assert-SmokeCondition ($paused.frame -gt $beforePause.frame -and $paused.paused) 'Escape paused the game with fresh telemetry'
}
Key-Tap -Key Escape
Start-Sleep -Milliseconds 350
Save-ClientCapture -Path (Join-Path $OutDir '12-resumed.png')
if ($hasTelemetry) {
    $resumed = Get-SmokeSnapshot
    Assert-SmokeCondition ($resumed.frame -gt $paused.frame -and -not $resumed.paused) 'Second Escape resumed the game with fresh telemetry'
}
Key-Tap -Key K
Start-Sleep -Milliseconds 350
Save-ClientCapture -Path (Join-Path $OutDir '13-journal.png')
Key-Tap -Key K
Start-Sleep -Milliseconds 350
Save-ClientCapture -Path (Join-Path $OutDir '14-journal-closed.png')

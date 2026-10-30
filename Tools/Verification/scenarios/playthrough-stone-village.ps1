# Plays chapter 2 (Fin at the stone golem village) from a new game to the ending with OS input only.
#   Run-Scenario.ps1 ... -Scenario playthrough-stone-village.ps1 -SaveKey <fresh> -Chapter stone-village
$RouteFile = 'route-stone-village.json'
. (Join-Path $PSScriptRoot 'common.ps1')
$script:WalkGoal = 'door'
. (Join-Path $PSScriptRoot 'combat.ps1')

# Walk to a goal, clear nearby enemies, press F at the prompt and read any dialogue,
# until the given condition on the telemetry snapshot holds.
function Do-At([string]$Goal, [scriptblock]$Done, [string]$Capture) {
    for ($attempt = 0; $attempt -lt 6; $attempt++) {
        Go-To $Goal | Out-Null
        Clear-Area 7 | Out-Null
        Release-All
        Start-Sleep -Milliseconds 250
        $snap = Get-Snap
        if (& $Done $snap) { return $snap }
        if ($snap.interaction -eq '') { Write-ArcaneLog "WARN no prompt at $Goal (x=$($snap.x), y=$($snap.y))"; continue }
        Write-ArcaneLog "INFO prompt at ${Goal}: $($snap.interaction)"
        if ($Capture) { Save-ClientCapture -Path (Join-Path $OutDir "$Capture.png") }
        Key-Tap -Key F
        Start-Sleep -Milliseconds 450
        Advance-Dialogue | Out-Null
        Start-Sleep -Milliseconds 300
        $snap = Get-Snap
        if (& $Done $snap) { return $snap }
    }
    throw "Could not finish $Goal"
}

$snap = Wait-Snap -Until { param($s) $s.chapter -eq 'stone-village' -and $s.ui -eq 'dialogue' } -Seconds 30 -What 'chapter 2 arrival'
Save-ClientCapture -Path (Join-Path $OutDir '00-arrival.png')
$snap = Advance-Dialogue
Assert-That ($snap.ui -eq 'play' -and $snap.phase -eq 0) 'arrival dialogue closed with F'

$snap = Do-At 'gate' { param($s) $s.phase -ge 1 } '01-gate'
Assert-That ($snap.phase -eq 1) 'greeting the big golem opens the village (phase 1)'
foreach ($i in 0, 1, 2) {
    $count = $i + 1
    $snap = Do-At "stone-$i" { param($s) $s.phase -ge 2 -or $s.objective -like "*($count/3)*" } "02-stone-$i"
}
Assert-That ((Get-Snap).phase -eq 2) 'three stones rolled with the mini-rocks; Mok named (phase 2)'
$snap = Do-At 'quarry' { param($s) $s.objective -like '*(1/2)*' -or $s.phase -ge 3 } '03-quarry'
$snap = Do-At 'spring' { param($s) $s.phase -ge 3 } '04-spring'
Assert-That ((Get-Snap).phase -eq 3) 'both observations send Mok to the cave (phase 3)'
Save-ClientCapture -Path (Join-Path $OutDir '05-follow.png')

Go-To 'cave' | Out-Null
$snap = Wait-Snap -Until { param($s) $s.phase -ge 4 } -Seconds 10 -What 'cave entry'
Assert-That ($snap.phase -eq 4) 'walking into the cave by OS input (phase 4)'
Save-ClientCapture -Path (Join-Path $OutDir '06-cave.png')
$snap = Do-At 'door' { param($s) $s.phase -ge 5 } '07-door'
Assert-That ($snap.phase -eq 5) 'reaching Mok at the door opens it (phase 5)'

$fightStart = [DateTime]::UtcNow
while ((Get-Snap).phase -lt 6) {
    Assert-Budget
    $snap = Get-Snap
    if (Handle-Interrupts $snap) { continue }
    if ((Get-RouteDistance 'door' $snap.x $snap.y) -gt 50) { Go-To 'door' | Out-Null; continue }
    $threats = @($snap.enemyList)
    if ($threats.Count -gt 0) { Fight-Step $snap $threats } else { Release-All }
    if (([DateTime]::UtcNow - $fightStart).TotalSeconds -gt 6 -and -not (Test-Path (Join-Path $OutDir '08-waves.png'))) { Save-ClientCapture -Path (Join-Path $OutDir '08-waves.png') }
    Start-Sleep -Milliseconds 80
}
Release-All
Assert-That ((Get-Snap).phase -eq 6) 'three waves held off by OS-input combat (phase 6)'
$snap = Wait-Snap -Until { param($s) $s.ui -eq 'ending' } -Seconds 10 -What 'ending'
Start-Sleep -Milliseconds 1200
Save-ClientCapture -Path (Join-Path $OutDir '09-voice.png')
Start-Sleep -Milliseconds 4200
Save-ClientCapture -Path (Join-Path $OutDir '10-ending.png')
Assert-That ($snap.ui -eq 'ending') 'the voice and the ending card appear'
Write-ArcaneLog ("INFO chapter 2 finished in {0:0.0} minutes with {1} defeat(s), level {2}" -f ([DateTime]::UtcNow - $script:Started).TotalMinutes, $script:Deaths, (Get-Snap).level)

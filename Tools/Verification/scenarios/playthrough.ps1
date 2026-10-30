# Plays the first field from a new game to the report with OS keyboard and mouse input only.
# Run with a fresh save key: -ExtraArgs '--arcane-save-key','play-<date>'
# Movement follows route.json (walking distance maps of the layout); combat aims the mouse at
# the nearest alerted enemy and holds the left button, dodging warned attacks with Space.
. (Join-Path $PSScriptRoot 'common.ps1')

. (Join-Path $PSScriptRoot 'combat.ps1')

# ---- run ----
$snap = Wait-Snap -Until { param($s) $s.ui -eq 'dialogue' } -Seconds 20 -What 'briefing'
Save-ClientCapture -Path (Join-Path $OutDir '00-briefing.png')
$snap = Advance-Dialogue
Assert-That ($snap.ui -eq 'play' -and $snap.phase -eq 0) 'briefing closed with F; phase 0'

$snap = Investigate 'root' '01-root'
Assert-That ((Get-Snap).phase -eq 1) 'investigating the root starts the ember fight (phase 1)'
Start-Sleep -Milliseconds 600
Save-ClientCapture -Path (Join-Path $OutDir '02-root-fight.png')
$snap = Clear-Area 10
$snap = Wait-Snap -Until { param($s) $s.phase -ge 2 } -Seconds 10 -What 'phase 2 after the root fight'
Assert-That ($snap.phase -eq 2) 'root embers cleared by OS-input combat (phase 2)'

$snap = Investigate 'vine' '03-vine'
$snap = Investigate 'brook' '04-brook'
Assert-That ($snap.brook) 'brook crystals surveyed'
$snap = Investigate 'stones' '05-stones'
$snap = Wait-Snap -Until { param($s) $s.phase -ge 3 } -Seconds 5 -What 'phase 3 after both surveys'
Assert-That ($snap.rocks -and $snap.phase -eq 3) 'both surveys open the ruin (phase 3)'

$snap = Go-To 'warden'
Save-ClientCapture -Path (Join-Path $OutDir '06-warden.png')
$fightStart = [DateTime]::UtcNow
while ((Get-Snap).phase -lt 4) {
    Assert-Budget
    $snap = Get-Snap
    if (Handle-Interrupts $snap) { continue }
    if (@($snap.enemyList | Where-Object { $_.boss }).Count -eq 0) { Start-Sleep -Milliseconds 200; continue }
    if ((Get-RouteDistance 'warden' $snap.x $snap.y) -gt 60) { Go-To 'warden' | Out-Null; continue }
    $threats = @($snap.enemyList)
    if ($threats.Count -gt 0) { Fight-Step $snap $threats }
    if (([DateTime]::UtcNow - $fightStart).TotalSeconds -gt 8 -and -not (Test-Path (Join-Path $OutDir '07-warden-fight.png'))) { Save-ClientCapture -Path (Join-Path $OutDir '07-warden-fight.png') }
    Start-Sleep -Milliseconds 80
}
Release-All
Assert-That ((Get-Snap).phase -eq 4) 'warden defeated (phase 4)'
$snap = Clear-Area 10

$snap = Investigate 'orin' '08-report'
$snap = Wait-Snap -Until { param($s) $s.phase -ge 5 } -Seconds 5 -What 'report'
Assert-That ($snap.phase -eq 5) 'report to Orin completes the first field (phase 5)'
Save-ClientCapture -Path (Join-Path $OutDir '09-complete.png')
Write-ArcaneLog ("INFO playthrough finished in {0:0.0} minutes with {1} defeat(s), level {2}" -f ([DateTime]::UtcNow - $script:Started).TotalMinutes, $script:Deaths, $snap.level)

# Relaunch with the same save: the field stays complete.
$s = [Math]::Exp(0.5 * [Math]::Log($snap.screenW / 1280.0) + 0.5 * [Math]::Log($snap.screenH / 720.0))
for ($attempt = 0; $attempt -lt 3 -and (Get-Process -Name ArcaneCastersRift -ErrorAction SilentlyContinue); $attempt++) {
    # A click can be cancelled when another window grabs focus mid-press; open the menu and press again.
    if ((Get-Snap).ui -ne 'pause') { Key-Tap -Key Escape; Start-Sleep -Milliseconds 300 }
    Mouse-MoveClient -X ([int]($snap.screenW / 2)) -Y ([int]($snap.screenH / 2 - 51 * $s))
    Start-Sleep -Milliseconds 120
    Mouse-Click -Button Left
    for ($i = 0; $i -lt 40 -and (Get-Process -Name ArcaneCastersRift -ErrorAction SilentlyContinue); $i++) { Start-Sleep -Milliseconds 100 }
}
$relaunch = Join-Path $OutDir 'relaunch'
Start-ArcaneGame -Exe $Exe -OutDir $relaunch -ExtraArgs $ExtraArgs
$OutDir = $relaunch
$snap = Wait-Snap -Until { param($s) $s.frame -gt 30 } -Seconds 20 -What 'relaunch telemetry'
Start-Sleep -Milliseconds 1200
$snap = Get-Snap
Assert-That ($snap.phase -eq 5 -and $snap.ui -eq 'play' -and @($snap.enemyList | Where-Object { $_.boss }).Count -eq 0) 'relaunch keeps the completed field and the defeated warden'
Save-ClientCapture -Path (Join-Path $OutDir '10-relaunched.png')

# Real keyboard and mouse checks. Run with a fresh save key, for example
#   -ExtraArgs '--arcane-save-key','input-<date>'
# Every action below is a SendInput event; telemetry is only read.
. (Join-Path $PSScriptRoot 'common.ps1')

function Get-UiScale($snap) { [Math]::Exp(0.5 * [Math]::Log($snap.screenW / 1280.0) + 0.5 * [Math]::Log($snap.screenH / 720.0)) }
# HUD positions measured from the layout in ArcaneHud (reference 1280x720).
function Get-UiPoint($snap, [string]$Name) {
    $s = Get-UiScale $snap
    $cx = $snap.screenW / 2; $cy = $snap.screenH / 2
    switch ($Name) {
        'dialogue' { return @([int]($cx), [int]($snap.screenH - 97 * $s)) }
        'journal-slot' { return @([int]($cx + 167 * $s), [int]($snap.screenH - 38 * $s)) }
        'journal-close' { return @([int]($cx + 356 * $s), [int]($cy - 223 * $s)) }
        'pause-continue' { return @([int]($cx), [int]($cy - 103 * $s)) }
        'pause-quit' { return @([int]($cx), [int]($cy - 51 * $s)) }
    }
    throw "Unknown UI point $Name"
}
function Click-Ui([string]$Name) {
    $snap = Get-Snap
    $point = Get-UiPoint $snap $Name
    Mouse-MoveClient -X $point[0] -Y $point[1]
    Start-Sleep -Milliseconds 200
    $seen = Get-Snap
    $gameY = $seen.screenH - $seen.mouseY
    if ([Math]::Abs($seen.mouseX - $point[0]) -gt 3 -or [Math]::Abs($gameY - $point[1]) -gt 3) { throw "Game reads the cursor at $($seen.mouseX),$gameY instead of $($point[0]),$($point[1]) for $Name" }
    Mouse-Click -Button Left
    Start-Sleep -Milliseconds 350
}

$saveKeyIndex = [Array]::IndexOf([string[]]$ExtraArgs, '--arcane-save-key')
if ($saveKeyIndex -lt 0) { throw 'Run input-checks with -ExtraArgs --arcane-save-key <fresh key>.' }

# 1. New game opens the briefing; clicking the dialogue panel advances it without casting.
$snap = Wait-Snap -Until { param($s) $s.ui -eq 'dialogue' } -Seconds 15 -What 'briefing dialogue'
Save-ClientCapture -Path (Join-Path $OutDir '01-briefing.png')
$casts = $snap.casts
for ($i = 0; $i -lt 8 -and (Get-Snap).ui -eq 'dialogue'; $i++) { Click-Ui 'dialogue' }
$snap = Get-Snap
Assert-That ($snap.ui -eq 'play' -and -not $snap.paused) 'mouse clicks on the dialogue panel advanced and closed the briefing'
Start-Sleep -Milliseconds 400
Assert-That ((Get-Snap).casts -eq $casts) 'closing dialogue with a click did not leak into an attack'

# 2. Movement recovers after the dialogue.
$before = Get-Snap
Key-Hold -Key D -Milliseconds 600
Start-Sleep -Milliseconds 250
$after = Get-Snap
Assert-That (($after.x - $before.x) -gt 0.8) "D moves right after dialogue (dx=$([Math]::Round($after.x - $before.x, 2)))"
Key-Hold -Key A -Milliseconds 600
Start-Sleep -Milliseconds 250
$back = Get-Snap
Assert-That (($back.x - $after.x) -lt -0.8) 'A moves left'
Key-Hold -Key W -Milliseconds 400
Key-Hold -Key S -Milliseconds 400
Start-Sleep -Milliseconds 250
Save-ClientCapture -Path (Join-Path $OutDir '02-moved.png')

# 3. Clicking the journal button opens it, its close button closes it, and neither casts.
$casts = (Get-Snap).casts
Click-Ui 'journal-slot'
$snap = Get-Snap
Assert-That ($snap.ui -eq 'journal' -and $snap.paused) 'clicking the K slot opened the spell journal'
Save-ClientCapture -Path (Join-Path $OutDir '03-journal-by-click.png')
Click-Ui 'journal-close'
Start-Sleep -Milliseconds 300
$snap = Get-Snap
Assert-That ($snap.ui -eq 'play' -and -not $snap.paused) 'journal close button resumed play'
Assert-That ($snap.casts -eq $casts) 'journal clicks did not leak into attacks'
Key-Tap -Key K
Start-Sleep -Milliseconds 300
Assert-That ((Get-Snap).ui -eq 'journal') 'K opens the journal'
Key-Tap -Key K
Start-Sleep -Milliseconds 300
Assert-That ((Get-Snap).ui -eq 'play') 'K closes the journal'

# 4. Escape pauses; the Continue button resumes and input still works.
Key-Tap -Key Escape
Start-Sleep -Milliseconds 300
Assert-That ((Get-Snap).ui -eq 'pause') 'Escape opens the pause menu'
Save-ClientCapture -Path (Join-Path $OutDir '04-pause.png')
Click-Ui 'pause-continue'
Start-Sleep -Milliseconds 300
$snap = Get-Snap
Assert-That ($snap.ui -eq 'play' -and -not $snap.paused -and $snap.casts -eq $casts) 'Continue resumed play without casting'
$before = Get-Snap
Key-Hold -Key W -Milliseconds 500
Start-Sleep -Milliseconds 250
Assert-That (((Get-Snap).y - $before.y) -gt 0.6) 'W moves up after the pause menu'

# 5. Mouse aim follows the cursor around Mira.
foreach ($case in @(@{ DX = 3; DY = 0; Check = { param($s) $s.aimX -gt 0.8 }; Name = 'right' },
                    @{ DX = -3; DY = 0; Check = { param($s) $s.aimX -lt -0.8 }; Name = 'left' },
                    @{ DX = 0; DY = 2.5; Check = { param($s) $s.aimY -gt 0.8 }; Name = 'up' },
                    @{ DX = 0; DY = -2.5; Check = { param($s) $s.aimY -lt -0.8 }; Name = 'down' })) {
    $snap = Get-Snap
    $point = ConvertTo-Client $snap ($snap.x + $case.DX) ($snap.y + $case.DY)
    Mouse-MoveClient -X $point.X -Y $point.Y
    Start-Sleep -Milliseconds 300
    $check = $case.Check
    Assert-That (& $check (Get-Snap)) "mouse aim points $($case.Name)"
}

# 6. Left button fires bolts toward the cursor.
$snap = Get-Snap
$point = ConvertTo-Client $snap ($snap.x + 3) ($snap.y)
Mouse-MoveClient -X $point.X -Y $point.Y
$casts = $snap.casts
Mouse-Hold -Button Left -Milliseconds 700
Start-Sleep -Milliseconds 200
$snap = Get-Snap
Assert-That ($snap.casts -ge $casts + 2) "holding the left button fires bolts ($($snap.casts - $casts) casts)"
Save-ClientCapture -Path (Join-Path $OutDir '05-bolts.png')

# 7. Space dodges in the movement direction.
Start-Sleep -Milliseconds 400
$before = Get-Snap
try { Key-Down -Key A; Start-Sleep -Milliseconds 80; Key-Tap -Key Space; Start-Sleep -Milliseconds 260 } finally { Key-Up -Key A }
$after = Get-Snap
Assert-That ($after.ready[6] -gt 0.5) 'Space starts the dodge cooldown'
Assert-That (($before.x - $after.x) -gt 1.3) "dodge carries Mira farther than walking (dx=$([Math]::Round($before.x - $after.x, 2)))"

# 8. F talks to Orin; Enter advances that dialogue.
$deadline = [DateTime]::UtcNow.AddSeconds(20)
while ([DateTime]::UtcNow -lt $deadline) {
    $snap = Get-Snap
    $step = Get-RouteStep 'orin' $snap.x $snap.y
    if (-not $step) { break }
    Set-MoveKeys $step.DX $step.DY
    Start-Sleep -Milliseconds 90
}
Stop-Moving
Start-Sleep -Milliseconds 200
Assert-That ((Get-Snap).interaction -ne '') "an interaction prompt is offered next to Orin ($((Get-Snap).interaction))"
Key-Tap -Key F
Start-Sleep -Milliseconds 350
Assert-That ((Get-Snap).ui -eq 'dialogue') 'F opens the conversation with Orin'
Save-ClientCapture -Path (Join-Path $OutDir '06-orin-dialogue.png')
for ($i = 0; $i -lt 6 -and (Get-Snap).ui -eq 'dialogue'; $i++) { Key-Tap -Key Enter; Start-Sleep -Milliseconds 250 }
Assert-That ((Get-Snap).ui -eq 'play') 'Enter closes the conversation'
$before = Get-Snap
Key-Hold -Key D -Milliseconds 500
Start-Sleep -Milliseconds 200
Assert-That (((Get-Snap).x - $before.x) -gt 0.6) 'D moves after the conversation'

# 9. Save and quit from the pause menu, then relaunch with the same save.
$beforeQuit = Get-Snap
Key-Tap -Key Escape
Start-Sleep -Milliseconds 300
Click-Ui 'pause-quit'
$exited = $false
for ($i = 0; $i -lt 60; $i++) { if (-not (Get-Process -Name ArcaneCastersRift -ErrorAction SilentlyContinue)) { $exited = $true; break }; Start-Sleep -Milliseconds 100 }
Assert-That $exited 'Save and quit closes the game'
$relaunch = Join-Path $OutDir 'relaunch'
Start-ArcaneGame -Exe $Exe -OutDir $relaunch -ExtraArgs $ExtraArgs
$OutDir = $relaunch
$snap = Wait-Snap -Until { param($s) $s.frame -gt 30 } -Seconds 20 -What 'relaunch telemetry'
Start-Sleep -Milliseconds 1500
$snap = Get-Snap
Assert-That ($snap.ui -eq 'play') 'relaunch with the saved game does not repeat the briefing'
Assert-That ($snap.phase -eq $beforeQuit.phase -and $snap.level -eq $beforeQuit.level) "saved phase and level are restored (phase $($snap.phase), level $($snap.level))"
Save-ClientCapture -Path (Join-Path $OutDir '07-relaunched.png')

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not ('ArcaneVerification.Native' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
namespace ArcaneVerification {
    public static class Native {
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT {
            public int dx, dy; public uint mouseData, dwFlags, time; public UIntPtr dwExtraInfo;
        }
        [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT {
            public ushort wVk, wScan; public uint dwFlags, time; public UIntPtr dwExtraInfo;
        }
        [StructLayout(LayoutKind.Explicit)] public struct INPUTUNION {
            [FieldOffset(0)] public MOUSEINPUT mouse;
            [FieldOffset(0)] public KEYBDINPUT keyboard;
        }
        [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public INPUTUNION data; }
        public static INPUT Keyboard(ushort virtualKey, bool up) {
            var input = new INPUT { type = 1 };
            input.data.keyboard.wVk = virtualKey;
            input.data.keyboard.wScan = (ushort)MapVirtualKey(virtualKey, 0);
            input.data.keyboard.dwFlags = 0x0008u | (up ? 0x0002u : 0u);
            return input;
        }
        public static INPUT Mouse(int x, int y, uint flags) {
            var input = new INPUT();
            input.data.mouse.dx = x;
            input.data.mouse.dy = y;
            input.data.mouse.dwFlags = flags;
            return input;
        }
        [DllImport("user32.dll", SetLastError=true)] public static extern uint SendInput(uint count, INPUT[] inputs, int size);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("dwmapi.dll")] public static extern int DwmFlush();
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint first, uint second, bool attach);
        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr window, out RECT rect);
        [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr window, ref POINT point);
        [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint kind);
        [DllImport("user32.dll")] public static extern void SwitchToThisWindow(IntPtr window, bool altTab);
        [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT point);
        [StructLayout(LayoutKind.Sequential)] public struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
        [DllImport("user32.dll")] public static extern bool GetLastInputInfo(ref LASTINPUTINFO info);
        public static uint LastInputTick() {
            var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };
            GetLastInputInfo(ref info);
            return info.dwTime;
        }
        public static double IdleSeconds() {
            var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };
            GetLastInputInfo(ref info);
            return unchecked((uint)Environment.TickCount - info.dwTime) / 1000.0;
        }
    }
}
'@
}
Add-Type -AssemblyName System.Drawing
$script:Game = $null
$script:Window = [IntPtr]::Zero
$script:OutputDirectory = $null
$script:HeldKeys = @{}
$script:HeldButtons = @{}
$script:LastSentTick = 0
$script:Refocused = 0

function Write-ArcaneLog {
    param([string]$Message)
    $line = '{0} {1}' -f [DateTimeOffset]::Now.ToString('o'), $Message
    if ($script:OutputDirectory) {
        [IO.File]::AppendAllText((Join-Path $script:OutputDirectory 'input.log'), $line + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
    }
    Write-Host $line
}
function Focus-ArcaneGame {
    Write-ArcaneLog 'Focus-ArcaneGame'
    if (-not $script:Game -or $script:Game.HasExited) { throw 'Game process is not running.' }
    $null = [ArcaneVerification.Native]::ShowWindow($script:Window, 9)
    $null = [ArcaneVerification.Native]::SetWindowPos($script:Window, [IntPtr](-1), 0, 0, 0, 0, 0x0043)
    $foreground = [ArcaneVerification.Native]::GetForegroundWindow()
    [uint32]$processIdentifier = 0
    $foregroundThread = [ArcaneVerification.Native]::GetWindowThreadProcessId($foreground, [ref]$processIdentifier)
    $gameThread = [ArcaneVerification.Native]::GetWindowThreadProcessId($script:Window, [ref]$processIdentifier)
    $currentThread = [ArcaneVerification.Native]::GetCurrentThreadId()
    $attached = @()
    try {
        foreach ($thread in @($foregroundThread, $gameThread) | Select-Object -Unique) {
            if ($thread -ne 0 -and $thread -ne $currentThread) {
                if ([ArcaneVerification.Native]::AttachThreadInput($currentThread, $thread, $true)) { $attached += $thread }
            }
        }
        $null = [ArcaneVerification.Native]::SetForegroundWindow($script:Window)
    } finally {
        foreach ($thread in $attached) { $null = [ArcaneVerification.Native]::AttachThreadInput($currentThread, $thread, $false) }
    }
    Start-Sleep -Milliseconds 200
    if ([ArcaneVerification.Native]::GetForegroundWindow() -ne $script:Window) {
        # Windows refuses focus requests from background processes; a minimise/restore or
        # SwitchToThisWindow usually passes. Neither sends key events to other programs.
        $null = [ArcaneVerification.Native]::ShowWindow($script:Window, 6)
        Start-Sleep -Milliseconds 150
        $null = [ArcaneVerification.Native]::ShowWindow($script:Window, 9)
        $null = [ArcaneVerification.Native]::SetForegroundWindow($script:Window)
        Start-Sleep -Milliseconds 250
    }
    if ([ArcaneVerification.Native]::GetForegroundWindow() -ne $script:Window) {
        [ArcaneVerification.Native]::SwitchToThisWindow($script:Window, $true)
        Start-Sleep -Milliseconds 300
    }
    if ([ArcaneVerification.Native]::GetForegroundWindow() -ne $script:Window) { throw 'Safety: game is not the foreground window; no input sent.' }
}
function Wait-UserIdle {
    param([int]$Seconds = 20, [int]$TimeoutMinutes = 60)
    $deadline = [DateTime]::UtcNow.AddMinutes($TimeoutMinutes)
    while ([ArcaneVerification.Native]::IdleSeconds() -lt $Seconds) {
        if ([DateTime]::UtcNow -gt $deadline) { throw "Safety: the desktop user stayed active for $TimeoutMinutes minutes; no input sent." }
        Start-Sleep -Seconds 2
    }
    Write-ArcaneLog ("User idle {0:0}s; starting" -f [ArcaneVerification.Native]::IdleSeconds())
}
function Assert-ArcaneForeground {
    if (-not $script:Game -or $script:Game.HasExited) { throw 'Game process is not running.' }
    if ([ArcaneVerification.Native]::GetForegroundWindow() -ne $script:Window) {
        # Only take the window back when nobody touched the desktop: the last input Windows saw
        # must be one of ours. Any physical key or mouse input since then stops the run.
        $last = [ArcaneVerification.Native]::LastInputTick()
        $human = $script:LastSentTick -ne 0 -and ([long]$last - [long]$script:LastSentTick) -gt 150
        if ($human) { throw 'Safety: someone used the desktop during the run; no input sent.' }
        $script:Refocused++
        if ($script:Refocused -gt 20) { throw 'Safety: another window kept taking focus; stopping.' }
        Write-ArcaneLog 'WARN another window took focus without user input; focusing the game again'
        Focus-ArcaneGame
    }
    if ([ArcaneVerification.Native]::GetForegroundWindow() -ne $script:Window) { throw 'Safety: foreground window changed; no input sent.' }
}
function Start-ArcaneGame {
    param([Parameter(Mandatory)][string]$Exe, [Parameter(Mandatory)][string]$OutDir, [string[]]$ExtraArgs = @())
    if ($script:Game -and -not $script:Game.HasExited) { throw 'A game is already running in this module.' }
    if (-not $env:ARCANE_SKIP_IDLE_WAIT) { Wait-UserIdle }
    $null = [ArcaneVerification.Native]::SetProcessDPIAware()
    $executable = (Resolve-Path -LiteralPath $Exe).Path
    $script:OutputDirectory = [IO.Path]::GetFullPath($OutDir)
    $null = [IO.Directory]::CreateDirectory($script:OutputDirectory)
    $telemetry = Join-Path $script:OutputDirectory 'telemetry.jsonl'
    if (Test-Path -LiteralPath $telemetry) { throw 'OutDir already contains telemetry.jsonl. Use a fresh output directory.' }
    Write-ArcaneLog "Start-ArcaneGame Exe=$executable OutDir=$script:OutputDirectory"
    # ProcessStartInfo on .NET Framework accepts one Windows command line.
    $arguments = @('-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720', '--arcane-telemetry', $telemetry) + $ExtraArgs
    $quoted = foreach ($argument in $arguments) {
        '"' + ([regex]::Replace([regex]::Replace($argument, '(\\*)"', '$1$1\"'), '(\\+)$', '$1$1')) + '"'
    }
    $script:Game = Start-Process -FilePath $executable -WorkingDirectory (Split-Path $executable) -ArgumentList ($quoted -join ' ') -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    do {
        if ($script:Game.HasExited) { throw "Game exited before opening a window (exit $($script:Game.ExitCode))." }
        $script:Game.Refresh()
        $script:Window = $script:Game.MainWindowHandle
        if ($script:Window -ne [IntPtr]::Zero) { break }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($script:Window -eq [IntPtr]::Zero) { throw 'Timed out waiting for the game window.' }
    Focus-ArcaneGame
}
function Send-ArcaneInput {
    param([ArcaneVerification.Native+INPUT]$InputEvent, [string]$Description)
    Assert-ArcaneForeground
    $sent = [ArcaneVerification.Native]::SendInput(1, @($InputEvent), [Runtime.InteropServices.Marshal]::SizeOf($InputEvent))
    $script:LastSentTick = [ArcaneVerification.Native]::LastInputTick()
    if ($sent -ne 1) { throw "SendInput failed ($([Runtime.InteropServices.Marshal]::GetLastWin32Error())): $Description" }
    Write-ArcaneLog "SendInput $Description"
}
function Get-ArcaneVirtualKey {
    param([string]$Key)
    switch ($Key.ToUpperInvariant()) {
        'SPACE' { return 0x20 } 'ESCAPE' { return 0x1B } 'ENTER' { return 0x0D }
        { $_ -in @('W','A','S','D','F','K','R','Q','E','1') } { return [int][char]$_ }
        default { throw "Unsupported key: $Key" }
    }
}
function Key-Down {
    param([Parameter(Mandatory)][string]$Key)
    Assert-ArcaneForeground
    $virtualKey = Get-ArcaneVirtualKey $Key
    $event = [ArcaneVerification.Native]::Keyboard($virtualKey, $false)
    Send-ArcaneInput $event "Key-Down $Key vk=$virtualKey scan=$($event.data.keyboard.wScan)"
    $script:HeldKeys[$Key.ToUpperInvariant()] = $true
}
function Key-Up {
    param([Parameter(Mandatory)][string]$Key)
    Assert-ArcaneForeground
    $virtualKey = Get-ArcaneVirtualKey $Key
    $event = [ArcaneVerification.Native]::Keyboard($virtualKey, $true)
    Send-ArcaneInput $event "Key-Up $Key vk=$virtualKey scan=$($event.data.keyboard.wScan)"
    $script:HeldKeys.Remove($Key.ToUpperInvariant())
}
function Wait-ArcaneInput {
    param([ValidateRange(0,600000)][int]$Milliseconds)
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while ($timer.ElapsedMilliseconds -lt $Milliseconds) {
        Assert-ArcaneForeground
        Start-Sleep -Milliseconds ([Math]::Max(0, [Math]::Min(25, $Milliseconds - [int]$timer.ElapsedMilliseconds)))
    }
}
function Key-Hold {
    param([Parameter(Mandatory)][string]$Key, [ValidateRange(1,600000)][int]$Milliseconds = 100)
    Assert-ArcaneForeground
    Write-ArcaneLog "Key-Hold $Key Milliseconds=$Milliseconds"
    try { Key-Down $Key; Wait-ArcaneInput $Milliseconds } finally { if ($script:HeldKeys.ContainsKey($Key.ToUpperInvariant())) { Key-Up $Key } }
}
function Key-Tap {
    param([Parameter(Mandatory)][string]$Key)
    Assert-ArcaneForeground
    Write-ArcaneLog "Key-Tap $Key"
    Key-Hold -Key $Key -Milliseconds 80
}
function Get-ArcaneClientRect {
    Assert-ArcaneForeground
    $rect = New-Object ArcaneVerification.Native+RECT
    $origin = New-Object ArcaneVerification.Native+POINT
    if (-not [ArcaneVerification.Native]::GetClientRect($script:Window, [ref]$rect) -or -not [ArcaneVerification.Native]::ClientToScreen($script:Window, [ref]$origin)) { throw 'Cannot get game client bounds.' }
    [pscustomobject]@{ X=$origin.X; Y=$origin.Y; Width=$rect.Right; Height=$rect.Bottom }
}
function Mouse-MoveClient {
    param([Parameter(Mandatory)][int]$X, [Parameter(Mandatory)][int]$Y)
    Assert-ArcaneForeground
    $rect = Get-ArcaneClientRect
    if ($X -lt 0 -or $Y -lt 0 -or $X -ge $rect.Width -or $Y -ge $rect.Height) { throw "Mouse point $X,$Y is outside the game client." }
    # Absolute SendInput coordinates are normalised over the whole virtual desktop and drifted by
    # tens of pixels on this multi-monitor setup; SetCursorPos takes physical pixels directly.
    # A zero relative move afterwards delivers the motion event to the game like a real mouse.
    $screenX = $rect.X + $X; $screenY = $rect.Y + $Y
    Assert-ArcaneForeground
    if (-not [ArcaneVerification.Native]::SetCursorPos($screenX, $screenY)) { throw "SetCursorPos failed for $screenX,$screenY" }
    $event = [ArcaneVerification.Native]::Mouse(0, 0, 0x0001)
    Send-ArcaneInput $event "Mouse-MoveClient X=$X Y=$Y screenX=$screenX screenY=$screenY"
    $actual = New-Object ArcaneVerification.Native+POINT
    $null = [ArcaneVerification.Native]::GetCursorPos([ref]$actual)
    if ([Math]::Abs($actual.X - $screenX) -gt 1 -or [Math]::Abs($actual.Y - $screenY) -gt 1) {
        # A physical mouse resting on the desk can nudge the cursor by a few pixels; place it once more.
        Write-ArcaneLog "WARN cursor drifted to $($actual.X),$($actual.Y); placing again"
        $null = [ArcaneVerification.Native]::SetCursorPos($screenX, $screenY)
        $null = [ArcaneVerification.Native]::GetCursorPos([ref]$actual)
        if ([Math]::Abs($actual.X - $screenX) -gt 8 -or [Math]::Abs($actual.Y - $screenY) -gt 8) { throw "Cursor landed at $($actual.X),$($actual.Y) instead of $screenX,$screenY" }
    }
}
function Send-ArcaneMouseButton {
    param([ValidateSet('Left','Right')][string]$Button, [bool]$Up)
    Assert-ArcaneForeground
    if ($Button -eq 'Left') { $flags = 2 } else { $flags = 8 }
    if ($Up) { $flags *= 2 }
    $event = [ArcaneVerification.Native]::Mouse(0, 0, $flags)
    Send-ArcaneInput $event "Mouse Button=$Button Up=$Up"
    if ($Up) { $script:HeldButtons.Remove($Button) } else { $script:HeldButtons[$Button] = $true }
}
function Mouse-Down {
    param([ValidateSet('Left','Right')][string]$Button = 'Left')
    Assert-ArcaneForeground
    Send-ArcaneMouseButton $Button $false
}
function Mouse-Up {
    param([ValidateSet('Left','Right')][string]$Button = 'Left')
    Assert-ArcaneForeground
    Send-ArcaneMouseButton $Button $true
}
function Mouse-Hold {
    param([ValidateSet('Left','Right')][string]$Button = 'Left', [ValidateRange(1,600000)][int]$Milliseconds = 100)
    Assert-ArcaneForeground
    Write-ArcaneLog "Mouse-Hold $Button Milliseconds=$Milliseconds"
    try { Mouse-Down $Button; Wait-ArcaneInput $Milliseconds } finally { if ($script:HeldButtons.ContainsKey($Button)) { Mouse-Up $Button } }
}
function Mouse-Click {
    param([ValidateSet('Left','Right')][string]$Button = 'Left')
    Assert-ArcaneForeground
    Write-ArcaneLog "Mouse-Click $Button"
    Mouse-Hold -Button $Button -Milliseconds 80
}
function Save-ClientCapture {
    param([Parameter(Mandatory)][string]$Path)
    Assert-ArcaneForeground
    Write-ArcaneLog "Save-ClientCapture Path=$Path"
    $rect = Get-ArcaneClientRect
    Write-ArcaneLog "Client bounds X=$($rect.X) Y=$($rect.Y) Width=$($rect.Width) Height=$($rect.Height) hwnd=$script:Window"
    $null = [ArcaneVerification.Native]::DwmFlush()
    if ($rect.Width -le 0 -or $rect.Height -le 0) { throw 'Game client is empty.' }
    $destination = [IO.Path]::GetFullPath($Path)
    $null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))
    $bitmap = New-Object Drawing.Bitmap($rect.Width, $rect.Height)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        Assert-ArcaneForeground
        $graphics.CopyFromScreen($rect.X, $rect.Y, 0, 0, $bitmap.Size, [Drawing.CopyPixelOperation]::SourceCopy)
        Assert-ArcaneForeground
        $bitmap.Save($destination, [Drawing.Imaging.ImageFormat]::Png)
    } finally { $graphics.Dispose(); $bitmap.Dispose() }
}
function Read-Telemetry {
    param([string]$OutDir = $script:OutputDirectory)
    $path = Join-Path $OutDir 'telemetry.jsonl'
    if (-not (Test-Path -LiteralPath $path)) { return }
    # The game may be halfway through writing its final line.
    $stream = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    $reader = [IO.StreamReader]::new($stream)
    try { $content = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $lines = $content -split "`n"
    for ($index = 0; $index -lt $lines.Length; $index++) {
        if ([string]::IsNullOrWhiteSpace($lines[$index])) { continue }
        try { ConvertFrom-Json -InputObject $lines[$index] } catch {
            if ($index -ne $lines.Length - 1) { throw }
        }
    }
}
function Get-LastTelemetry {
    param([string]$OutDir = $script:OutputDirectory)
    Read-Telemetry -OutDir $OutDir | Select-Object -Last 1
}
function Release-ArcaneInput {
    Write-ArcaneLog 'Release-ArcaneInput'
    $failures = @()
    foreach ($key in @($script:HeldKeys.Keys)) { try { Key-Up $key } catch { $failures += $_.Exception.Message } }
    foreach ($button in @($script:HeldButtons.Keys)) { try { Mouse-Up $button } catch { $failures += $_.Exception.Message } }
    if ($failures.Count) { throw ($failures -join '; ') }
}
function Stop-ArcaneGame {
    Write-ArcaneLog 'Stop-ArcaneGame'
    try { Release-ArcaneInput } finally {
        if ($script:Game -and -not $script:Game.HasExited) {
            $null = $script:Game.CloseMainWindow()
            if (-not $script:Game.WaitForExit(5000)) { $script:Game.Kill(); $script:Game.WaitForExit() }
        }
        $script:Game = $null
        $script:Window = [IntPtr]::Zero
    }
}
Export-ModuleMember -Function Wait-UserIdle, Start-ArcaneGame, Focus-ArcaneGame, Key-Down, Key-Up, Key-Tap, Key-Hold, Mouse-MoveClient, Mouse-Down, Mouse-Up, Mouse-Click, Mouse-Hold, Save-ClientCapture, Read-Telemetry, Get-LastTelemetry, Release-ArcaneInput, Stop-ArcaneGame, Write-ArcaneLog

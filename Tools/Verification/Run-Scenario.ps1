[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Exe,
    [Parameter(Mandatory)][string]$Scenario,
    [Parameter(Mandatory)][string]$OutDir,
    [string[]]$ExtraArgs = @(),
    # Isolates the run in its own save slot (development builds only), so the player's save is untouched.
    [string]$SaveKey = '',
    # Opens another chapter at launch (development builds), for example stone-village.
    [string]$Chapter = ''
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$exitCode = 0
if ($SaveKey) { $ExtraArgs = @($ExtraArgs) + @('--arcane-save-key', $SaveKey) }
if ($Chapter) { $ExtraArgs = @($ExtraArgs) + @('--arcane-chapter', $Chapter) }
try {
    Import-Module (Join-Path $PSScriptRoot 'ArcaneInput.psm1') -Force -DisableNameChecking
    $scenarioPath = (Resolve-Path -LiteralPath $Scenario).Path
    $OutDir = [IO.Path]::GetFullPath($OutDir)
    Start-ArcaneGame -Exe $Exe -OutDir $OutDir -ExtraArgs $ExtraArgs
    . $scenarioPath
    Write-ArcaneLog 'PASS Scenario completed'
} catch {
    $exitCode = 1
    if (Get-Command Write-ArcaneLog -ErrorAction SilentlyContinue) { Write-ArcaneLog "FAIL $($_.Exception.Message)" }
    Write-Error -ErrorAction Continue $_
} finally {
    if (Get-Command Stop-ArcaneGame -ErrorAction SilentlyContinue) {
        try { Stop-ArcaneGame } catch {
            $exitCode = 1
            Write-ArcaneLog "FAIL Cleanup: $($_.Exception.Message)"
            Write-Error -ErrorAction Continue $_
        }
    }
}
exit $exitCode

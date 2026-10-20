<#
Runs inside Windows Sandbox (see artifacts\installer\BuurmansRgb-test.wsb, made by build.ps1).
Installs BuurmansRgb silently, checks the result, uninstalls it and checks again. The results go
to C:\Results, which is artifacts\installer\sandbox-results on the host.
#>
$ErrorActionPreference = 'Continue'
$results = 'C:\Results'
$app = Join-Path $env:ProgramFiles 'BuurmansRgb'
$engineName = 'BuurmansRgb.Engine'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$uninstallKey = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall'
$checks = [ordered]@{}

Start-Transcript (Join-Path $results 'transcript.txt') -Force | Out-Null

function Test-Step([string]$Name, [scriptblock]$Test) {
    try {
        $passed = [bool](& $Test)
    }
    catch {
        Write-Host "$Name threw: $_"
        $passed = $false
    }

    $checks[$Name] = $passed
    Write-Host ("[{0}] {1}" -f ($(if ($passed) { 'OK' } else { 'FOUT' })), $Name)
}

function Wait-Until([scriptblock]$Condition, [int]$Seconds) {
    for ($i = 0; $i -lt $Seconds; $i++) {
        if (& $Condition) {
            return $true
        }

        Start-Sleep 1
    }

    return $false
}

function Get-AutoStart {
    (Get-ItemProperty $runKey -ErrorAction SilentlyContinue).BuurmansRgb
}

# Install
$setup = Start-Process 'C:\Setup\BuurmansRgb-setup.exe' -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=$results\setup.log" -Wait -PassThru
Write-Host "Setup exit code: $($setup.ExitCode)"
Test-Step 'Setup geslaagd (exitcode 0)' { $setup.ExitCode -eq 0 }
Test-Step 'Bestanden in Program Files' { Test-Path (Join-Path $app "$engineName.exe") }
Test-Step 'PawnIO geïnstalleerd' { Test-Path "$uninstallKey\PawnIO" }
Test-Step 'Service OpenRGB draait' { Wait-Until { (Get-Service OpenRGB -ErrorAction SilentlyContinue).Status -eq 'Running' } 60 }
Test-Step 'Poort 6742 luistert' { Wait-Until { Get-NetTCPConnection -LocalPort 6742 -State Listen -ErrorAction SilentlyContinue } 60 }
Test-Step 'Engine draait' { Wait-Until { Get-Process $engineName -ErrorAction SilentlyContinue } 30 }
Test-Step 'Starten met Windows staat aan' { (Get-AutoStart) -like "*$app*" }
Test-Step 'Engine verbonden met OpenRGB' {
    Wait-Until {
        $engine = Get-Process $engineName -ErrorAction SilentlyContinue
        $engine -and (Get-NetTCPConnection -RemotePort 6742 -State Established -OwningProcess $engine.Id -ErrorAction SilentlyContinue)
    } 60
}

Get-Service OpenRGB -ErrorAction SilentlyContinue | Format-List Name, Status, StartType | Out-String | Write-Host

# Uninstall; silently every question gets its default answer "Nee", so OpenRGB and PawnIO stay.
$uninstaller = Join-Path $app 'unins000.exe'
Start-Process $uninstaller -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=$results\uninstall.log" -Wait | Out-Null
Test-Step 'Verwijderen: bestanden weg' { Wait-Until { -not (Test-Path (Join-Path $app "$engineName.exe")) } 60 }
Test-Step 'Verwijderen: engine gestopt' { -not (Get-Process $engineName -ErrorAction SilentlyContinue) }
Test-Step 'Verwijderen: autostart weg' { -not (Get-AutoStart) }
Test-Step 'Verwijderen: OpenRGB blijft (standaard Nee)' { Get-Service OpenRGB -ErrorAction SilentlyContinue }

$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value }).Count
$summary = [ordered]@{ Passed = $checks.Count - $failed; Failed = $failed; Checks = $checks }
$summary | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $results 'result.json')
Write-Host "Klaar: $($summary.Passed) geslaagd, $failed mislukt. Zie C:\Results."
Stop-Transcript | Out-Null

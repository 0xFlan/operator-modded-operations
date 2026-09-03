[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$selector = Join-Path (Split-Path -Parent $PSScriptRoot) `
    'packaging\select_operator_mod_loader.ps1'
$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) `
    ('operator-loader-selector-test-' + [Guid]::NewGuid().ToString('N'))

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

try {
    $game = Join-Path $temporaryRoot 'game'
    [IO.Directory]::CreateDirectory($game) | Out-Null
    [IO.File]::WriteAllText((Join-Path $game 'OPERATOR.exe'), 'synthetic-game')
    $bep = Join-Path $game 'winhttp.dll'
    $melon = Join-Path $game 'version.dll'
    [IO.File]::WriteAllText($bep, 'synthetic-bep-bootstrap')
    [IO.File]::WriteAllText($melon, 'synthetic-melon-bootstrap')
    $policyPath = Join-Path $temporaryRoot 'policy.json'
    $policy = [ordered]@{
        schemaVersion = 1
        kind = 'operator-loader-selector-policy'
        loaders = [ordered]@{
            BepInEx = [ordered]@{
                canonicalFileName = 'winhttp.dll'
                knownBootstraps = @([ordered]@{
                    length = (Get-Item -LiteralPath $bep).Length
                    sha256 = Get-Sha256 $bep
                })
            }
            MelonLoader = [ordered]@{
                canonicalFileName = 'version.dll'
                knownBootstraps = @([ordered]@{
                    length = (Get-Item -LiteralPath $melon).Length
                    sha256 = Get-Sha256 $melon
                })
            }
        }
    }
    [IO.File]::WriteAllText(
        $policyPath,
        ($policy | ConvertTo-Json -Depth 7),
        [Text.UTF8Encoding]::new($false))

    & $selector -OperatorGameDir $game -Loader BepInEx -PolicyPath $policyPath
    if (-not (Test-Path -LiteralPath $bep) -or (Test-Path -LiteralPath $melon)) {
        throw 'BepInEx selection did not leave exactly its bootstrap active.'
    }
    $stateBefore = Get-Content -LiteralPath `
        (Join-Path $game '.operator-mod-loader-selector\state.json') -Raw |
        ConvertFrom-Json
    & $selector -OperatorGameDir $game -Loader BepInEx -PolicyPath $policyPath
    $stateAfter = Get-Content -LiteralPath `
        (Join-Path $game '.operator-mod-loader-selector\state.json') -Raw |
        ConvertFrom-Json
    if ($stateBefore.selectedBootstrapSha256 -cne $stateAfter.selectedBootstrapSha256) {
        throw 'Idempotent selection changed the selected bootstrap identity.'
    }

    & $selector -OperatorGameDir $game -Loader MelonLoader -PolicyPath $policyPath
    if ((Test-Path -LiteralPath $bep) -or -not (Test-Path -LiteralPath $melon)) {
        throw 'MelonLoader selection did not leave exactly its bootstrap active.'
    }
    & $selector -OperatorGameDir $game -Loader BepInEx -PolicyPath $policyPath
    if (-not (Test-Path -LiteralPath $bep) -or (Test-Path -LiteralPath $melon)) {
        throw 'Switch-back did not restore exactly BepInEx.'
    }

    [IO.File]::WriteAllText($melon, 'unknown-reinstalled-bootstrap')
    $unknownRefused = $false
    try {
        & $selector -OperatorGameDir $game -Loader BepInEx -PolicyPath $policyPath
    }
    catch { $unknownRefused = $true }
    if (-not $unknownRefused) { throw 'Unknown bootstrap was not refused.' }
    [IO.File]::Delete($melon)

    Write-Host 'PASS selector dual-installed switching, idempotence, switch-back, and unknown-bootstrap refusal'
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        [IO.Directory]::Delete($temporaryRoot, $true)
    }
}

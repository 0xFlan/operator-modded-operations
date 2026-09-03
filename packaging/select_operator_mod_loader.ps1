[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $OperatorGameDir,

    [Parameter(Mandatory = $true)]
    [ValidateSet('BepInEx', 'MelonLoader')]
    [string] $Loader,

    [string] $PolicyPath = (Join-Path $PSScriptRoot 'operator_loader_selector_manifest.json')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$selectorDirectoryName = '.operator-mod-loader-selector'
$stateFileName = 'state.json'
$journalFileName = 'journal.json'
$lockFileName = 'selector.lock'

function Get-Sha256 {
    param([Parameter(Mandatory = $true)][string] $Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-NormalizedPath {
    param([Parameter(Mandatory = $true)][string] $Path)
    return [IO.Path]::GetFullPath($Path).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
}

function Get-ContainedPath {
    param(
        [Parameter(Mandatory = $true)][string] $Root,
        [Parameter(Mandatory = $true)][string] $RelativePath
    )
    if ([IO.Path]::IsPathRooted($RelativePath) -or
        [string]::IsNullOrWhiteSpace($RelativePath) -or
        $RelativePath.IndexOfAny([char[]]"`r`n`0") -ge 0) {
        throw "Unsafe relative path: '$RelativePath'."
    }
    $segments = @($RelativePath.Replace('\', '/').Split('/'))
    if (@($segments | Where-Object { $_ -in @('', '.', '..') }).Count -ne 0) {
        throw "Unsafe relative path segment: '$RelativePath'."
    }
    $rootPath = Get-NormalizedPath $Root
    $candidate = $rootPath
    foreach ($segment in $segments) {
        $candidate = Join-Path $candidate $segment
    }
    $candidate = Get-NormalizedPath $candidate
    if (-not $candidate.StartsWith(
            $rootPath + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escaped the OPERATOR root: '$RelativePath'."
    }
    return $candidate
}

function Assert-NoReparseChain {
    param(
        [Parameter(Mandatory = $true)][string] $Root,
        [Parameter(Mandatory = $true)][string] $Path
    )
    $rootPath = Get-NormalizedPath $Root
    $candidate = Get-NormalizedPath $Path
    if ($candidate -ne $rootPath -and -not $candidate.StartsWith(
            $rootPath + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Selector path escaped the OPERATOR root: $candidate"
    }
    $cursor = $rootPath
    $relative = [IO.Path]::GetRelativePath($rootPath, $candidate)
    if ($relative -eq '.') { $relative = '' }
    foreach ($segment in @($relative.Split(
            [IO.Path]::DirectorySeparatorChar,
            [StringSplitOptions]::RemoveEmptyEntries))) {
        $cursor = Join-Path $cursor $segment
        if (Test-Path -LiteralPath $cursor) {
            $item = Get-Item -LiteralPath $cursor -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Selector refuses a reparse point: $cursor"
            }
        }
    }
}

function Assert-OperatorClosed {
    $live = @(Get-Process -Name 'OPERATOR', 'OPERATOR-Win64-Shipping' `
        -ErrorAction SilentlyContinue)
    if ($live.Count -ne 0) {
        throw 'Close OPERATOR completely before changing the selected mod loader.'
    }
}

function Write-JsonAtomically {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)] $Value
    )
    $stage = "$Path.stage-$([Guid]::NewGuid().ToString('N'))"
    [IO.File]::WriteAllText(
        $stage,
        ($Value | ConvertTo-Json -Depth 8),
        [Text.UTF8Encoding]::new($false))
    [IO.File]::Move($stage, $Path, $true)
}

function Get-KnownIdentity {
    param(
        [Parameter(Mandatory = $true)] $PolicyEntry,
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string] $Description
    )
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $null
    }
    $item = Get-Item -LiteralPath $Path -Force
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "$Description cannot be a reparse point: $Path"
    }
    $hash = Get-Sha256 $Path
    $matches = @($PolicyEntry.knownBootstraps | Where-Object {
        [long]$_.length -eq [long]$item.Length -and
        [string]$_.sha256 -ceq $hash
    })
    if ($matches.Count -ne 1) {
        throw "$Description is unknown or modified: $Path (length=$($item.Length), sha256=$hash)."
    }
    return [pscustomobject]@{ length = [long]$item.Length; sha256 = $hash }
}

function Get-InactivePrimaryPath {
    param(
        [Parameter(Mandatory = $true)][string] $SelectorRoot,
        [Parameter(Mandatory = $true)][string] $LoaderName,
        [Parameter(Mandatory = $true)][string] $Hash,
        [Parameter(Mandatory = $true)][string] $CanonicalFileName
    )
    return Get-ContainedPath $SelectorRoot (
        'inactive/{0}/{1}/{2}.inactive' -f $LoaderName, $Hash, $CanonicalFileName)
}

function Get-StoredCandidates {
    param(
        [Parameter(Mandatory = $true)][string] $SelectorRoot,
        [Parameter(Mandatory = $true)][string] $LoaderName,
        [Parameter(Mandatory = $true)] $PolicyEntry
    )
    $loaderStore = Get-ContainedPath $SelectorRoot ('inactive/' + $LoaderName)
    if (-not (Test-Path -LiteralPath $loaderStore -PathType Container)) {
        return @()
    }
    $candidates = @()
    foreach ($file in @(Get-ChildItem -LiteralPath $loaderStore -Recurse -File -Force |
            Where-Object { $_.Name -eq "$($PolicyEntry.canonicalFileName).inactive" })) {
        $identity = Get-KnownIdentity $PolicyEntry $file.FullName `
            "Stored $LoaderName bootstrap"
        $candidates += [pscustomobject]@{
            path = $file.FullName
            length = $identity.length
            sha256 = $identity.sha256
        }
    }
    return @($candidates)
}

function Recover-Journal {
    param(
        [Parameter(Mandatory = $true)][string] $GameRoot,
        [Parameter(Mandatory = $true)][string] $JournalPath
    )
    if (-not (Test-Path -LiteralPath $JournalPath -PathType Leaf)) { return }
    $journal = Get-Content -LiteralPath $JournalPath -Raw | ConvertFrom-Json
    if ([int]$journal.schemaVersion -ne 1 -or
        [string]$journal.kind -cne 'operator-loader-selector-journal') {
        throw "Selector recovery journal is malformed: $JournalPath"
    }
    foreach ($move in @($journal.moves | Select-Object -Reverse)) {
        $source = Get-ContainedPath $GameRoot ([string]$move.sourceRelativePath)
        $destination = Get-ContainedPath $GameRoot ([string]$move.destinationRelativePath)
        Assert-NoReparseChain $GameRoot $source
        Assert-NoReparseChain $GameRoot $destination
        $sourceExists = Test-Path -LiteralPath $source -PathType Leaf
        $destinationExists = Test-Path -LiteralPath $destination -PathType Leaf
        if ($sourceExists -and -not $destinationExists) {
            if ((Get-Sha256 $source) -cne [string]$move.sha256) {
                throw "Journal source identity changed: $source"
            }
            continue
        }
        if ($destinationExists -and -not $sourceExists) {
            if ((Get-Sha256 $destination) -cne [string]$move.sha256) {
                throw "Journal destination identity changed: $destination"
            }
            [IO.Directory]::CreateDirectory((Split-Path -Parent $source)) | Out-Null
            [IO.File]::Move($destination, $source)
            continue
        }
        throw "Cannot recover selector journal move: $source -> $destination"
    }
    [IO.File]::Delete($JournalPath)
    Write-Host 'Recovered an interrupted loader-selection transaction to its prior state.'
}

Assert-OperatorClosed
$gameRoot = Get-NormalizedPath $OperatorGameDir
if (-not (Test-Path -LiteralPath (Join-Path $gameRoot 'OPERATOR.exe') -PathType Leaf)) {
    throw "OPERATOR.exe is missing from the selected game root: $gameRoot"
}
Assert-NoReparseChain $gameRoot $gameRoot
$policyFullPath = Get-NormalizedPath $PolicyPath
$policy = Get-Content -LiteralPath $policyFullPath -Raw | ConvertFrom-Json
if ([int]$policy.schemaVersion -ne 1 -or
    [string]$policy.kind -cne 'operator-loader-selector-policy') {
    throw "Loader selector policy is unsupported: $policyFullPath"
}
foreach ($loaderName in @('BepInEx', 'MelonLoader')) {
    $entry = $policy.loaders.PSObject.Properties[$loaderName].Value
    if ($null -eq $entry -or
        [string]::IsNullOrWhiteSpace([string]$entry.canonicalFileName) -or
        @($entry.knownBootstraps).Count -eq 0) {
        throw "Loader selector policy is incomplete for $loaderName."
    }
    foreach ($known in @($entry.knownBootstraps)) {
        if ([long]$known.length -le 0 -or
            [string]$known.sha256 -cnotmatch '^[0-9a-f]{64}$') {
            throw "Loader selector policy contains an invalid $loaderName identity."
        }
    }
}

$selectorRoot = Get-ContainedPath $gameRoot $selectorDirectoryName
[IO.Directory]::CreateDirectory($selectorRoot) | Out-Null
Assert-NoReparseChain $gameRoot $selectorRoot
$lockPath = Get-ContainedPath $selectorRoot $lockFileName
$statePath = Get-ContainedPath $selectorRoot $stateFileName
$journalPath = Get-ContainedPath $selectorRoot $journalFileName
$lockStream = $null
try {
    $lockStream = [IO.File]::Open(
        $lockPath,
        [IO.FileMode]::OpenOrCreate,
        [IO.FileAccess]::ReadWrite,
        [IO.FileShare]::None)
    Recover-Journal $gameRoot $journalPath
    Assert-OperatorClosed

    $state = $null
    if (Test-Path -LiteralPath $statePath -PathType Leaf) {
        $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    }
    $otherLoader = if ($Loader -ceq 'BepInEx') { 'MelonLoader' } else { 'BepInEx' }
    $moves = New-Object 'System.Collections.Generic.List[object]'
    $identities = @{}
    foreach ($loaderName in @('BepInEx', 'MelonLoader')) {
        $entry = $policy.loaders.PSObject.Properties[$loaderName].Value
        $canonical = Get-ContainedPath $gameRoot ([string]$entry.canonicalFileName)
        Assert-NoReparseChain $gameRoot $canonical
        $identities[$loaderName] = Get-KnownIdentity $entry $canonical `
            "$loaderName native bootstrap"
    }

    $otherEntry = $policy.loaders.PSObject.Properties[$otherLoader].Value
    $otherCanonical = Get-ContainedPath $gameRoot ([string]$otherEntry.canonicalFileName)
    if ($null -ne $identities[$otherLoader]) {
        $otherIdentity = $identities[$otherLoader]
        $otherInactive = Get-InactivePrimaryPath $selectorRoot $otherLoader `
            $otherIdentity.sha256 ([string]$otherEntry.canonicalFileName)
        if (Test-Path -LiteralPath $otherInactive -PathType Leaf) {
            $storedIdentity = Get-KnownIdentity $otherEntry $otherInactive `
                "Stored $otherLoader bootstrap"
            if ($storedIdentity.sha256 -cne $otherIdentity.sha256) {
                throw "Stored $otherLoader bootstrap conflicts with the active bootstrap."
            }
            $otherInactive = Get-ContainedPath $selectorRoot (
                'inactive/{0}/{1}/duplicates/{2}-{3}.inactive' -f
                $otherLoader, $otherIdentity.sha256,
                [DateTime]::UtcNow.ToString('yyyyMMddHHmmssfffffff'),
                [string]$otherEntry.canonicalFileName)
        }
        $moves.Add([pscustomobject]@{
            source = $otherCanonical
            destination = $otherInactive
            sha256 = $otherIdentity.sha256
        })
    }

    $selectedEntry = $policy.loaders.PSObject.Properties[$Loader].Value
    $selectedCanonical = Get-ContainedPath $gameRoot ([string]$selectedEntry.canonicalFileName)
    if ($null -eq $identities[$Loader]) {
        $candidates = @(Get-StoredCandidates $selectorRoot $Loader $selectedEntry)
        if ($null -ne $state -and
            [string]$state.selectedLoader -ceq $Loader -and
            [string]$state.selectedBootstrapSha256 -cmatch '^[0-9a-f]{64}$') {
            $preferred = @($candidates | Where-Object {
                $_.sha256 -ceq [string]$state.selectedBootstrapSha256
            })
            if ($preferred.Count -eq 1) { $candidates = $preferred }
        }
        if ($candidates.Count -ne 1) {
            throw "Cannot select ${Loader}: expected exactly one known parked bootstrap, found $($candidates.Count)."
        }
        $moves.Add([pscustomobject]@{
            source = $candidates[0].path
            destination = $selectedCanonical
            sha256 = $candidates[0].sha256
        })
    }

    if ($moves.Count -ne 0) {
        $journalMoves = @($moves | ForEach-Object {
            [ordered]@{
                sourceRelativePath = [IO.Path]::GetRelativePath($gameRoot, $_.source).Replace('\', '/')
                destinationRelativePath = [IO.Path]::GetRelativePath($gameRoot, $_.destination).Replace('\', '/')
                sha256 = $_.sha256
            }
        })
        $journal = [ordered]@{
            schemaVersion = 1
            kind = 'operator-loader-selector-journal'
            requestedLoader = $Loader
            createdUtc = [DateTime]::UtcNow.ToString('o')
            moves = $journalMoves
        }
        Write-JsonAtomically $journalPath $journal
        foreach ($move in $moves) {
            Assert-OperatorClosed
            if (-not (Test-Path -LiteralPath $move.source -PathType Leaf) -or
                (Test-Path -LiteralPath $move.destination)) {
                throw "Selector transaction path changed: $($move.source) -> $($move.destination)"
            }
            if ((Get-Sha256 $move.source) -cne $move.sha256) {
                throw "Selector transaction source identity changed: $($move.source)"
            }
            [IO.Directory]::CreateDirectory((Split-Path -Parent $move.destination)) | Out-Null
            Assert-NoReparseChain $gameRoot (Split-Path -Parent $move.destination)
            [IO.File]::Move($move.source, $move.destination)
        }
    }

    $selectedIdentity = Get-KnownIdentity $selectedEntry $selectedCanonical `
        "$Loader selected bootstrap"
    if ($null -eq $selectedIdentity) {
        throw "The selected $Loader bootstrap is absent after selection."
    }
    if (Test-Path -LiteralPath $otherCanonical) {
        throw "The inactive $otherLoader bootstrap remains active after selection."
    }
    $newState = [ordered]@{
        schemaVersion = 1
        kind = 'operator-loader-selector-state'
        selectedLoader = $Loader
        selectedBootstrapSha256 = $selectedIdentity.sha256
        policySha256 = Get-Sha256 $policyFullPath
        updatedUtc = [DateTime]::UtcNow.ToString('o')
    }
    Write-JsonAtomically $statePath $newState
    if (Test-Path -LiteralPath $journalPath) {
        [IO.File]::Delete($journalPath)
    }
    Write-Host "Selected $Loader for OPERATOR. Exactly one known native bootstrap is active."
}
finally {
    if ($null -ne $lockStream) { $lockStream.Dispose() }
}

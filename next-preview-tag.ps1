<#
.SYNOPSIS
    Creates the next preview git tag based on the latest existing version tag.

.DESCRIPTION
    Finds the highest version tag in the repository and computes the next preview tag:

      * Latest is a stable release (e.g. v0.1.4):
        prompts for the new release version (e.g. 0.2.0), validates that it is a
        MAJOR.MINOR.PATCH greater than the latest stable, and tags v0.2.0-preview.1.

      * Latest is already a preview (e.g. v0.2.0-preview.26):
        bumps the preview counter and tags v0.2.0-preview.27.

    By default only a local tag is created. Use -Push to push it to the remote.

.PARAMETER Push
    Push the created tag to the remote after creating it locally.

.PARAMETER Remote
    Remote to push to when -Push is used. Defaults to 'origin'.

.EXAMPLE
    .\next-preview-tag.ps1

.EXAMPLE
    .\next-preview-tag.ps1 -Push
#>
[CmdletBinding()]
param(
    [switch]$Push,
    [string]$Remote = 'origin'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-HighestVersionTag {
    $tags = git tag --sort=-v:refname 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw 'Not a git repository (git tag failed).'
    }
    return $tags | Where-Object { $_ -match '^v\d+\.\d+\.\d+(-preview\.\d+)?$' } | Select-Object -First 1
}

function Read-NewReleaseVersion([version]$CurrentBase) {
    while ($true) {
        $value = (Read-Host 'Enter the new release version (e.g. 0.2.0)').Trim()
        if ($value -notmatch '^\d+\.\d+\.\d+$') {
            Write-Warning "'$value' is not a valid MAJOR.MINOR.PATCH version."
            continue
        }
        if ([version]$value -le $CurrentBase) {
            Write-Warning "New release version must be greater than the latest stable $CurrentBase."
            continue
        }
        return $value
    }
}

$latest = Get-HighestVersionTag

if (-not $latest) {
    Write-Host 'No version tags found in the repository.'
    $base = Read-NewReleaseVersion ([version]'0.0.0')
    $next = "$base-preview.1"
}
elseif ($latest -match '^v(?<base>\d+\.\d+\.\d+)-preview\.(?<n>\d+)$') {
    $next = "$($Matches['base'])-preview.$([int]$Matches['n'] + 1)"
    Write-Host "Latest tag is $latest -> next is v$next"
}
elseif ($latest -match '^v(?<base>\d+\.\d+\.\d+)$') {
    Write-Host "Latest tag is a stable release: $latest"
    $base = Read-NewReleaseVersion ([version]$Matches['base'])
    $next = "$base-preview.1"
}
else {
    throw "Unexpected version tag format: $latest"
}

$tag = "v$next"

git rev-parse -q --verify "refs/tags/$tag" | Out-Null
if ($LASTEXITCODE -eq 0) {
    throw "Tag '$tag' already exists."
}

$branch = git branch --show-current
if ($branch -notin @('main', 'master')) {
    Write-Warning "You are on branch '$branch', not 'main'."
}
if (git status --porcelain) {
    Write-Warning 'Working tree is not clean; the tag may not point to a buildable commit.'
}

$answer = Read-Host "Create tag '$tag' at HEAD? [y/N]"
if ($answer.Trim() -notmatch '^[yY]$') {
    Write-Host 'Aborted.'
    exit 1
}

git tag $tag
if ($LASTEXITCODE -ne 0) {
    throw "Failed to create tag '$tag'."
}
Write-Host "Created tag: $tag"

if ($Push) {
    git push $Remote $tag
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to push tag '$tag' to '$Remote'."
    }
    Write-Host "Pushed tag: $tag -> $Remote"
}

[CmdletBinding()]
param (
    [Parameter(Mandatory = $false)]
    [string]$SolutionPath = ".",

    [Parameter(Mandatory = $true)]
    [string]$ApiKey,

    # Lista projektów do spakowania (obsługuje dokładne nazwy oraz wzorce wieloznaczne wildcard *)
    [Parameter(Mandatory = $false)]
    [string[]]$ProjectNames = @(
        "Quorum.Backend.AdminAPI",
        "Quorum.Backend.AdminUI",
        "Quorum.Backend.EntityFramework"
    ),

    [Parameter(Mandatory = $false)]
    [string]$OutputDir = ".\artifacts",

    [Parameter(Mandatory = $false)]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "==> Czyszczenie folderu wyjściowego: $OutputDir" -ForegroundColor Cyan
if (Test-Path $OutputDir) {
    Remove-Item -Path $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

Write-Host "==> Wyszukiwanie wskazanych projektów .csproj..." -ForegroundColor Cyan

# Pobieranie wszystkich .csproj z pominięciem katalogów bin/obj
$allProjects = Get-ChildItem -Path $SolutionPath -Filter "*.csproj" -Recurse | 
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }

# Filtrowanie projektów zgodnie z listą $ProjectNames
$selectedProjects = @()
foreach ($pattern in $ProjectNames) {
    # Dodanie rozszerzenia .csproj do wzorca, jeśli użytkownik go nie podał
    $searchPattern = if ($pattern.EndsWith(".csproj")) { $pattern } else { "$pattern.csproj" }
    
    $matched = $allProjects | Where-Object { $_.Name -like $searchPattern }
    if ($matched) {
        $selectedProjects += $matched
    } else {
        Write-Warning "Nie znaleziono projektu pasującego do wzorca: $pattern"
    }
}

# Usunięcie ewentualnych duplikatów
$selectedProjects = $selectedProjects | Select-Object -Unique

if ($selectedProjects.Count -eq 0) {
    Write-Error "Nie znaleziono żadnych projektów pasujących do podanej listy."
    exit
}

Write-Host "Znaleziono $($selectedProjects.Count) projekt(ów) do spakowania:" -ForegroundColor Green
$selectedProjects | ForEach-Object { Write-Host " - $($_.Name)" -ForegroundColor Gray }

# 1. Budowanie i pakowanie do .nupkg
foreach ($project in $selectedProjects) {
    Write-Host "`n---> Pakowanie: $($project.Name)" -ForegroundColor Yellow
    
    dotnet pack $project.FullName `
        --configuration $Configuration `
        --output $OutputDir `
        /p:GeneratePackageOnBuild=false

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Błąd podczas pakowania projektu: $($project.Name)"
    }
}

# 2. Wysyłka paczek na nuget.org
$packages = Get-ChildItem -Path $OutputDir -Filter "*.nupkg" | 
    Where-Object { $_.Name -notmatch '\.symbols\.nupkg$' }

if ($packages.Count -eq 0) {
    Write-Warning "Nie wygenerowano żadnych paczek .nupkg do wysłania."
    exit
}

Write-Host "`n==> Wygenerowano $($packages.Count) paczke/paczek .nupkg." -ForegroundColor Cyan

foreach ($package in $packages) {
    Write-Host "`n---> Wysyłanie: $($package.Name) do nuget.org..." -ForegroundColor Yellow
    
    dotnet nuget push $package.FullName `
        --api-key $ApiKey `
        --source "https://api.nuget.org/v3/index.json" `
        --skip-duplicate

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Błąd podczas wysyłania paczki: $($package.Name)"
    }
}

Write-Host "`n==> Proces zakończony sukcesem!" -ForegroundColor Green
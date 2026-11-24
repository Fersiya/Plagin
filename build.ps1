<#
.SYNOPSIS
    Сборщик SubPanel-Setup.exe для Windows (PowerShell).
    Требуется установленный NSIS (Nullsoft Scriptable Install System).
#>
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-Location -LiteralPath $PSScriptRoot

Write-Host "==> Проверка версий..." -ForegroundColor Cyan

# 1. Считываем версии из файлов
$jsContent = Get-Content "js/main.js" -Raw
$xmlContent = Get-Content "CSXS/manifest.xml" -Raw
$nsiContent = Get-Content "installer/installer.nsi" -Raw

$v_js = [regex]::Match($jsContent, "APP_VERSION\s*=\s*'([^']+)'").Groups[1].Value
$v_xml = [regex]::Match($xmlContent, 'ExtensionBundleVersion="([^"]+)"').Groups[1].Value
$v_nsi = [regex]::Match($nsiContent, '!define\s+VERSION\s+"([^"]+)"').Groups[1].Value

if (-not $v_js -or -not $v_xml -or -not $v_nsi) {
    Write-Error "Не удалось извлечь версии из файлов: main.js='$v_js', manifest='$v_xml', nsi='$v_nsi'"
    exit 1
}

if ($v_js -ne $v_xml -or $v_js -ne $v_nsi) {
    Write-Error "Версии не совпадают: main.js=$v_js, manifest=$v_xml, installer=$v_nsi"
    exit 1
}

Write-Host "Версия: $v_js (проверено, совпадает)" -ForegroundColor Green

# 2. Ищем makensis.exe
$makensisCmd = Get-Command makensis -ErrorAction SilentlyContinue
$makensis = if ($makensisCmd) { $makensisCmd.Source } else { $null }
if (-not $makensis) {
    $candidates = @(
        "C:\Program Files (x86)\NSIS\makensis.exe",
        "C:\Program Files\NSIS\makensis.exe",
        "$env:LOCALAPPDATA\Programs\NSIS\makensis.exe"
    )
    foreach ($cand in $candidates) {
        if (Test-Path $cand) {
            $makensis = $cand
            break
        }
    }
}

if (-not $makensis) {
    Write-Error "makensis.exe не найден! Установите NSIS через: winget install NSIS.NSIS"
    exit 1
}

Write-Host "Используется NSIS: $makensis" -ForegroundColor Gray

# 3. Компиляция PrLauncher.exe
Write-Host "==> Сборка PrLauncher.exe..." -ForegroundColor Cyan
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (Test-Path $csc) {
    & $csc /target:winexe /optimize+ /out:"dist\PrLauncher.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.dll /r:System.Core.dll "launcher\PrLauncher.cs"
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Ошибка компиляции PrLauncher.cs!"
        exit 1
    }
    Write-Host "PrLauncher.exe скомпилирован успешно!" -ForegroundColor Green
} else {
    Write-Warning "csc.exe не найден, пропускаем компиляцию PrLauncher"
}

# 4. Подготовка структуры каталогов
Write-Host "==> Подготовка файлов сборки..." -ForegroundColor Cyan
if (Test-Path "build") {
    Remove-Item -Recurse -Force "build"
}
New-Item -ItemType Directory -Force -Path "build\payload" | Out-Null
New-Item -ItemType Directory -Force -Path "dist" | Out-Null

Copy-Item -Recurse -Force CSXS, css, js, jsx, index.html "build\payload\"
if (Test-Path "dist\PrLauncher.exe") {
    Copy-Item -Force "dist\PrLauncher.exe" "build\payload\"
}

# 5. Компиляция инсталлятора
Write-Host "==> Сборка инсталлятора с помощью NSIS..." -ForegroundColor Cyan
Push-Location "installer"
try {
    & $makensis -V2 "installer.nsi"
    if ($LASTEXITCODE -ne 0) {
        throw "NSIS завершился с ошибкой (код $LASTEXITCODE)"
    }
} finally {
    Pop-Location
}

$outputExe = "dist\SubPanel-Setup.exe"
if (-not (Test-Path $outputExe)) {
    Write-Error "Файл $outputExe не был создан!"
    exit 1
}

# 6. Хэши и результат
$fileItem = Get-Item $outputExe
$shaSetup = (Get-FileHash -Algorithm SHA256 (Join-Path $PSScriptRoot "dist\SubPanel-Setup.exe")).Hash.ToLower()

$shaLauncher = ""
$launcherPath = Join-Path $PSScriptRoot "dist\PrLauncher.exe"
if (Test-Path $launcherPath) {
    $shaLauncher = (Get-FileHash -Algorithm SHA256 $launcherPath).Hash.ToLower()
}

$sumsContent = "$shaSetup  SubPanel-Setup.exe`r`n"
if ($shaLauncher) {
    $sumsContent += "$shaLauncher  PrLauncher.exe`r`n"
}
$sumsPath = Join-Path $PSScriptRoot "dist\SHA256SUMS.txt"
[System.IO.File]::WriteAllText($sumsPath, $sumsContent, [System.Text.Encoding]::UTF8)

Write-Host "========================================" -ForegroundColor Green
Write-Host "Сборка успешно завершена!" -ForegroundColor Green
Write-Host "Инсталлятор: $($fileItem.FullName) ($([math]::Round($fileItem.Length / 1KB, 1)) КБ)"
Write-Host "SHA-256:     $shaSetup"
if ($shaLauncher) {
    $launcherItem = Get-Item $launcherPath
    Write-Host "Лаунчер:     $($launcherItem.FullName) ($([math]::Round($launcherItem.Length / 1KB, 1)) КБ)"
    Write-Host "SHA-256:     $shaLauncher"
}
Write-Host "========================================" -ForegroundColor Green

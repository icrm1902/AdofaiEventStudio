param(
    [string]$GameDir = "D:\steam\steamapps\common\A Dance of Fire and Ice",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$projectDir = [System.IO.Path]::GetFullPath((Split-Path -Parent $MyInvocation.MyCommand.Path))
$distDir = [System.IO.Path]::GetFullPath((Join-Path $projectDir "dist"))
$outputsDir = [System.IO.Path]::GetFullPath((Split-Path -Parent $projectDir))
$managedDir = Join-Path $GameDir "A Dance of Fire and Ice_Data\Managed"
$ummDir = Join-Path $managedDir "UnityModManager"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path -LiteralPath $csc)) {
    throw "找不到 C# 编译器: $csc"
}
if (-not (Test-Path -LiteralPath (Join-Path $managedDir "Assembly-CSharp.dll"))) {
    throw "游戏 Managed 目录无效: $managedDir"
}

if ($distDir.StartsWith($projectDir + [System.IO.Path]::DirectorySeparatorChar)) {
    if (Test-Path -LiteralPath $distDir) {
        Remove-Item -LiteralPath $distDir -Recurse -Force
    }
} else {
    throw "拒绝清理项目目录以外的路径: $distDir"
}

New-Item -ItemType Directory -Path $distDir -Force | Out-Null

$references = @(
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll",
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Core.dll",
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Xml.dll",
    (Join-Path $managedDir "netstandard.dll"),
    (Join-Path $managedDir "Newtonsoft.Json.dll"),
    (Join-Path $managedDir "Assembly-CSharp.dll"),
    (Join-Path $managedDir "Assembly-CSharp-firstpass.dll"),
    (Join-Path $managedDir "RDTools.dll"),
    (Join-Path $managedDir "UnityEngine.dll"),
    (Join-Path $managedDir "UnityEngine.CoreModule.dll"),
    (Join-Path $managedDir "UnityEngine.IMGUIModule.dll"),
    (Join-Path $managedDir "UnityEngine.TextRenderingModule.dll"),
    (Join-Path $managedDir "UnityEngine.ParticleSystemModule.dll"),
    (Join-Path $managedDir "UnityEngine.UI.dll"),
    (Join-Path $managedDir "Unity.TextMeshPro.dll"),
    (Join-Path $ummDir "UnityModManager.dll"),
    (Join-Path $ummDir "0Harmony.dll")
)

$sources = @(Get-ChildItem -LiteralPath (Join-Path $projectDir "src") -Filter "*.cs" |
    Sort-Object Name |
    ForEach-Object { $_.FullName })

$assemblyPath = Join-Path $distDir "AdofaiEventStudio.dll"
$arguments = @(
    "/nologo",
    "/target:library",
    "/platform:anycpu",
    "/optimize+",
    "/codepage:65001",
    "/out:$assemblyPath"
)

foreach ($reference in $references) {
    $arguments += "/reference:$reference"
}
$arguments += $sources

& $csc @arguments
if ($LASTEXITCODE -ne 0) {
    throw "DLL 编译失败，退出码: $LASTEXITCODE"
}

Copy-Item -LiteralPath (Join-Path $projectDir "Info.json") -Destination $distDir

if (-not $SkipTests) {
    $testPath = Join-Path $projectDir "bin-tests"
    New-Item -ItemType Directory -Path $testPath -Force | Out-Null
    $testAssembly = Join-Path $testPath "SmokeTest.exe"
    $testNewtonsoft = "C:\Program Files (x86)\LightingService\Newtonsoft.Json.dll"
    if (-not (Test-Path -LiteralPath $testNewtonsoft)) {
        throw "找不到仅供测试的 .NET Framework Newtonsoft.Json: $testNewtonsoft"
    }
    $testArguments = @(
        "/nologo",
        "/target:exe",
        "/platform:anycpu",
        "/codepage:65001",
        "/main:AdofaiEventStudio.SmokeTest",
        "/out:$testAssembly"
    )
    $testArguments += "/reference:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll"
    $testArguments += "/reference:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Core.dll"
    $testArguments += "/reference:$testNewtonsoft"
    $testArguments += (Join-Path $projectDir "src\EventProject.cs")
    $testArguments += (Join-Path $projectDir "tests\SmokeTest.cs")

    & $csc @testArguments
    if ($LASTEXITCODE -ne 0) {
        throw "冒烟测试编译失败，退出码: $LASTEXITCODE"
    }

    Copy-Item -LiteralPath $testNewtonsoft -Destination (Join-Path $testPath "Newtonsoft.Json.dll") -Force
    & $testAssembly "D:\adofai\testlevel\level.adofai"
    if ($LASTEXITCODE -ne 0) {
        throw "冒烟测试失败，退出码: $LASTEXITCODE"
    }

    $entryTestAssembly = Join-Path $testPath "EntryBindingTest.exe"
    $entryTestArguments = @(
        "/nologo",
        "/target:exe",
        "/platform:anycpu",
        "/codepage:65001",
        "/main:AdofaiEventStudio.EntryBindingTest",
        "/out:$entryTestAssembly",
        (Join-Path $projectDir "tests\EntryBindingTest.cs")
    )
    & $csc @entryTestArguments
    if ($LASTEXITCODE -ne 0) {
        throw "UMM 入口测试编译失败，退出码: $LASTEXITCODE"
    }

    Copy-Item -LiteralPath $assemblyPath -Destination $testPath -Force
    Copy-Item -LiteralPath (Join-Path $ummDir "UnityModManager.dll") -Destination $testPath -Force
    & $entryTestAssembly $assemblyPath $managedDir
    if ($LASTEXITCODE -ne 0) {
        throw "UMM 入口测试失败，退出码: $LASTEXITCODE"
    }
}

$zipPath = Join-Path $outputsDir "AdofaiEventStudio-1.0.1.zip"
Compress-Archive -Path (Join-Path $distDir "*") -DestinationPath $zipPath -Force

Write-Host "DLL: $assemblyPath"
Write-Host "ZIP: $zipPath"

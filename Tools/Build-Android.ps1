param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '../Builds/Android/BlockDodge.apk')
)

$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$output = [System.IO.Path]::GetFullPath($OutputPath)
$gradleHome = Join-Path ([System.IO.Path]::GetTempPath()) 'BDGradle'
$cacheSource = Join-Path $env:USERPROFILE '.gradle/caches'
$cacheLink = Join-Path $gradleHome 'caches'
if (!(Test-Path -LiteralPath $cacheSource)) {
    throw 'The Gradle dependency cache is missing. Open and build the Android project online once before using offline builds.'
}
New-Item -ItemType Directory -Force -Path (Join-Path $gradleHome 'init.d') | Out-Null
if (!(Test-Path -LiteralPath $cacheLink)) {
    New-Item -ItemType Junction -Path $cacheLink -Target $cacheSource | Out-Null
}
Set-Content -Encoding ascii -LiteralPath (Join-Path $gradleHome 'init.d/offline.gradle') -Value 'gradle.startParameter.offline = true'
$previousGradleHome = $env:GRADLE_USER_HOME
$previousOutput = $env:BLOCK_DODGE_ANDROID_OUTPUT
try {
    $env:GRADLE_USER_HOME = $gradleHome
    $env:BLOCK_DODGE_ANDROID_OUTPUT = $output
    & unity build $projectPath --target Android --execute-method BlockDodge.Editor.AndroidBuild.Run --allow-dirty-build --no-provenance --no-tail --timeout 900
    if ($LASTEXITCODE -ne 0) { throw "Unity build failed with exit code $LASTEXITCODE." }
    if (!(Test-Path -LiteralPath $output)) { throw "Unity did not produce the expected APK at $output." }
    Write-Host "APK created at $output"
}
finally {
    $env:GRADLE_USER_HOME = $previousGradleHome
    $env:BLOCK_DODGE_ANDROID_OUTPUT = $previousOutput
}

param([string]$InnoCompiler, [switch]$SkipInstaller)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$buildDir = Join-Path $projectRoot 'build'
$distDir = Join-Path $projectRoot 'dist'
New-Item -ItemType Directory -Force $buildDir,$distDir | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $compiler)) { throw 'The .NET Framework C# compiler is required.' }
$sources = Get-ChildItem $projectRoot -Filter '*.cs' | ForEach-Object FullName
$exePath = Join-Path $buildDir 'ViceCityHotkeys.exe'
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /warnaserror+ "/out:$exePath" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll @sources
if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed.' }
Copy-Item (Join-Path $projectRoot 'ViceCityHotkeys.exe.config') $buildDir
$report = Join-Path $buildDir 'test-results.txt'
$testProcess = Start-Process -FilePath $exePath -ArgumentList @('--self-test', ('"' + $report + '"')) -WindowStyle Hidden -PassThru -Wait
Get-Content $report
if ($testProcess.ExitCode -ne 0) { throw 'Self-tests failed.' }
$portableFiles = @($exePath, (Join-Path $buildDir 'ViceCityHotkeys.exe.config'), (Join-Path $projectRoot 'README.md'), (Join-Path $projectRoot 'LICENSE'))
Compress-Archive -LiteralPath $portableFiles -DestinationPath (Join-Path $distDir 'ViceCityHotkeys-1.0.0-Portable.zip') -Force
if (-not $SkipInstaller) {
    if (-not $InnoCompiler) {
        $candidates = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe")
        $InnoCompiler = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    }
    if (-not $InnoCompiler -or -not (Test-Path $InnoCompiler)) { throw 'Pass -InnoCompiler with the path to Inno Setup 6 ISCC.exe, or use -SkipInstaller.' }
    & $InnoCompiler (Join-Path $projectRoot 'setup.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
}
$checksums = Get-ChildItem $distDir -File | Where-Object { $_.Extension -in '.zip','.exe' } | ForEach-Object {
    $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
    '{0}  {1}' -f $hash.Hash.ToLowerInvariant(), $_.Name
}
$checksums | Set-Content (Join-Path $distDir 'SHA256SUMS.txt') -Encoding ascii
Write-Output "Build completed: $distDir"


param([Parameter(Mandatory=$true)][string]$DependencyDirectory, [string]$Destination=(Join-Path $PSScriptRoot 'artifacts/TrueTrack'))
$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$destinationPath=[IO.Path]::GetFullPath($Destination)
$src=Join-Path $PSScriptRoot 'src'
$runtime=Join-Path $destinationPath 'runtime'
New-Item -ItemType Directory -Force $runtime | Out-Null
foreach($name in @('wrapper.dll','Newtonsoft.Json.dll','rawaccel-default.json')){
    Copy-Item -LiteralPath (Join-Path $DependencyDirectory $name) -Destination $runtime -Force
}
if(Test-Path (Join-Path $DependencyDirectory 'licenses')){Copy-Item (Join-Path $DependencyDirectory 'licenses') $runtime -Recurse -Force}
$references=@('/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.IO.Compression.dll','/r:System.IO.Compression.FileSystem.dll',('/r:'+(Join-Path $runtime 'Newtonsoft.Json.dll')))
$files=@('Program.cs','Core.cs','Curve.cs','Native.cs','Driver.cs','AimForm.cs','MainForm.cs','Global.cs','Statistics.cs','Language.cs','Circles.cs','CircleForm.cs','CircleResults.cs','NaturalCircles.cs','Proposal.cs','Precision.cs','ResultStory.cs','Trajectory.cs','Sequential.cs','DriverSetup.cs','GameSetup.cs') | ForEach-Object {Join-Path $src $_}
& $compiler /nologo /platform:x64 /target:exe ('/out:'+(Join-Path $runtime 'VectorFit.Bridge.exe')) ('/r:'+(Join-Path $runtime 'wrapper.dll')) ('/r:'+(Join-Path $runtime 'Newtonsoft.Json.dll')) (Join-Path $src 'Bridge.cs')
if($LASTEXITCODE -ne 0){throw 'Bridge build failed'}
& $compiler /nologo /platform:x64 /target:winexe ('/win32icon:'+(Join-Path $src 'assets/truetrack.ico')) ('/resource:'+(Join-Path $src 'assets/truetrack.png')+',TrueTrack.Logo') /optimize+ /main:VectorFit.Program ('/win32manifest:'+(Join-Path $src 'app.manifest')) ('/out:'+(Join-Path $destinationPath 'TrueTrack.exe')) $references $files
if($LASTEXITCODE -ne 0){throw 'Application build failed'}
& $compiler /nologo /platform:x64 /target:winexe ('/win32icon:'+(Join-Path $src 'assets/truetrack.ico')) ('/resource:'+(Join-Path $src 'assets/truetrack.png')+',TrueTrack.Logo') /define:DEVELOPMENT /main:VectorFit.Program ('/out:'+(Join-Path $destinationPath 'VectorFit.Tests.exe')) $references $files (Join-Path $src 'Tests.cs')
if($LASTEXITCODE -ne 0){throw 'Development build failed'}
$config='<?xml version="1.0"?><configuration><startup><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.7.2" /></startup><runtime><assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1"><probing privatePath="runtime" /></assemblyBinding></runtime></configuration>'
[IO.File]::WriteAllText((Join-Path $destinationPath 'TrueTrack.exe.config'),$config)
[IO.File]::WriteAllText((Join-Path $destinationPath 'VectorFit.Tests.exe.config'),$config)


$bundled=Join-Path $PSScriptRoot 'vendor/rawaccel/RawAccel_v1.7.1.zip'
if(Test-Path $bundled){
    New-Item -ItemType Directory -Force (Join-Path $runtime 'installers') | Out-Null
    Copy-Item $bundled (Join-Path $runtime 'installers')
}

param([Parameter(Mandatory=$true)][string]$Zip,[Parameter(Mandatory=$true)][string]$Destination,[int]$ParentId=0,[switch]$NoLaunch)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$target=[IO.Path]::GetFullPath($Destination)
$stage=Join-Path ([IO.Path]::GetTempPath()) ('TrueTrack-stage-'+[Guid]::NewGuid().ToString('N'))
$backup=Join-Path $target ('update-backup-'+[DateTime]::Now.ToString('yyyyMMdd-HHmmss'))
$written=@()
try {
 New-Item -ItemType Directory $stage | Out-Null
 $archive=[IO.Compression.ZipFile]::OpenRead($Zip)
 try {
  foreach($entry in $archive.Entries){
   $name=$entry.FullName.Replace('\','/')
   if(!$name.StartsWith('TrueTrack/')){throw 'Unexpected package root'}
   $relative=$name.Substring(10)
   if(!$relative -or $name.EndsWith('/')){continue}
   if($relative.Split('/') -contains '..' -or $relative.Contains(':')){throw 'Unsafe archive path'}
   if(!($relative.StartsWith('runtime/') -or $relative -in @('TrueTrack.exe','TrueTrack.exe.config','Update-TrueTrack.ps1','README.md','ALGORITHM.md','LICENSE','THIRD_PARTY_NOTICES.md'))){throw 'Unexpected package file'}
   $file=Join-Path $stage $relative
   New-Item -ItemType Directory ([IO.Path]::GetDirectoryName($file)) -Force | Out-Null
   [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$file,$false)
  }
 } finally {$archive.Dispose()}
 foreach($required in @('TrueTrack.exe','TrueTrack.exe.config','runtime/Newtonsoft.Json.dll','runtime/VectorFit.Bridge.exe')){if(!(Test-Path -LiteralPath (Join-Path $stage $required))){throw 'Incomplete update package'}}
 if($ParentId -gt 0){Wait-Process -Id $ParentId -Timeout 120 -ErrorAction SilentlyContinue;if(Get-Process -Id $ParentId -ErrorAction SilentlyContinue){throw 'Close TrueTrack before updating'}}
 New-Item -ItemType Directory $backup -Force | Out-Null
 foreach($file in Get-ChildItem -LiteralPath $stage -File -Recurse){
  $relative=$file.FullName.Substring($stage.Length+1);$dest=Join-Path $target $relative;$old=Join-Path $backup $relative
  if(Test-Path -LiteralPath $dest){New-Item -ItemType Directory ([IO.Path]::GetDirectoryName($old)) -Force | Out-Null;Copy-Item -LiteralPath $dest -Destination $old}
  $written+=@{Destination=$dest;Backup=$old;Existed=(Test-Path -LiteralPath $dest)}
  New-Item -ItemType Directory ([IO.Path]::GetDirectoryName($dest)) -Force | Out-Null
  Copy-Item -LiteralPath $file.FullName -Destination $dest -Force
 }
 if(!$NoLaunch){Start-Process -FilePath (Join-Path $target 'TrueTrack.exe') -WorkingDirectory $target -WindowStyle Hidden}
} catch {
 foreach($item in $written){if($item.Existed){Copy-Item -LiteralPath $item.Backup -Destination $item.Destination -Force -ErrorAction Continue}else{Remove-Item -LiteralPath $item.Destination -ErrorAction Continue}}
 $message='TrueTrack update failed. Your measurements were not changed. '+$_.Exception.Message
 [IO.File]::WriteAllText((Join-Path $target 'update-error.txt'),$message)
 if(!$NoLaunch){Add-Type -AssemblyName System.Windows.Forms;[Windows.Forms.MessageBox]::Show($message) | Out-Null}
 throw
}

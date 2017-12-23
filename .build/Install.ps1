param($toolsPath, $project)

$scriptPath = Split-Path -parent $MyInvocation.MyCommand.Definition
$assemblyPath = Join-Path $scriptPath "Shuttle.Core.MSBuild.dll"

[System.Reflection.Assembly]::LoadFrom($assemblyPath)

$Installation = New-Object -TypeName Shuttle.Core.MSBuild.Installation -ArgumentList $toolsPath, $project
$Installation.Execute()

param([Parameter(Mandatory=$true)] [string] $TargetName)

$Dependencies = Get-ChildItem -Filter '*.dll' -Exclude $TargetName -Recurse | Resolve-Path -Relative

& $Env:ILREPACK /lib:$ENV:VINTAGE_STORY\Lib /lib:$ENV:VINTAGE_STORY /out:..\$TargetName $TargetName $Dependencies

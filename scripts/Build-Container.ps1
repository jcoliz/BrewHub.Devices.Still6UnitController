# Ensure needed env vars are set
$vars = @{
    ACRSERVER = "Logon server for container registry"
}

foreach( $var in $vars.GetEnumerator() )
{
    if (-not (Test-Path env:$($var.key))) 
    { 
        Write-Output "Please set env:$($var.Key) to $($var.Value)"
        Exit 
    }
}

$ModuleName = "brewhub-controller"
$ModuleVer = ./scripts/Get-Version.ps1
$ModuleTags = "${env:ACRSERVER}/${ModuleName}:$ModuleVer-amd64"
$ModuleTagsLocal = "${ModuleName}:local"

Invoke-Expression "docker build --rm --build-arg SOLUTION_VERSION=$ModuleVer -f .\docker\Dockerfile.amd64 -t $ModuleTags -t $ModuleTagsLocal ." -ErrorAction Stop


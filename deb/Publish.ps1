#
# Build a DEB package locally
#
# NOTE: Run from project root directory
#

$Application = "still6unitcontroller"

if (Test-Path publish_output)
{
    Remove-Item -Recurse publish_output    
}

dotnet publish --configuration Release --runtime linux-arm64 --no-self-contained --output publish_output/opt/$Application
$Version = Get-Content .\version.txt
wsl -e deb/dpkg-deb.sh bin $Application $Version arm64

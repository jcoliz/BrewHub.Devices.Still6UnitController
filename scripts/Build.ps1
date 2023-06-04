# Set project-local vars
$ModuleTags = Get-Content -Path obj\tags.txt

docker build --rm --build-arg tags=$ModuleTags -f .\docker\Dockerfile.amd64 -t $ModuleTags .

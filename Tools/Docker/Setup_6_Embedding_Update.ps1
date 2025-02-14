# Dot-source the common functions file.
. "$PSScriptRoot\Setup_0.ps1"

# This update script allows you to modify the API code and rebuild the container.
# 1. Edit the API code in your build context (for example, modify
#    C:\Projects\Jocys.com GitHub\VsAiCompanion\Tools\Docker\embedding_api\embedding_api.py)
# 2. Rebuild the container image using Podman. Ensure you are in the directory
#    that contains the Dockerfile.
podman build -t embedding-api ./embedding_api

# 3. Stop and remove the currently running container (if it exists).
podman stop embedding-api
podman rm embedding-api

# 4. Run a new container with the updated image.
podman run -d --name embedding-api -p 8000:8000 embedding-api

# 5. (Optional) Test the new API using curl or Invoke-RestMethod.
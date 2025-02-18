################################################################################
# File         : Setup_6_Embedding_Update.ps1
# Description  : Script to update the Embedding API container with new API code.
#                Rebuilds the container image using Podman, stops and removes the current
#                container, and then runs a new container with the updated image.
# Usage        : Modify the API code in the build context directory and run this script.
################################################################################

# Dot-source the common functions file.
. "$PSScriptRoot\Setup_0.ps1"

# This update script allows you to modify the API code and rebuild the container.
# 1. Edit the API code in your build context (for example, modify
#    C:\Projects\Jocys.com GitHub\VsAiCompanion\Tools\Docker\embedding_api\embedding_api.py)
# 2. Rebuild the container image using Podman. Ensure you are in the directory
#    that contains the Dockerfile.
# Command: build
#   --tag: assigns a name (and optionally a tag) to the built image.
podman build --tag embedding-api ./embedding_api

# 3. Stop and remove the currently running container (if it exists).
# Command: stop
#   stops a running container.
podman stop embedding-api
# Command: rm
#   removes the specified container.
podman rm embedding-api

# 4. Run a new container with the updated image.
# Command: run
#   --detach: run container in background.
#   --name: assign the container name "embedding-api".
#   --publish: map host port 8000 to container port 8000.
podman run --detach --name embedding-api --publish 8000:8000 embedding-api

# 5. (Optional) Test the new API using curl or Invoke-RestMethod.
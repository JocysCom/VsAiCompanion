## Overview

The solution leverages containerization to simplify the setup and management of several AI-related services such as pipelines, web interfaces, workflow automation, crawling, vector databases, and embedding APIs. The scripts coordinate tasks such as:

- Installing and configuring Docker Desktop/Engine or Podman.
- Backing up and restoring container images.
- Setting up specific containers (e.g., Pipelines, Open WebUI, n8n, Firecrawl with dedicated Redis, Qdrant, and Embedding API).
- Testing container connectivity and functionality.
- Updating container images with new code.

---

## Folder Structure & Script Descriptions

- **Setup_0.ps1**  
  Contains common helper functions used by all other setup scripts. Functions include verifying elevated privileges, setting the script location, downloading files, checking Git installation, resolving Docker/Podman executable paths, testing network ports, and performing backup restoration of container images.

- **Setup_1a_Docker.ps1**  
  Installs and configures Docker (including WSL on Windows) using either static binary downloads or Docker Desktop via winget. It verifies the installation and tests the Docker Engine using a "hello-world" container.

- **Setup_1a_Podman.ps1**  
  Provides a menu-driven interface to manage Podman installation on Windows. Options include:
  - Installing the Podman CLI (remote installer).
  - Installing Podman Desktop (UI).
  - Registering Podman as a Windows service (ensuring the Podman machine is running).
  - Removing the Podman service.

- **Setup_1b_BackupRestore.ps1**  
  Offers a menu for backing up or restoring container images using Docker or Podman. Backups are stored as tar archives in a designated "Backup" folder.

- **Setup_2a_Pipelines.ps1**  
  Sets up the Pipelines container from a pre-built image (“ghcr.io/open-webui/pipelines:main”). It includes functionality to install, back up, restore, uninstall the container, and even add additional pipeline files.

- **Setup_2b_OpenWebUI.ps1**  
  Manages the Open WebUI container. It handles image pulling (or backup restoration), container removal, update, backup, restore, and uninstallation operations via a convenient menu interface.

- **Setup_3_n8n.ps1**  
  Sets up and runs the n8n container. It verifies existence or creates a persistent volume (`n8n_data`), supports optional external domain configuration, and maps ports for workflow automation.

- **Setup_4_Firecrawl.ps1**  
  Sets up the Firecrawl container alongside a dedicated Redis container. The script creates a dedicated Docker network, launches Redis with a network alias, pulls the Firecrawl image, and then runs Firecrawl with appropriate environment variable overrides so that it can connect to Redis.

- **Setup_5_Qdrant.ps1**  
  Deploys the Qdrant vector database container. It ensures backup restoration if available, removes an existing container if needed, and starts Qdrant with proper port mapping.

- **Setup_6_Embedding.ps1**  
  Builds and runs the Embedding API container using Podman. The build context is set up with a Dockerfile, a requirements file, and API code based on FastAPI and Sentence Transformers. The container listens on port 8000.

- **Setup_6_Embedding_Test.ps1**  
  Tests the Embedding API by sending sample text lines, decoding the base64-encoded embeddings, and computing cosine similarity between repeated and distinct text inputs. The script outputs whether the test passed or failed based on expected similarity thresholds.

- **Setup_6_Embedding_Update.ps1**  
  Provides instructions and commands to update the Embedding API container. It rebuilds the container image with new code, stops and removes the running container, and then starts a new instance with the updated image.

---

## Prerequisites

- **Operating System:**  
  Windows 10/11 or any system supporting PowerShell (administrator privileges required for some scripts).

- **PowerShell Version:**  
  PowerShell 5.1 or higher.

- **Container Engine:**  
  Either Docker or Podman. If not installed, the scripts provide automated installation options:
  - Docker Desktop/Engine (via winget or static binary installation).
  - Podman CLI and optionally Podman Desktop (UI) for Windows.

- **Additional Requirements:**  
  - Windows Subsystem for Linux (WSL) for Docker/Podman machine initialization (when required).
  - Internet connectivity for downloading images and setup packages.

---

## How to Use

1. **Clone or Download the Repository:**  
   Place the repository (or the renamed folder "ai-containers") on your local machine.

2. **Open a PowerShell Session:**  
   Launch PowerShell as Administrator (if required by the specific script).

3. **Set the Working Directory:**  
   Change to the folder containing the scripts.

4. **Run the Scripts:**  
   Each script is self-contained and offers menu-driven options where applicable. For example:
   - Start with **Setup_1a_Docker.ps1** or **Setup_1a_Podman.ps1** to install your container engine.
   - Use **Setup_1b_BackupRestore.ps1** to manage container image backups.
   - Follow the menu prompts in **Setup_2a_Pipelines.ps1**, **Setup_2b_OpenWebUI.ps1**, etc., to deploy specific containers.
   - To set up the Embedding API and test it, run **Setup_6_Embedding.ps1** followed by **Setup_6_Embedding_Test.ps1**.
   - Use **Setup_6_Embedding_Update.ps1** to update the Embedding API with new code revisions.

5. **Review Output & Logs:**  
   The scripts display status messages and error details. Follow any prompts to ensure services are installed correctly.

---

## Troubleshooting Tips

- Ensure you run the scripts with proper administrator privileges when required.
- Verify that either Docker or Podman is correctly installed and available in your PATH.
- Check that the Windows Subsystem for Linux is enabled if you encounter issues with WSL-dependent operations.
- Review the terminal output for specific error messages, which can guide you in resolving permission or connectivity issues.

---

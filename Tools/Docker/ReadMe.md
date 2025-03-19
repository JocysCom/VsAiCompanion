## Overview

This solution uses containerization to simplify the installation, integration, and management of several AI and automation tools on Windows. The provided PowerShell scripts work with Docker or Podman to deploy containerized applications that are easy to install, update, and maintain. These tools eliminate complex manual configurations and dependency issues by isolating each service in its own container, allowing you to focus on solving business problems instead of technical setup.

## Containerized AI Tools Architecture

  <img alt="Podman Desktop" src="Images/Diagram.svg" width="640" height="360">

## Tools Provided

- **Docker / Podman Setup**  
  These scripts automatically install and configure the underlying container engine on your system. Docker and Podman allow you to run applications in isolated environments, ensuring that software dependencies and configurations don't conflict with your host system. They make deploying, updating, and troubleshooting services quick and consistent. This setup forms the backbone of the containerized solution, ensuring a smooth installation experience.
  <img alt="Podman Desktop" src="Images/Podman.png" width="640" height="360">

- **Portainer**  
  Portainer is a lightweight management UI that allows you to easily manage your Docker and Podman environments. It provides a simple, intuitive interface for creating, managing, and monitoring containers, volumes, networks, and images. Portainer simplifies container management by offering a visual dashboard where you can view container status, logs, and resource usage at a glance, making it perfect for both beginners and experienced users.
  <img alt="Portainer UI" src="Images/Portainer.png" width="640" height="360">

- **Open WebUI**  
  Open WebUI delivers a friendly graphical interface for managing your AI pipelines and container operations. It hides the underlying command-line complexity and provides real-time monitoring, status updates, and control options at the click of a button. This tool enables you to easily track system health, view logs, and manage services, making it ideal for users who prefer a visual approach.  
  <img alt="Open WebUI" src="Images/OpenWebUI.gif" width="640" height="360">

- **Pipelines for WebUI**  
  The Pipelines Container is designed to streamline and orchestrate your AI workflows. It encapsulates all necessary components to execute complex data processing, inference, and transformation tasks without requiring manual setup of multiple services. By providing an easy-to-backup and restore container, it ensures continuity and reliability in running your AI pipelines. This tool saves time and reduces errors by automating the entire pipeline process.

- **n8n Workflow Automation**  
  n8n is a powerful workflow automation platform that lets you connect multiple applications and automate tasks without writing code. It offers a visual interface to design, execute, and monitor complex workflows that integrate data between various services, solving common integration challenges. This tool is perfect for automating repetitive tasks, reducing manual errors, and saving time on business processes.  
  <img alt="n8n UI" src="Images/n8n.png" width="640" height="360">

- **Firecrawl Crawler**  
  Firecrawl is a dedicated web crawling tool that automates the extraction and organization of data from websites. It addresses the challenge of manually gathering web data by efficiently scraping and processing large volumes of information. Integrated with a dedicated Redis caching mechanism, Firecrawl enhances performance and reliability while minimizing system load. This makes it ideal for tasks like market research, content analysis, and data collection.

- **Qdrant Vector Database**  
  Qdrant is a specialized vector database for managing high-dimensional data, which is essential for semantic search and machine learning applications. It stores and retrieves numeric representations of data (embeddings) quickly, enabling efficient similarity searches and recommendation engines. This tool helps solve challenges related to processing complex data comparisons with ease and reliability. Its containerized setup makes it accessible even for those new to machine learning infrastructure.  
  
- **Embedding API**  
  The Embedding API converts raw text into semantically meaningful numeric vectors (embeddings) through advanced natural language processing models. It simplifies complex tasks like document similarity, clustering, and recommendation by providing a reliable, scalable API endpoint. This tool lets you harness the power of deep learning models without needing to manage heavy compute resources locally. As a result, developers can efficiently integrate natural language understanding into their applications.

- **NocoDB**  
  NocoDB transforms any spreadsheet into a smart relational database accessible via a modern web interface. It makes data management intuitive and collaborative, enabling users without technical expertise to create applications, manage records, and generate reports effortlessly. With features similar to Airtable, NocoDB helps teams manage their data visually and efficiently, reducing the need for complex coding or database management skills.  
  <img alt="NocoDB UI" src="Images/NocoDB.png" width="640" height="360">

## Folder Structure & Script Descriptions

- **Setup_0.ps1**  
  Contains the common helper functions used by all the other setup scripts.

- **Setup_1_VM.ps1**  
  Enables virtualization features on Windows (either WSL or Hyper-V) to support containerized applications.

- **Setup_1a_Docker.ps1**  
  Installs and configures Docker on Windows, ensuring that the container engine is ready to run your services.

- **Setup_1a_Podman.ps1**  
  Manages the installation of Podman, including the command-line tool and optionally its Desktop UI, for running containers.

- **Setup_1b_BackupRestore.ps1**  
  Offers a menu-driven interface for backing up and restoring container images, ensuring your environments can be easily preserved or restored.

- **Setup_1c_Portainer.ps1**  
  Installs and configures Portainer, a lightweight management UI for Docker and Podman environments, making container management visual and intuitive.

- **Setup_2a_Pipelines.ps1**  
  Deploys the Pipelines Container to orchestrate your AI workflows reliably with minimal manual intervention.

- **Setup_2b_OpenWebUI.ps1**  
  Installs the Open WebUI container, providing an intuitive interface to monitor and manage your containerized services.

- **Setup_3_n8n.ps1**  
  Sets up and runs the n8n container to automate and manage workflows through its user-friendly visual interface.

- **Setup_4_Firecrawl.ps1**  
  Launches the Firecrawl container along with a dedicated Redis container, simplifying web data extraction and caching.

- **Setup_5_Qdrant.ps1**  
  Deploys the Qdrant vector database container to handle semantic data searches and high-dimensional data management.

- **Setup_6_Embedding.ps1**  
  Builds and runs the Embedding API container, allowing you to convert text into semantically rich vectors using advanced NLP models.

- **Setup_6_Embedding_Test.ps1**  
  Tests the functionality of the Embedding API, ensuring that text is correctly converted into embeddings.

- **Setup_7_NocoDB.ps1**  
  Manages the NocoDB container to provide a no-code solution for database management through an easy-to-use web interface.
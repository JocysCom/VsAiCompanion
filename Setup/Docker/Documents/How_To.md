# Windows, WSL2 and n8n

```text
Windows 11                        ← real, bare-metal host
│
├── NVIDIA GPU Driver (installed on Windows)
│   └── nvidia-smi.exe (C:\Windows\System32\)
│
└── WSL2 subsystem (type-2 hypervisor built into Windows)  ← virtualisation layer
    │
    └── WSL2 distro "podman-machine-default" (Fedora CoreOS image)  ← **Podman Host VM**
        │
        ├── NVIDIA Container Toolkit ← INSTALLED HERE (Option 5)
        │   ├── nvidia-ctk
        │   └── CDI specifications: /etc/cdi/nvidia.yaml, /var/run/cdi/nvidia.yaml
        │
        ├── login shell: bash         ← what you reach with `podman machine ssh`
        │
        └── podman (rootless) daemon  ← container engine running as that user
            │
            ├── container "n8n"       ← Linux container, NOT a VM
            │   ├── /bin/sh (root)    ← started by `sudo podman exec -it --user root n8n /bin/sh`
            │   └── node /usr/bin/n8n ← the real application
            │
            └── container "qwen3-embedding-4b" ← Linux container, NOT a VM
                ├── /bin/sh (root)    ← started by `sudo podman exec -it --user root qwen3-embedding-4b /bin/sh`
                ├── ollama serve      ← the real application
                └── GPU access via: --device nvidia.com/gpu=all
```

```powershell
# Connect to Podman host VM (user: core)
podman machine ssh
```

You are now inside the Fedora CoreOS VM as user 'core'

```bash
# Connect to n8n container as default user
podman exec -it n8n /bin/sh
# Connect to container as root.
sudo podman exec -it --user root n8n /bin/sh
```

Typical things you might do **inside** the container

```sh
cd /                       # Go to top folder
ls -la                     # List folders
ps -o pid,comm,args        # show processes
kill -TERM 2               # stop PID 2 (example)
node /usr/local/bin/n8n &  # start n8n in background
disown                     # detach shell from that background job
exit                       # leave the container shell
```


# Fix Warning: "Using cgroups-v1 which is deprecated in favor of cgroups-v2 with Podman v5 and will be removed in a future version."

1. From Windows PowerShell stop the podman machine (Fedora CoreOS VM):
	
	podman machine stop
	
2. Open/create "%UserProfile%\.wslconfig" and add:

	[wsl2]
	kernelCommandLine = cgroup_no_v1=all

3. From Windows PowerShell completely shutdown WSL so the new kernel argument is picked up.
	
	wsl --shutdown
	
	Double-check that nothing is still running:
	
		wsl --list --running
	
4. From Windows PowerShell  start podman machine:

	podman machine start

5. Check CGroup Version. Should print "v2":

	podman info --format json | Select-String -Pattern "cgroup" -CaseSensitive:$false  

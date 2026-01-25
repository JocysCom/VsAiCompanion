---
name: self-improvement
description: Update, improve, and synchronise this repository’s AI agent instructions and related assets (including skills). Use when the user asks to modify the agent’s own instructions/processes, restructure instruction governance, migrate instruction content into skills, or run/adjust the sync pipeline that publishes `.ai/` sources into agent-specific folders.
---

# Self-Improvement (Instructions + Skills)

## Workflow

1. Identify which sources need changing under [`.ai/`](.ai:1) (master copies only).
2. Make changes in [`.ai/instructions.md`](.ai/instructions.md:1) and related `*.instructions.md` / `*.instructions-detail.md` files.
3. Do **not** edit generated outputs directly (they are produced by the sync script):
   - [`.roo/rules/`](.roo/rules:1)
   - [`.github/copilot-instructions.md`](.github/copilot-instructions.md:1)
   - [`AGENTS.md`](AGENTS.md:1)
4. When a change is ready, run the sync script from repository root:

```powershell
.\.ai\skills\self-improvement\tools\Update-AgentInstructions.ps1 AUTO
```

## Progressive disclosure

- For detailed policy, editable file list, and activation guidance, read [`references/details.md`](.ai/skills/self-improvement/references/details.md:1).

## Bundled tools

- Sync entrypoint (instructions + skills): [`tools/Update-AgentInstructions.ps1`](.ai/skills/self-improvement/tools/Update-AgentInstructions.ps1:1)

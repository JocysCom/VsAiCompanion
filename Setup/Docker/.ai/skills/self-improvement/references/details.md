# Self-Improvement — Detailed Instructions

This file contains detailed instructions for self-improvement. Load it only when the user requests self-improvement to avoid watering down main instructions.

## Editable instruction files (sources of truth)

You can update your own instruction files under [`.ai/`](.ai:1):

- [`.ai/instructions.md`](.ai/instructions.md:1) — the main system instructions file
- [`.ai/*instructions.md`](.ai:1) — additional instruction files (auto-included)
- [`.ai/*instructions-detail.md`](.ai:1) — detailed instruction files (read only when needed)

## Do not edit generated instruction files

The following locations are generated/managed outputs and must not be modified directly:

- [`.roo/rules/`](.roo/rules:1) (Roo Code)
- [`.github/copilot-instructions.md`](.github/copilot-instructions.md:1) (GitHub Copilot)
- [`AGENTS.md`](AGENTS.md:1) (Codex)

If you need to change instructions, edit the master copies in [`.ai/`](.ai:1) and run the activation step below.

## Single source of truth

**Never embed template content in instructions — reference template files instead.**

Example:

- ✅ "Template maintained in `pr/checklist.template.md`"
- ❌ Pasting template content into instructions

## Activation process

After editing instruction files (or master skills), run from repository root:

```powershell
.\.ai\skills\self-improvement\tools\Update-AgentInstructions.ps1 AUTO
```

This command updates and activates the modified instruction files into generated outputs (for example, [`.roo/rules/`](.roo/rules:1)).

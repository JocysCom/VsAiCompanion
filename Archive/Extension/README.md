# Archived Visual Studio Extension Code

These files are **not part of any build**. They are kept for reference in case the
Visual Studio integration is rebuilt later as an out-of-process extension.

## What was removed

The solution used to produce two artifacts from the same `Engine`:

- **Portable application** (`App`) — retained, now the only product.
- **Visual Studio extension** (`Extension`, `Extension1`) — removed.

`Extension` was the classic in-process VSIX (`net48`, VSSDK, `AsyncPackage`), and
`Extension1` was an unfinished port to the newer `VisualStudio.Extensibility` SDK.
Both were deleted along with the solution's `net48` target framework.

The full projects remain in git history at commit `0f8de3d7` (branch
`20250818_2000_add_mcp_support`) — restore with:

```
git show 0f8de3d7:Extension/JocysCom.VS.AiCompanionPackage.cs
git checkout 0f8de3d7 -- Extension Extension1
```

## Why these two files were kept

`SolutionHelper.cs` / `SolutionHelper.Output.cs` are the only substantial pieces of
domain logic in the extension — everything else was VSIX plumbing (package
registration, `.vsct` command tables, splash screen) that would be rewritten from a
template anyway.

They implement `ISolutionHelper` (see
[Plugins/Core/VsFunctions/ISolutionHelper.cs](../../Plugins/Core/VsFunctions/ISolutionHelper.cs)),
the interface the Engine calls to read solution/project/document state, the current
selection, error list entries, and exception details, and to write code back into the
editor. That mapping from AI-facing operations onto the VS object model is the part
worth not re-deriving.

The `Extension` and `Extension1` copies were byte-identical; this is that single copy.

## What would need to change for an out-of-process extension

This code cannot be used as-is. It is built for in-process execution:

- Uses `EnvDTE` / `EnvDTE80` (`DTE2`, `Solution2`, `Project`) via
  `ServiceProvider.GlobalProvider`, which only resolves inside `devenv.exe`.
- Uses `ThreadHelper.ThrowIfNotOnUIThread()` and
  `ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync()` — the VS main-thread
  model, which has no meaning in a separate process.
- Uses `Microsoft.VisualStudio.Shell.Interop` COM interfaces directly.

An out-of-proc rewrite would target `net8.0`+ and go through the
`VisualStudio.Extensibility` async APIs (`VisualStudioExtensibility.Workspaces()`,
`Documents()`, `Editor()`), with UI defined as Remote UI rather than direct WPF.
Treat these files as a specification of *what* the Engine needs from the IDE, not as
code to port line by line.

## Context

Visual Studio 2026 still runs on .NET Framework 4.8, so an in-process VSIX would still
require a `net48` assembly. Dropping `net48` from the solution is what makes the
in-process model unavailable — a future extension has to be out-of-process.

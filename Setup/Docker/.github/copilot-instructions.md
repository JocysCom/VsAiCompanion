==== START OF INSTRUCTIONS FROM: instructions.md ====

# Instructions from: instructions.md

# Cline Rules for this Directory

## Meta-Rule: Keep Rules Concise

**Description:**
When adding or modifying rules in this `.clinerules` file, prioritize clarity and conciseness. Ensure rules are actionable and directly address project standards or potential pitfalls without unnecessary verbosity.

**Rationale:**
A concise ruleset is easier to read, understand, and follow, maximizing its effectiveness.

---

**Important:** Before modifying files in this directory, please consult the following files for essential context:

- **`ReadMe.md`**: Provides an overview of the project, the tools involved, and the purpose of each `Setup_*.ps1` script.
- **`Requirements.md`**: Details the coding standards, common features (identified by two-letter codes), and specific implementation requirements for the PowerShell scripts. Understanding these requirements is crucial for maintaining consistency.

## Rule: Validate PowerShell Scripts After Modification

**Description:**
To maintain code quality and catch potential issues early, all PowerShell scripts (`*.ps1`) in this project should be validated using the `PSScriptAnalyzer` module after any modifications are made.

**Procedure:**

1. Ensure the `PSScriptAnalyzer` module is installed:
   `Install-Module -Name PSScriptAnalyzer -Scope CurrentUser -Force -Confirm:$false`
2. After modifying a `.ps1` file, run the analyzer on it:
   `Invoke-ScriptAnalyzer -Path .\path\to\your\script.ps1`
3. Review the output for any warnings or errors.
4. Address the identified issues before committing the changes. See the "Suppressing Acceptable Warnings" rule below for specific cases.

**Rationale:**
Using `PSScriptAnalyzer` helps enforce best practices, improve script reliability, and reduce potential runtime errors.

## Rule: Suppressing Acceptable PSScriptAnalyzer Warnings

**Description:**
While `PSScriptAnalyzer` is valuable, certain warnings might be acceptable or expected given the project structure, particularly in scripts utilizing the `Invoke-MenuLoop` pattern with shared state or generic function signatures.

**Acceptable Suppressions:**

- **`PSAvoidGlobalVars`**: This warning often appears in the main menu scripts (`Setup_*.ps1` excluding `Setup_0_*.ps1`) because global variables (`$global:enginePath`, `$global:containerName`, etc.) are intentionally used to share state between different menu actions invoked via `Invoke-MenuLoop`. Suppressing this rule during validation of these specific scripts is acceptable.
- **`PSReviewUnusedParameter`**: This warning can occur in script blocks passed to generic functions like `Update-Container`. The generic function might define a standard parameter signature for the script block (e.g., including `$ContainerEngineType`, `$ContainerName`), but a specific implementation of that script block might not use all provided parameters (e.g., if it relies on global variables instead). If the parameter is part of the required signature for the generic function, suppressing this warning for that specific parameter within the script block is acceptable. _Initially, attempts were made using `[SuppressMessageAttribute]`, but this did not work reliably within script blocks. Exclusion via command-line is preferred._
- **`PSShouldProcess`**: This warning can occur in wrapper functions (like `Update-NocoDBContainer`) that are decorated with `[CmdletBinding(SupportsShouldProcess=$true)]` but delegate the actual work (and the `ShouldProcess` call) to another function (like `Update-Container`). If the wrapper function itself doesn't perform actions requiring confirmation but correctly passes `-WhatIf`/`-Confirm` down via splatting or parameter binding to the inner function that _does_ call `ShouldProcess`, suppressing this warning _on the wrapper function_ is acceptable. Ensure the inner function correctly implements `ShouldProcess`. _Correction: Added a top-level `ShouldProcess` check to wrapper functions like `Update-n8nContainer` to resolve this instead of suppressing._

**Procedure for Suppression (During Validation):**
When validating a script where these specific warnings are expected and acceptable, use the `-ExcludeRule` parameter with `Invoke-ScriptAnalyzer`.

**Example:**
To validate `Setup_3_n8n.ps1` while ignoring expected global variable usage and unused parameters in the update script block:
`Invoke-ScriptAnalyzer -Path .\Setup_3_n8n.ps1 -ExcludeRule PSAvoidGlobalVars, PSReviewUnusedParameter, PSAvoidUsingWriteHost`

**Rationale:**
Suppressing these specific, understood warnings allows focusing on other potentially critical issues identified by the analyzer without being cluttered by expected noise inherent in the chosen design pattern (menu loop with shared state, generic function callbacks). Always ensure the suppression is justified and documented if necessary.

## Rule: Avoid Adding Temporary/Explanatory Comments to Code

**Description:**
When modifying code (e.g., replacing functions, fixing errors, removing whitespace), do not add comments into the code itself that explain the modification action (e.g., `# Replaced Write-Host`, `# Removed trailing whitespace`, `# Fixed syntax error`, `# Renamed function`, `# Added parameter`). Such comments are temporary artifacts of the development/debugging process and should not be part of the final committed code. **Specifically, AI agents using tools like `replace_in_file` or `write_to_file` MUST NOT include comments like `# Renumbered`, `# New item`, `# Fixed error`, `# Corrected syntax`, `# Using renamed function`, etc., in the generated code. These explanations belong _exclusively_ in the agent's thought process or the commit message, NEVER in the final code output.** Before completing a task, AI agents MUST perform a final review of all modified files to ensure any inadvertently added temporary comments have been removed.

**Rationale:**
Temporary or explanatory comments added during modification clutter the code, provide no long-term value, and can become outdated or misleading. Code changes should be understandable through the code itself and commit messages, not through temporary inline annotations.

## Rule: Use Correct PowerShell Output Cmdlets

**Description:**
Select the appropriate cmdlet for script output to prevent errors and ensure proper stream handling.

**Guidelines:**

- **`Write-Output`**: **Strictly** for function return values intended for assignment or pipeline use.
  - **AVOID** using it for status messages/prompts within functions that return values. Doing so pollutes the output stream and can cause type errors (e.g., `$var = MyFunc` results in `$var` being `Object[]` instead of `string` because `Write-Output` messages were captured).
- **`Write-Host`**: Use when direct console display is essential and redirection is undesirable (e.g., `Read-Host` prompts).
- **`Write-Warning`/`Error`/`Verbose`/`Debug`**: Use for their specific semantic purposes.

**Rationale:**
Prevents type mismatches when capturing function output. Improves script clarity and control over output streams.

Simplicity is very important because code should be easy for both humans and AI to understand. For example, avoid passing functions as arguments.

## Rule: Standard PowerShell Function Header Format

**Description:**
All PowerShell functions defined in `.ps1` files within this directory MUST adhere to the following header format to ensure consistency, readability, and proper IntelliSense support.

**Format:**

```powershell
#==============================================================================
# Function: FunctionName
#==============================================================================
<#
.SYNOPSIS
    A brief one-line summary of the function's purpose.
.DESCRIPTION
    A more detailed description explaining what the function does, how it works,
    and any important context or considerations.
.PARAMETER ParameterName1
    Description of the first parameter, including its purpose, expected data type,
    and whether it's mandatory or optional.
.PARAMETER ParameterName2
    Description of the second parameter.
.EXAMPLE
    PS C:\> FunctionName -ParameterName1 "Value"
    Explanation of what this example command achieves.
.EXAMPLE
    PS C:\> FunctionName -ParameterName1 "Value" -ParameterName2 $true
    Explanation of another usage scenario.
.OUTPUTS
    [DataType] Description of the object(s) returned by the function, if any.
    Use [void] if the function does not return a value.
.NOTES
    Additional notes such as author, dependencies, required privileges, known issues,
    or links to relevant documentation.
#>
function FunctionName {
    [CmdletBinding(SupportsShouldProcess=$true)] # Or [CmdletBinding()] if ShouldProcess not needed
    param(
        [Parameter(Mandatory=$true, HelpMessage="Help text for parameter 1.")]
        [string]$ParameterName1,

        [Parameter(Mandatory=$false)]
        [bool]$ParameterName2
    )

    # Function body starts here
    # Ensure ShouldProcess/ShouldContinue are called if SupportsShouldProcess=$true
    if ($PSCmdlet.ShouldProcess("TargetResource", "Action")) {
        # Perform action
    }
}

```

**Key Requirements:**

1. **Separator Block:** Each function MUST be preceded by the exact separator block:

    ```powershell
    #==============================================================================
    # Function: FunctionName
    #==============================================================================
    ```

    Replace `FunctionName` with the actual name of the function.
2. **Comment-Based Help:** A comment-based help block (`<# ... #>`) MUST be placed immediately before the `function` keyword (after the separator block).
3. **Help Block Content:** The help block SHOULD contain at least `.SYNOPSIS` and `.DESCRIPTION`. Include `.PARAMETER`, `.EXAMPLE`, `.OUTPUTS`, and `.NOTES` where applicable and helpful.
4. **`[CmdletBinding()]`:** Use the `[CmdletBinding()]` attribute for advanced functions, enabling common parameters and parameter validation. Use `[CmdletBinding(SupportsShouldProcess=$true)]` if the function performs actions that should support `-WhatIf` and `-Confirm`, and ensure `$PSCmdlet.ShouldProcess()` or `$PSCmdlet.ShouldContinue()` is called within the function body before performing the action.

**Rationale:**
This standard format significantly improves code readability by visually separating functions. The comment-based help block is essential for PowerShell's built-in help system (`Get-Help`) and provides rich IntelliSense information (parameter descriptions, types, summaries) in editors like VS Code, aiding development and maintenance. Consistent use of `[CmdletBinding()]` promotes robust function design.

## Rule: Follow "One Script Per Container" Architecture

**Description:**
Each container or service should have its own dedicated PowerShell script. When encountering scripts that manage multiple containers, they must be split into separate scripts following clear naming conventions.

**Guidelines:**

- Use descriptive naming: `Setup_4a_ServiceName_Redis.ps1`, `Setup_4b_ServiceName.ps1`
- Each script manages only one container's lifecycle (install, uninstall, update, backup, etc.)
- Dependencies between containers should be validated through dependency checking functions
- Shared resources (networks, volumes) should be managed by the first container that needs them

**Procedure:**

1. Identify all containers managed by a script
2. Create separate scripts for each container
3. Implement dependency checking in dependent scripts
4. Update menu systems and documentation accordingly

**Rationale:**
Separation of concerns improves maintainability, makes troubleshooting easier, follows microservices principles, and allows independent management of each container. This architecture also prevents conflicts and makes the codebase more modular.

## Rule: Eliminate All Hardcoded Values Using Global Variables

**Description:**
All hardcoded strings, numbers, and configuration values in PowerShell scripts must be replaced with descriptive global variables. This includes ports, paths, URLs, container names, network aliases, and any other configurable values.

**Guidelines:**

- Define all global variables at the top of the script in a dedicated section
- Use descriptive names: `$global:containerPort`, `$global:dataPath`, `$global:networkAlias`
- Replace ALL hardcoded values systematically throughout the script
- Ensure consistency between related scripts (e.g., Redis and application scripts should use matching network aliases)

**Common Hardcoded Values to Replace:**

- Port mappings: `"8080:8080"` → `"$($global:containerPort):$($global:containerPort)"`
- Volume mounts: `":/app/data"` → `":$global:dataPath"`
- Network aliases: `"myservice"` → `$global:networkAlias`
- URLs: `"http://service:8080"` → `"http://$($global:networkAlias):$($global:port)"`
- Container names, image names, volume names

**Procedure:**

1. Audit the entire script for hardcoded values
2. Create appropriate global variables with descriptive names
3. Replace all hardcoded occurrences systematically
4. Verify consistency across related scripts
5. Test the script to ensure all variables are properly referenced

**Rationale:**
Using global variables instead of hardcoded values improves maintainability, makes configuration changes easier, reduces errors, and ensures consistency across the codebase. It also makes scripts more flexible and reusable.

## Environment

- Terminal sessions use PowerShell by default; therefore, invoke scripts directly (e.g., `.\Script.ps1 -WhatIf`) instead of wrapping them in an extra `powershell -ExecutionPolicy Bypass -File` call.

==== END OF INSTRUCTIONS FROM: instructions.md ====

==== START OF INSTRUCTIONS FROM: tools-web-search.instructions.md ====

# Instructions from: tools-web-search.instructions.md

## System-message addendum: Web Search

You have access to the **`firecrawl-mcp-server`** tool, which performs live web searches.
Follow these rules to decide **when and how** to use it.

### 1 When to invoke `firecrawl-mcp-server`

Call the tool **before answering** if any of these is true:

- The user requests **time-sensitive** or **“latest/current”** information.  
  Example: “What’s the latest stable TypeScript version?”
- The topic involves **library/framework versions, releases, deprecations, security advisories, cloud-platform features, pricing, or usage statistics.**
- You are **not at least 95 % certain** your internal knowledge is up to date.
- The user explicitly says “search the web” or similar.

If the user explicitly instructs **not to search**, do not call the tool.

### 2 How to use the tool

1. Build a concise, specific query string.  
   Example: `newtonsoft json 14 .net 8 compatibility`
2. Invoke `firecrawl-mcp-server` with that query and retrieve results.  
3. Cross-check dates and at least two independent sources when possible.
4. For each fact you use, cite its result ID inline in the form  
   `` (no raw URLs).

### 3 After searching

- Integrate verified facts into your reply, placing citations immediately after the statements they support.  
- If the search fails to resolve uncertainty, state the uncertainty rather than guessing.

==== END OF INSTRUCTIONS FROM: tools-web-search.instructions.md ====


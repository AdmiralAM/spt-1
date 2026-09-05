# GitHub CLI on Windows: keep `gh` from losing Git

`gh` uses Git for repository-aware commands. If it reports `unable to find git executable in PATH`, Git is installed but invisible to the process that launched `gh`.

## Repair the current PowerShell session

```powershell
$gitCandidates = @(
  "$env:ProgramFiles\Git\cmd",
  "${env:ProgramFiles(x86)}\Git\cmd",
  "$env:LOCALAPPDATA\Programs\Git\cmd"
)
$gitDir = $gitCandidates | Where-Object { Test-Path (Join-Path $_ 'git.exe') } | Select-Object -First 1
if (-not $gitDir) { throw 'Git for Windows was not found; install it from https://git-scm.com/download/win' }
$env:Path = "$gitDir;$env:Path"
git --version
gh --version
gh auth status
```

## Make it persistent

Install Git for Windows with **Git from the command line and also from 3rd-party software** enabled. If Git is already installed, add its `cmd` directory (for example `C:\Program Files\Git\cmd`) to the user `Path` in **System Properties → Environment Variables**, then restart PowerShell, the editor, and Codex.

Do not add a repository-local shim, hard-code a personal token, or put credentials in workflow files. Verify with `Get-Command git` and `gh auth status` in the same terminal that runs checks.

## Preflight

```powershell
Get-Command git
Get-Command gh
git --version
gh auth status
gh pr checks <number>
```

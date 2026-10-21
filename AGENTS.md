# Codex Monitor repository instructions

## GitHub identity and account switching

- Before creating a commit or performing an authenticated GitHub write, run `C:\Users\ppotepa\.codex\scripts\repo-check.ps1` from the repository.
- The expected GitHub account is determined by the repository owner in `origin` and the configured repository profile.
- If the check reports `MISMATCH`, stop before committing or pushing. Do not change credentials automatically.
- When the user explicitly asks to use a different account, switch deliberately with the matching profile helper (`ppotepa.ps1` or `clickbytes.ps1`), rerun `repo-check.ps1 -CheckRemoteAuth`, and continue only after the result is `PASS`.
- After the requested commit or push is complete, the active account may be switched again only when the user asks for it.

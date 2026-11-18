# code-reviewer lessons (branch claude/issue-786-exception-responses-yioxnl)
- A sweep that replaces `exception.Message` in answers must be grepped across the whole host, not just the issue's list: AdminApi.cs:375 and :506 kept `{failure.Message}` on 503s (#786, b111861).
- A new `using DownfallArena.*` placed above `using System.*` breaks `dotnet_sort_system_directives_first = true`; run `dotnet format --verify-no-changes` on the copy before trusting it (#786, b111861, 8 files).

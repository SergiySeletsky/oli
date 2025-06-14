# Repository Guidelines for Automated Agents

This document provides guidance for any automated contributions to this
repository.

## Development Setup
- Python 3.12 is used for tooling. Install dependencies with `uv sync --dev`
  and activate the virtual environment with `source .venv/bin/activate`.
- Node.js 23.10.0 is required for the TypeScript UI.
- Use the stable Rust toolchain with the `rustfmt` and `clippy` components.

## Style and Linting
- Format Rust code via `cargo fmt`.
- Lint Rust code with `cargo clippy --all-targets --all-features -- -D warnings`.
- For TypeScript code run `npm run lint` and `npm run format`.
- These checks are bundled in pre‑commit hooks. Always execute
  `pre-commit run --all-files` before committing changes.

## Testing
- Execute `cargo test --all-features -- --test-threads=1` and ensure all tests
  pass locally before opening a pull request.

## Pull Requests
- Fill out `.github/pull_request_template.md` when creating a PR.
- Link any relevant issues in the description.

## .NET CLI Port Plan

This repository is migrating the original Rust-based CLI to a .NET implementation. Automated agents should maintain the progress notes below and update them as work continues.

### Plan
1. Recreate existing `oli` commands using `System.CommandLine` in the `OLI.NetCli` project.
2. Persist CLI state (such as selected model and agent mode) in a file.
3. Integrate LLM capabilities using packages like [AutoGen.NET](https://github.com/microsoft/autogen) or [Semantic Kernel](https://github.com/microsoft/semantic-kernel).
4. Achieve feature parity with the Rust CLI before deprecating it.
5. Migrate features incrementally, implementing at least ten per agent run and recording progress here.
6. Track ported files: `Program.cs`, `ConversationSummary.cs`, `ToolExecution.cs`, and `LspCommands.cs` are largely complete. New helper modules `ApiKeyCommands.cs`, `NetworkCommands.cs`, `LogCommands.cs`, and `PathCommands.cs` house additional commands.

### Accomplished
- Created the `dotnet/OLI.NetCli` project with `run`, `agent-mode`, and `models` commands.
- Added basic JSON-based state persistence in `Program.cs`.
- Implemented a `version` command to display the CLI version.
- Added `set-model` command for selecting the active model.
- Added task management commands `tasks` and `cancel-task`.
- Added conversation management via `clear-conversation`.
- Ported memory commands `memory-info`, `add-memory`, and `replace-memory-file`.
- Added state inspection commands `agent-status` and `state-path`.
- Added conversation commands `conversation` and `save-conversation`.
- Implemented memory utilities `memory-path`, `create-memory-file`, and `parsed-memory`.
- Introduced `AdditionalCommands.cs` to group supplemental CLI commands and keep `Program.cs` lean.
- Added task lifecycle commands `create-task` and `complete-task`.
- Implemented basic event subscription via `subscribe` and `unsubscribe`.
- Added maintenance commands `current-model`, `subscriptions`, `delete-memory-section`,
  `delete-task`, `task-info`, `reset-state`, `import-state`, `export-state`,
  `delete-memory-file`, and `list-memory-sections`.
- Added conversation utilities `summarize-conversation` and `conversation-stats`.
- Added file commands `read-file`, `read-file-numbered`, `read-file-lines`,
  `write-file`, `write-file-diff`, `edit-file`, `list-directory`, and `file-info`.
- Extended task tracking with timestamps, token counters, and tool counts.
- Added task utilities `task-stats`, `add-input-tokens`, and `add-tool-use`.
- Added filesystem commands `generate-write-diff`, `generate-edit-diff`,
  `create-directory`, `glob-search`, `glob-search-in-dir`, and `grep-search`.
- Added `memory-exists` command to check for the memory file.
- Added task utilities `task-count`, `clear-tasks`, `update-task-desc`, `export-tasks`, and `import-tasks`.
- Added conversation utilities `import-conversation`, `append-conversation`, and `delete-conversation-message`.
- Added `append-memory-file` and `state-info` commands.
- Added memory import/export commands `import-memory-file` and `export-memory-file`.
- Added conversation export command `export-conversation`.
- Added file utilities `append-file`, `copy-file`, `move-file`, `rename-file`, `delete-file`, `file-exists`, and `count-lines`.
- Added directory utilities `delete-directory` and `dir-exists`.
- Persisted tasks in `tasks.json` and conversation in `conversation.json`.
- Added conversation commands `conversation-length`, `conversation-last`, `conversation-search`, and `delete-conversation-range`.
- Added memory commands `memory-section-count`, `memory-entry-count`, and `memory-template`.
- Added task utilities `clear-completed-tasks` and `tasks-by-status`.
- Added conversation summary tracking with `ConversationSummary` model persisted
  in `summaries.json`.
- Implemented commands `conversation-char-count`, `summary-count`,
  `compress-conversation`, `clear-history`, `show-summaries`, `export-summaries`,
  `import-summaries`, and `delete-summary`.
- Added task commands `fail-task`, `current-task`, and `set-current-task` with
  tracking of the active task ID.
- Introduced tool execution tracking persisted to `tools.json` with commands
  `start-tool`, `update-tool-progress`, `complete-tool`, `fail-tool`,
  `cleanup-tools`, `list-tools`, `tool-info`, `tool-count`, and `running-tools`.
- Added automatic conversation compression with `set-auto-compress`,
  `set-compress-thresholds`, and `show-config` commands.
- Added working directory support via `set-working-dir` and `current-directory`.
- Added tool management commands `list-tools-by-task`, `delete-tool`,
  `set-tool-metadata`, `export-tools`, and `import-tools`.
- Added path helpers `tasks-path`, `conversation-path`, `summaries-path`, and
  `tools-path`.
- Added summary utilities `latest-summary`, `summary-info`, and
  `delete-summary-range`.
- Added task utilities `add-output-tokens` and `task-duration`.
- Added `subscription-count` command to show active subscription total.
- Added utility commands `estimate-tokens`, `extract-metadata`, `tool-description`,
  `has-active-tasks`, `task-statuses`, `validate-api-key`, `determine-provider`,
  `display-to-session`, `session-to-display`, and `summarize-text`.
- Added memory commands `memory-size`, `search-memory`, and `delete-memory-lines`.
- Added conversation commands `conversation-first`, `conversation-range`, and `conversation-info`.
- Added file helpers `list-directory-recursive`, `head-file`, `tail-file`, and `file-size`.
- Moved `TaskRecord` and `AppState` definitions into separate files for better organization.
- Added LSP management commands `lsp-start`, `lsp-stop`, `lsp-stop-all`, `lsp-list`, and `lsp-info`.
- Added LSP utilities `lsp-symbols`, `lsp-codelens`, `lsp-semantic-tokens`, `lsp-definition`, and `lsp-workspace-root`.
- Introduced `LspServerInfo` model and persisted servers in `lsp.json`.
- Added memory maintenance commands `merge-memory-file`, `reset-memory-file`, `copy-memory-section`, and `swap-memory-sections`.
- Added conversation utilities `list-conversation`, `conversation-at`, `delete-conversation-before`, `delete-conversation-after`, `delete-conversation-contains`, and `reverse-conversation`.
- Added memory line utilities `memory-lines`, `insert-memory-lines`, and `replace-memory-lines`.
- Added conversation commands `conversation-word-count` and `clear-summaries`.
- Added task helpers `latest-task`, `tasks-in-progress`, and `task-descriptions`.
- Added state inspection commands `state-version`, `state-summary`, and `state-files`.
- Added binary file helpers `read-binary-file` and `write-binary-file`.
- Added file hashing via `file-hash` and word counting with `file-word-count`.
- Added shell execution command `run-command`.
- Introduced simple RPC server with `start-rpc`, `stop-rpc`, `rpc-running`, and `rpc-notify` commands.
- Added task maintenance commands `purge-failed-tasks` and `tasks-overview`.
- Added file utilities `touch-file`, `copy-directory`, `move-directory`, and `rename-directory`.
- Added JSON helpers `read-json`, `write-json`, `json-format`, and `json-diff`.
- Added memory inspection commands `memory-head` and `memory-tail`.
- Added task utilities `task-rename`, `set-task-priority`, and `reopen-task`.
- Added file checks `file-writable`, `dir-writable`, and `directory-size`.
- Added memory utilities `memory-stats` and `memory-unique-words`.
- Added conversation export `conversation-to-html` and RPC event viewer `rpc-events`.
- Added command listing via `list-commands`.
- Added logging commands `set-log-level`, `show-log`, and `clear-log`.
- Added task utilities `search-tasks` and `task-history`.
- Added conversation command `dedupe-conversation`.
- Added memory section commands `export-memory-section`, `import-memory-section`, `open-memory`, and `list-memory-keys`.
- Persisted log level in `AppState`.
- Added log management commands `log-path`, `search-log`, and `export-log`.
- Added backup utilities `backup-state`, `restore-state`, `backup-memory`, `restore-memory`, `backup-conversation`, `restore-conversation`, and `list-backups`.
- Extracted logging helpers into `LogUtils` and introduced `BackupUtils` for file backups.
- Added backup commands `backup-tasks`, `restore-tasks`, `backup-tools`, `restore-tools`, `backup-summaries`, `restore-summaries`, `backup-lsp`, `restore-lsp`, and `backup-all`.
- Added task listing command `tasks-by-priority`.
- Added conversation editing via `conversation-insert` and state inspection with `open-state`.
- Moved LSP commands into `LspCommands.cs` and added `lsp-hover`, `lsp-completion`, `lsp-references`, `lsp-rename`, `lsp-signature`, `lsp-format`, `lsp-actions`, `lsp-folding-ranges`, `lsp-diagnostics`, and `lsp-open`.
- Added conversation utilities `conversation-replace`, `conversation-move`, and `conversation-role-count`.
- Added memory commands `sort-memory`, `search-memory-regex`, and `memory-word-frequency`.
- Added task utilities `tasks-by-created`, `reset-tasks`, `export-tasks-csv`, and `import-tasks-csv`.
- Added LSP helper commands `lsp-restart` and `lsp-path`.
- Added JSON utilities `json-merge` and `json-validate`.
- Added memory diff viewer `memory-diff`.
- Added regex log searching via `search-log-regex`.
- Added `grep-count` and `tail-file-follow` file utilities.
- Added task age viewer `task-age` and conversation exporter `export-conversation-text`.
- Added `open-tools` helper and `list-tool-names` command.
- Added `show-log-level` and `tail-log` commands for log inspection.
- Implemented task cleanup with `cleanup-tasks`.
- Added `conversation-average-length` to calculate average message length.
- Added task text import/export via `export-tasks-text` and `import-tasks-text`.
- Introduced memory utilities `memory-section-lines` and `rename-memory-section`.
- Added regex task search with `search-tasks-regex` and log splitting via `split-log`.
- Added task due dates and tagging with commands `set-task-due`, `task-due`,
  `add-task-tag`, `remove-task-tag`, `list-task-tags`, `tasks-by-tag`,
  `tasks-due-soon`, and `tasks-overdue`.
- Added file compression utilities `compress-log`, `compress-file`, and
  `decompress-file`.
- Added API key management commands `set-api-key`, `get-api-key`, and `clear-api-key`.
- Added network helpers `download-file` and `upload-file` for HTTP transfers.
- Introduced log utilities `open-log`, `rotate-log`, and `log-size`.
- Added path helpers `open-tasks` and `open-conversation` to quickly open state files.
- Added memory search command `search-memory` with `--ignore-case` option.
- Added conversation utilities `conversation-exists`, `conversation-has`, and `remove-empty-conversation`.
- Added task helpers `tasks-failed`, `task-status-counts`, and `tasks-recent`.
- Added `memory-sort-lines` to alphabetize memory file content.
- Added path helpers `open-summaries` and `open-lsp` for quick file access.
- Added task listing commands `tasks-pending`, `tasks-success`, and `tasks-by-updated`.
- Added tool utilities `list-tool-ids`, `tool-metadata`, `export-tool-run`, `import-tool-run`, and `clear-tools`.
- Added backup helpers `backup-path` and `open-backups`.
- Added log trimming command `trim-log`.
- Added `conversation-unique-words` to count vocabulary in a conversation.
- Added `memory-dedupe-lines` for removing duplicate memory lines.
- Implemented `grep-search-adv` and `glob-search-adv` respecting ignore patterns.
- Added `rpc-stream-events` and SSE support in `RpcServer`.
- Introduced tool progress commands `tool-progress` and `tool-progress-all`.
- Added task listing helpers `tasks-today` and `tasks-week`.
- Implemented `export-tasks-md` to save tasks as Markdown.
- Integrated Semantic Kernel to power model replies and summaries.
- Updated `run` to call the LLM and store the assistant response.
- Enhanced auto compression and conversation/text summarization with LLM calls.
- Added commands `summarize-file`, `summarize-memory-section`, `summarize-tasks`,
  `summarize-state`, `conversation-word-frequency`, and `tasks-due-today`.
- Added task utilities `task-exists`, `tasks-due-range`, `tasks-due-tomorrow`, and `count-task-tags`.
- Added summary helpers `summary-exists` and `append-summary`.
- Added memory command `memory-section-exists`.
- Added LSP utilities `export-lsp`, `import-lsp`, and `lsp-count`.
- Added task commands `tasks-this-month`, `tasks-due-next-week`, and `tasks-paused`.
- Added task control commands `pause-task` and `resume-task`.
- Added task notes commands `set-task-notes`, `show-task-notes`, and `append-task-notes`.
- Added conversation comparison via `conversation-diff` and backup helper `latest-backup`.
- Added bulk task controls `pause-all-tasks` and `resume-all-tasks`.
- Added `delete-task-notes`, `list-task-ids`, and archival commands `archive-task` and `unarchive-task`.
- Added `archived-tasks`, `tasks-created-before`, and `tasks-created-after` for date filtering.
- Added helpers `open-latest-backup` and `rpc-notify-file`.
- Added memory commands `sort-memory`, `search-memory-regex`, and `memory-word-frequency`.
- Added task utilities `tasks-by-created`, `reset-tasks`, `export-tasks-csv`, and `import-tasks-csv`.
- Added LSP helper commands `lsp-restart` and `lsp-path`.
- Added tool commands `clear-completed-tools`, `tool-duration`, `tools-by-status`,
  `tool-exists`, `latest-tool`, `tool-age`, `tools-recent`, `running-tool-count`,
  `tools-by-name`, and `tool-count-by-name`.
- Added task utilities `task-summary`, `delete-tasks-by-status`, and `next-task`.
- Added memory helpers `list-memory-files` and `memory-keywords`.
- Added conversation exporter `conversation-to-md`.
- Added tool helpers `open-latest-tool` and `tool-log`.
- Added notes checker `task-notes-exists` and log viewer `log-errors`.
- Added diff helper `state-diff` and conversation JSONL commands
  `conversation-to-jsonl` and `conversation-from-jsonl`.
- Added `memory-line-count` and task filters `tasks-with-notes`,
  `tasks-without-due`, and `tasks-without-tags`.
- Added memory editing commands `add-memory-section` and
  `update-memory-section`.
- Added conversation utilities `conversation-clear-after` and
  `conversation-slice`.
- Moved configuration and text utility commands `show-config`, `estimate-tokens`,
  `extract-metadata`, `tool-description`, `has-active-tasks`, `task-statuses`,
  `validate-api-key`, `determine-provider`, `display-to-session`,
  `session-to-display`, and `summarize-text` into `AdditionalCommands.cs`.
- Moved file operation commands into `FileCommands.cs` for better organization.
- Split conversation summary features into `SummaryCommands.cs` and added JSON
  helpers in `JsonCommands.cs`.
- Added binary file utilities and hashing commands to `FileCommands.cs`.
- Added conversation utilities `conversation-length`, `conversation-last`,
  `conversation-search`, `delete-conversation-range`, `conversation-first`,
  `conversation-range`, `conversation-info`, `list-conversation`,
  `conversation-at`, `delete-conversation-before`, `delete-conversation-after`,
  `delete-conversation-contains`, and `reverse-conversation` in
  `AdditionalCommands.cs`.
- Moved `clear-summaries` into `SummaryCommands.cs`.

### TODO for Next Run
- Implement actual model API calls in the `run` command using AutoGen.NET or Semantic Kernel.
- Persist conversation history and implement tool integrations similar to the Rust backend.
- Add unit tests and CI for the .NET CLI.
- Continue updating this section with progress and next steps.
- Expand RPC server capabilities to support real-time event streaming and subscription handling.
- Replace basic diff implementation with a robust algorithm and hook up LLM
  summarization for conversation utilities.
- Improve search commands to respect ignore files and binary detection.
- Continue porting LSP-based code intelligence features.
- Improve task filtering options and start integrating conversation summaries with LLM APIs.
- Hook up automatic conversation compression to LLM summarization.
- Implement RPC-based event streaming for subscriptions in the .NET CLI.
- Continue migrating commands from `Program.cs` into dedicated modules.

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
6. Track ported files: `Program.cs`, `ConversationSummary.cs`, and `ToolExecution.cs` are largely complete.

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

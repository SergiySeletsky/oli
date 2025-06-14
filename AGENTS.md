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

### TODO for Next Run
- Implement actual model API calls in the `run` command using AutoGen.NET or Semantic Kernel.
- Persist conversation history and implement tool integrations similar to the Rust backend.
- Add unit tests and CI for the .NET CLI.
- Continue updating this section with progress and next steps.
- Add real-time event streaming support to mirror Rust subscriptions.
- Replace basic diff implementation with a robust algorithm and hook up LLM
  summarization for conversation utilities.
- Improve search commands to respect ignore files and binary detection.
- Begin porting LSP-based code intelligence features.
- Persist tasks and conversation data in separate JSON files and load them on startup.

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

### Accomplished
- Created the `dotnet/OLI.NetCli` project with `run`, `agent-mode`, and `models` commands.
- Added basic JSON-based state persistence in `Program.cs`.
- Implemented a `version` command to display the CLI version.

### TODO for Next Run
- Implement actual model API calls in the `run` command using AutoGen.NET or Semantic Kernel.
- Bring over conversation history and tool integration features from the Rust CLI.
- Port memory management and conversation clearing APIs from Rust to the .NET CLI.
- Add unit tests and CI for the .NET CLI.
- Continue updating this section with progress and next steps.

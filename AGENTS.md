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

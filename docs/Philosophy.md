# Philosophy

## Why This Exists

DatumStudio MCP Core exists to provide a minimal, opinionated foundation for Unity editor tooling. The goal is to establish patterns and practices that enable deterministic, maintainable editor workflows.

## Design Principles

### Editor-First

This package is editor-only in v1. Runtime code may be added in future versions, but the initial focus is on editor tooling that improves development workflows.

### Minimalism

We avoid feature bloat. Every addition must justify its existence. If something can be done simply, we do it simply.

### Determinism

Tools should produce predictable, reproducible results. Non-deterministic behavior is considered a bug.

### Opinionated

This package makes choices. We don't try to support every possible use case. Instead, we provide a clear path forward for common scenarios.

## What We Don't Do

### Runtime Dependencies (v1)

We don't include runtime code in v1. The Runtime folder exists as a seam for potential future expansion, but no runtime functionality is provided.

### Third-Party Assets

We don't bundle third-party assets in samples. Samples use only Unity built-in assets and the package itself.

### Community Promises

Free tier support is limited to GitHub Issues. We don't promise Discord servers, community forums, or other community infrastructure for the free tier.

### Backwards Compatibility Guarantees

While we aim for stability, we reserve the right to make breaking changes if necessary. Major version bumps indicate potentially breaking changes.

### Universal Solutions

We don't try to solve every problem. This package focuses on specific use cases and patterns. If it doesn't fit your needs, that's okay.


---
name: pixelfx-unity
description: Tune PixelFX for Unity variants, author procedural pixel-art effects, or migrate a Patch PixelFX JavaScript effect into the Unity package.
---

# PixelFX for Unity

Read [the project authoring guide](../../docs/authoring.md) before editing an effect. It defines the coordinates, time, quality levels and acceptance checks.

Use the effect ID to find its implementation at `Packages/com.pixelfx.unity/Runtime/Effects/<effect-id>/`. Keep effect-specific renderers, particles, stage code and helpers together. Register the effect and its renderer in `Runtime/Core/PixelFxLibrary.cs`.

- Tune an effect through the existing variant settings when they can express the request. Use the Effect Library for preview and save a new variant or explicitly update the named user asset.
- For a new effect, identify the anchor and the visual beats. Follow Heavy Cleave for an anchored one-shot, Fireball Throw for a cast with travel and multiple hits, and Shuriken for particles that can be sampled directly by time.
- To continue the upstream migration, start at **Resume here** in [the migration checklist](../../docs/migration.md). Check the working tree and update the queue before editing. Use the pinned revision, inspect dependencies, preserve seed, anchor and default timing, and retain source attribution. Record a resumable checkpoint or update the completed entry after a seeded comparison.

Preview and runtime must use the same effect implementation. Verify Full, Simplified and Off at their intended timing before reporting completion.

## Commit each verified step

- Inspect `git status --short` and the existing staged and unstaged diffs at the start.
- Commit each completed, verified step locally before starting the next one. Give each commit one purpose, such as an effect port, a standalone refactor, or a docs change. Include the code, Unity assets and `.meta` files, and any sample or documentation updates needed for that purpose.
- Stage only the relevant paths or hunks. Review `git diff --cached` and `git diff --cached --check` before committing. Preserve other work already in the tree or index.
- For an effect port, include its verification evidence, catalog status, README totals and next queue item in the same commit. For unfinished work, record the remaining work and verification status in the migration checklist.

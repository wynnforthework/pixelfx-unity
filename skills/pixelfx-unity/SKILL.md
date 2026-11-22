---
name: pixelfx-unity
description: Tune PixelFX for Unity variants, author procedural pixel-art effects, or migrate a Patch PixelFX JavaScript effect into the Unity package.
---

# PixelFX for Unity

Read [the project authoring guide](../../docs/authoring.md) before editing an effect. It defines the coordinates, time, quality levels and acceptance checks.

- Tune an effect through the existing variant settings when they can express the request. Use the Effect Library for preview and save a new variant or explicitly update the named user asset.
- For a new effect, identify the anchor and the visual beats. Follow Heavy Cleave for an anchored one-shot, Fireball Throw for a cast with travel and multiple hits, and Shuriken for particles that can be sampled directly by time.
- To migrate an upstream effect, use the pinned revision in [the migration checklist](../../docs/migration.md). Inspect the source and helper dependencies. Preserve seed, anchor and default timing unless the task calls for a gameplay adaptation. Retain source attribution, compare seeded captures, and update the checklist after verification.

Preview and runtime must use the same effect implementation. Verify Full, Simplified and Off at their intended timing before reporting completion.

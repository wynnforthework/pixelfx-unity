# Authoring PixelFX effects

## Choose the shape of the effect

- For a color, scale, timing or density adjustment, use a variant. Keep the stable library ID and modify only its draft settings.
- For a one-shot anchored to an actor, use Heavy Cleave. Keep local drawing coordinates in pixels, with +X forward, +Y down, and (0,0) at the documented pivot. The player's sprite places that pivot in Unity world space.
- For a projectile with gameplay-dependent travel, use Fireball Throw. The timeline maps elapsed time to cast, flight and impact. Place effects at the context anchors. Compute flight time from distance and speed, accept zero distance, and accept explicit hit positions and strengths. Keep damage and target selection in gameplay code.
- For a time-sampled effect, use Shuriken. Derive each particle from the seed and its emission index so seeking does not require running the preceding frames. For stateful simulation, step at 120 Hz and reset and replay on backward seeks.

## Add or migrate a definition

Register a unique category/effect ID, defaults and representative thumbnail time in the library. Use existing painter primitives and buffers. Expose only settings that change gameplay readability or appearance. Validate ranges in the timeline and preserve legacy behavior at defaults. Use one shared rendering path for the editor and the runtime.

Full quality may add sparks, smoke and debris. Simplified retains the key shape, travel cues and hit flash. Off schedules timing only. Quality switches must not change duration or completion. Use stable seeds and avoid per-frame allocations in an effect's steady state.

## Verify

Compile in Unity 2022.3, preview at several times and check that seeking back returns the same frame. Check first and last frames, silhouettes, pivot, tight bounds, palette variation and the simplified look. For a migration, compare representative captures with upstream at the same scale, seed and time. Document adaptations and count a port as verified when gameplay and visual checks pass. For phased effects, also exercise short, long and zero-distance casts, different strengths and stop behavior.

Retain upstream copyright and MIT credits. Use the pinned upstream commit in the migration checklist. Keep original effects and approximate adaptations identifiable in that checklist.

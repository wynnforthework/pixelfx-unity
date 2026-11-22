# Authoring PixelFX effects

## Choose the shape of the effect

- For a color, scale, timing or density adjustment, use a variant. Keep the stable library ID and modify only its draft settings.
- For a one-shot anchored to an actor, use Heavy Cleave. Keep local drawing coordinates in pixels, with +X forward, +Y down, and (0,0) at the documented pivot. The player's sprite places that pivot in Unity world space.
- For a projectile with gameplay-dependent travel, use Fireball Throw. The timeline maps elapsed time to cast, flight and impact. Place effects at the context anchors. Compute flight time from distance and speed, accept zero distance, and accept explicit hit positions and strengths. Keep damage and target selection in gameplay code.
- For a time-sampled effect, use Shuriken. Derive each particle from the seed and its emission index so seeking does not require running the preceding frames. For stateful simulation, step at 120 Hz and reset and replay on backward seeks.

## Actor-relative effect layers

Choose a layer for each effect stage or visual element. The names describe how it overlaps the bound actor. Anchors set the position independently and can be overridden for a stage or hit.

| Layer | Default anchor | Draw order relative to the actor |
| --- | --- | --- |
| Lower | Feet | Draw the whole element behind the actor. Use for ground marks or shadows. |
| Middle | Body center | Draw the far half behind the actor and the near half in front. For a ring in the standard view, these are the upper and lower arcs of the ring. |
| Upper | Head | Draw the whole element in front of the actor. A foreground impact can use the body or hit anchor and keep this draw order. |

For Middle, split the effect using its authored near/far geometry. A ring uses its own center and plane, so the split follows the ring as it moves. Both passes share the same anchor and playback time. Each stage selects its own actor and may use only the layers it needs.

For an A-to-B cast, put the cast ring in Middle relative to A and play the projectile along its travel path. Put the explosion in Upper relative to B, at B's hit anchor, and follow B throughout the impact phase.

Bind each stage using `PixelFxActorBinding`. The player sorts one order behind or ahead of the actor's `SpriteRenderer` or enclosing `SortingGroup` on the same sorting layer. Reserve one order on either side of the actor. A stage without authored split passes draws entirely in front when bound to Middle.

For Fireball Throw, an explicit `LaunchPoint` sets the projectile's world-space origin. Otherwise, a following cast binding supplies the origin at release. The flight duration is scheduled from the initial distance, and each hit may follow its own actor after the projectile reaches the snapshotted landing point.

## Add or migrate a definition

Place each effect under `Packages/com.pixelfx.unity/Runtime/Effects/<effect-id>/`. Preserve the full ID path, such as `melee/heavy-cleave` or `moves/fireball-throw`. Keep the effect renderer, particles, effect-specific stage code, and dedicated helpers in that directory. Split a large effect by responsibility with effect-prefixed filenames. Common playback, variants, and the registry live in `Runtime/Core/`. Reusable rasterization and texture code live in `Runtime/Rendering/`. Preserve Unity `.meta` files when moving existing scripts or assets.

Register a unique effect ID, defaults, duration, thumbnail time, canvas bounds, and renderer factory in `Runtime/Core/PixelFxLibrary.cs`. Use existing painter primitives and buffers. Expose only settings that change gameplay readability or appearance. Validate ranges in the timeline and preserve legacy behavior at defaults. Use one shared rendering path for the editor and the runtime.

Include an asset named after the final segment of the effect ID, such as `fireball-throw.asset`, in the effect folder. It can be assigned to the same `PixelFxVariant` field as a user variant. Use a built-in asset as the template, set its name and effect ID, and give the new asset its own Unity GUID. The built-in asset resolves its settings from the C# registry through `useLibraryDefaults`. Saved variants keep a snapshot of their settings.

Full quality may add sparks, smoke and debris. Simplified retains the key shape, travel cues and hit flash. Off schedules timing only. Quality switches must not change duration or completion. Use stable seeds and avoid per-frame allocations in an effect's steady state.

## Verify

Compile in Unity 2022.3, preview at several times and check that seeking back returns the same frame. Check first and last frames, silhouettes, pivot, tight bounds, palette variation and the simplified look. For a migration, compare representative captures with upstream at the same scale, seed and time. Document adaptations and count a port as verified when gameplay and visual checks pass. For phased effects, also exercise short, long and zero-distance casts, different strengths and stop behavior.

Retain upstream copyright and MIT credits. Use the pinned upstream commit in the migration checklist. Keep original effects and approximate adaptations identifiable in that checklist.

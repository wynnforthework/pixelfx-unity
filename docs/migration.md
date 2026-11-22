# Upstream migration checklist

Pinned upstream revision [patch-gg/pixelfx@5294518](https://github.com/patch-gg/pixelfx/tree/5294518ce7c72e48b6100a744596e0acf57f2826). This file is the source of truth for the 244 effect IDs and the migration queue.

## Resume here

| Effect or dependency | Status | Next action |
| --- | --- | --- |
| `impacts/parry` | Next | Port the blade cross, broken ring and spark fan. Compare seed 11 at 30 FPS. |
| `impacts/hit-flash` | Blocked on shared actor masks | Add sprite mask, recolor and outline support, then verify the white and red beats on an actor. |
| `moves/fireball-throw` | Playable adaptation | Compare the charge and blast to upstream and record remaining differences. |

1. Read this queue and run `git status --short` at the Unity project root. Check the effect folder and existing diffs before resuming an in-progress entry.
2. Use a checkout of the pinned upstream revision. The source for each ID is `src/fx/<effect-id>.js`. Follow the [authoring guide](authoring.md) and [repository skill](../skills/pixelfx-unity/SKILL.md).
3. Set the queue status to **In progress** before starting. After implementation, record any unfinished step, dependency, or validation failure directly in the queue before stopping.
4. Run `PixelFx.Editor.PixelFxValidation.Run` in Unity 2022.3 and compare representative Full frames against the pinned JavaScript source at the same seed and 30 FPS. Check anchor, duration, the library preview, asset playback, and the Simplified and Off quality levels.
5. Check a catalog entry when it passes. Store the seed, comparison frames and any intentional differences in the completed entry or a linked evidence file. Update both README totals to match the checked entries. The original Shuriken belongs outside the upstream count.

An unchecked entry without a queue note is pending. Keep upcoming batches small enough to leave a verified checkpoint between them.

## Latest checkpoint

**2 / 244 verified** in Unity. Parry is next.

| Effect | Verification | Evidence |
| --- | --- | --- |
| `impacts/hit-spark` | Unity 2022.3.62f3 package validation, quality and backward-seek checks passed. Seed 4, 136 × 80, pivot (44, 64), 30 FPS. All 15 playback frames matched upstream alpha, and RGB differed by at most one 8-bit level. | [Comparison](media/hit-spark-comparison.png) · [Unity animation](media/hit-spark.gif) |
| `melee/heavy-cleave` | Verified in Unity and accepted in Play Mode. | [Comparison](media/heavy-cleave-unity-comparison.gif) |

For byte comparisons, use upstream `renderFrames(...).frames[i].clear` RGBA data. The sprite-sheet exporter multiplies color by alpha when pasting each frame. Unity one-shots finish at their duration, so compare frames sampled before that endpoint.

## Upstream catalog

## arcane

- [ ] `arcane/arcane-beam`
- [ ] `arcane/arcane-missiles`
- [ ] `arcane/arcane-orb`
- [ ] `arcane/blink-in`
- [ ] `arcane/blink-out`
- [ ] `arcane/magic-circle-cast`
- [ ] `arcane/mana-burst`
- [ ] `arcane/portal`
- [ ] `arcane/rune-circle`
- [ ] `arcane/spell-charge`
- [ ] `arcane/star-fall`
- [ ] `arcane/time-stop`

## critters

- [ ] `critters/bat`
- [ ] `critters/bird`
- [ ] `critters/butterfly`
- [ ] `critters/eyeball`
- [ ] `critters/fish-school`
- [ ] `critters/flying-skull`
- [ ] `critters/frog`
- [ ] `critters/ghost`
- [ ] `critters/jellyfish`
- [ ] `critters/slime`
- [ ] `critters/snake`
- [ ] `critters/wisp`

## explosions

- [ ] `explosions/barrel-explosion`
- [ ] `explosions/big-explosion`
- [ ] `explosions/cartoon-pow`
- [ ] `explosions/dirt-explosion`
- [ ] `explosions/firework-burst`
- [ ] `explosions/firework-rocket`
- [ ] `explosions/grenade`
- [ ] `explosions/implosion`
- [ ] `explosions/nuke`
- [ ] `explosions/plasma-explosion`
- [ ] `explosions/small-explosion`
- [ ] `explosions/smoke-bomb`

## fire

- [ ] `fire/brazier`
- [ ] `fire/campfire`
- [ ] `fire/candle`
- [ ] `fire/fire-breath`
- [ ] `fire/fire-ring`
- [ ] `fire/fire-tornado`
- [ ] `fire/fireball`
- [ ] `fire/flame-pillar`
- [ ] `fire/flamethrower`
- [ ] `fire/meteor`
- [ ] `fire/spirit-flame`
- [ ] `fire/torch`

## holy

- [ ] `holy/blessing`
- [ ] `holy/divine-shield`
- [ ] `holy/feather-burst`
- [ ] `holy/halo`
- [ ] `holy/heal-aoe`
- [ ] `holy/heal`
- [ ] `holy/holy-pillar`
- [ ] `holy/resurrection`
- [ ] `holy/smite`
- [ ] `holy/sunburst`

## ice

- [ ] `ice/blizzard-vortex`
- [ ] `ice/freeze`
- [ ] `ice/frost-breath`
- [ ] `ice/frost-nova`
- [ ] `ice/ice-crystal`
- [ ] `ice/ice-lance`
- [ ] `ice/ice-shatter`
- [ ] `ice/ice-spikes`
- [ ] `ice/icicle-fall`
- [ ] `ice/snowflake-burst`

## impacts

- [ ] `impacts/blunt-hit`
- [ ] `impacts/bullet-impact`
- [ ] `impacts/crit-hit`
- [ ] `impacts/ground-slam`
- [ ] `impacts/heavy-landing`
- [ ] `impacts/hit-flash`
- [x] `impacts/hit-spark`
- [ ] `impacts/impact-frame`
- [ ] `impacts/parry`
- [ ] `impacts/ricochet`
- [ ] `impacts/shield-block`

## lightning

- [ ] `lightning/ball-lightning`
- [ ] `lightning/chain-lightning`
- [ ] `lightning/charge-up`
- [ ] `lightning/electrified`
- [ ] `lightning/lightning-strike`
- [ ] `lightning/plasma-ball`
- [ ] `lightning/spark-arc`
- [ ] `lightning/storm-cloud`
- [ ] `lightning/tesla-coil`
- [ ] `lightning/thunderclap`
- [ ] `lightning/zap-hit`

## melee

- [ ] `melee/claw-swipe`
- [ ] `melee/dagger-flurry`
- [ ] `melee/double-slash`
- [ ] `melee/hammer-smash`
- [x] `melee/heavy-cleave`
- [ ] `melee/scythe-reap`
- [ ] `melee/spear-sweep`
- [ ] `melee/spin-slash`
- [ ] `melee/sword-slash`
- [ ] `melee/thrust`
- [ ] `melee/uppercut`
- [ ] `melee/whip-crack`

## moves

- [ ] `moves/arrow-shot`
- [ ] `moves/dash-strike`
- [ ] `moves/earth-stomp`
- [ ] `moves/fireball-throw` — playable C# adaptation. Upstream charge and blast details remain.
- [ ] `moves/ground-pound`
- [ ] `moves/holy-smite`
- [ ] `moves/ice-lance-throw`
- [ ] `moves/lightning-call`
- [ ] `moves/meteor-call`
- [ ] `moves/pistol-shots`
- [ ] `moves/rocket-launcher`
- [ ] `moves/sword-combo`

## nature

- [ ] `nature/acid-splash`
- [ ] `nature/boulder`
- [ ] `nature/earth-spike`
- [ ] `nature/healing-bloom`
- [ ] `nature/leaf-storm`
- [ ] `nature/poison-cloud`
- [ ] `nature/quake`
- [ ] `nature/root-snare`
- [ ] `nature/spore-burst`
- [ ] `nature/thorns`
- [ ] `nature/tornado`
- [ ] `nature/toxic-puddle`
- [ ] `nature/vine-whip`

## pickups

- [ ] `pickups/chest-open`
- [ ] `pickups/coin-spin`
- [ ] `pickups/collect-burst`
- [ ] `pickups/crystal-shard`
- [ ] `pickups/gem-spin`
- [ ] `pickups/heart-pickup`
- [ ] `pickups/key-glint`
- [ ] `pickups/loot-beam`
- [ ] `pickups/potion`
- [ ] `pickups/power-star`
- [ ] `pickups/xp-orb`

## projectiles

- [ ] `projectiles/arrow`
- [ ] `projectiles/bullet-tracer`
- [ ] `projectiles/cannonball`
- [ ] `projectiles/energy-disc`
- [ ] `projectiles/flare`
- [ ] `projectiles/homing-missile`
- [ ] `projectiles/laser-bolt`
- [ ] `projectiles/magic-missile`
- [ ] `projectiles/muzzle-pistol`
- [ ] `projectiles/muzzle-shotgun`
- [ ] `projectiles/muzzle-smg`
- [ ] `projectiles/plasma-shot`
- [ ] `projectiles/rocket`

## scifi

- [ ] `scifi/emp-blast`
- [ ] `scifi/energy-core`
- [ ] `scifi/force-field`
- [ ] `scifi/glitch`
- [ ] `scifi/hologram`
- [ ] `scifi/jetpack`
- [ ] `scifi/laser-grid`
- [ ] `scifi/scanner`
- [ ] `scifi/teleporter-pad`
- [ ] `scifi/tractor-beam`
- [ ] `scifi/warp-in`
- [ ] `scifi/warp-out`

## shadow

- [ ] `shadow/black-hole`
- [ ] `shadow/curse`
- [ ] `shadow/dark-explosion`
- [ ] `shadow/eclipse`
- [ ] `shadow/grasping-hands`
- [ ] `shadow/hex-sigil`
- [ ] `shadow/life-steal`
- [ ] `shadow/necro-skulls`
- [ ] `shadow/shadow-flames`
- [ ] `shadow/shadow-tendrils`
- [ ] `shadow/soul-drain`
- [ ] `shadow/void-orb`

## spawn

- [ ] `spawn/coin-drop`
- [ ] `spawn/disintegrate`
- [ ] `spawn/dissolve-burn`
- [ ] `spawn/egg-hatch`
- [ ] `spawn/poof`
- [ ] `spawn/respawn-beam`
- [ ] `spawn/shatter-death`
- [ ] `spawn/soul-rise`
- [ ] `spawn/spawn-portal`
- [ ] `spawn/summon-rise`

## status

- [ ] `status/bleeding`
- [ ] `status/burning`
- [ ] `status/charmed`
- [ ] `status/confused`
- [ ] `status/cursed`
- [ ] `status/frozen`
- [ ] `status/haste`
- [ ] `status/petrified`
- [ ] `status/poisoned`
- [ ] `status/rage`
- [ ] `status/sleep`
- [ ] `status/slowed`
- [ ] `status/stunned`

## transitions

- [ ] `transitions/blinds`
- [ ] `transitions/burn-away`
- [ ] `transitions/crt-off`
- [ ] `transitions/diamond-wipe`
- [ ] `transitions/glitch-wipe`
- [ ] `transitions/iris`
- [ ] `transitions/pixel-dissolve`
- [ ] `transitions/shatter-wipe`
- [ ] `transitions/slash-wipe`
- [ ] `transitions/star-iris`

## ui

- [ ] `ui/achievement`
- [ ] `ui/coin-collect`
- [ ] `ui/combo-counter`
- [ ] `ui/confetti`
- [ ] `ui/countdown`
- [ ] `ui/crit-number`
- [ ] `ui/damage-number`
- [ ] `ui/exclamation`
- [ ] `ui/health-bar-hit`
- [ ] `ui/heart-lost`
- [ ] `ui/level-up`
- [ ] `ui/quest-complete`

## water

- [ ] `water/bubbles`
- [ ] `water/drip`
- [ ] `water/fountain`
- [ ] `water/geyser`
- [ ] `water/rain-splash`
- [ ] `water/ripples`
- [ ] `water/splash`
- [ ] `water/water-jet`
- [ ] `water/water-orb`
- [ ] `water/waterfall`
- [ ] `water/wave-crash`
- [ ] `water/whirlpool`

## weather

- [ ] `weather/aurora`
- [ ] `weather/blizzard`
- [ ] `weather/cherry-blossoms`
- [ ] `weather/clouds`
- [ ] `weather/falling-leaves`
- [ ] `weather/fireflies`
- [ ] `weather/fog`
- [ ] `weather/god-rays`
- [ ] `weather/rain`
- [ ] `weather/shooting-stars`
- [ ] `weather/snow`
- [ ] `weather/storm`

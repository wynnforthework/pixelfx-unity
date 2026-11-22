# PixelFX for Unity

Procedural pixel art effects for Unity 2022.3 LTS and newer. The package includes Heavy Cleave, Fireball Throw, and the original Shuriken.

## Open the demo

Open the repository root in Unity. Choose **Tools > PixelFX > Open Demo**. If prompted, let Unity import and compile the sample, then open the menu again. Enter Play Mode and use the buttons to switch effects. The original **Open Heavy Cleave Demo** also remains available.

For an existing project, use **Window > Package Manager > + > Add package from git URL**. After the public release is available, use this URL pattern with the repository owner:

```text
https://github.com/<owner>/pixelfx-unity.git?path=/Packages/com.pixelfx.unity#v0.1.0
```

For a local checkout, use **Add package from disk** and select `Packages/com.pixelfx.unity/package.json`. Open the demo from the Tools menu or import **PixelFX Demo** from the package's Samples tab.

## Preview and author variants

Open **Tools > PixelFX > Effect Library**. Search the grid by effect name, ID, category or variant name. Select a tile to play, pause, step or scrub its animation in Edit Mode. Tweaks affect the preview draft. **Save As Variant** creates an asset under `Assets`. **Update Variant** writes the draft back to the selected user asset and supports Undo. Distance, secondary target and quality are preview controls.

## Play from code

Add a `PixelFxPlayer` component. Reference a saved `PixelFxVariant` in the field below, or pass an ID from `PixelFxLibrary` to play the defaults.

```csharp
using PixelFx;
using UnityEngine;

public sealed class FireballExample : MonoBehaviour
{
    [SerializeField] private PixelFxPlayer player;
    [SerializeField] private PixelFxVariant fireball;

    public void Cast(Vector3 muzzle, Vector3 target, Vector3 splash)
    {
        PixelFxPlayback playback = player.Play(fireball, new PixelFxContext
        {
            Position = transform.position,
            LaunchPoint = muzzle,
            LandingPoint = target,
            Hits = new[] { new PixelFxHit(target), new PixelFxHit(splash, 0.5f) }
        });
        playback.Completed += result => Debug.Log(result.WasStopped ? "Stopped" : "Finished");
    }
}
```

Flight speed is in Unity units per effect second. Settings and target positions are copied when playback starts. Passing no hit list creates one full-strength impact at the landing point. Pass an empty list to omit impacts. `PixelFxPlayer.DefaultQuality` sets the global default and `PixelFxContext.Quality` overrides it per playback. **Full** draws all layers, **Simplified** omits decorative particles, and **Off** keeps timing and completion while allocating no render surfaces. Use `Stop()` on the returned handle to cancel playback.

## Migration progress

On Unity 2022.3.62f3 Windows editor in headless batch mode, Heavy Cleave averaged the following CPU work per 30 FPS effect sample. Texture submission measures the CPU call without a graphics device.

| Quality | Instances | Simulation and raster | Texture submission |
| --- | ---: | ---: | ---: |
| Full | 1 | 3.609 ms | 0.017 ms |
| Full | 10 | 34.530 ms | 0.159 ms |
| Full | 30 | 105.078 ms | 0.504 ms |
| Simplified | 1 | 2.854 ms | 0.012 ms |
| Simplified | 10 | 28.192 ms | 0.155 ms |
| Simplified | 30 | 80.382 ms | 0.422 ms |

The measured playback samples allocated 0 managed bytes after initialization. The editor command **Tools > PixelFX > Measure Quality (1, 10, 30)** repeats this benchmark.

### Upstream checklist

Baseline [patch-gg/pixelfx@5294518](https://github.com/patch-gg/pixelfx/tree/5294518ce7c72e48b6100a744596e0acf57f2826). **1 / 244 verified against the upstream look**, plus **1 gameplay-adapted move** and **1 original effect**. See the [effect checklist](docs/migration.md) for acceptance status.

- [x] Runtime playback, library preview, serialized variants, and quality levels
- [x] Heavy Cleave and original Shuriken
- [x] Fireball Throw with spatial cast, travel, and multiple impacts
- [ ] Refine Fireball Throw against the upstream stage art
- [ ] Port melee and impacts, then projectiles and chained moves
- [ ] Port elemental and status effects, then environment, creatures, and UI
- [ ] Add reusable actor masks, action timing, and front/back sprite layers

The [authoring guide](docs/authoring.md) and [Codex skill](skills/pixelfx-unity/SKILL.md) live with the project. Runtime and editor code are MIT licensed. Credits for the upstream recipes are included in the package's license and notices.

using UnityEngine;

namespace PixelFx.Samples
{
    [RequireComponent(typeof(PixelFxPlayer))]
    public sealed class PixelFxDemo : MonoBehaviour
    {
        [SerializeField] private PixelFxVariant variant;
        private PixelFxPlayer player;
        private PixelFxPlayback current;
        private SpriteRenderer caster, target;
        private Sprite actorSprite;
        private Texture2D actorTexture;
        private float layeredTime;

        private void Start()
        {
            player = GetComponent<PixelFxPlayer>();
            Play(PixelFxLibrary.HeavyCleave);
        }

        private void OnGUI()
        {
            if (player == null) return;
            GUILayout.BeginArea(new Rect(12, 12, Screen.width - 24, 96));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Heavy Cleave")) Play(PixelFxLibrary.HeavyCleave);
            if (GUILayout.Button("Hit Spark")) Play(PixelFxLibrary.HitSpark);
            if (GUILayout.Button("Parry")) Play(PixelFxLibrary.Parry);
            if (GUILayout.Button("Blunt Hit")) Play(PixelFxLibrary.BluntHit);
            if (GUILayout.Button("Critical Hit")) Play(PixelFxLibrary.CriticalHit);
            if (GUILayout.Button("Fireball Throw")) Play(PixelFxLibrary.FireballThrow);
            if (GUILayout.Button("Shuriken")) Play(PixelFxLibrary.Shuriken);
            if (variant != null && GUILayout.Button("Play Assigned Effect"))
            {
                current?.Stop();
                HideActors();
                current = player.Play(variant, Context());
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Layered Fireball")) PlayLayeredFireball();
            if (GUILayout.Button("Ground Slam")) PlayGroundEffect(PixelFxLibrary.GroundSlam);
            if (GUILayout.Button("Hit Flash")) PlayHitFlash();
            if (GUILayout.Button("Divine Shield")) PlayActorBuff(PixelFxLibrary.DivineShield);
            if (GUILayout.Button("Blessing")) PlayActorBuff(PixelFxLibrary.Blessing);
            if (GUILayout.Button("Halo")) PlayActorBuff(PixelFxLibrary.Halo, PixelFxLayer.Upper);
            if (GUILayout.Button("Heal")) PlayActorBuff(PixelFxLibrary.Heal);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Healing Ring")) PlayActorBuff(PixelFxLibrary.HealingRing);
            if (GUILayout.Button("Holy Pillar")) PlayActorBuff(PixelFxLibrary.HolyPillar);
            if (GUILayout.Button("Smite")) PlayActorBuff(PixelFxLibrary.Smite);
            if (GUILayout.Button("Sunburst")) Play(PixelFxLibrary.Sunburst);
            if (GUILayout.Button("Feather Burst")) PlayActorBuff(PixelFxLibrary.FeatherBurst);
            if (GUILayout.Button("Resurrection")) PlayActorBuff(PixelFxLibrary.Resurrection);
            if (GUILayout.Button("Flame Pillar")) PlayGroundEffect(PixelFxLibrary.FlamePillar);
            if (GUILayout.Button("Fire Tornado")) PlayGroundEffect(PixelFxLibrary.FireTornado);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        public void Play(string effect)
        {
            current?.Stop();
            HideActors();
            current = player.Play(effect, context: Context());
        }

        private void PlayLayeredFireball()
        {
            current?.Stop();
            EnsureActors();
            caster.enabled = target.enabled = true;
            layeredTime = 0;
            MoveActors();
            Vector3 hit = target.bounds.center;
            current = player.Play(PixelFxLibrary.FireballThrow, context: new PixelFxContext
            {
                CastBinding = new PixelFxActorBinding(caster, PixelFxLayer.Middle),
                LandingPoint = hit,
                Hits = new[] { new PixelFxHit(hit, 1, new PixelFxActorBinding(target, PixelFxLayer.Upper, target.transform)) },
            });
            current.Completed += _ => HideActors();
        }

        private void PlayGroundEffect(string effect)
        {
            current?.Stop();
            EnsureActors();
            caster.enabled = true;
            target.enabled = false;
            caster.transform.position = transform.position + new Vector3(3.3f, 1, 0);
            current = player.Play(effect, context: new PixelFxContext
            {
                Binding = new PixelFxActorBinding(caster, PixelFxLayer.Middle, follow: false)
            });
            current.Completed += _ => HideActors();
        }

        private void PlayHitFlash()
        {
            current?.Stop();
            EnsureActors();
            caster.enabled = false;
            target.enabled = true;
            target.transform.position = transform.position + new Vector3(3.3f, 1.3f, 0);
            current = player.Play(PixelFxLibrary.HitFlash, context: new PixelFxContext
            {
                Binding = new PixelFxActorBinding(target, PixelFxLayer.Upper)
            });
        }

        private void PlayActorBuff(string effect, PixelFxLayer layer = PixelFxLayer.Middle)
        {
            current?.Stop();
            EnsureActors();
            caster.enabled = true;
            target.enabled = false;
            caster.transform.position = transform.position + new Vector3(3.3f, 1, 0);
            current = player.Play(effect, context: new PixelFxContext
            {
                Binding = new PixelFxActorBinding(caster, layer)
            });
            current.Completed += _ => HideActors();
        }

        private void EnsureActors()
        {
            if (caster != null) return;
            CreateActorSprite();
            caster = CreateActor("Caster A", new Color32(111, 188, 240, 255), 10);
            target = CreateActor("Target B", new Color32(138, 211, 149, 255), 20);
        }

        private void Update()
        {
            if (caster == null || !caster.enabled || !target.enabled) return;
            layeredTime += Time.deltaTime;
            MoveActors();
        }

        private void MoveActors()
        {
            var start = transform.position + Vector3.up * 0.7f;
            caster.transform.position = start + Vector3.up * (0.32f * Mathf.Sin(layeredTime * 4));
            target.transform.position = start + new Vector3(4.89f, Mathf.Sin(Mathf.Max(0, layeredTime - 1.5f) * 3) * 0.55f);
        }

        private SpriteRenderer CreateActor(string label, Color color, int sortOrder)
        {
            var actor = new GameObject(label, typeof(SpriteRenderer));
            actor.transform.SetParent(transform, false);
            var renderer = actor.GetComponent<SpriteRenderer>();
            renderer.sprite = actorSprite;
            renderer.color = color;
            renderer.sortingOrder = sortOrder;
            return renderer;
        }

        private void CreateActorSprite()
        {
            const int width = 18, height = 36;
            actorTexture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                bool inside = y < 13 ? (x >= 3 && x < 8) || (x >= 10 && x < 15)
                    : y < 27 ? x >= 1 && x < 17 : x >= 4 && x < 14 && y < 35;
                if (inside) pixels[x + y * width] = x < 5 ? new Color32(151, 151, 151, 255) : new Color32(235, 235, 235, 255);
            }
            actorTexture.SetPixels32(pixels);
            actorTexture.Apply(false, false);
            actorSprite = Sprite.Create(actorTexture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 18);
        }

        private void HideActors()
        {
            if (caster != null) caster.enabled = target.enabled = false;
        }

        private void OnDestroy()
        {
            if (actorSprite != null) Destroy(actorSprite);
            if (actorTexture != null) Destroy(actorTexture);
        }

        private PixelFxContext Context()
        {
            Vector3 start = transform.position;
            Vector3 target = start + new Vector3(4.89f, 0, 0);
            return new PixelFxContext
            {
                Position = start,
                LaunchPoint = start,
                LandingPoint = target,
                Hits = new[] { new PixelFxHit(target), new PixelFxHit(target + new Vector3(2.15f, 0.75f), 0.5f) },
            };
        }
    }
}

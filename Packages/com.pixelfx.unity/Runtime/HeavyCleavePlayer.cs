using System;
using Unity.Profiling;
using UnityEngine;

namespace PixelFx
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class HeavyCleavePlayer : MonoBehaviour
    {
        private static readonly ProfilerMarker Raster = new ProfilerMarker("PixelFx.HeavyCleave.Raster");
        private static readonly ProfilerMarker Upload = new ProfilerMarker("PixelFx.HeavyCleave.Upload");
        public const float ClipDuration = (float)HeavyCleaveEffect.Duration;

        [SerializeField] private bool playOnStart = true;
        [SerializeField] private bool flip;
        [SerializeField] private int seed = 5;
        [SerializeField, Min(1)] private float pixelsPerUnit = 32;
        [SerializeField, Range(0, 2)] private float lightStrength = 1;
        [SerializeField] private Color32 stone = new Color32(74, 63, 105, 255);
        [SerializeField] private Color32[] palette = Array.Empty<Color32>();

        private readonly HeavyCleaveEffect effect = new HeavyCleaveEffect();
        private readonly uint[] colors = new uint[8];
        private readonly byte[] rgba = new byte[HeavyCleaveEffect.Width * HeavyCleaveEffect.Height * 4];
        private SpriteRenderer spriteRenderer;
        private Texture2D texture;
        private Sprite sprite;
        private float elapsed;
        private int previousFrame;
        private bool playing;

        private void Start()
        {
            if (playOnStart && texture == null) Play();
        }

        public void Play() => BeginPlayback(seed, flip, true);

        public void Play(int randomSeed, bool faceLeft) => BeginPlayback(randomSeed, faceLeft, true);

        private void BeginPlayback(int randomSeed, bool faceLeft, bool automatic)
        {
            if (texture == null) InitializeTexture();
            bool custom = palette != null && palette.Length == colors.Length;
            if (custom)
                for (int i = 0; i < colors.Length; i++) colors[i] = Pack(palette[i]);
            effect.Reset(randomSeed, custom ? colors : null, Pack(stone));
            spriteRenderer.flipX = faceLeft;
            spriteRenderer.enabled = true;
            elapsed = 0;
            previousFrame = 0;
            RenderAt(0);
            playing = automatic;
        }

        private void InitializeTexture()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            int w = HeavyCleaveEffect.Width, h = HeavyCleaveEffect.Height;
            texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "PixelFX Heavy Cleave", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            };
            var pivot = new Vector2(HeavyCleaveEffect.OriginX / (float)w, 1 - HeavyCleaveEffect.OriginY / (float)h);
            sprite = Sprite.Create(texture, new Rect(0, 0, w, h), pivot, Mathf.Max(1, pixelsPerUnit), 0, SpriteMeshType.FullRect);
            spriteRenderer.sprite = sprite;
        }

        private void Update()
        {
            if (!playing) return;
            elapsed += Time.deltaTime;
            if (elapsed >= ClipDuration)
            {
                Stop();
                return;
            }
            int frame = Mathf.FloorToInt(elapsed * 30);
            if (frame == previousFrame) return;
            previousFrame = frame;
            RenderAt(frame / 30.0);
        }

        public void Stop()
        {
            playing = false;
            if (spriteRenderer != null) spriteRenderer.enabled = false;
        }

        private void OnDisable() => Stop();

        private void OnDestroy()
        {
            Release(sprite);
            Release(texture);
        }

        private static void Release(UnityEngine.Object asset)
        {
            if (asset == null) return;
            if (Application.isPlaying) Destroy(asset);
            else DestroyImmediate(asset);
        }

        // Manual playback lets the sample measure the same simulation frames at each instance count.
        public void BeginManualPlayback(int randomSeed) => BeginPlayback(randomSeed, false, false);

        public void RenderAt(double seconds)
        {
            if (texture == null) throw new InvalidOperationException("Begin playback before rendering.");
            using (Raster.Auto())
            {
                effect.Advance(seconds);
                effect.Render(rgba, true, lightStrength);
            }
            using (Upload.Auto())
            {
                texture.LoadRawTextureData(rgba);
                texture.Apply(false, false);
            }
        }

        private static uint Pack(Color32 c) => (uint)(c.r << 16 | c.g << 8 | c.b);
    }
}

using UnityEngine;

namespace PixelFx.Samples
{
    [RequireComponent(typeof(PixelFxPlayer))]
    public sealed class PixelFxDemo : MonoBehaviour
    {
        [SerializeField] private PixelFxVariant variant;
        private PixelFxPlayer player;
        private PixelFxPlayback current;

        private void Start()
        {
            player = GetComponent<PixelFxPlayer>();
            Play(PixelFxLibrary.HeavyCleave);
        }

        private void OnGUI()
        {
            if (player == null) return;
            GUILayout.BeginArea(new Rect(12, 12, Screen.width - 24, 72));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Heavy Cleave")) Play(PixelFxLibrary.HeavyCleave);
            if (GUILayout.Button("Hit Spark")) Play(PixelFxLibrary.HitSpark);
            if (GUILayout.Button("Parry")) Play(PixelFxLibrary.Parry);
            if (GUILayout.Button("Fireball Throw")) Play(PixelFxLibrary.FireballThrow);
            if (GUILayout.Button("Shuriken")) Play(PixelFxLibrary.Shuriken);
            if (variant != null && GUILayout.Button("Play Assigned Effect"))
            {
                current?.Stop();
                current = player.Play(variant, Context());
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        public void Play(string effect)
        {
            current?.Stop();
            current = player.Play(effect, context: Context());
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

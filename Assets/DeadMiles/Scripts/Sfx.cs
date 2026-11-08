using UnityEngine;

namespace DeadMiles
{
    /// <summary>Synthesised sound effects (no audio files needed) and a small 2D voice pool.</summary>
    public static class Sfx
    {
        const int Rate = 44100;
        static AudioClip engine, impact, splat, pickup, click, cash;
        static AudioSource[] pool;
        static int next;

        public static void Init(Transform parent)
        {
            pool = new AudioSource[10];
            for (int i = 0; i < pool.Length; i++)
            {
                var go = new GameObject("sfx" + i);
                go.transform.SetParent(parent, false);
                pool[i] = go.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
                pool[i].spatialBlend = 0f;
            }
        }

        public static void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (pool == null || clip == null) return;
            var s = pool[next];
            next = (next + 1) % pool.Length;
            s.pitch = pitch;
            s.PlayOneShot(clip, volume);
        }

        static AudioClip Make(string name, float seconds, System.Func<float, float> f, bool loop = false)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate), -1f, 1f);
            if (loop)
            {
                // crossfade the ends so the loop is seamless
                int fade = Rate / 50;
                for (int i = 0; i < fade; i++)
                {
                    float k = i / (float)fade;
                    data[i] = data[i] * k + data[n - fade + i] * (1f - k);
                }
                System.Array.Resize(ref data, n - fade);
                n -= fade;
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Noise(int seed) => Mathf.Sin(seed * 12.9898f + 78.233f) * 43758.5453f % 1f;

        /// <summary>V8-ish rumble: firing pulses at ~45 Hz with harmonics and grit. Pitch is driven by RPM.</summary>
        public static AudioClip EngineLoop()
        {
            if (engine != null) return engine;
            float lp = 0f;
            var rng = new System.Random(7);
            engine = Make("engine", 1.02f, t =>
            {
                float f0 = 44f;
                float phase = t * f0;
                float pulse = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(phase * Mathf.PI * 2f)), 6f);
                float body = Mathf.Sin(phase * Mathf.PI * 2f) * 0.5f + Mathf.Sin(phase * Mathf.PI * 4f) * 0.25f
                             + Mathf.Sin(phase * Mathf.PI * 1f + 0.3f) * 0.3f;
                float n = (float)rng.NextDouble() * 2f - 1f;
                lp += (n - lp) * 0.08f;
                return (pulse * 0.8f + body * 0.35f + lp * 0.5f) * 0.55f;
            }, true);
            return engine;
        }

        public static AudioClip Impact()
        {
            if (impact != null) return impact;
            float lp = 0f;
            var rng = new System.Random(3);
            impact = Make("impact", 0.45f, t =>
            {
                float n = (float)rng.NextDouble() * 2f - 1f;
                lp += (n - lp) * 0.15f;
                float env = Mathf.Exp(-t * 12f);
                float thump = Mathf.Sin(t * 70f * Mathf.PI * 2f) * Mathf.Exp(-t * 18f);
                return (lp * 1.6f * env + thump * 0.9f);
            });
            return impact;
        }

        public static AudioClip Splat()
        {
            if (splat != null) return splat;
            float lp = 0f;
            var rng = new System.Random(5);
            splat = Make("splat", 0.35f, t =>
            {
                float n = (float)rng.NextDouble() * 2f - 1f;
                lp += (n - lp) * 0.35f;
                float env = Mathf.Exp(-t * 14f);
                float thud = Mathf.Sin(t * 95f * Mathf.PI * 2f) * Mathf.Exp(-t * 25f);
                return lp * env * 1.2f + thud * 0.8f;
            });
            return splat;
        }

        public static AudioClip Pickup()
        {
            if (pickup != null) return pickup;
            pickup = Make("pickup", 0.3f, t =>
            {
                float f = t < 0.12f ? 660f : 990f;
                return Mathf.Sin(t * f * Mathf.PI * 2f) * Mathf.Exp(-t * 6f) * 0.5f;
            });
            return pickup;
        }

        public static AudioClip Click()
        {
            if (click != null) return click;
            click = Make("click", 0.06f, t => Mathf.Sin(t * 1800f * Mathf.PI * 2f) * Mathf.Exp(-t * 80f) * 0.5f);
            return click;
        }

        public static AudioClip Cash()
        {
            if (cash != null) return cash;
            cash = Make("cash", 0.5f, t =>
            {
                float a = Mathf.Sin(t * 1320f * Mathf.PI * 2f) * Mathf.Exp(-t * 9f);
                float b = t > 0.08f ? Mathf.Sin(t * 1760f * Mathf.PI * 2f) * Mathf.Exp(-(t - 0.08f) * 9f) : 0f;
                return (a + b) * 0.35f;
            });
            return cash;
        }
    }
}

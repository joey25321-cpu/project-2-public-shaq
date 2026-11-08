using UnityEngine;

namespace DeadMiles
{
    /// <summary>Shared particle systems (one per effect type, emitted into on demand).</summary>
    public static class Fx
    {
        static ParticleSystem dust, smoke, blood, sparks;
        static Transform root;

        public static void Init(Transform parent)
        {
            root = new GameObject("FX").transform;
            root.SetParent(parent, false);
            var soft = Tex.Soft();
            dust = Make("Dust", Mats.Particle("pDust", soft, new Color(0.8f, 0.68f, 0.52f, 0.55f)), 1.6f, 0f, -0.05f, 600, false);
            smoke = Make("Smoke", Mats.Particle("pSmoke", soft, new Color(0.15f, 0.14f, 0.13f, 0.6f)), 2.2f, 0f, -0.08f, 300, false);
            blood = Make("Blood", Mats.Particle("pBlood", soft, new Color(0.45f, 0.02f, 0.02f, 1f)), 1.2f, 1.2f, 0f, 1500, true);
            sparks = Make("Sparks", Mats.Particle("pSparks", soft, new Color(1f, 0.85f, 0.4f, 1f)), 0.35f, 1f, 0f, 400, true);

            var sm = smoke.sizeOverLifetime;
            sm.enabled = true;
            sm.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 2.5f));
            var dm = dust.sizeOverLifetime;
            dm.enabled = true;
            dm.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));
            FadeOut(dust);
            FadeOut(smoke);
            FadeOut(sparks);
        }

        static ParticleSystem Make(string name, Material mat, float life, float gravity, float gravNeg, int max, bool collide)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = life;
            main.gravityModifier = gravity + gravNeg;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.startSpeed = 0f;
            var em = ps.emission;
            em.enabled = false;
            var sh = ps.shape;
            sh.enabled = false;
            if (collide)
            {
                var col = ps.collision;
                col.enabled = true;
                col.type = ParticleSystemCollisionType.World;
                col.mode = ParticleSystemCollisionMode.Collision3D;
                col.quality = ParticleSystemCollisionQuality.Medium;
                col.dampen = 0.7f;
                col.bounce = 0.1f;
                col.lifetimeLoss = 0f;
            }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        static void FadeOut(ParticleSystem ps)
        {
            var c = ps.colorOverLifetime;
            c.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
            c.color = new ParticleSystem.MinMaxGradient(g);
        }

        static void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, float size, float life, Color color, int count = 1)
        {
            if (ps == null) return;
            var ep = new ParticleSystem.EmitParams
            {
                position = pos,
                velocity = vel,
                startSize = size,
                startLifetime = life,
                startColor = color,
                applyShapeToPosition = false
            };
            ps.Emit(ep, count);
        }

        public static void Dust(Vector3 p, Vector3 v)
        {
            Emit(dust, p, v + Random.insideUnitSphere * 0.8f + Vector3.up * 0.6f, Random.Range(0.6f, 1.4f),
                Random.Range(0.8f, 1.6f), Color.white);
        }

        public static void Smoke(Vector3 p)
        {
            Emit(smoke, p, Vector3.up * 1.2f + Random.insideUnitSphere * 0.3f, Random.Range(0.4f, 0.8f),
                Random.Range(1.2f, 2.2f), Color.white);
        }

        public static void Blood(Vector3 p, Vector3 v, int count)
        {
            for (int i = 0; i < count; i++)
                Emit(blood, p + Random.insideUnitSphere * 0.25f, v + Random.insideUnitSphere * 4f + Vector3.up * 2f,
                    Random.Range(0.05f, 0.18f), Random.Range(0.8f, 1.6f), new Color(Random.Range(0.6f, 1f), 1f, 1f, 1f));
        }

        public static void Sparks(Vector3 p, int count)
        {
            for (int i = 0; i < count; i++)
                Emit(sparks, p, Random.insideUnitSphere * 6f + Vector3.up * 2f, Random.Range(0.04f, 0.09f),
                    Random.Range(0.2f, 0.45f), Color.white);
        }

        public static void Burst(Vector3 p, Color c, int count)
        {
            for (int i = 0; i < count; i++)
                Emit(dust, p, Random.insideUnitSphere * 3f + Vector3.up * 2f, Random.Range(0.3f, 0.7f), 0.8f, c);
        }

        /// <summary>Looping rocket flame attached to the car; toggle via emission.enabled.</summary>
        public static ParticleSystem CreateFlame(Transform parent, Vector3 localPos)
        {
            var go = new GameObject("BoostFlame");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.LookRotation(Vector3.back);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 0.25f;
            main.startSpeed = 9f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.45f, 0.1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            var em = ps.emission;
            em.rateOverTime = 160f;
            em.enabled = false;
            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 6f;
            sh.radius = 0.08f;
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            FadeOut(ps);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mats.Particle("pFlame", Tex.Soft(), new Color(1f, 0.7f, 0.3f, 1f));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }
    }
}

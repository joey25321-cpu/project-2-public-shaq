using System.Collections.Generic;
using UnityEngine;

namespace DeadMiles
{
    /// <summary>
    /// A walker built as a 10-bone physics ragdoll (rigidbodies + CharacterJoints).
    /// While alive it is kinematic and shuffles toward the truck; when the truck hits
    /// it, every bone becomes a real rigidbody and momentum is exchanged with the car.
    /// </summary>
    public class Zombie : MonoBehaviour
    {
        class Bone
        {
            public Transform t;
            public Rigidbody rb;
            public Collider col;
            public Quaternion baseLocal;
        }

        readonly List<Bone> bones = new List<Bone>();
        Bone pelvis, chest, head, legL, legR, shinL, shinR, armL, armR;
        CapsuleCollider trigger;
        bool dead;
        float phase, walkSpeed, scale;
        float idleUntil;

        public bool Dead => dead;

        public static Zombie Spawn(Transform parent, Vector3 feetPos, System.Random rng)
        {
            var go = new GameObject("Walker");
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(feetPos, Quaternion.Euler(0f, -90f, 0f)); // face the oncoming truck (-X)
            var z = go.AddComponent<Zombie>();
            z.Build(rng);
            return z;
        }

        void Build(System.Random rng)
        {
            float R() => (float)rng.NextDouble();

            scale = 0.9f + R() * 0.22f;
            bool big = R() < 0.18f;
            float girth = big ? 1.35f : 1f;
            transform.localScale = Vector3.one * scale;
            walkSpeed = 0.7f + R() * 0.9f;
            phase = R() * 10f;

            var skin = Mats.Lit("zskin", Color.white, 0.25f, 0f, Tex.Skin());
            Color[] shirts = { new Color(0.35f, 0.38f, 0.42f), new Color(0.45f, 0.18f, 0.14f), new Color(0.22f, 0.3f, 0.2f), new Color(0.6f, 0.58f, 0.5f), new Color(0.15f, 0.17f, 0.25f) };
            Color[] pants = { new Color(0.18f, 0.22f, 0.32f), new Color(0.3f, 0.26f, 0.2f), new Color(0.12f, 0.12f, 0.12f) };
            int si = rng.Next(shirts.Length), pi = rng.Next(pants.Length);
            var shirt = Mats.Lit("zshirt" + si, shirts[si], 0.15f, 0f, Tex.Cloth());
            var pant = Mats.Lit("zpant" + pi, pants[pi], 0.15f, 0f, Tex.Cloth());
            var blood = Mats.Lit("zblood", new Color(0.28f, 0.03f, 0.02f), 0.6f, 0f);

            var root = transform;
            var capsule = Geo.Prim(PrimitiveType.Capsule);
            var cube = Geo.Prim(PrimitiveType.Cube);
            var sphere = Geo.Prim(PrimitiveType.Sphere);

            // Bones are posed in root space (feet at y=0, facing +Z), then nested.
            pelvis = MakeBox(root, null, "pelvis", new Vector3(0f, 0.95f, 0f), Quaternion.identity,
                new Vector3(0.36f * girth, 0.22f, 0.24f * girth), Vector3.zero, 12f * girth, pant, cube);
            chest = MakeBox(root, pelvis, "chest", new Vector3(0f, 1.06f, 0f), Quaternion.Euler(12f, 0f, 0f),
                new Vector3(0.42f * girth, 0.5f, 0.26f * girth), new Vector3(0f, 0.25f, 0f), 18f * girth, shirt, cube);
            head = MakeSphere(root, chest, "head", new Vector3(0f, 1.55f, 0.1f), Quaternion.Euler(20f, 0f, 0f), 0.25f,
                new Vector3(0f, 0.13f, 0f), 5f, skin, sphere);

            // arms reach forward, zombie style
            var armDir = new Vector3(0f, -0.35f, 1f).normalized;
            var foreDir = new Vector3(0f, -0.15f, 1f).normalized;
            var armRot = Quaternion.FromToRotation(Vector3.down, armDir);
            var foreRot = Quaternion.FromToRotation(Vector3.down, foreDir);
            float sx = 0.26f * girth;
            var shoulderL = new Vector3(-sx, 1.48f, 0.08f);
            var shoulderR = new Vector3(sx, 1.48f, 0.08f);
            armL = MakeLimb(root, chest, "armL", shoulderL, armRot, 0.3f, 0.065f, 2.2f, shirt, capsule);
            armR = MakeLimb(root, chest, "armR", shoulderR, armRot, 0.3f, 0.065f, 2.2f, shirt, capsule);
            MakeLimb(root, armL, "foreL", shoulderL + armDir * 0.3f, foreRot, 0.3f, 0.055f, 1.5f, skin, capsule);
            MakeLimb(root, armR, "foreR", shoulderR + armDir * 0.3f, foreRot, 0.3f, 0.055f, 1.5f, skin, capsule);

            var legRot = Quaternion.identity;
            legL = MakeLimb(root, pelvis, "thighL", new Vector3(-0.1f * girth, 0.9f, 0f), legRot, 0.43f, 0.085f * girth, 7f, pant, capsule);
            legR = MakeLimb(root, pelvis, "thighR", new Vector3(0.1f * girth, 0.9f, 0f), legRot, 0.43f, 0.085f * girth, 7f, pant, capsule);
            shinL = MakeLimb(root, legL, "shinL", new Vector3(-0.1f * girth, 0.47f, 0f), legRot, 0.45f, 0.07f, 4f, pant, capsule);
            shinR = MakeLimb(root, legR, "shinR", new Vector3(0.1f * girth, 0.47f, 0f), legRot, 0.45f, 0.07f, 4f, pant, capsule);

            // gore details & eyes (visual only)
            Geo.Part(chest.t, cube, blood, new Vector3(0.08f, 0.3f, 0.135f * girth), new Vector3(0.18f, 0.2f, 0.01f), Quaternion.Euler(0, 0, 25f), "stain");
            var eye = Mats.Emissive("zeye", new Color(0.9f, 0.85f, 0.6f), new Color(0.6f, 0.5f, 0.2f));
            Geo.Part(head.t, sphere, eye, new Vector3(-0.05f, 0.15f, 0.11f), Vector3.one * 0.035f, Quaternion.identity, "eye", false);
            Geo.Part(head.t, sphere, eye, new Vector3(0.05f, 0.15f, 0.11f), Vector3.one * 0.035f, Quaternion.identity, "eye", false);

            foreach (var b in bones) b.baseLocal = b.t.localRotation;

            // one cheap trigger for the alive state
            var rootRb = gameObject.AddComponent<Rigidbody>();
            rootRb.isKinematic = true;
            trigger = gameObject.AddComponent<CapsuleCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.95f, 0.1f);
            trigger.height = 1.9f;
            trigger.radius = 0.45f;

            SetRagdoll(false);
        }

        Bone Add(Transform root, Bone parent, string name, Vector3 pos, Quaternion rot, float mass)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = Vector3.one;
            if (parent != null) go.transform.SetParent(parent.t, true);
            var b = new Bone { t = go.transform, rb = go.AddComponent<Rigidbody>() };
            b.rb.mass = mass;
            b.rb.SetDamping(0.05f, 0.5f);
            b.rb.interpolation = RigidbodyInterpolation.None;
            b.rb.maxDepenetrationVelocity = 3f;
            bones.Add(b);
            if (parent != null)
            {
                var j = go.AddComponent<CharacterJoint>();
                j.connectedBody = parent.rb;
                j.anchor = Vector3.zero;
                j.axis = Vector3.up;            // twist along the bone
                j.swingAxis = Vector3.right;    // bend forward / back
                j.lowTwistLimit = new SoftJointLimit { limit = -25f };
                j.highTwistLimit = new SoftJointLimit { limit = 25f };
                j.swing1Limit = new SoftJointLimit { limit = 60f };
                j.swing2Limit = new SoftJointLimit { limit = 30f };
                j.enableProjection = true;
                j.projectionDistance = 0.05f;
                j.enablePreprocessing = false;
            }
            return b;
        }

        Bone MakeBox(Transform root, Bone parent, string name, Vector3 pos, Quaternion rot, Vector3 size, Vector3 center,
            float mass, Material m, Mesh mesh)
        {
            var b = Add(root, parent, name, pos, rot, mass);
            var c = b.t.gameObject.AddComponent<BoxCollider>();
            c.center = center;
            c.size = size;
            b.col = c;
            Geo.Part(b.t, mesh, m, center, size, Quaternion.identity, name + "_mesh");
            return b;
        }

        Bone MakeSphere(Transform root, Bone parent, string name, Vector3 pos, Quaternion rot, float diameter, Vector3 center,
            float mass, Material m, Mesh mesh)
        {
            var b = Add(root, parent, name, pos, rot, mass);
            var c = b.t.gameObject.AddComponent<SphereCollider>();
            c.center = center;
            c.radius = diameter * 0.5f;
            b.col = c;
            Geo.Part(b.t, mesh, m, center, new Vector3(diameter * 0.9f, diameter, diameter), Quaternion.identity, name + "_mesh");
            return b;
        }

        /// <summary>A limb whose local -Y runs from the joint along the bone.</summary>
        Bone MakeLimb(Transform root, Bone parent, string name, Vector3 joint, Quaternion rot, float length, float radius,
            float mass, Material m, Mesh capsule)
        {
            var b = Add(root, parent, name, joint, rot, mass);
            var c = b.t.gameObject.AddComponent<CapsuleCollider>();
            c.direction = 1;
            c.center = new Vector3(0f, -length * 0.5f, 0f);
            c.height = length + radius * 2f;
            c.radius = radius;
            b.col = c;
            Geo.Part(b.t, capsule, m, c.center, new Vector3(radius * 2f, (length + radius * 2f) * 0.5f, radius * 2f),
                Quaternion.identity, name + "_mesh");
            return b;
        }

        void SetRagdoll(bool on)
        {
            foreach (var b in bones)
            {
                b.rb.isKinematic = !on;
                b.rb.detectCollisions = on;
                b.col.enabled = on;
                b.rb.interpolation = on ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
            }
        }

        // ------------------------------------------------------------------ alive behaviour

        void Update()
        {
            if (dead) return;
            var gm = GameManager.I;
            if (gm == null || gm.Car == null) return;

            float carX = gm.Car.transform.position.x;
            float dx = transform.position.x - carX;
            if (dx > 70f || dx < -30f) return; // off screen: don't animate

            float t = Time.time;
            bool walking = gm.State == GameState.Driving && dx > 2f && t > idleUntil;
            if (walking)
            {
                var p = transform.position;
                p.x -= walkSpeed * Time.deltaTime;
                p.y = WorldGen.Height(p.x);
                transform.position = p;
            }

            float speed = walking ? walkSpeed * 4.5f : 1.2f;
            float s = Mathf.Sin(t * speed + phase);
            float sway = walking ? 1f : 0.25f;
            legL.t.localRotation = legL.baseLocal * Quaternion.Euler(s * 28f * sway, 0f, 0f);
            legR.t.localRotation = legR.baseLocal * Quaternion.Euler(-s * 28f * sway, 0f, 0f);
            shinL.t.localRotation = shinL.baseLocal * Quaternion.Euler(-Mathf.Max(0f, -s) * 35f * sway, 0f, 0f);
            shinR.t.localRotation = shinR.baseLocal * Quaternion.Euler(-Mathf.Max(0f, s) * 35f * sway, 0f, 0f);
            chest.t.localRotation = chest.baseLocal * Quaternion.Euler(0f, s * 6f, Mathf.Sin(t * speed * 0.5f + phase) * 6f);
            head.t.localRotation = head.baseLocal * Quaternion.Euler(Mathf.Sin(t * 1.3f + phase) * 8f, Mathf.Sin(t * 0.7f + phase) * 20f, 0f);
            armL.t.localRotation = armL.baseLocal * Quaternion.Euler(Mathf.Sin(t * 2.1f + phase) * 10f, 0f, 0f);
            armR.t.localRotation = armR.baseLocal * Quaternion.Euler(Mathf.Sin(t * 2.3f + phase + 1f) * 10f, 0f, 0f);
            pelvis.t.localPosition = new Vector3(0f, 0.95f + Mathf.Abs(s) * 0.03f * sway, 0f);
        }

        void OnTriggerEnter(Collider other)
        {
            if (dead) return;
            var car = other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<CarController>() : null;
            if (car == null || !car.released) return;
            Die(car);
        }

        void Die(CarController car)
        {
            dead = true;
            trigger.enabled = false;
            var rootRb = GetComponent<Rigidbody>();
            if (rootRb != null) rootRb.detectCollisions = false;

            Vector3 carVel = car.Body.GetVel();
            float impact = carVel.magnitude;
            SetRagdoll(true);
            foreach (var b in bones)
            {
                var v = carVel * Random.Range(0.85f, 1.25f) + Vector3.up * Random.Range(1.5f, 2f + impact * 0.18f);
                v.z += Random.Range(-1.5f, 1.5f);
                b.rb.SetVel(v);
                b.rb.angularVelocity = Random.insideUnitSphere * (4f + impact * 0.3f);
            }

            // the truck loses a little momentum and takes a little damage
            car.Body.SetVel(carVel * (1f - Mathf.Clamp(0.012f * scale, 0f, 0.03f)));
            car.TakeDamage(1.2f + impact * 0.05f);

            var hitPoint = chest.t.position;
            Fx.Blood(hitPoint, carVel * 0.4f, Mathf.Clamp((int)(impact * 3f), 10, 60));
            Sfx.Play(Sfx.Splat(), Mathf.Clamp01(0.4f + impact / 25f), Random.Range(0.8f, 1.15f));
            GameManager.I?.OnZombieKilled(hitPoint);

            Destroy(gameObject, 10f);
        }
    }
}

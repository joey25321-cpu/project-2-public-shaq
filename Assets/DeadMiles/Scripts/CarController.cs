using UnityEngine;

namespace DeadMiles
{
    /// <summary>
    /// Physically simulated truck: real rigidbody mass, WheelCollider suspension and tyre
    /// friction, engine torque curve, air control, fuel, armour and a rocket booster.
    /// The car is locked to the X/Y plane (2.5D side view) but everything else is full 3D physics.
    /// Local axes: +Z forward (world +X), +Y up, +X right (world -Z).
    /// </summary>
    public class CarController : MonoBehaviour
    {
        public static readonly Quaternion BaseRot = Quaternion.Euler(0f, 90f, 0f);

        public CarStats stats;
        public Rigidbody Body { get; private set; }

        // live state
        public float fuel, health, boost;
        public bool released;
        public bool Grounded { get; private set; }
        public float ForwardSpeed { get; private set; }
        public bool Boosting { get; private set; }
        public float Rpm01 { get; private set; }

        // input (set by GameManager each frame)
        [System.NonSerialized] public float inThrottle, inBrake, inTilt;
        [System.NonSerialized] public bool inBoost;

        readonly WheelCollider[] wheels = new WheelCollider[4];
        readonly Transform[] wheelVis = new Transform[4];
        ParticleSystem boostFlame;
        AudioSource engine;
        float lastHitTime;

        const float WheelRadius = 0.42f;
        const float Wheelbase = 1.42f;   // half wheelbase
        const float Track = 0.84f;       // half track

        public static CarController Create(Vector3 position, CarStats st)
        {
            var go = new GameObject("Truck");
            go.transform.SetPositionAndRotation(position, BaseRot);
            var c = go.AddComponent<CarController>();
            c.Build(st);
            return c;
        }

        // ------------------------------------------------------------------ build

        void Build(CarStats st)
        {
            stats = st;
            fuel = st.fuelCapacity;
            health = st.maxHealth;
            boost = st.boostCapacity;

            Body = gameObject.AddComponent<Rigidbody>();
            Body.mass = st.mass;
            Body.SetDamping(0.02f, 0.4f);
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.isKinematic = true;

            // chassis colliders
            var lower = gameObject.AddComponent<BoxCollider>();
            lower.center = new Vector3(0f, 0.82f, 0.05f);
            lower.size = new Vector3(1.8f, 0.62f, 4.3f);
            Compat.SetFriction(lower, 0.45f);
            var cabin = gameObject.AddComponent<BoxCollider>();
            cabin.center = new Vector3(0f, 1.5f, -0.15f);
            cabin.size = new Vector3(1.65f, 0.8f, 1.7f);
            Compat.SetFriction(cabin, 0.45f);

            Body.centerOfMass = new Vector3(0f, 0.45f, 0.15f);

            // wheels: FL, FR, RL, RR
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                float x = (i % 2 == 0) ? -Track : Track;
                float z = front ? Wheelbase : -Wheelbase;
                var wgo = new GameObject(front ? "WheelF" : "WheelR");
                wgo.transform.SetParent(transform, false);
                wgo.transform.localPosition = new Vector3(x, 0.62f, z);
                var w = wgo.AddComponent<WheelCollider>();
                w.radius = WheelRadius;
                w.mass = 22f;
                w.wheelDampingRate = 0.3f;
                w.suspensionDistance = st.travel;
                w.forceAppPointDistance = 0.12f;
                var spring = new JointSpring { spring = st.spring, damper = st.damper, targetPosition = 0.45f };
                w.suspensionSpring = spring;
                w.forwardFriction = Friction(0.35f, 1.0f, 0.9f, 0.6f, st.grip);
                w.sidewaysFriction = Friction(0.25f, 1.0f, 0.6f, 0.75f, st.grip);
                wheels[i] = w;
            }
            wheels[0].ConfigureVehicleSubsteps(5f, 12, 15);

            BuildVisuals(st);
            BuildAudio();
        }

        static WheelFrictionCurve Friction(float exSlip, float exVal, float asSlip, float asVal, float stiff) =>
            new WheelFrictionCurve
            {
                extremumSlip = exSlip, extremumValue = exVal,
                asymptoteSlip = asSlip, asymptoteValue = asVal,
                stiffness = stiff
            };

        void BuildVisuals(CarStats st)
        {
            var root = new GameObject("Visual").transform;
            root.SetParent(transform, false);

            var paint = Mats.Lit("carPaint", new Color(0.78f, 0.36f, 0.1f), 0.55f, 0.15f,
                Tex.RustPaint(), Tex.RustPaintNormal(), 0.6f);
            var trim = Mats.Lit("carTrim", new Color(0.55f, 0.55f, 0.55f), 0.35f, 0.8f, Tex.DarkMetal());
            var glass = Mats.Lit("carGlass", new Color(0.05f, 0.07f, 0.08f), 0.95f, 0.1f);
            var chrome = Mats.Lit("chrome", new Color(0.85f, 0.85f, 0.85f), 0.85f, 1f);
            var tyre = Mats.Lit("tyre", new Color(0.09f, 0.09f, 0.09f), 0.15f, 0f, Tex.DarkMetal());
            var head = Mats.Emissive("headlight", new Color(1f, 0.95f, 0.8f), new Color(2.5f, 2.2f, 1.6f));
            var tail = Mats.Emissive("taillight", new Color(0.6f, 0.05f, 0.03f), new Color(1.6f, 0.1f, 0.05f));

            // body
            Geo.Box(root, paint, new Vector3(0f, 0.85f, 0.05f), new Vector3(1.86f, 0.55f, 4.3f), default, "chassis");
            Geo.Box(root, paint, new Vector3(0f, 1.17f, 1.4f), new Vector3(1.8f, 0.2f, 1.45f), new Vector3(3f, 0f, 0f), "hood");
            Geo.Box(root, paint, new Vector3(0f, 1.25f, -0.15f), new Vector3(1.72f, 0.35f, 1.7f), default, "cabinLow");
            Geo.Box(root, glass, new Vector3(0f, 1.62f, -0.2f), new Vector3(1.66f, 0.46f, 1.45f), default, "windows");
            Geo.Box(root, glass, new Vector3(0f, 1.6f, 0.58f), new Vector3(1.62f, 0.55f, 0.06f), new Vector3(-28f, 0f, 0f), "windshield");
            Geo.Box(root, paint, new Vector3(0f, 1.9f, -0.25f), new Vector3(1.74f, 0.09f, 1.5f), default, "roof");
            // bed
            Geo.Box(root, paint, new Vector3(-0.9f, 1.28f, -1.45f), new Vector3(0.07f, 0.42f, 1.7f), default, "bedL");
            Geo.Box(root, paint, new Vector3(0.9f, 1.28f, -1.45f), new Vector3(0.07f, 0.42f, 1.7f), default, "bedR");
            Geo.Box(root, paint, new Vector3(0f, 1.28f, -2.17f), new Vector3(1.86f, 0.42f, 0.07f), default, "tailgate");
            // bumpers & lights
            Geo.Box(root, trim, new Vector3(0f, 0.68f, 2.22f), new Vector3(1.98f, 0.24f, 0.22f), default, "bumperF");
            Geo.Box(root, trim, new Vector3(0f, 0.68f, -2.22f), new Vector3(1.98f, 0.22f, 0.18f), default, "bumperR");
            Geo.Box(root, chrome, new Vector3(0f, 0.98f, 2.19f), new Vector3(1.2f, 0.22f, 0.05f), default, "grille");
            Geo.Box(root, head, new Vector3(-0.68f, 1.0f, 2.2f), new Vector3(0.32f, 0.16f, 0.05f), default, "lightL");
            Geo.Box(root, head, new Vector3(0.68f, 1.0f, 2.2f), new Vector3(0.32f, 0.16f, 0.05f), default, "lightR");
            Geo.Box(root, tail, new Vector3(-0.82f, 1.05f, -2.2f), new Vector3(0.18f, 0.22f, 0.05f), default, "tailL");
            Geo.Box(root, tail, new Vector3(0.82f, 1.05f, -2.2f), new Vector3(0.18f, 0.22f, 0.05f), default, "tailR");
            // wheel arches (dark)
            foreach (float z in new[] { Wheelbase, -Wheelbase })
                foreach (float x in new[] { -0.94f, 0.94f })
                    Geo.Box(root, trim, new Vector3(x, 1.08f, z), new Vector3(0.06f, 0.12f, 1.1f), default, "arch");

            // armour upgrades
            if (st.armorLevel >= 1)
            {
                var plate = Mats.Lit("plate", new Color(0.35f, 0.35f, 0.33f), 0.3f, 0.85f, Tex.DarkMetal());
                Geo.Box(root, plate, new Vector3(-0.95f, 0.9f, 0.1f), new Vector3(0.05f, 0.4f, 2.6f), default, "doorPlateL");
                Geo.Box(root, plate, new Vector3(0.95f, 0.9f, 0.1f), new Vector3(0.05f, 0.4f, 2.6f), default, "doorPlateR");
                if (st.armorLevel >= 2)
                {
                    // ram wedge
                    Geo.Box(root, plate, new Vector3(0f, 0.75f, 2.45f), new Vector3(2.0f, 0.55f, 0.12f), new Vector3(-25f, 0f, 0f), "ram");
                    for (int i = -3; i <= 3; i++)
                        Geo.Part(root, PrimitiveType.Cylinder, chrome, new Vector3(i * 0.28f, 0.75f, 2.62f),
                            new Vector3(0.06f, 0.18f, 0.06f), Quaternion.Euler(80f, 0f, 0f), "spike");
                }
                if (st.armorLevel >= 3)
                    for (int i = 0; i < 5; i++)
                        Geo.Box(root, plate, new Vector3(-0.64f + i * 0.32f, 1.6f, 0.65f),
                            new Vector3(0.05f, 0.6f, 0.05f), new Vector3(-28f, 0f, 0f), "windowBar");
                if (st.armorLevel >= 4)
                    Geo.Box(root, plate, new Vector3(0f, 1.98f, -0.25f), new Vector3(1.8f, 0.08f, 1.6f), default, "roofPlate");
            }

            // booster
            if (st.boosterLevel >= 1)
            {
                var rocket = Mats.Lit("rocket", new Color(0.82f, 0.82f, 0.8f), 0.6f, 0.7f, Tex.DarkMetal());
                var stripe = Mats.Lit("rocketStripe", new Color(0.7f, 0.08f, 0.05f), 0.5f, 0.2f);
                float r = 0.22f + st.boosterLevel * 0.025f;
                Geo.Part(root, PrimitiveType.Cylinder, rocket, new Vector3(0f, 1.45f, -1.35f), new Vector3(r * 2f, 0.75f, r * 2f),
                    Quaternion.Euler(90f, 0f, 0f), "rocket");
                Geo.Part(root, PrimitiveType.Cylinder, stripe, new Vector3(0f, 1.45f, -1.0f), new Vector3(r * 2.05f, 0.08f, r * 2.05f),
                    Quaternion.Euler(90f, 0f, 0f), "stripe");
                Geo.Part(root, PrimitiveType.Sphere, rocket, new Vector3(0f, 1.45f, -0.6f), new Vector3(r * 2f, r * 2f, r * 3f),
                    Quaternion.identity, "nose");
                Geo.Part(root, PrimitiveType.Cylinder, trim, new Vector3(0f, 1.45f, -2.15f), new Vector3(r * 1.6f, 0.08f, r * 1.6f),
                    Quaternion.Euler(90f, 0f, 0f), "nozzle");
                boostFlame = Fx.CreateFlame(transform, new Vector3(0f, 1.45f, -2.3f));
            }

            // wheels
            for (int i = 0; i < 4; i++)
            {
                var pivot = new GameObject("wheelVis").transform;
                pivot.SetParent(transform, false);
                Geo.Part(pivot, PrimitiveType.Cylinder, tyre, Vector3.zero, new Vector3(WheelRadius * 2f, 0.16f, WheelRadius * 2f),
                    Quaternion.Euler(0f, 0f, 90f), "tyre");
                Geo.Part(pivot, PrimitiveType.Cylinder, chrome, Vector3.zero, new Vector3(0.48f, 0.17f, 0.48f),
                    Quaternion.Euler(0f, 0f, 90f), "rim");
                Geo.Part(pivot, PrimitiveType.Cube, trim, Vector3.zero, new Vector3(0.36f, 0.1f, 0.1f), Quaternion.identity, "spoke");
                wheelVis[i] = pivot;
            }
            SyncWheels();
        }

        void BuildAudio()
        {
            engine = gameObject.AddComponent<AudioSource>();
            engine.clip = Sfx.EngineLoop();
            engine.loop = true;
            engine.spatialBlend = 0f;
            engine.volume = 0.3f;
            engine.playOnAwake = false;
            engine.Play();
        }

        // ------------------------------------------------------------------ control

        public void Release()
        {
            released = true;
            Body.isKinematic = false;
            Body.SetVel(Vector3.zero);
            Body.angularVelocity = Vector3.zero;
        }

        public void Freeze()
        {
            released = false;
            Body.isKinematic = true;
        }

        public void AddFuel(float fraction) => fuel = Mathf.Min(stats.fuelCapacity, fuel + stats.fuelCapacity * fraction);

        public void TakeDamage(float amount)
        {
            if (!released) return;
            health -= amount * stats.damageScale;
        }

        void FixedUpdate()
        {
            if (!released || Body.isKinematic)
            {
                foreach (var w in wheels) { w.motorTorque = 0f; w.brakeTorque = 1e5f; }
                return;
            }

            float dt = Time.fixedDeltaTime;
            var vel = Body.GetVel();
            ForwardSpeed = Vector3.Dot(vel, transform.forward);

            Grounded = false;
            foreach (var w in wheels) if (w.isGrounded) { Grounded = true; break; }

            bool hasFuel = fuel > 0f;
            float throttle = hasFuel ? Mathf.Clamp01(inThrottle) : 0f;
            float brakeIn = Mathf.Clamp01(inBrake);

            // engine: torque falls off towards top speed
            float speed01 = Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / stats.topSpeed);
            float drive = throttle * stats.torque * (1f - speed01 * speed01);
            float brakeTorque = 0f;

            if (brakeIn > 0f)
            {
                if (ForwardSpeed > 1.5f) brakeTorque = 3200f * brakeIn;
                else if (hasFuel) drive = -stats.torque * 0.45f * brakeIn * Mathf.Clamp01(1f + ForwardSpeed / 7f); // reverse
            }
            else if (throttle < 0.01f) brakeTorque = 40f; // rolling resistance

            for (int i = 0; i < 4; i++)
            {
                wheels[i].motorTorque = drive * 0.25f;
                wheels[i].brakeTorque = brakeTorque;
            }

            // lean / air control (torque about world Z)
            float tilt = Mathf.Clamp(inTilt, -1f, 1f);
            float tiltAccel = Grounded ? 1.6f : 5.5f;
            Body.AddTorque(new Vector3(0f, 0f, tilt * tiltAccel), ForceMode.Acceleration);
            if (!Grounded && Mathf.Abs(tilt) < 0.1f)
            {
                var av = Body.angularVelocity;
                av.z *= 1f - 0.6f * dt;
                Body.angularVelocity = av;
            }

            // rocket booster
            Boosting = inBoost && boost > 0f;
            if (Boosting)
            {
                Body.AddForce(transform.forward * stats.boostAccel, ForceMode.Acceleration);
                boost = Mathf.Max(0f, boost - dt);
            }

            // fuel burn
            if (hasFuel) fuel = Mathf.Max(0f, fuel - dt * (0.18f + 0.82f * throttle));

            Rpm01 = Mathf.Lerp(Rpm01, GearRpm(speed01, throttle), dt * 8f);

            ConstrainToPlane();
        }

        static float GearRpm(float speed01, float throttle)
        {
            const int gears = 4;
            float g = speed01 * gears;
            float within = g - Mathf.Floor(g);
            if (speed01 >= 0.999f) within = 1f;
            return Mathf.Clamp01(0.2f + within * 0.65f * (0.4f + throttle * 0.6f) + throttle * 0.15f);
        }

        /// <summary>Keeps the truck on the side-view plane: no Z drift, no yaw or roll.</summary>
        void ConstrainToPlane()
        {
            var v = Body.GetVel();
            v.z = 0f;
            Body.SetVel(v);
            var av = Body.angularVelocity;
            av.x = 0f; av.y = 0f;
            Body.angularVelocity = av;

            var p = Body.position;
            if (Mathf.Abs(p.z) > 0.001f) { p.z = 0f; Body.position = p; }

            var fwd = Body.rotation * Vector3.forward;
            var up = Body.rotation * Vector3.up;
            if (Mathf.Abs(fwd.z) > 0.002f || Mathf.Abs(up.z) > 0.002f)
            {
                var f = new Vector3(fwd.x, fwd.y, 0f);
                if (f.sqrMagnitude < 1e-4f) return;
                f.Normalize();
                var n = new Vector3(-f.y, f.x, 0f);          // in-plane perpendicular
                if (Vector3.Dot(n, up) < 0f) n = -n;          // keep upside-down if we are upside-down
                Body.rotation = Quaternion.LookRotation(f, n);
            }
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            SyncWheels();

            if (engine != null)
            {
                float target = released ? 0.55f + Rpm01 * 1.25f : 0.5f;
                engine.pitch = Mathf.Lerp(engine.pitch, target, Time.deltaTime * 6f);
                engine.volume = released ? 0.22f + inThrottle * 0.18f + (Boosting ? 0.1f : 0f) : 0.15f;
                if (fuel <= 0f && released) engine.volume = Mathf.Lerp(engine.volume, 0f, Time.deltaTime);
            }

            if (boostFlame != null)
            {
                var em = boostFlame.emission;
                em.enabled = Boosting;
            }

            if (released)
            {
                // wheel dust
                if (Grounded && Mathf.Abs(ForwardSpeed) > 4f && Random.value < Mathf.Abs(ForwardSpeed) * 0.02f)
                {
                    var w = wheels[Random.Range(2, 4)];
                    if (w.GetGroundHit(out var hit))
                        Fx.Dust(hit.point + Vector3.up * 0.1f, -transform.forward * Mathf.Abs(ForwardSpeed) * 0.15f);
                }
                // smoke when badly damaged
                if (health < stats.maxHealth * 0.35f && Random.value < 0.3f)
                    Fx.Smoke(transform.TransformPoint(new Vector3(0f, 1.3f, 1.6f)));
            }
        }

        void SyncWheels()
        {
            for (int i = 0; i < 4; i++)
            {
                if (wheels[i] == null || wheelVis[i] == null) continue;
                wheels[i].GetWorldPose(out var pos, out var rot);
                wheelVis[i].SetPositionAndRotation(pos, rot);
            }
        }

        void OnCollisionEnter(Collision c)
        {
            if (!released) return;
            if (c.collider.GetComponentInParent<Zombie>() != null) return; // zombies handle themselves

            float dv = c.impulse.magnitude / Body.mass;
            if (dv > 3.5f && Time.time - lastHitTime > 0.15f)
            {
                lastHitTime = Time.time;
                TakeDamage((dv - 3.5f) * 7f);
                var p = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
                Fx.Sparks(p, Mathf.Clamp((int)(dv * 4f), 4, 30));
                Sfx.Play(Sfx.Impact(), Mathf.Clamp01(dv / 10f), Random.Range(0.85f, 1.1f));
                GameManager.I?.Shake(Mathf.Clamp01(dv / 12f));
            }
        }

        public bool UpsideDown => transform.up.y < -0.1f;
    }
}

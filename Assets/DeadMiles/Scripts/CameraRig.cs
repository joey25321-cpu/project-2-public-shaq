using UnityEngine;

namespace DeadMiles
{
    /// <summary>Side-on follow camera with speed-based look-ahead and zoom, plus a garage orbit.</summary>
    public class CameraRig : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        Transform target;
        CarController car;
        bool garage;
        Vector3 vel;
        float shake;
        float orbit;

        public static CameraRig Create()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1500f;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            go.AddComponent<AudioListener>();
            var rig = go.AddComponent<CameraRig>();
            rig.Cam = cam;
            return rig;
        }

        public void Garage(CarController c, bool snap = true)
        {
            car = c; target = c.transform; garage = true;
            if (snap) { orbit = 0f; Snap(); }
        }

        public void Follow(CarController c)
        {
            car = c; target = c.transform; garage = false;
        }

        public void Shake(float amount) => shake = Mathf.Max(shake, amount);

        void Snap()
        {
            if (target == null) return;
            ComputeGarage(out var p, out var look);
            transform.position = p;
            transform.rotation = Quaternion.LookRotation(look - p);
        }

        void ComputeGarage(out Vector3 pos, out Vector3 look)
        {
            var c = target.position;
            float a = -0.55f + Mathf.Sin(orbit * 0.15f) * 0.35f;
            pos = c + new Vector3(Mathf.Sin(a) * 9f, 2.6f, -Mathf.Cos(a) * 9f);
            // keep the truck on the left third, menu on the right
            look = c + new Vector3(0f, 1.0f, 0f) + transform.right * 2.6f;
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;

            if (garage)
            {
                orbit += dt;
                ComputeGarage(out var gp, out var glook);
                transform.position = Vector3.SmoothDamp(transform.position, gp, ref vel, 0.6f);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(glook - transform.position), dt * 3f);
                return;
            }

            float speed = car != null && car.Body != null ? car.Body.GetVel().x : 0f;
            float ahead = Mathf.Clamp(speed * 0.38f, -3f, 11f);
            var c = target.position;
            var look = new Vector3(c.x + ahead, c.y + 1.2f, 0f);
            float dist = 17f + Mathf.Clamp(Mathf.Abs(speed), 0f, 45f) * 0.22f;
            var desired = look + new Vector3(0f, 2.8f + Mathf.Abs(speed) * 0.025f, -dist);

            // never sink into the embankment in front of the road
            float ground = WorldGen.GroundHeight(desired.x, desired.z) + 1.5f;
            if (desired.y < ground) desired.y = ground;

            transform.position = Vector3.SmoothDamp(transform.position, desired, ref vel, 0.18f);
            var rot = Quaternion.LookRotation(look - transform.position);

            if (shake > 0.001f)
            {
                rot *= Quaternion.Euler(Random.Range(-1f, 1f) * shake * 2f, Random.Range(-1f, 1f) * shake * 2f, 0f);
                shake = Mathf.MoveTowards(shake, 0f, dt * 2.5f);
            }
            transform.rotation = rot;
        }
    }
}

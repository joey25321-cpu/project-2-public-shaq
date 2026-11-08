using System.Collections.Generic;
using UnityEngine;
#if UNITY_6000_0_OR_NEWER
using PhysMat = UnityEngine.PhysicsMaterial;
using PhysMatCombine = UnityEngine.PhysicsMaterialCombine;
#else
using PhysMat = UnityEngine.PhysicMaterial;
using PhysMatCombine = UnityEngine.PhysicMaterialCombine;
#endif

namespace DeadMiles
{
    /// <summary>
    /// Small helpers that hide API names that changed between Unity versions,
    /// so the project compiles on Unity 6 and on 2022 LTS.
    /// </summary>
    public static class Compat
    {
        public static Vector3 GetVel(this Rigidbody rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        public static void SetVel(this Rigidbody rb, Vector3 v)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
        }

        public static void SetDamping(this Rigidbody rb, float linear, float angular)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = linear;
            rb.angularDamping = angular;
#else
            rb.drag = linear;
            rb.angularDrag = angular;
#endif
        }

        static readonly Dictionary<int, PhysMat> physMats = new Dictionary<int, PhysMat>();

        /// <summary>Assigns a cached physics material with the given friction and bounciness.</summary>
        public static void SetFriction(Collider c, float friction, float bounce = 0f)
        {
            int key = Mathf.RoundToInt(friction * 100f) * 1000 + Mathf.RoundToInt(bounce * 100f);
            if (!physMats.TryGetValue(key, out var m) || m == null)
            {
                m = new PhysMat("pm_" + key)
                {
                    dynamicFriction = friction,
                    staticFriction = friction,
                    bounciness = bounce,
                    frictionCombine = PhysMatCombine.Average,
                    bounceCombine = PhysMatCombine.Average
                };
                physMats[key] = m;
            }
            c.sharedMaterial = m;
        }
    }
}

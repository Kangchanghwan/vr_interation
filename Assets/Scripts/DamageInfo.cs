using UnityEngine;

namespace DamageSystem
{

    public enum DamageType
    {
        Explosion, Shoot
    }
    public class DamageInfo
    {
        public float dmg = 0.0f;
        public Vector3 hitDir = Vector3.zero;
        public Vector3 hitPoint = Vector3.zero;
        public float explosionRadius = 0.0f;
        public float upwardsModifier = 0.0f;
        public float hitForce = 0.0f;
        public ForceMode forceMode = ForceMode.Impulse;
        public DamageType damageType = DamageType.Shoot;
        public bool canDismember = false;
        public GameObject sender = null;

        public DamageInfo() { }
        
    }
}


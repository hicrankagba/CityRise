using System.Collections.Generic;
using UnityEngine;

namespace CityRise.Life
{
    /// <summary>
    /// Dükkan içinde müşteri: boş noktalar arasında (raflara çarpmadan) dolaşır, arada durup bakar,
    /// bazen kasaya gider. Kasiyer için yalnızca hafif sallanma (idleOnly).
    /// </summary>
    public class InteriorWanderer : MonoBehaviour
    {
        public List<Vector3> spots = new List<Vector3>();
        public Vector3 register;
        public bool idleOnly;
        public float speed = 1.0f;

        Transform body;
        Vector3 goal;
        float pause, phase;
        System.Random rng;

        void Start()
        {
            body = transform.childCount > 0 ? transform.GetChild(0) : transform;
            rng = new System.Random(Random.Range(0, int.MaxValue));
            phase = (float)rng.NextDouble() * 10f;
            goal = transform.localPosition;
            pause = (float)rng.NextDouble() * 2f;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            if (idleOnly || spots.Count == 0)
            {
                phase += dt * 2f;
                body.localRotation = Quaternion.Euler(0, Mathf.Sin(phase) * 12f, 0);
                return;
            }
            if (pause > 0)
            {
                pause -= dt;
                body.localPosition = Vector3.zero;
                if (pause <= 0) goal = rng.NextDouble() < 0.2 ? register : spots[rng.Next(spots.Count)];
                return;
            }
            var pos = transform.localPosition;
            var to = goal - pos;
            to.y = 0;
            float step = speed * dt;
            if (to.magnitude <= step)
            {
                transform.localPosition = new Vector3(goal.x, pos.y, goal.z);
                pause = 1.5f + (float)rng.NextDouble() * 3f;
                return;
            }
            var dir = to.normalized;
            transform.localPosition = pos + dir * step;
            transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.LookRotation(dir), 8f * dt);
            phase += dt * 9f;
            body.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(phase)) * 0.05f, 0);
            body.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(phase) * 6f);
        }
    }
}

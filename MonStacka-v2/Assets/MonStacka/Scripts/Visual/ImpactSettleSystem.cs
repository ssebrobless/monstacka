using System.Collections.Generic;
using MonStacka.Core;
using UnityEngine;

namespace MonStacka.Visual
{
    public sealed class ImpactSettleSystem : MonoBehaviour
    {
        private readonly Dictionary<EdgeKey, float> activeImpacts = new();
        private float impactAmplitudeWorld = 0.04f;
        private float decayTime = 0.2f;
        private float reboundFrequency = 9f;

        public IReadOnlyDictionary<EdgeKey, float> ActiveImpacts => activeImpacts;

        public void Configure(float cellWorldSize, BorderDeformTuningProfile tuning, bool previewOnly)
        {
            var scale = previewOnly ? tuning.previewAmplitudeScale : 1f;
            impactAmplitudeWorld = tuning.impactImpulseCells * cellWorldSize * scale;
            decayTime = tuning.settleDurationSeconds;
            reboundFrequency = tuning.reboundFrequency;
        }

        public void Trigger(IEnumerable<EdgeKey> edges)
        {
            var now = Time.time;
            foreach (var edge in edges)
            {
                activeImpacts[edge] = now;
            }
        }

        public float GetImpulse(VertexMeta meta, float timeNow)
        {
            var edge = new EdgeKey(meta.LocalCell, meta.Edge);
            if (!activeImpacts.TryGetValue(edge, out var startedAt))
            {
                return 0f;
            }

            var elapsed = timeNow - startedAt;
            if (elapsed >= decayTime * 5f)
            {
                activeImpacts.Remove(edge);
                return 0f;
            }

            var decay = Mathf.Exp(-elapsed / Mathf.Max(0.0001f, decayTime));
            var oscillation = Mathf.Cos(elapsed * reboundFrequency * Mathf.PI * 2f);
            return impactAmplitudeWorld * decay * oscillation;
        }
    }
}

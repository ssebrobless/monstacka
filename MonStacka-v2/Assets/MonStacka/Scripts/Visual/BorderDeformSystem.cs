using MonStacka.Core;
using UnityEngine;

namespace MonStacka.Visual
{
    [RequireComponent(typeof(MeshFilter))]
    public sealed class BorderDeformSystem : MonoBehaviour
    {
        private readonly struct SeamPairKey
        {
            public readonly int Ax;
            public readonly int Ay;
            public readonly int Bx;
            public readonly int By;
            public readonly int SegmentIndex;

            public SeamPairKey(int ax, int ay, int bx, int by, int segmentIndex)
            {
                Ax = ax;
                Ay = ay;
                Bx = bx;
                By = by;
                SegmentIndex = segmentIndex;
            }
        }

        private MeshFilter meshFilter;
        private Mesh workingMesh;
        private VertexMeta[] metadata = new VertexMeta[0];
        private Vector3[] restVertices = new Vector3[0];
        private Vector3[] animatedVertices = new Vector3[0];
        private float[] displacements = new float[0];
        private float[] velocities = new float[0];
        private VertexContactState[] contacts = new VertexContactState[0];
        private Vector2Int boardOrigin;
        private int ownerPieceId;
        private ImpactSettleSystem impactSettleSystem;
        private BorderDeformTuningProfile tuning;
        private float cellWorldSize = 1f;
        private bool previewOnly;

        public void Initialize(OutlineBuildResult buildResult, int pieceId, float worldCellSize, BorderDeformTuningProfile tuningProfile, bool isPreviewOnly)
        {
            meshFilter = GetComponent<MeshFilter>();
            workingMesh = Instantiate(buildResult.Mesh);
            workingMesh.name = $"{buildResult.Mesh.name}_Runtime";
            meshFilter.sharedMesh = workingMesh;
            metadata = buildResult.VertexMetadata;
            restVertices = workingMesh.vertices;
            animatedVertices = new Vector3[restVertices.Length];
            displacements = new float[restVertices.Length];
            velocities = new float[restVertices.Length];
            contacts = new VertexContactState[metadata.Length];
            ownerPieceId = pieceId;
            impactSettleSystem = GetComponent<ImpactSettleSystem>();
            tuning = tuningProfile;
            cellWorldSize = worldCellSize;
            previewOnly = isPreviewOnly;
        }

        public void ApplyNeighborMap(NeighborMap neighborMap, Vector2Int origin, int pieceId)
        {
            boardOrigin = origin;
            ownerPieceId = pieceId;
            for (var index = 0; index < metadata.Length; index += 1)
            {
                contacts[index] = SeamContactSystem.Resolve(metadata[index], neighborMap, boardOrigin, ownerPieceId);
            }
        }

        private void LateUpdate()
        {
            if (workingMesh == null || metadata.Length == 0)
            {
                return;
            }

            var now = Time.time;
            var dt = Mathf.Max(Time.deltaTime, 0.0001f);
            for (var index = 0; index < metadata.Length; index += 1)
            {
                var meta = metadata[index];
                var contact = index < contacts.Length ? contacts[index] : default;
                var target = ComputeTarget(meta, contact, now);
                if (impactSettleSystem != null)
                {
                    target += impactSettleSystem.GetImpulse(meta, now);
                }

                var displacement = displacements[index];
                var velocity = velocities[index];
                var force = ((target - displacement) * tuning.springStiffness) - (velocity * tuning.springDamping);
                velocity += force * dt;
                displacement += velocity * dt;
                var maxDisplacement = tuning.maxDisplacementCells * cellWorldSize * (previewOnly ? tuning.previewAmplitudeScale : 1f);
                displacement = Mathf.Clamp(displacement, -maxDisplacement, maxDisplacement);
                displacements[index] = displacement;
                velocities[index] = velocity;

                animatedVertices[index] = restVertices[index] + (meta.Normal * displacement);
            }

            workingMesh.vertices = animatedVertices;
            workingMesh.RecalculateBounds();
        }

        private float ComputeTarget(VertexMeta meta, VertexContactState contact, float now)
        {
            var scale = previewOnly ? tuning.previewAmplitudeScale : 1f;

            if (contact.IsTouching && contact.NeighborPieceId != -1)
            {
                var boardCell = boardOrigin + meta.LocalCell;
                var seamKey = GetSeamPairKey(boardCell, meta);
                var seamHash = HashToUnit(seamKey.Ax, seamKey.Ay, seamKey.Bx, seamKey.By, seamKey.SegmentIndex);
                var wave = Mathf.Sin((now * tuning.seamDriveFrequency * Mathf.PI * 2f) + (seamHash * Mathf.PI * 2f));
                var noise = Mathf.PerlinNoise(seamHash * 0.71f, now * tuning.edgeNoiseDensity * 0.9f) * 2f - 1f;
                var seamSignal = Mathf.Lerp(wave, noise, tuning.seamNoiseBlend);
                var sign = IsPrimary(boardCell, boardCell + GetNeighborOffset(meta.Edge)) ? 1f : -1f;
                return seamSignal * tuning.seamAmplitudeCells * cellWorldSize * sign * scale;
            }

            var slowNoise = Mathf.PerlinNoise(meta.PhaseSeed * tuning.edgeNoiseDensity, now * tuning.ambientDriveFrequency) * 2f - 1f;
            var fastNoise = Mathf.PerlinNoise(now * tuning.ambientFlutterFrequency, meta.SegmentPhaseSeed * tuning.edgeNoiseDensity) * 2f - 1f;
            var flutter = Mathf.Sin((now * tuning.ambientFlutterFrequency * Mathf.PI * 2f) + meta.SegmentPhaseSeed) * 0.2f;
            var ambientSignal = (slowNoise * 0.55f) + (fastNoise * 0.30f) + (flutter * 0.15f);
            return ambientSignal * tuning.ambientAmplitudeCells * cellWorldSize * scale;
        }

        private static SeamPairKey GetSeamPairKey(Vector2Int boardCell, VertexMeta meta)
        {
            var neighbor = boardCell + GetNeighborOffset(meta.Edge);
            if (IsPrimary(boardCell, neighbor))
            {
                return new SeamPairKey(boardCell.x, boardCell.y, neighbor.x, neighbor.y, meta.SegmentIndex);
            }

            return new SeamPairKey(neighbor.x, neighbor.y, boardCell.x, boardCell.y, meta.SegmentIndex);
        }

        private static bool IsPrimary(Vector2Int a, Vector2Int b)
        {
            return a.x < b.x || (a.x == b.x && a.y <= b.y);
        }

        private static Vector2Int GetNeighborOffset(EdgeDirection edge)
        {
            return edge switch
            {
                EdgeDirection.Up => new Vector2Int(0, -1),
                EdgeDirection.Down => new Vector2Int(0, 1),
                EdgeDirection.Left => Vector2Int.left,
                EdgeDirection.Right => Vector2Int.right,
                _ => Vector2Int.zero,
            };
        }

        private static float HashToUnit(params int[] values)
        {
            unchecked
            {
                var hash = 17;
                foreach (var value in values)
                {
                    hash = (hash * 31) + value;
                }
                return (hash & 0x7fffffff) / (float)int.MaxValue;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (tuning == null || metadata == null || metadata.Length == 0)
            {
                return;
            }

            var lineScale = tuning.debugLineScaleCells * Mathf.Max(0.01f, cellWorldSize);
            for (var index = 0; index < metadata.Length; index += 4)
            {
                var meta = metadata[index];
                var contact = index < contacts.Length ? contacts[index] : default;
                var displacement = index < displacements.Length ? displacements[index] : 0f;
                var center = transform.TransformPoint(meta.SegmentCenter + (meta.Normal * displacement));
                var normal = transform.TransformDirection(meta.Normal).normalized;

                if (tuning.debugDrawSegments)
                {
                    Gizmos.color = contact.IsTouching ? new Color(1f, 0.2f, 0.7f, 1f) : new Color(0.2f, 0.9f, 1f, 1f);
                    Gizmos.DrawSphere(center, lineScale * 0.08f);
                }

                if (tuning.debugDrawDisplacement)
                {
                    Gizmos.color = new Color(0.9f, 0.95f, 0.2f, 1f);
                    Gizmos.DrawLine(center, center + (normal * lineScale));
                }

                if (tuning.debugDrawSeamLinks && contact.IsTouching)
                {
                    Gizmos.color = new Color(1f, 0.4f, 0.4f, 1f);
                    Gizmos.DrawLine(center, center + (normal * lineScale));
                }

                if (tuning.debugDrawImpacts && impactSettleSystem != null && impactSettleSystem.ActiveImpacts.ContainsKey(new EdgeKey(meta.LocalCell, meta.Edge)))
                {
                    Gizmos.color = new Color(1f, 0.85f, 0.1f, 1f);
                    Gizmos.DrawWireSphere(center, lineScale * 0.12f);
                }
            }
        }
#endif
    }
}

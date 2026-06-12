using System.Collections.Generic;
using MonStacka.Core;
using UnityEngine;

namespace MonStacka.Visual
{
    public sealed class SpriteBorderPulseSystem : MonoBehaviour
    {
        private const int SegmentCount = 72;
        private const float AlphaCutoff = 0.05f;
        private const int InwardBleedPixels = 1;
        private const float MinSaturationForTint = 0.18f;
        private const float MinValueForTint = 0.18f;
        private const float MaxValueForTint = 0.94f;
        private static readonly Dictionary<string, Sprite> SegmentSpriteCache = new();

        private sealed class SegmentLayer
        {
            public int Index;
            public Vector3 Normal;
            public SpriteRenderer Renderer;
            public Sprite[] Frames;
            public float Displacement;
            public float Velocity;
            public float ExposedWeight = 1f;
            public float TouchWeight;
            public float ContactSign;
            public float ImpactWeight;
            public float ImpactStartedAt = -1f;
            public float Phase;
        }

        private struct SideState
        {
            public float Count;
            public float Exposed;
            public float Touching;
            public float ContactSign;
        }

        private readonly List<SegmentLayer> segments = new();
        private readonly HashSet<Vector2Int> localCellSet = new();
        private BorderDeformTuningProfile tuning;
        private float amplitudeScale = 1f;
        private float baseInset = 0.0085f;
        private int ownerPieceId;
        private SideState upState;
        private SideState downState;
        private SideState leftState;
        private SideState rightState;

        public void Initialize(
            Transform host,
            Sprite[] bodyFrames,
            IReadOnlyCollection<Vector2Int> localCells,
            BorderDeformTuningProfile tuningProfile,
            bool previewOnly,
            float pulseScale,
            Color tintColor)
        {
            tuning = tuningProfile;
            amplitudeScale = pulseScale * (previewOnly ? tuning.previewAmplitudeScale * 0.92f : 0.92f);
            baseInset = previewOnly ? 0.0022f : 0.0018f;
            localCellSet.Clear();
            foreach (var cell in localCells)
            {
                localCellSet.Add(cell);
            }

            var sampledTint = SampleDominantTint(bodyFrames, tintColor);

            segments.Clear();
            for (var index = 0; index < SegmentCount; index += 1)
            {
                CreateSegment(host, bodyFrames, index, previewOnly, sampledTint);
            }

            ResetSideState();
            SetFrameIndex(0);
        }

        public void ApplyNeighborMap(NeighborMap neighborMap, Vector2Int origin, int pieceId)
        {
            ownerPieceId = pieceId;
            ResetSideState();

            foreach (var localCell in localCellSet)
            {
                var boardCell = origin + localCell;
                if (!neighborMap.TryGetValue(boardCell, out var info))
                {
                    continue;
                }

                ProcessSide(localCell, new Vector2Int(0, -1), ref upState, info.TopExposed, info.TopNeighborPieceId);
                ProcessSide(localCell, new Vector2Int(0, 1), ref downState, info.BottomExposed, info.BottomNeighborPieceId);
                ProcessSide(localCell, Vector2Int.left, ref leftState, info.LeftExposed, info.LeftNeighborPieceId);
                ProcessSide(localCell, Vector2Int.right, ref rightState, info.RightExposed, info.RightNeighborPieceId);
            }

            for (var index = 0; index < segments.Count; index += 1)
            {
                var segment = segments[index];
                var weights = GetDirectionalWeights(segment.Normal);
                var exposed = Blend(weights, GetExposedRatio(upState), GetExposedRatio(downState), GetExposedRatio(leftState), GetExposedRatio(rightState));
                var touching = Blend(weights, GetTouchRatio(upState), GetTouchRatio(downState), GetTouchRatio(leftState), GetTouchRatio(rightState));
                var sign = Blend(weights, GetContactSign(upState), GetContactSign(downState), GetContactSign(leftState), GetContactSign(rightState));

                segment.ExposedWeight = Mathf.Clamp01(exposed);
                segment.TouchWeight = Mathf.Clamp01(touching);
                segment.ContactSign = Mathf.Clamp(sign, -1f, 1f);
            }
        }

        public void TriggerImpact(IEnumerable<EdgeKey> impactedEdges)
        {
            var now = Time.time;
            var sideWeights = new float[4];
            foreach (var edge in impactedEdges)
            {
                switch (edge.Edge)
                {
                    case EdgeDirection.Up:
                        sideWeights[0] = 1f;
                        break;
                    case EdgeDirection.Down:
                        sideWeights[1] = 1f;
                        break;
                    case EdgeDirection.Left:
                        sideWeights[2] = 1f;
                        break;
                    case EdgeDirection.Right:
                        sideWeights[3] = 1f;
                        break;
                }
            }

            foreach (var segment in segments)
            {
                var weights = GetDirectionalWeights(segment.Normal);
                segment.ImpactWeight = Blend(weights, sideWeights[0], sideWeights[1], sideWeights[2], sideWeights[3]);
                if (segment.ImpactWeight > 0.01f)
                {
                    segment.ImpactStartedAt = now;
                }
            }
        }

        public void ManualUpdate(float now)
        {
            var rawTargets = new float[segments.Count];
            for (var index = 0; index < segments.Count; index += 1)
            {
                var segment = segments[index];
                var segmentTravel = ((Mathf.PI * 2f) / SegmentCount) * segment.Index;
                var ambientPrimary = Mathf.Sin((now * tuning.ambientDriveFrequency * Mathf.PI * 2f) - (segmentTravel * 2.35f) + segment.Phase);
                var ambientSecondary = Mathf.Sin((now * tuning.ambientFlutterFrequency * 0.11f * Mathf.PI * 2f) - (segmentTravel * 4.65f) + (segment.Phase * 0.52f));
                var ambient = ((ambientPrimary * 0.68f) + (ambientSecondary * 0.32f)) *
                    tuning.ambientAmplitudeCells *
                    Mathf.Lerp(0.28f, 1f, segment.ExposedWeight);
                var seam = Mathf.Sin((now * tuning.seamDriveFrequency * Mathf.PI * 2f) - (segmentTravel * 2.85f) + segment.Phase * 1.08f) *
                    tuning.seamAmplitudeCells *
                    segment.TouchWeight *
                    segment.ContactSign;
                var impact = ComputeImpact(segment, now) * segment.ImpactWeight;
                rawTargets[index] = (ambient + seam + impact) * amplitudeScale;
            }

            var dt = Mathf.Max(Time.deltaTime, 0.0001f);
            for (var index = 0; index < segments.Count; index += 1)
            {
                var segment = segments[index];
                var previous = rawTargets[(index - 1 + segments.Count) % segments.Count];
                var previousFar = rawTargets[(index - 2 + segments.Count) % segments.Count];
                var next = rawTargets[(index + 1) % segments.Count];
                var nextFar = rawTargets[(index + 2) % segments.Count];
                var target =
                    (rawTargets[index] * 0.42f) +
                    (previous * 0.24f) +
                    (next * 0.24f) +
                    (previousFar * 0.05f) +
                    (nextFar * 0.05f);

                var force = ((target - segment.Displacement) * tuning.springStiffness) - (segment.Velocity * tuning.springDamping);
                segment.Velocity += force * dt;
                segment.Displacement += segment.Velocity * dt;
                segment.Displacement = Mathf.Clamp(segment.Displacement, -tuning.maxDisplacementCells, tuning.maxDisplacementCells);

                segment.Renderer.transform.localPosition = segment.Normal * (segment.Displacement - baseInset);
            }
        }

        public void SetFrameIndex(int frameIndex)
        {
            foreach (var segment in segments)
            {
                if (segment.Frames == null || segment.Frames.Length == 0)
                {
                    continue;
                }

                segment.Renderer.sprite = segment.Frames[Mathf.Clamp(frameIndex, 0, segment.Frames.Length - 1)];
            }
        }

        private void CreateSegment(Transform host, Sprite[] bodyFrames, int segmentIndex, bool previewOnly, Color tintColor)
        {
            var angle = (Mathf.PI * 2f * segmentIndex) / SegmentCount;
            var normal = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f).normalized;

            var go = new GameObject($"BorderSegment_{segmentIndex:00}");
            go.transform.SetParent(host, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = -10;
            renderer.color = new Color(1f, 1f, 1f, previewOnly ? 0.84f : 0.88f);

            var frames = new Sprite[bodyFrames.Length];
            for (var frameIndex = 0; frameIndex < bodyFrames.Length; frameIndex += 1)
            {
                frames[frameIndex] = BuildSegmentSprite(bodyFrames[frameIndex], segmentIndex, normal, tintColor);
            }

            segments.Add(new SegmentLayer
            {
                Index = segmentIndex,
                Normal = normal,
                Renderer = renderer,
                Frames = frames,
                Phase = segmentIndex * 0.23f,
            });
        }

        private float ComputeImpact(SegmentLayer segment, float now)
        {
            if (segment.ImpactStartedAt < 0f)
            {
                return 0f;
            }

            var elapsed = now - segment.ImpactStartedAt;
            if (elapsed >= tuning.settleDurationSeconds * 4f)
            {
                segment.ImpactStartedAt = -1f;
                return 0f;
            }

            var decay = Mathf.Exp(-elapsed / Mathf.Max(0.001f, tuning.settleDurationSeconds));
            var oscillation = Mathf.Cos(elapsed * tuning.reboundFrequency * Mathf.PI * 2f);
            return tuning.impactImpulseCells * decay * oscillation;
        }

        private static Sprite BuildSegmentSprite(Sprite source, int segmentIndex, Vector3 fallbackNormal, Color tintColor)
        {
            var cacheKey = $"{source.GetInstanceID()}:{segmentIndex}:{Mathf.RoundToInt(tintColor.r * 255f)}:{Mathf.RoundToInt(tintColor.g * 255f)}:{Mathf.RoundToInt(tintColor.b * 255f)}";
            if (SegmentSpriteCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var rect = source.rect;
            var texture = source.texture;
            var width = Mathf.RoundToInt(rect.width);
            var height = Mathf.RoundToInt(rect.height);
            var pixels = texture.GetPixels(
                Mathf.RoundToInt(rect.x),
                Mathf.RoundToInt(rect.y),
                width,
                height
            );

            var output = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var outputPixels = new Color[width * height];
            var center = ComputeOpaqueCentroid(pixels, width, height);
            var contourPixels = GetOrderedContourPixels(pixels, width, height, center);
            if (contourPixels.Count > 0)
            {
                var start = Mathf.FloorToInt((segmentIndex / (float)SegmentCount) * contourPixels.Count);
                var end = Mathf.FloorToInt(((segmentIndex + 1) / (float)SegmentCount) * contourPixels.Count);
                var padding = Mathf.Max(1, contourPixels.Count / (SegmentCount * 10));
                if (end <= start)
                {
                    end = start + 1;
                }

                for (var offset = -padding; offset <= (end - start) + padding; offset += 1)
                {
                    var contourIndex = PositiveModulo(start + offset, contourPixels.Count);
                    var point = contourPixels[contourIndex];
                    var x = point.x;
                    var y = point.y;
                    var index = y * width + x;
                    var alpha = Mathf.Clamp01(pixels[index].a);
                    var normal = EstimateNormal(pixels, width, height, x, y, center, fallbackNormal);
                    var solid = new Color(tintColor.r, tintColor.g, tintColor.b, alpha);
                    outputPixels[index] = solid;
                    BleedIntoBody(outputPixels, width, height, x, y, normal, solid);
                }
            }

            output.SetPixels(outputPixels);
            output.Apply();

            var pivot = new Vector2(source.pivot.x / rect.width, source.pivot.y / rect.height);
            var sprite = Sprite.Create(output, new Rect(0f, 0f, width, height), pivot, source.pixelsPerUnit);
            sprite.name = $"{source.name}_BorderSegment_{segmentIndex:00}";
            SegmentSpriteCache[cacheKey] = sprite;
            return sprite;
        }

        private static Color SampleDominantTint(Sprite[] bodyFrames, Color fallback)
        {
            if (bodyFrames == null || bodyFrames.Length == 0)
            {
                return fallback;
            }

            var weightedColor = Vector3.zero;
            var totalWeight = 0f;

            foreach (var frame in bodyFrames)
            {
                if (!frame)
                {
                    continue;
                }

                var rect = frame.rect;
                var texture = frame.texture;
                var pixels = texture.GetPixels(
                    Mathf.RoundToInt(rect.x),
                    Mathf.RoundToInt(rect.y),
                    Mathf.RoundToInt(rect.width),
                    Mathf.RoundToInt(rect.height)
                );

                foreach (var color in pixels)
                {
                    if (color.a < 0.12f)
                    {
                        continue;
                    }

                    Color.RGBToHSV(color, out _, out var saturation, out var value);
                    if (saturation < MinSaturationForTint || value < MinValueForTint || value > MaxValueForTint)
                    {
                        continue;
                    }

                    var weight = color.a * Mathf.Max(0.2f, saturation);
                    weightedColor += new Vector3(color.r, color.g, color.b) * weight;
                    totalWeight += weight;
                }
            }

            if (totalWeight <= 0.0001f)
            {
                return fallback;
            }

            var rgb = weightedColor / totalWeight;
            return new Color(rgb.x, rgb.y, rgb.z, 1f);
        }

        private static bool IsEdgePixel(Color[] pixels, int width, int height, int x, int y)
        {
            for (var dy = -1; dy <= 1; dy += 1)
            {
                for (var dx = -1; dx <= 1; dx += 1)
                {
                    if (dx == 0 && dy == 0)
                    {
                        continue;
                    }

                    if (IsTransparent(pixels, width, height, x + dx, y + dy))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static Vector2 ComputeOpaqueCentroid(Color[] pixels, int width, int height)
        {
            var sum = Vector2.zero;
            var count = 0f;
            for (var y = 0; y < height; y += 1)
            {
                for (var x = 0; x < width; x += 1)
                {
                    var color = pixels[(y * width) + x];
                    if (color.a < AlphaCutoff)
                    {
                        continue;
                    }

                    sum += new Vector2(x, y);
                    count += 1f;
                }
            }

            if (count <= 0.0001f)
            {
                return new Vector2((width - 1) * 0.5f, (height - 1) * 0.5f);
            }

            return sum / count;
        }

        private static List<Vector2Int> GetOrderedContourPixels(Color[] pixels, int width, int height, Vector2 center)
        {
            var contour = new List<(Vector2Int point, float angle, float distance)>();
            for (var y = 0; y < height; y += 1)
            {
                for (var x = 0; x < width; x += 1)
                {
                    var index = (y * width) + x;
                    if (pixels[index].a < AlphaCutoff || !IsEdgePixel(pixels, width, height, x, y))
                    {
                        continue;
                    }

                    var dx = x - center.x;
                    var dy = y - center.y;
                    var angle = Mathf.Atan2(dy, dx);
                    if (angle < 0f)
                    {
                        angle += Mathf.PI * 2f;
                    }

                    contour.Add((new Vector2Int(x, y), angle, (dx * dx) + (dy * dy)));
                }
            }

            contour.Sort((left, right) =>
            {
                var angleCompare = left.angle.CompareTo(right.angle);
                if (angleCompare != 0)
                {
                    return angleCompare;
                }

                return left.distance.CompareTo(right.distance);
            });

            var ordered = new List<Vector2Int>(contour.Count);
            var seen = new HashSet<int>();
            foreach (var entry in contour)
            {
                var key = (entry.point.y * width) + entry.point.x;
                if (seen.Add(key))
                {
                    ordered.Add(entry.point);
                }
            }

            return ordered;
        }

        private static Vector3 EstimateNormal(Color[] pixels, int width, int height, int x, int y, Vector2 center, Vector3 fallbackNormal)
        {
            var nx = 0f;
            var ny = 0f;

            for (var dy = -1; dy <= 1; dy += 1)
            {
                for (var dx = -1; dx <= 1; dx += 1)
                {
                    if (dx == 0 && dy == 0)
                    {
                        continue;
                    }

                    if (!IsTransparent(pixels, width, height, x + dx, y + dy))
                    {
                        continue;
                    }

                    nx -= dx;
                    ny -= dy;
                }
            }

            var normal = new Vector2(nx, ny);
            if (normal.sqrMagnitude < 0.001f)
            {
                normal = new Vector2(x - center.x, y - center.y);
            }

            if (normal.sqrMagnitude < 0.001f)
            {
                normal = new Vector2(fallbackNormal.x, fallbackNormal.y);
            }

            normal.Normalize();
            return new Vector3(normal.x, normal.y, 0f);
        }

        private static int GetSegmentIndex(Vector3 normal)
        {
            var angle = Mathf.Atan2(normal.y, normal.x);
            if (angle < 0f)
            {
                angle += Mathf.PI * 2f;
            }

            return Mathf.Clamp(Mathf.FloorToInt((angle / (Mathf.PI * 2f)) * SegmentCount), 0, SegmentCount - 1);
        }

        private static int PositiveModulo(int value, int modulo)
        {
            if (modulo <= 0)
            {
                return 0;
            }

            var result = value % modulo;
            return result < 0 ? result + modulo : result;
        }

        private static void BleedIntoBody(Color[] outputPixels, int width, int height, int x, int y, Vector3 normal, Color color)
        {
            var inwardX = Mathf.RoundToInt(-normal.x);
            var inwardY = Mathf.RoundToInt(-normal.y);
            for (var step = 1; step <= InwardBleedPixels; step += 1)
            {
                var nx = x + (inwardX * step);
                var ny = y + (inwardY * step);
                if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                {
                    continue;
                }

                var index = ny * width + nx;
                outputPixels[index] = color;
            }
        }

        private static bool IsTransparent(Color[] pixels, int width, int height, int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return true;
            }

            return pixels[y * width + x].a < AlphaCutoff;
        }

        private void ResetSideState()
        {
            upState = default;
            downState = default;
            leftState = default;
            rightState = default;
            foreach (var segment in segments)
            {
                segment.ExposedWeight = 1f;
                segment.TouchWeight = 0f;
                segment.ContactSign = 0f;
                segment.ImpactWeight = 0f;
            }
        }

        private void ProcessSide(Vector2Int localCell, Vector2Int neighborOffset, ref SideState state, bool isExposed, int neighborPieceId)
        {
            if (localCellSet.Contains(localCell + neighborOffset))
            {
                return;
            }

            state.Count += 1f;
            if (isExposed || neighborPieceId <= 0 || neighborPieceId == ownerPieceId)
            {
                state.Exposed += 1f;
                return;
            }

            state.Touching += 1f;
            state.ContactSign += ownerPieceId < neighborPieceId ? 1f : -1f;
        }

        private static float GetExposedRatio(SideState state)
        {
            return state.Count <= 0f ? 1f : state.Exposed / state.Count;
        }

        private static float GetTouchRatio(SideState state)
        {
            return state.Count <= 0f ? 0f : state.Touching / state.Count;
        }

        private static float GetContactSign(SideState state)
        {
            return state.Touching <= 0f ? 0f : state.ContactSign / state.Touching;
        }

        private static Vector4 GetDirectionalWeights(Vector3 normal)
        {
            var up = Mathf.Max(0f, normal.y);
            var down = Mathf.Max(0f, -normal.y);
            var left = Mathf.Max(0f, -normal.x);
            var right = Mathf.Max(0f, normal.x);
            var total = Mathf.Max(0.0001f, up + down + left + right);
            return new Vector4(up / total, down / total, left / total, right / total);
        }

        private static float Blend(Vector4 weights, float up, float down, float left, float right)
        {
            return (weights.x * up) + (weights.y * down) + (weights.z * left) + (weights.w * right);
        }
    }
}

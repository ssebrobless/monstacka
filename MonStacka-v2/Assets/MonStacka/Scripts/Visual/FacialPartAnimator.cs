using System.Collections.Generic;
using System.Linq;
using MonStacka.Core;
using UnityEngine;

namespace MonStacka.Visual
{
    // Renders the artist-separated facial feature layers (PSD-derived, see
    // tools/generate_layered_sheets.py) on top of the featureless body sprite and
    // drives them with procedural motion. Features never deform the body sprite.
    public sealed class FacialPartAnimator : MonoBehaviour
    {
        private sealed class FeatureLayer
        {
            public Transform Holder;
            public SpriteRenderer Renderer;
            public FeatureMotion Motion;
            public Vector3 BasePosition;
            public float WidthUnits;
            public float HeightUnits;
            public float NextAt;
            public bool Active;
            public float StartedAt;
            public float Duration;
            public Vector2 RoamFrom;
            public Vector2 RoamTo;
            public float Phase;
        }

        private static readonly Dictionary<string, Sprite> FeatureSpriteCache = new();

        private readonly List<FeatureLayer> layers = new();
        private float pixelsPerCell = 112f;
        private float cellWorldSize = 1f;

        public bool Animates { get; private set; }

        public void Initialize(
            PieceSkinData skinData,
            PieceType pieceType,
            int rotation,
            IReadOnlyCollection<Vector2Int> localCells,
            float worldCellSize,
            bool useFullBoxCoordinates,
            bool animate)
        {
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            layers.Clear();
            Animates = false;

            if (!skinData || skinData.featuresSheet == null || skinData.features == null || skinData.features.Length == 0)
            {
                return;
            }

            if (!ConnectedBodyBuilder.MatchesFullDefinition(pieceType, rotation, localCells))
            {
                return;
            }

            pixelsPerCell = skinData.pixelsPerCell;
            cellWorldSize = worldCellSize;
            var boxPx = skinData.boxSize * skinData.pixelsPerCell;
            var turns = ConnectedBodyBuilder.GetTextureTurns(skinData, pieceType, rotation);
            var rotatedDefinition = PieceDefinitions.GetCells(pieceType, rotation);
            var offsetX = 0;
            var offsetY = 0;
            if (!useFullBoxCoordinates)
            {
                offsetX = rotatedDefinition.Min(cell => cell.x) * skinData.pixelsPerCell;
                offsetY = rotatedDefinition.Min(cell => cell.y) * skinData.pixelsPerCell;
            }

            for (var index = 0; index < skinData.features.Length; index += 1)
            {
                var seed = skinData.features[index];
                var center = new Vector2(
                    seed.boxRect.x + (seed.boxRect.width * 0.5f),
                    seed.boxRect.y + (seed.boxRect.height * 0.5f));
                for (var turn = 0; turn < turns; turn += 1)
                {
                    center = new Vector2(boxPx - center.y, center.x);
                }

                var holder = new GameObject($"Feature_{seed.featureName}");
                holder.transform.SetParent(transform, false);
                holder.transform.localPosition = new Vector3(
                    (center.x - offsetX) / pixelsPerCell * cellWorldSize,
                    -((center.y - offsetY) / pixelsPerCell) * cellWorldSize,
                    -0.01f - (index * 0.0008f));
                holder.transform.localRotation = Quaternion.Euler(0f, 0f, -90f * turns);

                var renderer = holder.AddComponent<SpriteRenderer>();
                renderer.drawMode = SpriteDrawMode.Sliced;
                renderer.sprite = GetFeatureSprite(skinData, pieceType, index);
                var widthUnits = seed.boxRect.width / pixelsPerCell * cellWorldSize;
                var heightUnits = seed.boxRect.height / pixelsPerCell * cellWorldSize;
                renderer.size = new Vector2(widthUnits, heightUnits);

                layers.Add(new FeatureLayer
                {
                    Holder = holder.transform,
                    Renderer = renderer,
                    Motion = animate ? seed.motion : FeatureMotion.Static,
                    BasePosition = holder.transform.localPosition,
                    WidthUnits = widthUnits,
                    HeightUnits = heightUnits,
                    NextAt = Time.time + Random.Range(0.6f, 4f),
                    Phase = Random.Range(0f, 20f),
                });
            }

            Animates = animate && layers.Any(layer => layer.Motion != FeatureMotion.Static);
        }

        public void ManualUpdate(float now)
        {
            if (!Animates)
            {
                return;
            }

            foreach (var layer in layers)
            {
                switch (layer.Motion)
                {
                    case FeatureMotion.Roam:
                        UpdateRoam(layer, now);
                        break;
                    case FeatureMotion.Blink:
                        UpdateBlink(layer, now);
                        break;
                    case FeatureMotion.SquashPulse:
                        UpdateSquashPulse(layer, now);
                        break;
                    case FeatureMotion.Twitch:
                        UpdateTwitch(layer, now);
                        break;
                    case FeatureMotion.Chatter:
                        UpdateChatter(layer, now);
                        break;
                    case FeatureMotion.Flick:
                        UpdateFlick(layer, now);
                        break;
                    case FeatureMotion.Drip:
                        UpdateDrip(layer, now);
                        break;
                    case FeatureMotion.Flow:
                        UpdateFlow(layer, now);
                        break;
                }
            }
        }

        private void UpdateRoam(FeatureLayer layer, float now)
        {
            if (!TryStartLoop(layer, now, 0.3f, out var t, out var justStarted))
            {
                return;
            }

            if (justStarted)
            {
                var radius = Mathf.Min(layer.WidthUnits, layer.HeightUnits) * 0.09f;
                layer.RoamFrom = layer.Holder.localPosition - layer.BasePosition;
                layer.RoamTo = Random.value < 0.3f
                    ? Vector2.zero
                    : Random.insideUnitCircle * radius;
            }

            var eased = Mathf.SmoothStep(0f, 1f, t);
            var offset = Vector2.Lerp(layer.RoamFrom, layer.RoamTo, eased);
            layer.Holder.localPosition = layer.BasePosition + new Vector3(offset.x, offset.y, 0f);
            if (t >= 1f)
            {
                // Roaming holds its end pose instead of snapping back; only timing resets.
                layer.Active = false;
                layer.NextAt = now + Random.Range(0.9f, 1.8f);
            }
        }

        private void UpdateBlink(FeatureLayer layer, float now)
        {
            var big = layer.HeightUnits > cellWorldSize * 0.8f;
            if (!TryStartLoop(layer, now, big ? 0.38f : 0.24f, out var t, out _))
            {
                return;
            }

            var squash = 1f - (0.92f * Mathf.Sin(Mathf.PI * t));
            layer.Holder.localScale = new Vector3(1f, Mathf.Max(0.06f, squash), 1f);
            FinishLoopIfDone(layer, now, t, 2.2f, 6f);
        }

        private void UpdateSquashPulse(FeatureLayer layer, float now)
        {
            if (!TryStartLoop(layer, now, 0.5f, out var t, out _))
            {
                return;
            }

            var wave = Mathf.Sin(Mathf.PI * t);
            layer.Holder.localScale = new Vector3(1f + (0.07f * wave), 1f - (0.1f * wave), 1f);
            FinishLoopIfDone(layer, now, t, 3f, 7f);
        }

        private void UpdateTwitch(FeatureLayer layer, float now)
        {
            if (!TryStartLoop(layer, now, 0.22f, out var t, out _))
            {
                return;
            }

            var angle = 9f * Mathf.Sin(2f * Mathf.PI * t) * (1f - t);
            layer.Holder.localRotation = BaseRotation(layer) * Quaternion.Euler(0f, 0f, angle);
            FinishLoopIfDone(layer, now, t, 2.5f, 7f);
        }

        private void UpdateChatter(FeatureLayer layer, float now)
        {
            if (!TryStartLoop(layer, now, 0.55f, out var t, out _))
            {
                return;
            }

            var amplitude = 1.8f / pixelsPerCell * cellWorldSize;
            var jitter = amplitude * Mathf.Sin(40f * (now - layer.StartedAt)) * (1f - t);
            layer.Holder.localPosition = layer.BasePosition + (layer.Holder.localRotation * new Vector3(0f, jitter, 0f));
            FinishLoopIfDone(layer, now, t, 2f, 6f);
        }

        private void UpdateFlick(FeatureLayer layer, float now)
        {
            if (!TryStartLoop(layer, now, 0.5f, out var t, out _))
            {
                return;
            }

            var stretch = 1f + (0.14f * Mathf.Sin(Mathf.PI * t));
            ApplyTopAnchoredScaleY(layer, stretch);
            FinishLoopIfDone(layer, now, t, 4f, 9f);
        }

        private void UpdateDrip(FeatureLayer layer, float now)
        {
            var stretch = 1f + (0.05f * Mathf.Sin((now * 1.1f) + layer.Phase));
            ApplyTopAnchoredScaleY(layer, stretch);
            var alpha = 0.75f + (0.25f * (0.5f + (0.5f * Mathf.Sin((now * 1.7f) + layer.Phase))));
            var color = layer.Renderer.color;
            color.a = alpha;
            layer.Renderer.color = color;
        }

        private void UpdateFlow(FeatureLayer layer, float now)
        {
            var wave = Mathf.Sin((now * 2.3f) + layer.Phase);
            var secondaryWave = Mathf.Sin((now * 3.1f) + layer.Phase + 1.4f);
            layer.Holder.localPosition = layer.BasePosition;
            layer.Holder.localScale = new Vector3(
                1f + (0.028f * wave),
                1f + (0.014f * secondaryWave),
                1f
            );
            layer.Holder.localRotation = BaseRotation(layer) * Quaternion.Euler(0f, 0f, wave * 1.8f);

            var color = layer.Renderer.color;
            color.a = 0.78f + (0.22f * (0.5f + (0.5f * secondaryWave)));
            layer.Renderer.color = color;
        }

        private void ApplyTopAnchoredScaleY(FeatureLayer layer, float scaleY)
        {
            layer.Holder.localScale = new Vector3(1f, scaleY, 1f);
            // Keep the top edge fixed in the feature's local frame: the center shifts
            // down (local -Y) by half the added height.
            var drop = (scaleY - 1f) * layer.HeightUnits * 0.5f;
            layer.Holder.localPosition = layer.BasePosition + (layer.Holder.localRotation * new Vector3(0f, -drop, 0f));
        }

        private static Quaternion BaseRotation(FeatureLayer layer)
        {
            // The holder's persistent orientation is the piece-turn rotation set at
            // Initialize; motions treat it as the rest pose.
            return Quaternion.Euler(0f, 0f, Mathf.Round(layer.Holder.localEulerAngles.z / 90f) * 90f);
        }

        private static bool TryStartLoop(FeatureLayer layer, float now, float duration, out float t, out bool justStarted)
        {
            justStarted = false;
            if (!layer.Active)
            {
                if (now < layer.NextAt)
                {
                    t = 0f;
                    return false;
                }

                layer.Active = true;
                layer.StartedAt = now;
                layer.Duration = duration;
                justStarted = true;
            }

            t = Mathf.Clamp01((now - layer.StartedAt) / Mathf.Max(0.01f, layer.Duration));
            return true;
        }

        private static void FinishLoopIfDone(FeatureLayer layer, float now, float t, float intervalMin, float intervalMax)
        {
            if (t < 1f)
            {
                return;
            }

            layer.Active = false;
            layer.NextAt = now + Random.Range(intervalMin, intervalMax);
            layer.Holder.localPosition = layer.BasePosition;
            layer.Holder.localScale = Vector3.one;
            layer.Holder.localRotation = BaseRotation(layer);
        }

        private static Sprite GetFeatureSprite(PieceSkinData skinData, PieceType pieceType, int seedIndex)
        {
            var cacheKey = $"{pieceType}:{seedIndex}:{skinData.GetInstanceID()}";
            if (FeatureSpriteCache.TryGetValue(cacheKey, out var cached) && cached != null)
            {
                return cached;
            }

            var texture = skinData.featuresSheet.texture;
            var rect = skinData.features[seedIndex].sheetRect;
            var spriteRect = new Rect(
                rect.x,
                texture.height - (rect.y + rect.height),
                rect.width,
                rect.height);
            var sprite = Sprite.Create(texture, spriteRect, new Vector2(0.5f, 0.5f), skinData.pixelsPerCell);
            sprite.name = $"{pieceType}_feature_{skinData.features[seedIndex].featureName}";
            FeatureSpriteCache[cacheKey] = sprite;
            return sprite;
        }
    }
}

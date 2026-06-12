using UnityEngine;

namespace MonStacka.Visual
{
    [CreateAssetMenu(menuName = "MonStacka/Border Deform Tuning")]
    public sealed class BorderDeformTuningProfile : ScriptableObject
    {
        [Header("Spring")]
        public float springStiffness = 180f;
        public float springDamping = 34f;
        public float maxDisplacementCells = 0.028f;

        [Header("Ambient Border Life")]
        public float ambientAmplitudeCells = 0.0075f;
        public float ambientDriveFrequency = 2.6f;
        public float ambientFlutterFrequency = 15.5f;
        public float edgeNoiseDensity = 3.3f;

        [Header("Touching Seams")]
        public float seamAmplitudeCells = 0.013f;
        public float seamDriveFrequency = 5.4f;
        public float seamNoiseBlend = 0.62f;

        [Header("Impact Settle")]
        public float impactImpulseCells = 0.018f;
        public float settleDurationSeconds = 0.09f;
        public float reboundFrequency = 13.5f;

        [Header("Preview")]
        public float previewAmplitudeScale = 0.9f;

        [Header("Debug")]
        public bool debugDrawSegments;
        public bool debugDrawSeamLinks;
        public bool debugDrawDisplacement;
        public bool debugDrawImpacts;
        public float debugLineScaleCells = 0.22f;
    }
}

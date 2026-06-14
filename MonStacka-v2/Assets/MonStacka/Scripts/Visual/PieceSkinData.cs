using System;
using MonStacka.Core;
using UnityEngine;

namespace MonStacka.Visual
{
    [CreateAssetMenu(menuName = "MonStacka/PieceSkinData")]
    public class PieceSkinData : ScriptableObject
    {
        public PieceType pieceType;
        public Sprite[] bodyFrames;
        public RectInt cropBounds;
        public Sprite featuresSheet;
        public FeatureSeed[] features;
        public int baseRotation;
        public int boxSize = 3;
        public int pixelsPerCell = 112;
    }

    public enum FeatureMotion
    {
        Static = 0,
        Roam = 1,
        Blink = 2,
        SquashPulse = 3,
        Twitch = 4,
        Chatter = 5,
        Flick = 6,
        Drip = 7,
        Flow = 8,
    }

    [Serializable]
    public struct FeatureSeed
    {
        public string featureName;
        public FeatureMotion motion;
        // Pixel rect in the features sheet (top-left origin) - the sprite crop source.
        public RectInt sheetRect;
        // Pixel rect in the piece's normalized box space (top-left origin, pixelsPerCell
        // per cell, base rotation) - where the feature sits on the generated body frames.
        public Rect boxRect;
    }
}

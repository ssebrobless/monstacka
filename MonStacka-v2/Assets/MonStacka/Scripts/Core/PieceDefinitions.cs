using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonStacka.Core
{
    public enum PieceType
    {
        I = 1,
        O = 2,
        T = 3,
        S = 4,
        Z = 5,
        J = 6,
        L = 7,
    }

    public enum EdgeDirection
    {
        Up,
        Down,
        Left,
        Right,
    }

    [Serializable]
    public struct PieceInstance
    {
        public PieceType Type;
        public int Rotation;
        public int X;
        public int Y;
        public int PieceId;

        public PieceInstance(PieceType type, int rotation, int x, int y, int pieceId = -1)
        {
            Type = type;
            Rotation = ((rotation % 4) + 4) % 4;
            X = x;
            Y = y;
            PieceId = pieceId;
        }
    }

    public static class PieceDefinitions
    {
        public const int Columns = 10;
        public const int VisibleRows = 20;
        public const int HiddenRows = 4;
        public const int TotalRows = VisibleRows + HiddenRows;
        public const int TargetLines = 40;

        public static readonly IReadOnlyDictionary<PieceType, Vector2Int[][]> Definitions =
            new Dictionary<PieceType, Vector2Int[][]>
            {
                [PieceType.I] = new[]
                {
                    new[] { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(3, 1) },
                    new[] { new Vector2Int(2, 0), new Vector2Int(2, 1), new Vector2Int(2, 2), new Vector2Int(2, 3) },
                    new[] { new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 2), new Vector2Int(3, 2) },
                    new[] { new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(1, 2), new Vector2Int(1, 3) },
                },
                [PieceType.O] = new[]
                {
                    new[] { new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(1, 1), new Vector2Int(2, 1) },
                    new[] { new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(1, 1), new Vector2Int(2, 1) },
                    new[] { new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(1, 1), new Vector2Int(2, 1) },
                    new[] { new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(1, 1), new Vector2Int(2, 1) },
                },
                [PieceType.T] = new[]
                {
                    new[] { new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1) },
                    new[] { new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(1, 2) },
                    new[] { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(1, 2) },
                    new[] { new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, 2) },
                },
                [PieceType.S] = new[]
                {
                    new[] { new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) },
                    new[] { new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(2, 2) },
                    new[] { new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(0, 2), new Vector2Int(1, 2) },
                    new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, 2) },
                },
                [PieceType.Z] = new[]
                {
                    new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(2, 1) },
                    new[] { new Vector2Int(2, 0), new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(1, 2) },
                    new[] { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, 2), new Vector2Int(2, 2) },
                    new[] { new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(0, 2) },
                },
                [PieceType.J] = new[]
                {
                    new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1) },
                    new[] { new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(1, 1), new Vector2Int(1, 2) },
                    new[] { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(2, 2) },
                    new[] { new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(0, 2), new Vector2Int(1, 2) },
                },
                [PieceType.L] = new[]
                {
                    new[] { new Vector2Int(2, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1) },
                    new[] { new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(1, 2), new Vector2Int(2, 2) },
                    new[] { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(0, 2) },
                    new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(1, 2) },
                },
            };

        public static readonly IReadOnlyDictionary<PieceType, Color> PieceColors =
            new Dictionary<PieceType, Color>
            {
                [PieceType.I] = Hex("#53d4ff"),
                [PieceType.O] = Hex("#ffd452"),
                [PieceType.T] = Hex("#be78ff"),
                [PieceType.S] = Hex("#53d37b"),
                [PieceType.Z] = Hex("#ff6f6f"),
                [PieceType.J] = Hex("#ff5ec8"),
                [PieceType.L] = Hex("#ff9a45"),
            };

        private static readonly IReadOnlyDictionary<string, Vector2Int[]> KicksJLSTZ =
            new Dictionary<string, Vector2Int[]>
            {
                ["0>1"] = ToOffsets((0, 0), (-1, 0), (-1, 1), (0, -2), (-1, -2)),
                ["1>0"] = ToOffsets((0, 0), (1, 0), (1, -1), (0, 2), (1, 2)),
                ["1>2"] = ToOffsets((0, 0), (1, 0), (1, -1), (0, 2), (1, 2)),
                ["2>1"] = ToOffsets((0, 0), (-1, 0), (-1, 1), (0, -2), (-1, -2)),
                ["2>3"] = ToOffsets((0, 0), (1, 0), (1, 1), (0, -2), (1, -2)),
                ["3>2"] = ToOffsets((0, 0), (-1, 0), (-1, -1), (0, 2), (-1, 2)),
                ["3>0"] = ToOffsets((0, 0), (-1, 0), (-1, -1), (0, 2), (-1, 2)),
                ["0>3"] = ToOffsets((0, 0), (1, 0), (1, 1), (0, -2), (1, -2)),
            };

        private static readonly IReadOnlyDictionary<string, Vector2Int[]> KicksI =
            new Dictionary<string, Vector2Int[]>
            {
                ["0>1"] = ToOffsets((0, 0), (-2, 0), (1, 0), (-2, -1), (1, 2)),
                ["1>0"] = ToOffsets((0, 0), (2, 0), (-1, 0), (2, 1), (-1, -2)),
                ["1>2"] = ToOffsets((0, 0), (-1, 0), (2, 0), (-1, 2), (2, -1)),
                ["2>1"] = ToOffsets((0, 0), (1, 0), (-2, 0), (1, -2), (-2, 1)),
                ["2>3"] = ToOffsets((0, 0), (2, 0), (-1, 0), (2, 1), (-1, -2)),
                ["3>2"] = ToOffsets((0, 0), (-2, 0), (1, 0), (-2, -1), (1, 2)),
                ["3>0"] = ToOffsets((0, 0), (1, 0), (-2, 0), (1, -2), (-2, 1)),
                ["0>3"] = ToOffsets((0, 0), (-1, 0), (2, 0), (-1, 2), (2, -1)),
            };

        public static PieceInstance CreateSpawnPiece(PieceType type) => new(type, 0, 3, 0, -1);

        public static IReadOnlyList<Vector2Int> GetCells(PieceType type, int rotation)
        {
            var normalized = ((rotation % 4) + 4) % 4;
            return Definitions[type][normalized];
        }

        public static Vector2Int[] GetAbsoluteCells(PieceInstance piece)
        {
            var source = GetCells(piece.Type, piece.Rotation);
            var result = new Vector2Int[source.Count];
            for (var i = 0; i < source.Count; i += 1)
            {
                result[i] = new Vector2Int(piece.X + source[i].x, piece.Y + source[i].y);
            }
            return result;
        }

        public static IReadOnlyList<Vector2Int> GetKickOffsets(PieceType type, int fromRotation, int toRotation)
        {
            var key = $"{((fromRotation % 4) + 4) % 4}>{((toRotation % 4) + 4) % 4}";
            if (type == PieceType.O)
            {
                return ToOffsets((0, 0));
            }

            return type == PieceType.I && KicksI.TryGetValue(key, out var iKicks)
                ? iKicks
                : KicksJLSTZ.TryGetValue(key, out var kicks)
                    ? kicks
                    : ToOffsets((0, 0));
        }

        public static int GetBoxSize(PieceType type) => type is PieceType.I or PieceType.O ? 4 : 3;

        public static int GetBaseRotation(PieceType type) => type switch
        {
            PieceType.I => 1,
            PieceType.T => 2,
            PieceType.J => 3,
            PieceType.L => 1,
            _ => 0,
        };

        public static RectInt GetFrameBounds(PieceType type, int frameIndex)
        {
            return type switch
            {
                PieceType.I when frameIndex == 0 => new RectInt(403, 182, 151, 571),
                PieceType.I => new RectInt(401, 182, 153, 571),
                PieceType.O => new RectInt(43, 314, 299, 289),
                PieceType.T => new RectInt(622, 371, 432, 291),
                PieceType.S => new RectInt(38, 5, 430, 289),
                PieceType.Z => new RectInt(585, 19, 435, 292),
                PieceType.J => new RectInt(49, 626, 293, 427),
                PieceType.L => new RectInt(595, 622, 291, 430),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
            };
        }

        private static Vector2Int[] ToOffsets(params (int x, int y)[] offsets)
        {
            var result = new Vector2Int[offsets.Length];
            for (var index = 0; index < offsets.Length; index += 1)
            {
                result[index] = new Vector2Int(offsets[index].x, offsets[index].y);
            }
            return result;
        }

        private static Color Hex(string html)
        {
            return ColorUtility.TryParseHtmlString(html, out var color) ? color : Color.white;
        }
    }
}

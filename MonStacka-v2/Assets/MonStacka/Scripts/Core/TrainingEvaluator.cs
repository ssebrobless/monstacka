using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MonStacka.Core
{
    public readonly struct TrainingEvaluation
    {
        public TrainingEvaluation(int? optimalInputs, int actualInputs)
        {
            OptimalInputs = optimalInputs;
            ActualInputs = actualInputs;
        }

        public int? OptimalInputs { get; }
        public int ActualInputs { get; }
        public bool IsFault => OptimalInputs.HasValue && ActualInputs > OptimalInputs.Value;
        public string Message => OptimalInputs.HasValue
            ? $"{ActualInputs} inputs used / {OptimalInputs.Value} optimal"
            : "No lookup data for this placement.";
    }

    public static class TrainingEvaluator
    {
        private enum Action
        {
            TapLeft,
            TapRight,
            DashLeft,
            DashRight,
            RotateCw,
            RotateCcw,
            RotateFlip,
        }

        private static readonly Action[] Actions =
        {
            Action.TapLeft,
            Action.TapRight,
            Action.DashLeft,
            Action.DashRight,
            Action.RotateCw,
            Action.RotateCcw,
            Action.RotateFlip,
        };

        private static readonly Dictionary<PieceType, Dictionary<string, int>> Lookup = BuildLookup();

        public static TrainingEvaluation Evaluate(PieceInstance piece, int actualInputs)
        {
            var optimal = GetOptimalInputCount(piece.Type, piece.X, piece.Rotation);
            return new TrainingEvaluation(optimal, actualInputs);
        }

        public static int? GetOptimalInputCount(PieceType pieceType, int x, int rotation)
        {
            return Lookup.TryGetValue(pieceType, out var placements) &&
                   placements.TryGetValue(PlacementKey(x, rotation), out var result)
                ? result
                : null;
        }

        private static Dictionary<PieceType, Dictionary<string, int>> BuildLookup()
        {
            return System.Enum.GetValues(typeof(PieceType))
                .Cast<PieceType>()
                .ToDictionary(piece => piece, BuildPieceLookup);
        }

        private static Dictionary<string, int> BuildPieceLookup(PieceType type)
        {
            var start = PieceDefinitions.CreateSpawnPiece(type);
            var queue = new Queue<PieceInstance>();
            var costs = new Dictionary<string, int>();
            var placements = new Dictionary<string, int>();

            queue.Enqueue(start);
            costs[StateKey(start)] = 0;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var currentCost = costs[StateKey(current)];
                var landed = DropToGround(current);
                var landedKey = PlacementKey(landed.X, landed.Rotation);
                if (!placements.TryGetValue(landedKey, out var knownPlacementCost) || currentCost < knownPlacementCost)
                {
                    placements[landedKey] = currentCost;
                }

                foreach (var action in Actions)
                {
                    var next = ApplyAction(current, action);
                    if (!next.HasValue)
                    {
                        continue;
                    }

                    var key = StateKey(next.Value);
                    var nextCost = currentCost + 1;
                    if (costs.TryGetValue(key, out var knownCost) && knownCost <= nextCost)
                    {
                        continue;
                    }

                    costs[key] = nextCost;
                    queue.Enqueue(next.Value);
                }
            }

            return placements;
        }

        private static PieceInstance? ApplyAction(PieceInstance piece, Action action)
        {
            return action switch
            {
                Action.TapLeft => MovePiece(piece, -1),
                Action.TapRight => MovePiece(piece, 1),
                Action.DashLeft => DashPiece(piece, -1),
                Action.DashRight => DashPiece(piece, 1),
                Action.RotateCw => Rotate(piece, 1, useKicks: true),
                Action.RotateCcw => Rotate(piece, -1, useKicks: true),
                Action.RotateFlip => Rotate(piece, 2, useKicks: false),
                _ => null,
            };
        }

        private static PieceInstance? MovePiece(PieceInstance piece, int dx)
        {
            var candidate = new PieceInstance(piece.Type, piece.Rotation, piece.X + dx, piece.Y, piece.PieceId);
            return IsValidOnEmptyBoard(candidate) ? candidate : null;
        }

        private static PieceInstance? DashPiece(PieceInstance piece, int dx)
        {
            var next = piece;
            while (true)
            {
                var moved = MovePiece(next, dx);
                if (!moved.HasValue)
                {
                    return next.X == piece.X ? null : next;
                }

                next = moved.Value;
            }
        }

        private static PieceInstance DropToGround(PieceInstance piece)
        {
            var next = piece;
            while (true)
            {
                var dropped = new PieceInstance(next.Type, next.Rotation, next.X, next.Y + 1, next.PieceId);
                if (!IsValidOnEmptyBoard(dropped))
                {
                    return next;
                }

                next = dropped;
            }
        }

        private static PieceInstance? Rotate(PieceInstance piece, int step, bool useKicks)
        {
            var from = piece.Rotation;
            var to = ((from + step) % 4 + 4) % 4;
            var rotated = new PieceInstance(piece.Type, to, piece.X, piece.Y, piece.PieceId);
            if (!useKicks)
            {
                return IsValidOnEmptyBoard(rotated) ? rotated : null;
            }

            foreach (var kick in PieceDefinitions.GetKickOffsets(piece.Type, from, to))
            {
                var candidate = new PieceInstance(piece.Type, to, piece.X + kick.x, piece.Y - kick.y, piece.PieceId);
                if (IsValidOnEmptyBoard(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool IsValidOnEmptyBoard(PieceInstance piece)
        {
            foreach (var cell in PieceDefinitions.GetAbsoluteCells(piece))
            {
                if (cell.x < 0 || cell.x >= PieceDefinitions.Columns || cell.y >= PieceDefinitions.TotalRows)
                {
                    return false;
                }
            }

            return true;
        }

        private static string StateKey(PieceInstance piece) => $"{piece.X}:{piece.Y}:{piece.Rotation}";

        private static string PlacementKey(int x, int rotation) => $"{x}:{((rotation % 4) + 4) % 4}";
    }
}

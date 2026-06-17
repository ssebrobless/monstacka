using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MonStacka.Core
{
    public struct CellEdgeInfo
    {
        public bool TopExposed;
        public bool BottomExposed;
        public bool LeftExposed;
        public bool RightExposed;
        public int TopNeighborPieceId;
        public int BottomNeighborPieceId;
        public int LeftNeighborPieceId;
        public int RightNeighborPieceId;
    }

    public sealed class NeighborMap
    {
        public Dictionary<Vector2Int, CellEdgeInfo> Cells { get; } = new();

        public bool TryGetValue(Vector2Int position, out CellEdgeInfo info)
        {
            return Cells.TryGetValue(position, out info);
        }
    }

    public readonly struct PieceLockEvent
    {
        public readonly int PieceId;
        public readonly PieceType PieceType;
        public readonly int Rotation;
        public readonly IReadOnlyList<Vector2Int> Cells;
        public readonly Vector2Int BoxOrigin;
        public readonly bool CameFromHold;
        public readonly int RotationInputs;

        public PieceLockEvent(int pieceId, PieceType pieceType, int rotation, IReadOnlyList<Vector2Int> cells, Vector2Int boxOrigin, bool cameFromHold = false, int rotationInputs = 0)
        {
            PieceId = pieceId;
            PieceType = pieceType;
            Rotation = rotation;
            Cells = cells;
            BoxOrigin = boxOrigin;
            CameFromHold = cameFromHold;
            RotationInputs = rotationInputs;
        }
    }

    public sealed class LockedPieceRecord
    {
        public int PieceId;
        public PieceType PieceType;
        public int Rotation;
        public List<Vector2Int> Cells = new();
        public List<Vector2Int> SourceCells = new();
        public Vector2Int? BoxOrigin;
    }

    public sealed class BoardState
    {
        /// <summary>Grid value reserved for nuisance/garbage cells (piece types use 1-7).</summary>
        public const int GarbageCellValue = 8;
        /// <summary>Grid value reserved for timed Guard Pressure rows.</summary>
        public const int GuardPressureCellValue = 9;

        private readonly struct TrainingSnapshot
        {
            public TrainingSnapshot(PieceInstance activePiece, IEnumerable<PieceType> queue)
            {
                ActivePiece = activePiece;
                Queue = queue.ToList();
            }

            public PieceInstance ActivePiece { get; }
            public List<PieceType> Queue { get; }
        }

        private readonly PieceBag bag;
        private readonly PieceType[] supportedPieces;
        private readonly int? lineTarget;
        private readonly MonStackaMode mode;
        private readonly string trainingFeedbackMode;
        private readonly Dictionary<int, LockedPieceRecord> lockedPieces = new();
        private readonly Dictionary<int, PieceType> pieceTypeById = new();
        private readonly Dictionary<int, int> pieceRotationById = new();
        private readonly Dictionary<int, Vector2Int> pieceOriginById = new();
        private readonly List<Vector2Int> territorySourceCells = new();
        private readonly List<Vector2Int> territoryClaimOrder = new();
        private readonly HashSet<Vector2Int> territoryClaimSet = new();
        private readonly Dictionary<int, float> pieceScoreMultipliers = new();
        private int nextPieceId = 1;
        private PieceInstance activePiece;
        private TrainingSnapshot? trainingSnapshot;

        public int[,] Grid { get; private set; }
        public int[,] PieceIds { get; private set; }
        public int[,] SourceCellXs { get; private set; }
        public int[,] SourceCellYs { get; private set; }
        public bool HasActivePiece { get; private set; }
        public PieceInstance ActivePiece => activePiece;
        public bool HasHoldPiece { get; private set; }
        public PieceType HoldPiece { get; private set; }
        public bool HoldUsed { get; private set; }
        /// <summary>True while the current active piece was swapped in from the hold box.</summary>
        public bool ActivePieceCameFromHold { get; private set; }
        public Queue<PieceType> NextQueue { get; }
        public bool HasSpawnedAny { get; private set; }
        public bool GameOver { get; private set; }
        public bool SprintComplete { get; private set; }
        public int Lines { get; private set; }
        public int Score { get; private set; }
        public int PiecesPlaced { get; private set; }
        public int CurrentPieceInputs { get; private set; }
        public int CurrentPieceRotations { get; private set; }
        public int TrainingFaults { get; private set; }
        public int TrainingPerfectStreak { get; private set; }
        public string LastTrainingFaultMessage { get; private set; } = string.Empty;

        public event Action<PieceLockEvent> OnPieceLocked;
        public event Action<int> OnPieceRotated;
        public event Action<int> OnLinesCleared;
        public event Action<int, PieceType?> OnPointsGained;

        public BoardState(
            IEnumerable<PieceType> piecePool = null,
            int? seed = null,
            int? targetLines = null,
            MonStackaMode selectedMode = MonStackaMode.Ogbm,
            string trainingFeedback = "show",
            IReadOnlyDictionary<PieceType, float> spawnWeights = null)
        {
            supportedPieces = (piecePool ?? Enum.GetValues(typeof(PieceType)).Cast<PieceType>()).Distinct().ToArray();
            bag = new PieceBag(supportedPieces, seed, spawnWeights);
            lineTarget = targetLines;
            mode = selectedMode;
            trainingFeedbackMode = string.IsNullOrWhiteSpace(trainingFeedback) ? "show" : trainingFeedback;
            Grid = new int[PieceDefinitions.TotalRows, PieceDefinitions.Columns];
            PieceIds = new int[PieceDefinitions.TotalRows, PieceDefinitions.Columns];
            SourceCellXs = new int[PieceDefinitions.TotalRows, PieceDefinitions.Columns];
            SourceCellYs = new int[PieceDefinitions.TotalRows, PieceDefinitions.Columns];
            NextQueue = new Queue<PieceType>();
            Reset();
        }

        public void Reset()
        {
            Grid = new int[PieceDefinitions.TotalRows, PieceDefinitions.Columns];
            PieceIds = new int[PieceDefinitions.TotalRows, PieceDefinitions.Columns];
            SourceCellXs = new int[PieceDefinitions.TotalRows, PieceDefinitions.Columns];
            SourceCellYs = new int[PieceDefinitions.TotalRows, PieceDefinitions.Columns];
            lockedPieces.Clear();
            pieceTypeById.Clear();
            pieceRotationById.Clear();
            pieceOriginById.Clear();
            ClearTerritoryOverlays();
            pieceScoreMultipliers.Clear();
            nextPieceId = 1;
            HasActivePiece = false;
            HasHoldPiece = false;
            HoldPiece = default;
            HoldUsed = false;
            ActivePieceCameFromHold = false;
            NextQueue.Clear();
            HasSpawnedAny = false;
            GameOver = false;
            SprintComplete = false;
            Lines = 0;
            Score = 0;
            PiecesPlaced = 0;
            CurrentPieceInputs = 0;
            CurrentPieceRotations = 0;
            TrainingFaults = 0;
            TrainingPerfectStreak = 0;
            LastTrainingFaultMessage = string.Empty;
            trainingSnapshot = null;
            bag.EnsureQueue(NextQueue, false);
            SpawnNext();
        }

        public bool SpawnNext(PieceType? forcedType = null)
        {
            if (GameOver)
            {
                return false;
            }

            bag.EnsureQueue(NextQueue, HasSpawnedAny);
            var type = forcedType ?? NextQueue.Dequeue();
            var piece = PieceDefinitions.CreateSpawnPiece(type);
            if (!IsValid(piece))
            {
                HasActivePiece = false;
                GameOver = true;
                return false;
            }

            activePiece = piece;
            HasActivePiece = true;
            HoldUsed = false;
            ActivePieceCameFromHold = false;
            HasSpawnedAny = true;
            CurrentPieceRotations = 0;
            bag.EnsureQueue(NextQueue, true);
            CaptureTrainingSnapshot();
            return true;
        }

        public bool EnsureActivePiece()
        {
            if (HasActivePiece)
            {
                return true;
            }

            return !GameOver && SpawnNext();
        }

        public bool TryMove(int dx, int dy)
        {
            if (!HasActivePiece || GameOver)
            {
                return false;
            }

            var candidate = activePiece;
            candidate.X += dx;
            candidate.Y += dy;
            if (!IsValid(candidate))
            {
                return false;
            }

            activePiece = candidate;
            return true;
        }

        public bool TrySoftDrop()
        {
            if (!TryMove(0, 1))
            {
                return false;
            }

            if (mode != MonStackaMode.Training)
            {
                AwardScore(1);
            }

            return true;
        }

        public bool HardDrop()
        {
            if (!HasActivePiece || GameOver)
            {
                return false;
            }

            var distance = 0;
            while (TryMove(0, 1))
            {
                distance += 1;
            }

            if (mode != MonStackaMode.Training)
            {
                AwardScore(distance * 2);
            }

            return LockPiece();
        }

        /// <summary>External score award (assist bonuses, story modifiers).</summary>
        public void AddScore(int points)
        {
            AddScore(points, null);
        }

        public void AddScore(int points, PieceType? sourcePiece)
        {
            if (points > 0 && !GameOver)
            {
                AwardScore(points, sourcePiece);
            }
        }

        private void AwardScore(int points, PieceType? sourcePiece = null)
        {
            if (points <= 0)
            {
                return;
            }

            Score += points;
            OnPointsGained?.Invoke(points, sourcePiece);
        }

        public void MarkPieceScoreDebuffed(int pieceId, float scoreMultiplier)
        {
            if (pieceId <= 0)
            {
                return;
            }

            pieceScoreMultipliers[pieceId] = Mathf.Clamp(scoreMultiplier, 0f, 1f);
        }

        public bool IsPieceScoreDebuffed(int pieceId) =>
            pieceScoreMultipliers.ContainsKey(pieceId);

        public void RegisterTrainingInput()
        {
            if (mode == MonStackaMode.Training && HasActivePiece && !GameOver)
            {
                CurrentPieceInputs += 1;
            }
        }

        public bool TryRotate(int step)
        {
            if (!HasActivePiece || GameOver)
            {
                return false;
            }

            var from = activePiece.Rotation;
            var to = ((from + step) % 4 + 4) % 4;
            var rotated = activePiece;
            rotated.Rotation = to;
            foreach (var kick in PieceDefinitions.GetKickOffsets(rotated.Type, from, to))
            {
                var candidate = rotated;
                candidate.X += kick.x;
                candidate.Y -= kick.y;
                if (IsValid(candidate))
                {
                    activePiece = candidate;
                    CurrentPieceRotations += 1;
                    OnPieceRotated?.Invoke(CurrentPieceRotations);
                    return true;
                }
            }

            return false;
        }

        public bool TryHold()
        {
            if (!HasActivePiece || HoldUsed || GameOver)
            {
                return false;
            }

            var currentType = activePiece.Type;
            if (HasHoldPiece)
            {
                var swap = HoldPiece;
                HoldPiece = currentType;
                var candidate = PieceDefinitions.CreateSpawnPiece(swap);
                if (!IsValid(candidate))
                {
                    GameOver = true;
                    HasActivePiece = false;
                    return false;
                }

                activePiece = candidate;
                HasActivePiece = true;
                ActivePieceCameFromHold = true;
                CurrentPieceRotations = 0;
            }
            else
            {
                HoldPiece = currentType;
                HasHoldPiece = true;
                HasActivePiece = false;
                if (!SpawnNext())
                {
                    return false;
                }
            }

            HoldUsed = true;
            return true;
        }

        public bool TrySwapHoldWithUpcoming(int upcomingIndex)
        {
            if (!HasHoldPiece || GameOver || upcomingIndex < 0)
            {
                return false;
            }

            bag.EnsureQueue(NextQueue, HasSpawnedAny);
            var queue = NextQueue.ToList();
            if (upcomingIndex >= queue.Count)
            {
                return false;
            }

            var oldHold = HoldPiece;
            HoldPiece = queue[upcomingIndex];
            queue[upcomingIndex] = oldHold;

            NextQueue.Clear();
            foreach (var piece in queue)
            {
                NextQueue.Enqueue(piece);
            }

            return true;
        }

        public PieceInstance GetGhostPiece()
        {
            if (!HasActivePiece)
            {
                return default;
            }

            var ghost = activePiece;
            while (IsValid(new PieceInstance(ghost.Type, ghost.Rotation, ghost.X, ghost.Y + 1, ghost.PieceId)))
            {
                ghost.Y += 1;
            }

            return ghost;
        }

        public bool IsGrounded()
        {
            if (!HasActivePiece)
            {
                return false;
            }

            var candidate = activePiece;
            candidate.Y += 1;
            return !IsValid(candidate);
        }

        public bool LockPiece()
        {
            if (!HasActivePiece)
            {
                return false;
            }

            if (mode == MonStackaMode.Training)
            {
                LockTrainingPiece();
                return true;
            }

            var pieceId = nextPieceId++;
            var absoluteCells = PieceDefinitions.GetAbsoluteCells(activePiece);
            var placedCells = new List<Vector2Int>(absoluteCells.Length);

            foreach (var cell in absoluteCells)
            {
                if (cell.y < 0 || cell.y >= PieceDefinitions.TotalRows || cell.x < 0 || cell.x >= PieceDefinitions.Columns)
                {
                    continue;
                }

                Grid[cell.y, cell.x] = (int)activePiece.Type;
                PieceIds[cell.y, cell.x] = pieceId;
                SourceCellXs[cell.y, cell.x] = cell.x - activePiece.X;
                SourceCellYs[cell.y, cell.x] = cell.y - activePiece.Y;
                placedCells.Add(cell);
            }

            lockedPieces[pieceId] = new LockedPieceRecord
            {
                PieceId = pieceId,
                PieceType = activePiece.Type,
                Rotation = activePiece.Rotation,
                Cells = placedCells.ToList(),
                SourceCells = placedCells.Select(cell => new Vector2Int(cell.x - activePiece.X, cell.y - activePiece.Y)).ToList(),
                BoxOrigin = new Vector2Int(activePiece.X, activePiece.Y),
            };
            pieceTypeById[pieceId] = activePiece.Type;
            pieceRotationById[pieceId] = activePiece.Rotation;
            pieceOriginById[pieceId] = new Vector2Int(activePiece.X, activePiece.Y);

            var cameFromHold = ActivePieceCameFromHold;
            HasActivePiece = false;
            ActivePieceCameFromHold = false;
            PiecesPlaced += 1;
            OnPieceLocked?.Invoke(new PieceLockEvent(pieceId, activePiece.Type, activePiece.Rotation, placedCells, new Vector2Int(activePiece.X, activePiece.Y), cameFromHold, CurrentPieceRotations));

            ClearLines();
            if (lineTarget.HasValue && Lines >= lineTarget.Value)
            {
                SprintComplete = true;
                GameOver = true;
            }
            else if (HasLockedCellsInHiddenRows())
            {
                GameOver = true;
            }

            return true;
        }

        public int ClearLines(bool notify = true)
        {
            var keptTypes = new List<int[]>();
            var keptIds = new List<int[]>();
            var keptSourceXs = new List<int[]>();
            var keptSourceYs = new List<int[]>();
            var clearedRows = new HashSet<int>();
            var strongestScoreMultiplier = 1f;
            var hasScoreDebuff = false;
            var cleared = 0;

            for (var row = 0; row < PieceDefinitions.TotalRows; row += 1)
            {
                var full = true;
                for (var col = 0; col < PieceDefinitions.Columns; col += 1)
                {
                    var cell = new Vector2Int(col, row);
                    if (Grid[row, col] == 0 || territoryClaimSet.Contains(cell) || territorySourceCells.Contains(cell))
                    {
                        full = false;
                        break;
                    }
                }

                if (full && IsGuardPressureRow(row))
                {
                    full = false;
                }

                if (full)
                {
                    for (var col = 0; col < PieceDefinitions.Columns; col += 1)
                    {
                        if (pieceScoreMultipliers.TryGetValue(PieceIds[row, col], out var multiplier))
                        {
                            strongestScoreMultiplier = Mathf.Min(strongestScoreMultiplier, multiplier);
                            hasScoreDebuff = true;
                        }
                    }

                    cleared += 1;
                    clearedRows.Add(row);
                    continue;
                }

                var typeRow = new int[PieceDefinitions.Columns];
                var idRow = new int[PieceDefinitions.Columns];
                var sourceXRow = new int[PieceDefinitions.Columns];
                var sourceYRow = new int[PieceDefinitions.Columns];
                for (var col = 0; col < PieceDefinitions.Columns; col += 1)
                {
                    typeRow[col] = Grid[row, col];
                    idRow[col] = PieceIds[row, col];
                    sourceXRow[col] = SourceCellXs[row, col];
                    sourceYRow[col] = SourceCellYs[row, col];
                }

                keptTypes.Add(typeRow);
                keptIds.Add(idRow);
                keptSourceXs.Add(sourceXRow);
                keptSourceYs.Add(sourceYRow);
            }

            while (keptTypes.Count < PieceDefinitions.TotalRows)
            {
                keptTypes.Insert(0, new int[PieceDefinitions.Columns]);
                keptIds.Insert(0, new int[PieceDefinitions.Columns]);
                keptSourceXs.Insert(0, new int[PieceDefinitions.Columns]);
                keptSourceYs.Insert(0, new int[PieceDefinitions.Columns]);
            }

            for (var row = 0; row < PieceDefinitions.TotalRows; row += 1)
            {
                for (var col = 0; col < PieceDefinitions.Columns; col += 1)
                {
                    Grid[row, col] = keptTypes[row][col];
                    PieceIds[row, col] = keptIds[row][col];
                    SourceCellXs[row, col] = keptSourceXs[row][col];
                    SourceCellYs[row, col] = keptSourceYs[row][col];
                }
            }

            if (cleared > 0)
            {
                Lines += cleared;
                var baseLineScore = cleared switch
                {
                    1 => 100,
                    2 => 300,
                    3 => 500,
                    4 => 800,
                    _ => 0,
                };
                var finalLineScore = hasScoreDebuff
                    ? Mathf.Max(1, Mathf.RoundToInt(baseLineScore * strongestScoreMultiplier))
                    : baseLineScore;
                AwardScore(finalLineScore);
                TransformTerritoryClaimsAfterLineClear(clearedRows);
                RebuildLockedPiecesFromGrid();
                if (notify)
                {
                    OnLinesCleared?.Invoke(cleared);
                }
            }

            return cleared;
        }

        private void LockTrainingPiece()
        {
            var lockedPiece = activePiece;
            var cameFromHold = ActivePieceCameFromHold;
            var evaluation = TrainingEvaluator.Evaluate(lockedPiece, CurrentPieceInputs);
            PiecesPlaced += 1;
            ActivePieceCameFromHold = false;
            OnPieceLocked?.Invoke(new PieceLockEvent(-1, lockedPiece.Type, lockedPiece.Rotation, PieceDefinitions.GetAbsoluteCells(lockedPiece), new Vector2Int(lockedPiece.X, lockedPiece.Y), cameFromHold, CurrentPieceRotations));

            if (evaluation.IsFault)
            {
                TrainingFaults += 1;
                TrainingPerfectStreak = 0;
                LastTrainingFaultMessage = evaluation.Message;
            }
            else
            {
                TrainingPerfectStreak += 1;
                LastTrainingFaultMessage = string.Empty;
            }

            if (evaluation.IsFault && trainingFeedbackMode == "redo" && trainingSnapshot.HasValue)
            {
                RestoreTrainingSnapshot(trainingSnapshot.Value);
                return;
            }

            Grid = new int[PieceDefinitions.TotalRows, PieceDefinitions.Columns];
            PieceIds = new int[PieceDefinitions.TotalRows, PieceDefinitions.Columns];
            SourceCellXs = new int[PieceDefinitions.TotalRows, PieceDefinitions.Columns];
            SourceCellYs = new int[PieceDefinitions.TotalRows, PieceDefinitions.Columns];
            lockedPieces.Clear();
            pieceTypeById.Clear();
            pieceRotationById.Clear();
            pieceOriginById.Clear();
            ClearTerritoryOverlays();
            pieceScoreMultipliers.Clear();
            HasActivePiece = false;
            HasHoldPiece = false;
            HoldUsed = false;
            Lines = 0;
            CurrentPieceInputs = 0;
            SpawnNext();
        }

        private void CaptureTrainingSnapshot()
        {
            if (mode != MonStackaMode.Training || !HasActivePiece)
            {
                trainingSnapshot = null;
                CurrentPieceInputs = 0;
                CurrentPieceRotations = 0;
                return;
            }

            trainingSnapshot = new TrainingSnapshot(activePiece, NextQueue);
            CurrentPieceInputs = 0;
            CurrentPieceRotations = 0;
        }

        private void RestoreTrainingSnapshot(TrainingSnapshot snapshot)
        {
            Grid = new int[PieceDefinitions.TotalRows, PieceDefinitions.Columns];
            PieceIds = new int[PieceDefinitions.TotalRows, PieceDefinitions.Columns];
            SourceCellXs = new int[PieceDefinitions.TotalRows, PieceDefinitions.Columns];
            SourceCellYs = new int[PieceDefinitions.TotalRows, PieceDefinitions.Columns];
            lockedPieces.Clear();
            pieceTypeById.Clear();
            pieceRotationById.Clear();
            pieceOriginById.Clear();
            ClearTerritoryOverlays();
            pieceScoreMultipliers.Clear();
            activePiece = snapshot.ActivePiece;
            HasActivePiece = true;
            HasHoldPiece = false;
            HoldUsed = false;
            Lines = 0;
            CurrentPieceInputs = 0;
            CurrentPieceRotations = 0;
            NextQueue.Clear();
            foreach (var piece in snapshot.Queue)
            {
                NextQueue.Enqueue(piece);
            }

            trainingSnapshot = snapshot;
        }

        public bool IsGameOver() => GameOver;

        private bool HasLockedCellsInHiddenRows()
        {
            for (var row = 0; row < PieceDefinitions.HiddenRows; row += 1)
            {
                for (var col = 0; col < PieceDefinitions.Columns; col += 1)
                {
                    if (Grid[row, col] != 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool IsValid(PieceInstance piece)
        {
            var cells = PieceDefinitions.GetAbsoluteCells(piece);
            foreach (var cell in cells)
            {
                if (cell.x < 0 || cell.x >= PieceDefinitions.Columns || cell.y >= PieceDefinitions.TotalRows)
                {
                    return false;
                }

                if (cell.y >= 0 && Grid[cell.y, cell.x] != 0)
                {
                    return false;
                }
            }

            return true;
        }

        public IReadOnlyCollection<LockedPieceRecord> GetLockedPieceGroups()
        {
            return lockedPieces.Values
                .Select(record => new LockedPieceRecord
                {
                    PieceId = record.PieceId,
                    PieceType = record.PieceType,
                    Rotation = record.Rotation,
                    Cells = record.Cells.ToList(),
                    SourceCells = record.SourceCells.ToList(),
                    BoxOrigin = record.BoxOrigin,
                })
                .ToList();
        }

        public NeighborMap GetNeighborMap(bool includeActive = true)
        {
            var occupancy = new Dictionary<Vector2Int, (int pieceId, PieceType pieceType)>();
            foreach (var record in lockedPieces.Values)
            {
                foreach (var cell in record.Cells)
                {
                    occupancy[cell] = (record.PieceId, record.PieceType);
                }
            }

            if (includeActive && HasActivePiece)
            {
                foreach (var cell in PieceDefinitions.GetAbsoluteCells(activePiece))
                {
                    occupancy[cell] = (-1, activePiece.Type);
                }
            }

            var map = new NeighborMap();
            foreach (var entry in occupancy)
            {
                var position = entry.Key;
                var top = occupancy.TryGetValue(position + new Vector2Int(0, -1), out var topNeighbor) ? topNeighbor.pieceId : -1;
                var bottom = occupancy.TryGetValue(position + new Vector2Int(0, 1), out var bottomNeighbor) ? bottomNeighbor.pieceId : -1;
                var left = occupancy.TryGetValue(position + Vector2Int.left, out var leftNeighbor) ? leftNeighbor.pieceId : -1;
                var right = occupancy.TryGetValue(position + Vector2Int.right, out var rightNeighbor) ? rightNeighbor.pieceId : -1;

                map.Cells[position] = new CellEdgeInfo
                {
                    TopNeighborPieceId = top,
                    BottomNeighborPieceId = bottom,
                    LeftNeighborPieceId = left,
                    RightNeighborPieceId = right,
                    TopExposed = top == -1,
                    BottomExposed = bottom == -1,
                    LeftExposed = left == -1,
                    RightExposed = right == -1,
                };
            }

            return map;
        }

        private void RebuildLockedPiecesFromGrid()
        {
            lockedPieces.Clear();
            for (var row = 0; row < PieceDefinitions.TotalRows; row += 1)
            {
                for (var col = 0; col < PieceDefinitions.Columns; col += 1)
                {
                    var pieceId = PieceIds[row, col];
                    if (pieceId <= 0)
                    {
                        continue;
                    }

                    if (!lockedPieces.TryGetValue(pieceId, out var record))
                    {
                        var pieceType = pieceTypeById.TryGetValue(pieceId, out var mappedPieceType)
                            ? mappedPieceType
                            : (PieceType)Grid[row, col];
                        var rotation = pieceRotationById.TryGetValue(pieceId, out var mappedRotation) ? mappedRotation : 0;
                        record = new LockedPieceRecord
                        {
                            PieceId = pieceId,
                            PieceType = pieceType,
                            Rotation = rotation,
                            BoxOrigin = pieceOriginById.TryGetValue(pieceId, out var origin) ? origin : null,
                        };
                        lockedPieces[pieceId] = record;
                    }

                    record.Cells.Add(new Vector2Int(col, row));
                    record.SourceCells.Add(new Vector2Int(SourceCellXs[row, col], SourceCellYs[row, col]));
                }
            }

            foreach (var record in lockedPieces.Values)
            {
                if (TryComputeBoxOrigin(record.PieceType, record.Rotation, record.Cells, out var origin))
                {
                    record.BoxOrigin = origin;
                    pieceOriginById[record.PieceId] = origin;
                }
            }
        }

        /// <summary>Raised whenever garbage/repair cells change so the view layer can refresh.</summary>
        public event Action OnGarbageChanged;

        /// <summary>All visible enemy cells currently on the board (includes Stitch repairs, Guard Pressure, and Territory overlays).</summary>
        public List<Vector2Int> GetGarbageCells()
        {
            var cells = new List<Vector2Int>();
            var seen = new HashSet<Vector2Int>();
            for (var row = 0; row < PieceDefinitions.TotalRows; row += 1)
            {
                for (var col = 0; col < PieceDefinitions.Columns; col += 1)
                {
                    if (IsEnemyCell(Grid[row, col]))
                    {
                        AddVisibleEnemyCell(cells, seen, new Vector2Int(col, row));
                    }
                }
            }
            foreach (var source in territorySourceCells)
            {
                AddVisibleEnemyCell(cells, seen, source);
            }
            foreach (var claim in territoryClaimOrder)
            {
                AddVisibleEnemyCell(cells, seen, claim);
            }
            return cells;
        }

        public IReadOnlyList<Vector2Int> GetTerritorySourceCells() =>
            territorySourceCells.ToList();

        public IReadOnlyList<Vector2Int> GetTerritoryClaimedCells() =>
            territoryClaimOrder.ToList();

        public int GetTerritoryClaimedBlockCount()
        {
            var blockIds = new HashSet<int>();
            var fallbackCells = 0;
            foreach (var cell in territoryClaimOrder)
            {
                if (cell.x < 0 || cell.x >= PieceDefinitions.Columns || cell.y < 0 || cell.y >= PieceDefinitions.TotalRows)
                {
                    continue;
                }

                var pieceId = PieceIds[cell.y, cell.x];
                if (pieceId > 0)
                {
                    blockIds.Add(pieceId);
                }
                else
                {
                    fallbackCells += 1;
                }
            }

            return blockIds.Count + fallbackCells;
        }

        public bool IsTerritoryClaimed(Vector2Int cell) =>
            territoryClaimSet.Contains(cell);

        public void SeedTerritorySource(int? column = null, int? row = null)
        {
            territorySourceCells.Clear();
            territoryClaimOrder.Clear();
            territoryClaimSet.Clear();

            var source = new Vector2Int(
                Mathf.Clamp(column ?? (PieceDefinitions.Columns / 2), 0, PieceDefinitions.Columns - 1),
                Mathf.Clamp(row ?? (PieceDefinitions.TotalRows - 2), 0, PieceDefinitions.TotalRows - 1)
            );
            territorySourceCells.Add(source);
            OnGarbageChanged?.Invoke();
        }

        public bool TryClaimAdjacentTerritoryCell(System.Random rng)
        {
            if (territorySourceCells.Count == 0)
            {
                return false;
            }

            var frontier = territorySourceCells.ToArray();
            var candidates = new List<Vector2Int>();
            for (var row = 0; row < PieceDefinitions.TotalRows; row += 1)
            {
                for (var col = 0; col < PieceDefinitions.Columns; col += 1)
                {
                    var cell = new Vector2Int(col, row);
                    if (territoryClaimSet.Contains(cell) || !IsClaimablePlayerBlock(row, col))
                    {
                        continue;
                    }

                    foreach (var claimed in frontier)
                    {
                        if (Mathf.Abs(claimed.x - col) + Mathf.Abs(claimed.y - row) == 1)
                        {
                            candidates.Add(cell);
                            break;
                        }
                    }
                }
            }

            if (candidates.Count == 0)
            {
                return false;
            }

            var claimedCell = candidates[rng.Next(candidates.Count)];
            ClaimTerritoryBlock(claimedCell);
            OnGarbageChanged?.Invoke();
            return true;
        }

        public bool ClearOldestTerritoryClaim()
        {
            if (territoryClaimOrder.Count == 0)
            {
                return false;
            }

            var removed = territoryClaimOrder[0];
            var removedPieceId = PieceIds[removed.y, removed.x];
            if (removedPieceId > 0)
            {
                for (var index = territoryClaimOrder.Count - 1; index >= 0; index -= 1)
                {
                    var cell = territoryClaimOrder[index];
                    if (PieceIds[cell.y, cell.x] != removedPieceId)
                    {
                        continue;
                    }

                    territoryClaimOrder.RemoveAt(index);
                    territoryClaimSet.Remove(cell);
                }
            }
            else
            {
                territoryClaimOrder.RemoveAt(0);
                territoryClaimSet.Remove(removed);
            }

            OnGarbageChanged?.Invoke();
            return true;
        }

        public int ClearRowsUnlockedByTerritoryUnclaim()
        {
            return ClearLines(notify: false);
        }

        public bool TryConsumeTopLayerPiece(out int pieceId, out PieceType pieceType, out int removedCells)
        {
            pieceId = 0;
            pieceType = default;
            removedCells = 0;

            for (var row = 0; row < PieceDefinitions.TotalRows && pieceId == 0; row += 1)
            {
                for (var col = 0; col < PieceDefinitions.Columns; col += 1)
                {
                    if (!IsClaimablePlayerBlock(row, col) || PieceIds[row, col] <= 0)
                    {
                        continue;
                    }

                    pieceId = PieceIds[row, col];
                    pieceType = pieceTypeById.TryGetValue(pieceId, out var mappedType)
                        ? mappedType
                        : (PieceType)Grid[row, col];
                    break;
                }
            }

            if (pieceId == 0)
            {
                return false;
            }

            var consumedCells = new HashSet<Vector2Int>();
            for (var row = 0; row < PieceDefinitions.TotalRows; row += 1)
            {
                for (var col = 0; col < PieceDefinitions.Columns; col += 1)
                {
                    if (PieceIds[row, col] != pieceId)
                    {
                        continue;
                    }

                    Grid[row, col] = 0;
                    PieceIds[row, col] = 0;
                    SourceCellXs[row, col] = 0;
                    SourceCellYs[row, col] = 0;
                    consumedCells.Add(new Vector2Int(col, row));
                    removedCells += 1;
                }
            }

            if (removedCells <= 0)
            {
                return false;
            }

            lockedPieces.Remove(pieceId);
            pieceTypeById.Remove(pieceId);
            pieceRotationById.Remove(pieceId);
            pieceOriginById.Remove(pieceId);
            pieceScoreMultipliers.Remove(pieceId);
            RemoveTerritoryOverlays(consumedCells);
            RebuildLockedPiecesFromGrid();
            OnGarbageChanged?.Invoke();
            return true;
        }

        public int GetGuardPressureRowCount()
        {
            var count = 0;
            for (var row = 0; row < PieceDefinitions.TotalRows; row += 1)
            {
                if (IsGuardPressureRow(row))
                {
                    count += 1;
                }
            }

            return count;
        }

        /// <summary>
        /// Pushes the whole stack up one row and inserts a garbage row at the bottom
        /// with a single hole. Tops out if the stack is pushed past the hidden rows.
        /// </summary>
        public void AddGarbageRow(int holeColumn)
        {
            if (GameOver)
            {
                return;
            }

            holeColumn = Mathf.Clamp(holeColumn, 0, PieceDefinitions.Columns - 1);

            for (var col = 0; col < PieceDefinitions.Columns; col += 1)
            {
                if (Grid[0, col] != 0)
                {
                    GameOver = true;
                    HasActivePiece = false;
                    return;
                }
            }

            for (var row = 0; row < PieceDefinitions.TotalRows - 1; row += 1)
            {
                for (var col = 0; col < PieceDefinitions.Columns; col += 1)
                {
                    Grid[row, col] = Grid[row + 1, col];
                    PieceIds[row, col] = PieceIds[row + 1, col];
                    SourceCellXs[row, col] = SourceCellXs[row + 1, col];
                    SourceCellYs[row, col] = SourceCellYs[row + 1, col];
                }
            }

            var bottom = PieceDefinitions.TotalRows - 1;
            for (var col = 0; col < PieceDefinitions.Columns; col += 1)
            {
                Grid[bottom, col] = col == holeColumn ? 0 : GarbageCellValue;
                PieceIds[bottom, col] = 0;
                SourceCellXs[bottom, col] = 0;
                SourceCellYs[bottom, col] = 0;
            }

            foreach (var record in lockedPieces.Values)
            {
                for (var index = 0; index < record.Cells.Count; index += 1)
                {
                    record.Cells[index] = new Vector2Int(record.Cells[index].x, record.Cells[index].y - 1);
                }
                if (record.BoxOrigin.HasValue)
                {
                    record.BoxOrigin = new Vector2Int(record.BoxOrigin.Value.x, record.BoxOrigin.Value.y - 1);
                }
            }
            ShiftTerritoryClaims(-1);

            if (HasActivePiece && !IsValid(activePiece))
            {
                var lifted = activePiece;
                lifted.Y -= 1;
                if (IsValid(lifted))
                {
                    activePiece = lifted;
                }
            }

            OnGarbageChanged?.Invoke();
        }

        /// <summary>
        /// Pushes the stack upward and inserts a full timed Guard Pressure row at the bottom.
        /// Returns false only when the board has already topped out.
        /// </summary>
        public bool AddGuardPressureRow()
        {
            if (GameOver)
            {
                return false;
            }

            for (var col = 0; col < PieceDefinitions.Columns; col += 1)
            {
                if (Grid[0, col] != 0)
                {
                    GameOver = true;
                    HasActivePiece = false;
                    return false;
                }
            }

            for (var row = 0; row < PieceDefinitions.TotalRows - 1; row += 1)
            {
                for (var col = 0; col < PieceDefinitions.Columns; col += 1)
                {
                    Grid[row, col] = Grid[row + 1, col];
                    PieceIds[row, col] = PieceIds[row + 1, col];
                    SourceCellXs[row, col] = SourceCellXs[row + 1, col];
                    SourceCellYs[row, col] = SourceCellYs[row + 1, col];
                }
            }

            var bottom = PieceDefinitions.TotalRows - 1;
            for (var col = 0; col < PieceDefinitions.Columns; col += 1)
            {
                Grid[bottom, col] = GuardPressureCellValue;
                PieceIds[bottom, col] = 0;
                SourceCellXs[bottom, col] = 0;
                SourceCellYs[bottom, col] = 0;
            }

            foreach (var record in lockedPieces.Values)
            {
                for (var index = 0; index < record.Cells.Count; index += 1)
                {
                    record.Cells[index] = new Vector2Int(record.Cells[index].x, record.Cells[index].y - 1);
                }
                if (record.BoxOrigin.HasValue)
                {
                    record.BoxOrigin = new Vector2Int(record.BoxOrigin.Value.x, record.BoxOrigin.Value.y - 1);
                }
            }
            ShiftTerritoryClaims(-1);

            if (HasActivePiece && !IsValid(activePiece))
            {
                var lifted = activePiece;
                lifted.Y -= 1;
                if (IsValid(lifted))
                {
                    activePiece = lifted;
                }
            }

            OnGarbageChanged?.Invoke();
            return true;
        }

        public bool ClearOldestGuardPressureRow()
        {
            for (var row = 0; row < PieceDefinitions.TotalRows; row += 1)
            {
                if (!IsGuardPressureRow(row))
                {
                    continue;
                }

                for (var dropRow = row; dropRow > 0; dropRow -= 1)
                {
                    for (var col = 0; col < PieceDefinitions.Columns; col += 1)
                    {
                        Grid[dropRow, col] = Grid[dropRow - 1, col];
                        PieceIds[dropRow, col] = PieceIds[dropRow - 1, col];
                        SourceCellXs[dropRow, col] = SourceCellXs[dropRow - 1, col];
                        SourceCellYs[dropRow, col] = SourceCellYs[dropRow - 1, col];
                    }
                }

                for (var col = 0; col < PieceDefinitions.Columns; col += 1)
                {
                    Grid[0, col] = 0;
                    PieceIds[0, col] = 0;
                    SourceCellXs[0, col] = 0;
                    SourceCellYs[0, col] = 0;
                }

                RebuildLockedPiecesFromGrid();
                OnGarbageChanged?.Invoke();
                return true;
            }

            return false;
        }

        private bool IsGuardPressureRow(int row)
        {
            for (var col = 0; col < PieceDefinitions.Columns; col += 1)
            {
                if (Grid[row, col] != GuardPressureCellValue)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsEnemyCell(int value) =>
            value == GarbageCellValue || value == GuardPressureCellValue;

        private static void AddVisibleEnemyCell(List<Vector2Int> cells, HashSet<Vector2Int> seen, Vector2Int cell)
        {
            if (seen.Add(cell))
            {
                cells.Add(cell);
            }
        }

        private bool IsClaimablePlayerBlock(int row, int col)
        {
            var value = Grid[row, col];
            return value > 0 && value < GarbageCellValue;
        }

        private void ClaimTerritoryBlock(Vector2Int claimedCell)
        {
            var pieceId = PieceIds[claimedCell.y, claimedCell.x];
            if (pieceId <= 0)
            {
                territoryClaimOrder.Add(claimedCell);
                territoryClaimSet.Add(claimedCell);
                return;
            }

            for (var row = 0; row < PieceDefinitions.TotalRows; row += 1)
            {
                for (var col = 0; col < PieceDefinitions.Columns; col += 1)
                {
                    if (PieceIds[row, col] != pieceId)
                    {
                        continue;
                    }

                    var cell = new Vector2Int(col, row);
                    if (territoryClaimSet.Add(cell))
                    {
                        territoryClaimOrder.Add(cell);
                    }
                }
            }
        }

        private void ClearTerritoryOverlays()
        {
            territorySourceCells.Clear();
            territoryClaimOrder.Clear();
            territoryClaimSet.Clear();
        }

        private void RemoveTerritoryOverlays(HashSet<Vector2Int> cells)
        {
            if (cells.Count == 0)
            {
                return;
            }

            territorySourceCells.RemoveAll(cells.Contains);
            territoryClaimOrder.RemoveAll(cells.Contains);
            territoryClaimSet.RemoveWhere(cells.Contains);
        }

        private void ShiftTerritoryClaims(int deltaY)
        {
            if (territoryClaimOrder.Count == 0)
            {
                return;
            }

            territoryClaimSet.Clear();
            for (var index = territoryClaimOrder.Count - 1; index >= 0; index -= 1)
            {
                var shifted = new Vector2Int(territoryClaimOrder[index].x, territoryClaimOrder[index].y + deltaY);
                if (shifted.y < 0 || shifted.y >= PieceDefinitions.TotalRows || Grid[shifted.y, shifted.x] == 0)
                {
                    territoryClaimOrder.RemoveAt(index);
                    continue;
                }

                territoryClaimOrder[index] = shifted;
                territoryClaimSet.Add(shifted);
            }

            OnGarbageChanged?.Invoke();
        }

        private void TransformTerritoryClaimsAfterLineClear(HashSet<int> clearedRows)
        {
            if (territoryClaimOrder.Count == 0 || clearedRows.Count == 0)
            {
                return;
            }

            territoryClaimSet.Clear();
            for (var index = territoryClaimOrder.Count - 1; index >= 0; index -= 1)
            {
                var claim = territoryClaimOrder[index];
                if (clearedRows.Contains(claim.y))
                {
                    territoryClaimOrder.RemoveAt(index);
                    continue;
                }

                var rowsClearedBelow = clearedRows.Count(clearedRow => clearedRow > claim.y);
                var shifted = new Vector2Int(claim.x, claim.y + rowsClearedBelow);
                if (shifted.y < 0 || shifted.y >= PieceDefinitions.TotalRows || Grid[shifted.y, shifted.x] == 0)
                {
                    territoryClaimOrder.RemoveAt(index);
                    continue;
                }

                territoryClaimOrder[index] = shifted;
                territoryClaimSet.Add(shifted);
            }

            OnGarbageChanged?.Invoke();
        }

        /// <summary>Seeds scattered garbage cells near the bottom/edges (Aggraso territory pressure).</summary>
        public void SeedTerritoryCells(int cellCount, int? seed = null)
        {
            var rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
            var placed = 0;
            var attempts = 0;
            while (placed < cellCount && attempts < cellCount * 30)
            {
                attempts += 1;
                var edgeBias = rng.NextDouble() < 0.6;
                var col = edgeBias
                    ? (rng.NextDouble() < 0.5 ? rng.Next(0, 2) : rng.Next(PieceDefinitions.Columns - 2, PieceDefinitions.Columns))
                    : rng.Next(0, PieceDefinitions.Columns);
                var row = PieceDefinitions.TotalRows - 1 - rng.Next(0, 4);
                if (Grid[row, col] != 0)
                {
                    continue;
                }

                Grid[row, col] = GarbageCellValue;
                PieceIds[row, col] = 0;
                SourceCellXs[row, col] = 0;
                SourceCellYs[row, col] = 0;
                placed += 1;
            }

            if (placed > 0)
            {
                OnGarbageChanged?.Invoke();
            }
        }

        /// <summary>
        /// Removes up to maxCells garbage cells, preferring the bottom rows and edges
        /// (Aggraso Guard Break / Sorrisol Digest). Returns how many were removed.
        /// </summary>
        public int ClearGarbageCells(int maxCells)
        {
            var removed = 0;
            for (var row = PieceDefinitions.TotalRows - 1; row >= 0 && removed < maxCells; row -= 1)
            {
                for (var offset = 0; offset < PieceDefinitions.Columns && removed < maxCells; offset += 1)
                {
                    var col = offset % 2 == 0 ? offset / 2 : PieceDefinitions.Columns - 1 - (offset / 2);
                    if (Grid[row, col] != GarbageCellValue)
                    {
                        continue;
                    }

                    Grid[row, col] = 0;
                    PieceIds[row, col] = 0;
                    SourceCellXs[row, col] = 0;
                    SourceCellYs[row, col] = 0;
                    removed += 1;
                }
            }

            if (removed > 0)
            {
                OnGarbageChanged?.Invoke();
            }
            return removed;
        }

        /// <summary>
        /// Fills the deepest covered hole with a repair cell (Dousema Stitch).
        /// Returns true if a hole was repaired.
        /// </summary>
        public bool TryRepairDeepestHole()
        {
            for (var row = PieceDefinitions.TotalRows - 1; row >= 0; row -= 1)
            {
                for (var col = 0; col < PieceDefinitions.Columns; col += 1)
                {
                    if (Grid[row, col] != 0)
                    {
                        continue;
                    }

                    var covered = false;
                    for (var above = row - 1; above >= 0; above -= 1)
                    {
                        if (Grid[above, col] != 0)
                        {
                            covered = true;
                            break;
                        }
                    }

                    if (!covered)
                    {
                        continue;
                    }

                    Grid[row, col] = GarbageCellValue;
                    PieceIds[row, col] = 0;
                    SourceCellXs[row, col] = 0;
                    SourceCellYs[row, col] = 0;
                    OnGarbageChanged?.Invoke();
                    return true;
                }
            }

            return false;
        }

        private static bool TryComputeBoxOrigin(
            PieceType pieceType,
            int rotation,
            IReadOnlyCollection<Vector2Int> absoluteCells,
            out Vector2Int origin)
        {
            origin = default;
            if (absoluteCells.Count == 0)
            {
                return false;
            }

            var definitionCells = PieceDefinitions.GetCells(pieceType, rotation).ToArray();
            if (definitionCells.Length != absoluteCells.Count)
            {
                return false;
            }

            var minDefinitionX = definitionCells.Min(cell => cell.x);
            var minDefinitionY = definitionCells.Min(cell => cell.y);
            var minAbsoluteX = absoluteCells.Min(cell => cell.x);
            var minAbsoluteY = absoluteCells.Min(cell => cell.y);
            var candidateOrigin = new Vector2Int(minAbsoluteX - minDefinitionX, minAbsoluteY - minDefinitionY);
            var expected = definitionCells
                .Select(cell => new Vector2Int(candidateOrigin.x + cell.x, candidateOrigin.y + cell.y))
                .OrderBy(cell => cell.y)
                .ThenBy(cell => cell.x)
                .ToArray();
            var actual = absoluteCells
                .OrderBy(cell => cell.y)
                .ThenBy(cell => cell.x)
                .ToArray();

            for (var index = 0; index < expected.Length; index += 1)
            {
                if (expected[index] != actual[index])
                {
                    return false;
                }
            }

            origin = candidateOrigin;
            return true;
        }
    }
}

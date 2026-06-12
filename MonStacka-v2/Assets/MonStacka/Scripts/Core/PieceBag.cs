using System;
using System.Collections.Generic;
using System.Linq;

namespace MonStacka.Core
{
    public sealed class PieceBag
    {
        private static readonly PieceType[] DefaultOpeningPool = { PieceType.I, PieceType.J, PieceType.L, PieceType.T };

        private readonly List<PieceType> piecePool;
        private readonly List<PieceType> openingPool;
        private readonly Random random;
        private readonly IReadOnlyDictionary<PieceType, float> spawnWeights;

        public PieceBag(
            IEnumerable<PieceType> pieces,
            int? seed = null,
            IReadOnlyDictionary<PieceType, float> weights = null)
        {
            piecePool = pieces.Distinct().ToList();
            openingPool = DefaultOpeningPool.Where(piecePool.Contains).DefaultIfEmpty(piecePool.First()).ToList();
            random = seed.HasValue ? new Random(seed.Value) : new Random();
            spawnWeights = weights != null && weights.Count > 0 ? weights : null;
        }

        public void EnsureQueue(Queue<PieceType> queue, bool hasSpawned, int minimumCount = 7)
        {
            while (queue.Count < minimumCount)
            {
                foreach (var piece in !hasSpawned && queue.Count == 0 ? MakeOpeningBag() : MakeBag())
                {
                    queue.Enqueue(piece);
                }
            }
        }

        public List<PieceType> MakeOpeningBag()
        {
            var first = openingPool[random.Next(openingPool.Count)];
            var rest = Shuffle(piecePool.Where(piece => piece != first).ToList());
            rest.Insert(0, first);
            return rest;
        }

        /// <summary>
        /// Builds one bag. Without weights this is a standard shuffled 7-bag. With
        /// weights (story spawn bias), each piece contributes 1 guaranteed copy plus
        /// extra copies for weight above 1 (fractional remainder rolled randomly),
        /// so every piece still appears while focused pieces spawn more often.
        /// </summary>
        public List<PieceType> MakeBag()
        {
            if (spawnWeights == null)
            {
                return Shuffle(piecePool.ToList());
            }

            var bag = new List<PieceType>();
            foreach (var piece in piecePool)
            {
                var weight = spawnWeights.TryGetValue(piece, out var value) ? Math.Max(0.1f, value) : 1f;
                var copies = Math.Max(1, (int)Math.Floor(weight));
                var remainder = weight - (float)Math.Floor(weight);
                if (remainder > 0f && random.NextDouble() < remainder)
                {
                    copies += 1;
                }

                for (var copy = 0; copy < copies; copy += 1)
                {
                    bag.Add(piece);
                }
            }

            return Shuffle(bag);
        }

        private List<PieceType> Shuffle(List<PieceType> pieces)
        {
            for (var index = pieces.Count - 1; index > 0; index -= 1)
            {
                var swapIndex = random.Next(index + 1);
                (pieces[index], pieces[swapIndex]) = (pieces[swapIndex], pieces[index]);
            }
            return pieces;
        }
    }
}

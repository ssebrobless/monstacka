using MonStacka.Core;
using UnityEngine;

namespace MonStacka.Visual
{
    public struct VertexContactState
    {
        public bool IsExposed;
        public bool IsTouching;
        public int NeighborPieceId;
    }

    public static class SeamContactSystem
    {
        public static VertexContactState Resolve(VertexMeta meta, NeighborMap map, Vector2Int origin, int ownerPieceId)
        {
            var boardCell = origin + meta.LocalCell;
            if (!map.TryGetValue(boardCell, out var info))
            {
                return new VertexContactState
                {
                    IsExposed = true,
                    IsTouching = false,
                    NeighborPieceId = -1,
                };
            }

            return meta.Edge switch
            {
                EdgeDirection.Up => CreateState(info.TopExposed, info.TopNeighborPieceId, ownerPieceId),
                EdgeDirection.Down => CreateState(info.BottomExposed, info.BottomNeighborPieceId, ownerPieceId),
                EdgeDirection.Left => CreateState(info.LeftExposed, info.LeftNeighborPieceId, ownerPieceId),
                EdgeDirection.Right => CreateState(info.RightExposed, info.RightNeighborPieceId, ownerPieceId),
                _ => default,
            };
        }

        private static VertexContactState CreateState(bool exposed, int neighborPieceId, int ownerPieceId)
        {
            var touching = !exposed && neighborPieceId != ownerPieceId;
            return new VertexContactState
            {
                IsExposed = exposed,
                IsTouching = touching,
                NeighborPieceId = touching ? neighborPieceId : -1,
            };
        }
    }
}

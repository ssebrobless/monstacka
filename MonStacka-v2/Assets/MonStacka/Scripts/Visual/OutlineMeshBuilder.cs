using System.Collections.Generic;
using MonStacka.Core;
using UnityEngine;

namespace MonStacka.Visual
{
    public readonly struct EdgeKey
    {
        public readonly Vector2Int LocalCell;
        public readonly EdgeDirection Edge;

        public EdgeKey(Vector2Int localCell, EdgeDirection edge)
        {
            LocalCell = localCell;
            Edge = edge;
        }
    }

    public struct VertexMeta
    {
        public Vector3 RestPosition;
        public Vector3 Normal;
        public Vector2Int LocalCell;
        public EdgeDirection Edge;
        public int SegmentIndex;
        public int SegmentCount;
        public Vector3 SegmentCenter;
        public float PhaseSeed;
        public float SegmentPhaseSeed;
    }

    public sealed class OutlineBuildResult
    {
        public Mesh Mesh { get; set; }
        public VertexMeta[] VertexMetadata { get; set; }
    }

    public sealed class OutlineMeshBuilder
    {
        public OutlineBuildResult BuildOutline(IReadOnlyCollection<Vector2Int> cells, float cellWorldSize, int subdivisions = 6, float bandWidth = 0.03f)
        {
            var cellSet = new HashSet<Vector2Int>(cells);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var metadata = new List<VertexMeta>();

            foreach (var cell in cells)
            {
                EmitEdge(cell, EdgeDirection.Up, GetNeighborOffset(EdgeDirection.Up), new Vector3(0f, 1f, 0f));
                EmitEdge(cell, EdgeDirection.Down, GetNeighborOffset(EdgeDirection.Down), new Vector3(0f, -1f, 0f));
                EmitEdge(cell, EdgeDirection.Left, GetNeighborOffset(EdgeDirection.Left), new Vector3(-1f, 0f, 0f));
                EmitEdge(cell, EdgeDirection.Right, GetNeighborOffset(EdgeDirection.Right), new Vector3(1f, 0f, 0f));
            }

            var mesh = new Mesh
            {
                name = "MonStackaOutline"
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return new OutlineBuildResult
            {
                Mesh = mesh,
                VertexMetadata = metadata.ToArray(),
            };

            void EmitEdge(Vector2Int cell, EdgeDirection edge, Vector2Int neighborOffset, Vector3 normal)
            {
                if (cellSet.Contains(cell + neighborOffset))
                {
                    return;
                }

                for (var segment = 0; segment < subdivisions; segment += 1)
                {
                    var startT = segment / (float)subdivisions;
                    var endT = (segment + 1) / (float)subdivisions;
                    var innerA = GetEdgePoint(cell, edge, startT, cellWorldSize);
                    var innerB = GetEdgePoint(cell, edge, endT, cellWorldSize);
                    var outerA = innerA + normal * bandWidth;
                    var outerB = innerB + normal * bandWidth;

                    var baseIndex = vertices.Count;
                    vertices.Add(innerA);
                    vertices.Add(innerB);
                    vertices.Add(outerA);
                    vertices.Add(outerB);

                    triangles.Add(baseIndex + 0);
                    triangles.Add(baseIndex + 2);
                    triangles.Add(baseIndex + 1);
                    triangles.Add(baseIndex + 1);
                    triangles.Add(baseIndex + 2);
                    triangles.Add(baseIndex + 3);

                    var phaseSeed = HashSeed(cell.x, cell.y, (int)edge, segment);
                    var segmentPhaseSeed = HashSeed(cell.x, cell.y, (int)edge, segment, 991) * 1.618f;
                    AddMeta(innerA);
                    AddMeta(innerB);
                    AddMeta(outerA);
                    AddMeta(outerB);

                    void AddMeta(Vector3 restPosition)
                    {
                        metadata.Add(new VertexMeta
                        {
                            RestPosition = restPosition,
                            Normal = normal,
                            LocalCell = cell,
                            Edge = edge,
                            SegmentIndex = segment,
                            SegmentCount = subdivisions,
                            SegmentCenter = (innerA + innerB + outerA + outerB) * 0.25f,
                            PhaseSeed = phaseSeed,
                            SegmentPhaseSeed = segmentPhaseSeed,
                        });
                    }
                }
            }
        }

        private static Vector2Int GetNeighborOffset(EdgeDirection edge)
        {
            // Board cell coordinates increase downward, so "up" is -Y and "down" is +Y.
            return edge switch
            {
                EdgeDirection.Up => new Vector2Int(0, -1),
                EdgeDirection.Down => new Vector2Int(0, 1),
                EdgeDirection.Left => Vector2Int.left,
                EdgeDirection.Right => Vector2Int.right,
                _ => Vector2Int.zero,
            };
        }

        private static Vector3 GetEdgePoint(Vector2Int cell, EdgeDirection edge, float t, float cellWorldSize)
        {
            var left = cell.x * cellWorldSize;
            var right = (cell.x + 1) * cellWorldSize;
            var top = -cell.y * cellWorldSize;
            var bottom = -(cell.y + 1) * cellWorldSize;

            return edge switch
            {
                EdgeDirection.Up => new Vector3(Mathf.Lerp(left, right, t), top, 0f),
                EdgeDirection.Down => new Vector3(Mathf.Lerp(left, right, t), bottom, 0f),
                EdgeDirection.Left => new Vector3(left, Mathf.Lerp(top, bottom, t), 0f),
                EdgeDirection.Right => new Vector3(right, Mathf.Lerp(top, bottom, t), 0f),
                _ => Vector3.zero,
            };
        }

        private static float HashSeed(params int[] values)
        {
            unchecked
            {
                var hash = 17;
                foreach (var value in values)
                {
                    hash = hash * 31 + value;
                }
                return (hash & 0x7fffffff) * 0.017f;
            }
        }
    }
}

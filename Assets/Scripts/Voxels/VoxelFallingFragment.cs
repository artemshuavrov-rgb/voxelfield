using System.Collections.Generic;
using Swihoni.Util.Math;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Voxels
{
    // A detached part of the authoritative voxel map becomes a temporary
    // physics object. It breaks into the existing colored debris on impact.
    public sealed class VoxelFallingFragment : MonoBehaviour
    {
        public readonly struct Cell
        {
            public readonly Position3Int position;
            public readonly Voxel voxel;

            public Cell(Position3Int position, Voxel voxel)
            {
                this.position = position;
                this.voxel = voxel;
            }
        }

        private static readonly Position3Int[] Directions =
        {
            new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0),
            new(0, -1, 0), new(0, 0, 1), new(0, 0, -1)
        };

        private static readonly Vector3[] Normals =
        {
            Vector3.right, Vector3.left, Vector3.up,
            Vector3.down, Vector3.forward, Vector3.back
        };

        private static readonly Vector3[] Tangents =
        {
            Vector3.up, Vector3.down, Vector3.right,
            Vector3.right, Vector3.right, Vector3.left
        };

        private static readonly Vector3[] Bitangents =
        {
            Vector3.forward, Vector3.forward, Vector3.back,
            Vector3.forward, Vector3.up, Vector3.up
        };

        private readonly List<VoxelDestructionSample> m_ImpactSamples = new(12);
        private VoxelDestructionEffects m_Effects;
        private Mesh m_Mesh;
        private Vector3 m_InitialOrigin;
        private float m_Age;
        private float m_ImpactAge = -1f;
        public int CellCount { get; private set; }

        public static VoxelFallingFragment Spawn(ChunkManager manager, IReadOnlyList<Cell> cells, int start, int count,
                                                 VoxelDestructionEffects effects)
        {
            if (count <= 0) return null;
            var go = new GameObject("Falling voxel fragment", typeof(MeshFilter), typeof(MeshRenderer));
            SceneManager.MoveGameObjectToScene(go, manager.gameObject.scene);
            go.layer = 2; // Ignore Raycast: shots continue through transient rubble.
            var fragment = go.AddComponent<VoxelFallingFragment>();
            fragment.m_Effects = effects;
            fragment.CellCount = count;

            int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue, maxZ = int.MinValue;
            var occupied = new HashSet<Position3Int>();
            for (int i = start; i < start + count; i++)
            {
                Position3Int p = cells[i].position;
                occupied.Add(p);
                minX = Mathf.Min(minX, p.x); minY = Mathf.Min(minY, p.y); minZ = Mathf.Min(minZ, p.z);
                maxX = Mathf.Max(maxX, p.x); maxY = Mathf.Max(maxY, p.y); maxZ = Mathf.Max(maxZ, p.z);
            }

            Vector3 origin = new((minX + maxX + 1) * 0.5f,
                                 (minY + maxY + 1) * 0.5f,
                                 (minZ + maxZ + 1) * 0.5f);
            go.transform.position = origin;
            fragment.m_InitialOrigin = origin;
            var vertices = new List<Vector3>(count * 24);
            var triangles = new List<int>(count * 36);
            var normals = new List<Vector3>(count * 24);
            var colors = new List<Color32>(count * 24);
            var uvs = new List<Vector2>(count * 24);
            for (int i = start; i < start + count; i++)
            {
                Cell cell = cells[i];
                Vector3 center = (Vector3) cell.position + Vector3.one * 0.5f - origin;
                for (int face = 0; face < 6; face++)
                {
                    if (occupied.Contains(cell.position + Directions[face])) continue;
                    Vector3 normal = Normals[face], tangent = Tangents[face], bitangent = Bitangents[face];
                    Vector3 faceCenter = center + normal * 0.5f;
                    int index = vertices.Count;
                    vertices.Add(faceCenter - tangent * 0.5f - bitangent * 0.5f);
                    vertices.Add(faceCenter + tangent * 0.5f - bitangent * 0.5f);
                    vertices.Add(faceCenter + tangent * 0.5f + bitangent * 0.5f);
                    vertices.Add(faceCenter - tangent * 0.5f + bitangent * 0.5f);
                    triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
                    triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 3);
                    Vector2Int tile = cell.voxel.TexturePosition();
                    float u = tile.x * Voxel.TileRatio, v = tile.y * Voxel.TileRatio;
                    uvs.Add(new Vector2(u, v));
                    uvs.Add(new Vector2(u + Voxel.TileRatio, v));
                    uvs.Add(new Vector2(u + Voxel.TileRatio, v + Voxel.TileRatio));
                    uvs.Add(new Vector2(u, v + Voxel.TileRatio));
                    for (int j = 0; j < 4; j++)
                    {
                        normals.Add(normal);
                        colors.Add(cell.voxel.color);
                    }
                }
                if (fragment.m_ImpactSamples.Count < 12 && (i - start) % Mathf.Max(1, count / 12) == 0)
                    fragment.m_ImpactSamples.Add(new VoxelDestructionSample((Vector3) cell.position + Vector3.one * 0.5f,
                                                                             cell.voxel.color, cell.voxel.texture));
            }

            fragment.m_Mesh = new Mesh {name = "Falling voxel mesh", indexFormat = IndexFormat.UInt32};
            fragment.m_Mesh.SetVertices(vertices);
            fragment.m_Mesh.SetTriangles(triangles, 0);
            fragment.m_Mesh.SetNormals(normals);
            fragment.m_Mesh.SetColors(colors);
            fragment.m_Mesh.SetUVs(0, uvs);
            fragment.m_Mesh.RecalculateBounds();
            go.GetComponent<MeshFilter>().sharedMesh = fragment.m_Mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = manager.TerrainMaterial;

            // One collider per small occupied region keeps irregular ruins from
            // resting on the empty space inside a single giant bounding box.
            const int colliderCellSize = 4;
            var buckets = new Dictionary<Vector3Int, (Vector3Int min, Vector3Int max)>();
            for (int i = start; i < start + count; i++)
            {
                Position3Int p = cells[i].position;
                var value = new Vector3Int(p.x, p.y, p.z);
                var bucket = new Vector3Int((p.x - minX) / colliderCellSize,
                                            (p.y - minY) / colliderCellSize,
                                            (p.z - minZ) / colliderCellSize);
                if (buckets.TryGetValue(bucket, out var bounds))
                    buckets[bucket] = (Vector3Int.Min(bounds.min, value), Vector3Int.Max(bounds.max, value));
                else buckets.Add(bucket, (value, value));
            }
            if (buckets.Count > 48)
                AddCollider(go, new Vector3Int(minX, minY, minZ), new Vector3Int(maxX, maxY, maxZ), origin);
            else
                foreach (var bounds in buckets.Values) AddCollider(go, bounds.min, bounds.max, origin);

            var body = go.AddComponent<Rigidbody>();
            body.mass = Mathf.Clamp(count * 0.12f, 0.12f, 200f);
            body.linearDamping = 0.05f;
            body.angularDamping = 0.3f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.linearVelocity = Vector3.down * 2.6f;
            body.angularVelocity = Random.insideUnitSphere * 0.7f;
            return fragment;
        }

        private static void AddCollider(GameObject go, Vector3Int min, Vector3Int max, Vector3 origin)
        {
            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.center = (Vector3) (min + max) * 0.5f + Vector3.one * 0.5f - origin;
            collider.size = (Vector3) (max - min) + Vector3.one;
        }

        private void Update()
        {
            m_Age += Time.deltaTime;
            if (m_ImpactAge >= 0f)
            {
                m_ImpactAge += Time.deltaTime;
                transform.localScale = Vector3.one * Mathf.Max(0.01f, 1f - m_ImpactAge / 0.42f);
                if (m_ImpactAge >= 0.42f) Destroy(gameObject);
            }
            else if (m_Age > 9f) BreakApart();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (m_ImpactAge < 0f && m_Age > 0.12f && collision.relativeVelocity.magnitude > 1.4f)
                BreakApart();
        }

        private void BreakApart()
        {
            if (m_ImpactAge >= 0f) return;
            m_ImpactAge = 0f;
            if (m_Effects && m_ImpactSamples.Count > 0)
            {
                for (int i = 0; i < m_ImpactSamples.Count; i++)
                {
                    VoxelDestructionSample sample = m_ImpactSamples[i];
                    m_ImpactSamples[i] = new VoxelDestructionSample(transform.TransformPoint(sample.position - m_InitialOrigin),
                                                                    sample.color, sample.texture);
                }
                m_Effects.EmitCollapse(m_ImpactSamples, transform.position);
            }
            Rigidbody body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            foreach (BoxCollider collider in GetComponents<BoxCollider>()) collider.enabled = false;
        }

        private void OnDestroy()
        {
            if (m_Mesh) Destroy(m_Mesh);
        }
    }
}

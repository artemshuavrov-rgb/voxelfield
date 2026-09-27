using System.Collections.Generic;
using Swihoni.Util.Math;
using UnityEngine;

namespace Voxels
{
    // Flood fills components exposed by destruction. Small pieces detach in the
    // edit frame; large pieces are searched over several frames instead of being
    // incorrectly declared supported when a fixed voxel limit is reached.
    public sealed class VoxelStructuralGravity
    {
        private const int ImmediateSearchBudget = 768;
        private const int FrameSearchBudget = 3072;
        private const int MaxCellsPerFallingFragment = 4096;
        private const int MaxVisualSamples = 24;

        private static readonly Position3Int[] Neighbors =
        {
            new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0),
            new(0, -1, 0), new(0, 0, 1), new(0, 0, -1)
        };

        private sealed class SearchRequest
        {
            public Position3Int seed;
            public List<(Chunk, Position3Int, Voxel)> undo;
        }

        private sealed class PendingFragment
        {
            public List<VoxelFallingFragment.Cell> cells;
            public int start, count;
        }

        private readonly ChunkManager m_Manager;
        private readonly Queue<SearchRequest> m_Requests = new();
        private readonly Queue<Position3Int> m_SearchQueue = new();
        private readonly HashSet<Position3Int> m_SearchVisited = new();
        private readonly HashSet<Position3Int> m_Resolved = new();
        private readonly List<Position3Int> m_Component = new();
        private readonly List<VoxelDestructionSample> m_Samples = new(MaxVisualSamples);
        private readonly Queue<PendingFragment> m_PendingFragments = new();
        private readonly TouchedChunks m_DeferredTouched = new();
        private SearchRequest m_Current;
        private int m_Revision, m_SearchRevision;

        public VoxelStructuralGravity(ChunkManager manager) => m_Manager = manager;

        public void Reset()
        {
            m_Requests.Clear();
            m_SearchQueue.Clear();
            m_SearchVisited.Clear();
            m_Resolved.Clear();
            m_Component.Clear();
            m_PendingFragments.Clear();
            m_DeferredTouched.Clear();
            m_Current = null;
            m_Revision++;
        }

        public void CollapseUnsupported(IReadOnlyList<Position3Int> removed, List<(Chunk, Position3Int, Voxel)> undo,
                                        TouchedChunks touched)
        {
            NotifyTopologyChanged();
            var seeds = new HashSet<Position3Int>();
            foreach (Position3Int removedPosition in removed)
            foreach (Position3Int direction in Neighbors)
            {
                Position3Int seed = removedPosition + direction;
                if (seeds.Add(seed) && IsSolid(seed))
                    m_Requests.Enqueue(new SearchRequest {seed = seed, undo = undo});
            }
            ProcessSearches(ImmediateSearchBudget, touched);
        }

        public void NotifyTopologyChanged()
        {
            m_Revision++;
            m_Resolved.Clear();
        }

        public void Tick()
        {
            if (m_Requests.Count > 0 || m_Current != null)
            {
                ProcessSearches(FrameSearchBudget, m_DeferredTouched);
                if (m_DeferredTouched.Count > 0) m_DeferredTouched.UpdateMesh();
            }
            if (m_PendingFragments.Count > 0)
            {
                PendingFragment next = m_PendingFragments.Dequeue();
                VoxelFallingFragment.Spawn(m_Manager, next.cells, next.start, next.count, GetEffects());
            }
        }

        private void ProcessSearches(int budget, TouchedChunks touched)
        {
            while (budget > 0)
            {
                if (m_Current == null)
                {
                    if (m_Requests.Count == 0) return;
                    m_Current = m_Requests.Dequeue();
                    if (m_Resolved.Contains(m_Current.seed) || !IsSolid(m_Current.seed) ||
                        HasVerticalFoundation(m_Current.seed))
                    {
                        m_Resolved.Add(m_Current.seed);
                        m_Current = null;
                        continue;
                    }
                    BeginCurrentSearch();
                }
                else if (m_SearchRevision != m_Revision) BeginCurrentSearch();

                if (m_SearchQueue.Count == 0)
                {
                    foreach (Position3Int p in m_Component) m_Resolved.Add(p);
                    if (m_Component.Count > 0) CollapseComponent(m_Current.undo, touched);
                    m_Current = null;
                    continue;
                }

                Position3Int position = m_SearchQueue.Dequeue();
                if (!IsSolid(position)) continue;
                m_Component.Add(position);
                budget--;
                if (IsAnchored(position))
                {
                    foreach (Position3Int p in m_Component) m_Resolved.Add(p);
                    m_Current = null;
                    continue;
                }
                foreach (Position3Int direction in Neighbors)
                {
                    Position3Int next = position + direction;
                    if (m_SearchVisited.Add(next) && IsSolid(next)) m_SearchQueue.Enqueue(next);
                }
            }
        }

        private void BeginCurrentSearch()
        {
            m_SearchRevision = m_Revision;
            m_SearchQueue.Clear();
            m_SearchVisited.Clear();
            m_Component.Clear();
            m_SearchQueue.Enqueue(m_Current.seed);
            m_SearchVisited.Add(m_Current.seed);
        }

        private bool IsAnchored(Position3Int position)
        {
            int minX = m_Manager.Map.dimension.lowerBound.Value.x * m_Manager.ChunkSize;
            int minY = m_Manager.Map.dimension.lowerBound.Value.y * m_Manager.ChunkSize;
            int minZ = m_Manager.Map.dimension.lowerBound.Value.z * m_Manager.ChunkSize;
            int maxX = (m_Manager.Map.dimension.upperBound.Value.x + 1) * m_Manager.ChunkSize - 1;
            int maxZ = (m_Manager.Map.dimension.upperBound.Value.z + 1) * m_Manager.ChunkSize - 1;
            // An unbreakable material alone is not a foundation: an isolated
            // authored structure can be made from it and still must fall.
            return position.y <= minY || position.x <= minX || position.x >= maxX ||
                   position.z <= minZ || position.z >= maxZ;
        }

        private bool HasVerticalFoundation(Position3Int seed)
        {
            int minY = m_Manager.Map.dimension.lowerBound.Value.y * m_Manager.ChunkSize;
            for (int y = seed.y - 1; y >= minY; y--)
                if (!IsSolid(new Position3Int(seed.x, y, seed.z))) return false;
            return true;
        }

        private void CollapseComponent(List<(Chunk, Position3Int, Voxel)> undo, TouchedChunks touched)
        {
            m_Samples.Clear();
            var cells = new List<VoxelFallingFragment.Cell>(m_Component.Count);
            int sampleStride = Mathf.Max(1, m_Component.Count / MaxVisualSamples);
            var empty = new VoxelChange {density = 0, hasBlock = false, natural = false};
            for (int i = 0; i < m_Component.Count; i++)
            {
                Position3Int position = m_Component[i];
                Chunk chunk = m_Manager.GetChunkFromWorldPosition(position);
                if (!chunk) continue;
                Position3Int local = m_Manager.WorldVoxelToChunkVoxel(position, chunk);
                Voxel before = chunk.GetVoxelNoCheck(local);
                if (!before.HasBlock && before.density < 128) continue;
                undo?.Add((chunk, local, before));
                cells.Add(new VoxelFallingFragment.Cell(position, before));
                chunk.SetVoxelDataNoCheck(local, empty);
                m_Manager.AddChunksToUpdateFromVoxel(local, chunk, touched);
                if (i % sampleStride == 0 && m_Samples.Count < MaxVisualSamples)
                    m_Samples.Add(new VoxelDestructionSample((Vector3) position + Vector3.one * 0.5f,
                                                              before.color, before.texture));
            }
            if (cells.Count == 0 || Application.isBatchMode) return;
            var effects = GetEffects();
            effects.EmitCollapse(m_Samples, (Vector3) cells[0].position + Vector3.one * 0.5f);
            int firstCount = Mathf.Min(MaxCellsPerFallingFragment, cells.Count);
            VoxelFallingFragment.Spawn(m_Manager, cells, 0, firstCount, effects);
            for (int start = firstCount; start < cells.Count; start += MaxCellsPerFallingFragment)
                m_PendingFragments.Enqueue(new PendingFragment
                {
                    cells = cells, start = start,
                    count = Mathf.Min(MaxCellsPerFallingFragment, cells.Count - start)
                });
        }

        private VoxelDestructionEffects GetEffects()
        {
            var effects = m_Manager.GetComponent<VoxelDestructionEffects>();
            if (!effects) effects = m_Manager.gameObject.AddComponent<VoxelDestructionEffects>();
            effects.Initialize(m_Manager.TerrainMaterial);
            return effects;
        }

        private bool IsSolid(Position3Int position)
        {
            Voxel? voxel = m_Manager.GetVoxel(position);
            return voxel.HasValue && (voxel.Value.HasBlock || voxel.Value.density >= 128);
        }
    }
}

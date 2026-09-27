using System.Collections.Generic;
using Swihoni.Util.Math;
using UnityEngine;

namespace Voxels
{
    // Resolves only components exposed by an edit. A bounded search prevents
    // a shot into the ground from traversing the entire terrain in one frame.
    public sealed class VoxelStructuralGravity
    {
        private const int MaxSearchVoxels = 4096;
        private const int MaxCollapseVoxels = 2048;
        private const int MaxSearchesPerEdit = 16;
        private const int MaxVisualSamples = 24;

        private static readonly Position3Int[] Neighbors =
        {
            new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0),
            new(0, -1, 0), new(0, 0, 1), new(0, 0, -1)
        };

        private readonly ChunkManager m_Manager;
        private readonly HashSet<Position3Int> m_Visited = new();
        private readonly Queue<Position3Int> m_Queue = new();
        private readonly List<Position3Int> m_Component = new();
        private readonly List<VoxelDestructionSample> m_Samples = new(MaxVisualSamples);

        public VoxelStructuralGravity(ChunkManager manager) => m_Manager = manager;

        public void CollapseUnsupported(IReadOnlyList<Position3Int> removed, List<(Chunk, Position3Int, Voxel)> undo, TouchedChunks touched)
        {
            m_Visited.Clear();
            int searches = 0;
            foreach (Position3Int removedPosition in removed)
            foreach (Position3Int direction in Neighbors)
            {
                Position3Int seed = removedPosition + direction;
                if (m_Visited.Contains(seed) || !IsSolid(seed) || searches >= MaxSearchesPerEdit) continue;
                searches++;
                if (!FindUnsupportedComponent(seed)) continue;
                CollapseComponent(undo, touched);
            }
        }

        private bool FindUnsupportedComponent(Position3Int seed)
        {
            m_Queue.Clear();
            m_Component.Clear();
            m_Visited.Add(seed);
            m_Queue.Enqueue(seed);
            bool supported = false;
            int minX = m_Manager.Map.dimension.lowerBound.Value.x * m_Manager.ChunkSize;
            int minY = m_Manager.Map.dimension.lowerBound.Value.y * m_Manager.ChunkSize;
            int minZ = m_Manager.Map.dimension.lowerBound.Value.z * m_Manager.ChunkSize;
            int maxX = (m_Manager.Map.dimension.upperBound.Value.x + 1) * m_Manager.ChunkSize - 1;
            int maxY = (m_Manager.Map.dimension.upperBound.Value.y + 1) * m_Manager.ChunkSize - 1;
            int maxZ = (m_Manager.Map.dimension.upperBound.Value.z + 1) * m_Manager.ChunkSize - 1;

            while (m_Queue.Count > 0 && m_Component.Count < MaxSearchVoxels)
            {
                Position3Int position = m_Queue.Dequeue();
                Voxel voxel = m_Manager.GetVoxel(position).Value;
                m_Component.Add(position);
                if (!voxel.IsBreakable || position.x <= minX || position.x >= maxX ||
                    position.y <= minY || position.y >= maxY || position.z <= minZ || position.z >= maxZ)
                    supported = true;

                foreach (Position3Int direction in Neighbors)
                {
                    Position3Int next = position + direction;
                    if (!m_Visited.Add(next)) continue;
                    if (IsSolid(next)) m_Queue.Enqueue(next);
                }
            }
            // Anything larger than the budget is treated as stable. It can be
            // rechecked after subsequent edits, without a long frame stall.
            return !supported && m_Queue.Count == 0 && m_Component.Count <= MaxCollapseVoxels;
        }

        private void CollapseComponent(List<(Chunk, Position3Int, Voxel)> undo, TouchedChunks touched)
        {
            m_Samples.Clear();
            Vector3 center = Vector3.zero;
            int sampleStride = Mathf.Max(1, m_Component.Count / MaxVisualSamples);
            var empty = new VoxelChange {density = 0, hasBlock = false, natural = false};
            for (int i = 0; i < m_Component.Count; i++)
            {
                Position3Int position = m_Component[i];
                Chunk chunk = m_Manager.GetChunkFromWorldPosition(position);
                if (!chunk) continue;
                Position3Int local = m_Manager.WorldVoxelToChunkVoxel(position, chunk);
                Voxel before = chunk.GetVoxelNoCheck(local);
                undo?.Add((chunk, local, before));
                chunk.SetVoxelDataNoCheck(local, empty);
                m_Manager.AddChunksToUpdateFromVoxel(local, chunk, touched);
                Vector3 visualPosition = (Vector3) position + Vector3.one * 0.5f;
                center += visualPosition;
                if (i % sampleStride == 0 && m_Samples.Count < MaxVisualSamples)
                    m_Samples.Add(new VoxelDestructionSample(visualPosition, before.color, before.texture));
            }
            if (m_Samples.Count == 0 || Application.isBatchMode) return;
            var effects = m_Manager.GetComponent<VoxelDestructionEffects>();
            if (!effects) effects = m_Manager.gameObject.AddComponent<VoxelDestructionEffects>();
            effects.Initialize(m_Manager.TerrainMaterial);
            effects.EmitCollapse(m_Samples, center / m_Component.Count);
        }

        private bool IsSolid(Position3Int position)
        {
            Voxel? voxel = m_Manager.GetVoxel(position);
            return voxel.HasValue && (voxel.Value.HasBlock || voxel.Value.density >= 128);
        }
    }
}

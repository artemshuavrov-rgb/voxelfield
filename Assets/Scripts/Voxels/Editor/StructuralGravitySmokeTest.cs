#if UNITY_EDITOR
using System;
using Swihoni.Util.Math;
using UnityEditor;
using UnityEngine;

namespace Voxels.Editor
{
    public static class StructuralGravitySmokeTest
    {
        [MenuItem("Voxelfield/Verify Structural Gravity")]
        private static void Verify()
        {
            try { VerifyCore(); }
            catch (Exception exception) { Debug.LogError($"Structural gravity exception: {exception}"); }
        }

        private static void VerifyCore()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("Enter Play Mode and start the solo demo before running the gravity check.");
                return;
            }

            ChunkManager manager = UnityEngine.Object.FindFirstObjectByType<ChunkManager>();
            if (!manager || manager.Map?.dimension?.lowerBound is null)
            {
                Debug.LogError("Gravity check needs a loaded map.");
                return;
            }

            int minX = manager.Map.dimension.lowerBound.Value.x * manager.ChunkSize + 2;
            int maxX = (manager.Map.dimension.upperBound.Value.x + 1) * manager.ChunkSize - 2;
            int minZ = manager.Map.dimension.lowerBound.Value.z * manager.ChunkSize + 2;
            int maxZ = (manager.Map.dimension.upperBound.Value.z + 1) * manager.ChunkSize - 2;
            int minY = manager.Map.dimension.lowerBound.Value.y * manager.ChunkSize + 3;
            int maxY = (manager.Map.dimension.upperBound.Value.y + 1) * manager.ChunkSize - 4;
            for (int y = maxY; y >= minY; y--)
            for (int x = minX; x <= maxX; x++)
            for (int z = minZ; z <= maxZ; z++)
            {
                var lower = new Position3Int(x, y, z);
                var upper = new Position3Int(x, y + 1, z);
                if (Solid(manager.GetVoxel(lower)) || Solid(manager.GetVoxel(upper))) continue;
                if (Solid(manager.GetVoxel(upper + new Position3Int(1, 0, 0))) ||
                    Solid(manager.GetVoxel(upper + new Position3Int(-1, 0, 0))) ||
                    Solid(manager.GetVoxel(upper + new Position3Int(0, 1, 0))) ||
                    Solid(manager.GetVoxel(upper + new Position3Int(0, 0, 1))) ||
                    Solid(manager.GetVoxel(upper + new Position3Int(0, 0, -1)))) continue;

                var block = new VoxelChange
                {
                    position = lower, form = VoxelVolumeForm.Single, hasBlock = true,
                    density = 255, isBreakable = true, natural = false,
                    texture = VoxelTexture.Solid, color = new Color32(255, 200, 60, 255)
                };
                manager.ApplyVoxelChanges(block);
                block.position = upper;
                manager.ApplyVoxelChanges(block);
                var timer = System.Diagnostics.Stopwatch.StartNew();
                manager.ApplyVoxelChanges(new VoxelChange
                {
                    position = lower, form = VoxelVolumeForm.Single,
                    hasBlock = false, density = 0, natural = false
                });
                timer.Stop();
                bool collapsed = !Solid(manager.GetVoxel(upper));
                if (collapsed) Debug.Log($"Structural gravity PASS: unsupported block at {upper} crumbled; edit took {timer.Elapsed.TotalMilliseconds:F1} ms.");
                else Debug.LogError($"Structural gravity FAIL: unsupported block at {upper} remained.");
                return;
            }
            Debug.LogError("Structural gravity check found no empty test space in the map.");
        }

        private static bool Solid(Voxel? voxel)
            => voxel.HasValue && (voxel.Value.HasBlock || voxel.Value.density >= 128);
    }
}
#endif

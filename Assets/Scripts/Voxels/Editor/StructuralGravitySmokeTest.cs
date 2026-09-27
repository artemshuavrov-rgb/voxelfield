#if UNITY_EDITOR
using System;
using Swihoni.Util.Math;
using UnityEditor;
using UnityEngine;

namespace Voxels.Editor
{
    public static class StructuralGravitySmokeTest
    {
        [MenuItem("Voxelfield/Verify Large Structural Collapse")]
        private static void VerifyLarge()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("Start the solo demo before running the large collapse check.");
                return;
            }
            ChunkManager manager = UnityEngine.Object.FindFirstObjectByType<ChunkManager>();
            if (!manager || manager.Map?.dimension?.lowerBound is null) return;
            const int width = 16, height = 9, depth = 16;
            int minX = manager.Map.dimension.lowerBound.Value.x * manager.ChunkSize + 2;
            int maxX = (manager.Map.dimension.upperBound.Value.x + 1) * manager.ChunkSize - width - 2;
            int minY = manager.Map.dimension.lowerBound.Value.y * manager.ChunkSize + 3;
            int maxY = (manager.Map.dimension.upperBound.Value.y + 1) * manager.ChunkSize - height - 2;
            int minZ = manager.Map.dimension.lowerBound.Value.z * manager.ChunkSize + 2;
            int maxZ = (manager.Map.dimension.upperBound.Value.z + 1) * manager.ChunkSize - depth - 2;
            for (int y = maxY; y >= minY; y--)
            for (int x = minX; x <= maxX; x += 2)
            for (int z = minZ; z <= maxZ; z += 2)
            {
                bool empty = true;
                for (int ix = -1; ix <= width && empty; ix++)
                for (int iy = -1; iy <= height && empty; iy++)
                for (int iz = -1; iz <= depth; iz++)
                    if (Solid(manager.GetVoxel(new Position3Int(x + ix, y + iy, z + iz))))
                    {
                        empty = false;
                        break;
                    }
                if (!empty) continue;

                var support = new Position3Int(x + width / 2, y - 1, z + depth / 2);
                manager.ApplyVoxelChanges(new VoxelChange
                {
                    position = support, form = VoxelVolumeForm.Single, hasBlock = true,
                    density = 255, isBreakable = true, natural = false,
                    texture = VoxelTexture.Solid, color = new Color32(255, 190, 55, 255)
                });
                manager.ApplyVoxelChanges(new VoxelChange
                {
                    position = new Position3Int(x, y, z),
                    upperBound = new Position3Int(x + width - 1, y + height - 1, z + depth - 1),
                    form = VoxelVolumeForm.Prism, hasBlock = true,
                    density = 255, isBreakable = true, natural = false, noRandom = true,
                    texture = VoxelTexture.Solid, color = new Color32(255, 190, 55, 255)
                });
                manager.ApplyVoxelChanges(new VoxelChange
                {
                    position = support, form = VoxelVolumeForm.Single,
                    hasBlock = false, density = 0, natural = false
                });

                float deadline = Time.realtimeSinceStartup + 3f;
                VoxelFallingFragment fragment = null;
                float startingY = 0f;
                void CheckLarge()
                {
                    if (!fragment)
                    {
                        foreach (VoxelFallingFragment candidate in UnityEngine.Object.FindObjectsByType<VoxelFallingFragment>(FindObjectsSortMode.None))
                            if (candidate.CellCount >= width * height * depth)
                            {
                                fragment = candidate;
                                startingY = candidate.transform.position.y;
                                break;
                            }
                    }
                    if (fragment && fragment.transform.position.y < startingY - 0.2f)
                    {
                        EditorApplication.update -= CheckLarge;
                        Debug.Log($"Large structural gravity PASS: {fragment.CellCount} voxels detached and fell.");
                    }
                    else if (Time.realtimeSinceStartup > deadline)
                    {
                        EditorApplication.update -= CheckLarge;
                        Debug.LogError("Large structural gravity FAIL: component remained suspended.");
                    }
                }
                EditorApplication.update += CheckLarge;
                Debug.Log($"Large structural gravity: cut support under {width * height * depth} voxels.");
                return;
            }
            Debug.LogWarning("Large structural gravity check found no empty test volume on this map.");
        }

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
                if (Solid(manager.GetVoxel(lower)) || Solid(manager.GetVoxel(upper)) ||
                    Solid(manager.GetVoxel(lower + new Position3Int(0, -1, 0)))) continue;
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
                VoxelFallingFragment fragment = UnityEngine.Object.FindFirstObjectByType<VoxelFallingFragment>();
                if (!collapsed || !fragment)
                {
                    Debug.LogError($"Structural gravity FAIL: detached block at {upper} did not become a falling fragment.");
                    return;
                }
                Debug.Log($"Structural gravity: block at {upper} detached; edit took {timer.Elapsed.TotalMilliseconds:F1} ms.");
                float startingY = fragment.transform.position.y;
                float checkAfter = Time.realtimeSinceStartup + 0.22f;
                void CheckFall()
                {
                    if (Time.realtimeSinceStartup < checkAfter) return;
                    EditorApplication.update -= CheckFall;
                    if (fragment && fragment.transform.position.y < startingY - 0.2f)
                        Debug.Log($"Structural gravity PASS: detached object fell {startingY - fragment.transform.position.y:F2} m.");
                    else Debug.LogError("Structural gravity FAIL: detached object remained in the air.");
                }
                EditorApplication.update += CheckFall;
                return;
            }
            Debug.LogError("Structural gravity check found no empty test space in the map.");
        }

        private static bool Solid(Voxel? voxel)
            => voxel.HasValue && (voxel.Value.HasBlock || voxel.Value.density >= 128);
    }
}
#endif

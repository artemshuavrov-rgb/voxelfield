using Swihoni.Components;
using Swihoni.Sessions;
using Swihoni.Sessions.Items.Modifiers;
using Swihoni.Sessions.Modes;
using Swihoni.Sessions.Player.Components;
using Swihoni.Sessions.Player.Modifiers;
using Swihoni.Util.Math;
using UnityEngine;
using Voxels;
using Voxels.Map;
using Random = UnityEngine.Random;

namespace Voxelfield.Session.Mode
{
    [CreateAssetMenu(fileName = "Warmup", menuName = "Session/Mode/Warmup", order = 0)]
    public class DeathmatchMode : DeathmatchModeBase
    {
        protected override void SpawnPlayer(in SessionContext context, bool begin = false)
        {
            base.SpawnPlayer(context, begin);
            if (context.player.With(out InventoryComponent inventory))
            {
                // Slot 1 is selected by default by the input system. Make it a rifle.
                PlayerItemManagerModiferBehavior.SetItemAtIndex(inventory, ItemId.Rifle, 0);
                PlayerItemManagerModiferBehavior.SetItemAtIndex(inventory, ItemId.Pickaxe, 1);
            }
            if (context.player.With(out MoveComponent move) && context.player.With(out CameraComponent camera))
            {
                Vector3 position = move.position.Value;
                int chunkSize = context.GetChunkManager().ChunkSize;
                DimensionComponent bounds = context.GetMapManager().Map.dimension;
                Position3Int lower = bounds.lowerBound, upper = bounds.upperBound;
                Vector3 center = new((lower.x + upper.x + 1) * chunkSize * 0.5f,
                                     0f,
                                     (lower.z + upper.z + 1) * chunkSize * 0.5f);
                Vector3 toCenter = center - new Vector3(position.x, 0, position.z);
                if (toCenter.sqrMagnitude > 1f)
                    camera.yaw.Value = Mathf.Atan2(toCenter.x, toCenter.z) * Mathf.Rad2Deg;
                camera.pitch.Value = 8f;
            }
        }

        protected override float CalculateWeaponDamage(in PlayerHitContext context)
        {
            float baseDamage = base.CalculateWeaponDamage(context);
            return ShowdownMode.CalculateDamageWithMovement(context, baseDamage);
        }

        protected override Vector3 GetSpawnPosition(in SessionContext context)
        {
            // The authored markers can be inside the Castle's rock formations. Pick a
            // clear, supported patch of ground instead of leaving the player in a wall.
            var blockers = new Collider[8];
            Vector3 best = default;
            float bestScore = float.NegativeInfinity;
            for (int x = -36; x <= 36; x += 6)
            for (int z = -36; z <= 36; z += 6)
            {
                if (!context.PhysicsScene.Raycast(new Vector3(x, 1000f, z), Vector3.down,
                                                  out RaycastHit ground, 1500f,
                                                  Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    || ground.normal.y < 0.75f) continue;

                Vector3 feet = ground.point + Vector3.up * 0.55f;
                if (context.PhysicsScene.OverlapCapsule(feet, feet + Vector3.up * 1.2f,
                                                        0.35f, blockers, Physics.DefaultRaycastLayers,
                                                        QueryTriggerInteraction.Ignore) != 0) continue;

                bool supported = true;
                foreach (Vector3 direction in new[] { Vector3.left, Vector3.right, Vector3.forward, Vector3.back })
                {
                    if (!context.PhysicsScene.Raycast(ground.point + direction * 2f + Vector3.up * 3f,
                                                      Vector3.down, out RaycastHit nearby, 6f,
                                                      Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                        || nearby.normal.y < 0.65f || Mathf.Abs(nearby.point.y - ground.point.y) > 2.5f)
                    {
                        supported = false;
                        break;
                    }
                }
                if (!supported) continue;

                Vector3 eye = ground.point + Vector3.up * 1.6f;
                float score = -new Vector2(x, z).magnitude * 0.8f;
                foreach (Vector3 direction in new[] { Vector3.left, Vector3.right, Vector3.forward, Vector3.back })
                {
                    score += context.PhysicsScene.Raycast(eye, direction, out RaycastHit wall, 14f,
                                                          Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                        ? wall.distance : 14f;
                }
                if (score <= bestScore) continue;
                bestScore = score;
                best = ground.point + Vector3.up * 0.15f;
            }
            if (bestScore > float.NegativeInfinity) return best;
            return GetRandomPosition(context);
        }

        public static Vector3 GetRandomPosition(in SessionContext context)
        {
            int chunkSize = context.GetChunkManager().ChunkSize;
            DimensionComponent dimension = context.GetMapManager().Map.dimension;
            Position3Int lower = dimension.lowerBound, upper = dimension.upperBound;
            for (var _ = 0; _ < 32; _++)
            {
                var position = new Vector3
                {
                    x = Random.Range(lower.x * chunkSize, (upper.x + 1) * chunkSize),
                    y = 1000.0f,
                    z = Random.Range(lower.z * chunkSize, (upper.z + 1) * chunkSize)
                };
                if (context.PhysicsScene.Raycast(position, Vector3.down, out RaycastHit hit, float.PositiveInfinity))
                    return hit.point + new Vector3 {y = 0.1f};
            }
            return new Vector3 {y = 8.0f};
        }
    }
}

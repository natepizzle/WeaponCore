﻿using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRageMath;
using WeaponCore.Projectiles;
using WeaponCore.Support;

namespace WeaponCore
{
    public partial class Session
    {
        internal void ProcessHits()
        {
            Projectile p;
            while (Projectiles.Hits.TryDequeue(out p))
            {
                var maxObjects = p.T.System.MaxObjectsHit;
                for (int i = 0; i < p.HitList.Count; i++)
                {
                    var hitEnt = p.HitList[i];
                    if (p.BaseDamagePool <= 0 || p.ObjectsHit >= maxObjects)
                    {
                        p.State = Projectile.ProjectileState.Depleted;
                        Projectiles.HitEntityPool[p.PoolId].Return(hitEnt);
                        continue;
                    }
                    switch (hitEnt.EventType)
                    {
                        case HitEntity.Type.Shield:
                            DamageShield(hitEnt, p);
                            continue;
                        case HitEntity.Type.Grid:
                            DamageGrid(hitEnt, p);
                            continue;
                        case HitEntity.Type.Destroyable:
                            DamageDestObj(hitEnt, p);
                            continue;
                        case HitEntity.Type.Voxel:
                            DamageVoxel(hitEnt, p);
                            continue;
                        case HitEntity.Type.Proximity:
                            ExplosionProximity(hitEnt, p);
                            continue;
                    }
                    Projectiles.HitEntityPool[p.PoolId].Return(hitEnt);
                }

                if (p.BaseDamagePool <= 0)
                {
                    //Log.Line($"Depleted2: pool:{projectile.BaseDamagePool} - objHit:{projectile.ObjectsHit}");
                    p.State = Projectile.ProjectileState.Depleted;
                }
                p.HitList.Clear();
            }
        }

        private void DamageShield(HitEntity hitEnt, Projectile p)
        {
            var shield = hitEnt.Entity as IMyTerminalBlock;
            var system = p.T.System;
            if (shield == null || !hitEnt.HitPos.HasValue) return;
            p.ObjectsHit++;
            SApi.PointAttackShield(shield, hitEnt.HitPos.Value, p.T.FiringCube.EntityId, p.BaseDamagePool, false, true);
            if (system.Values.Ammo.Mass > 0)
            {
                var speed = system.Values.Ammo.Trajectory.DesiredSpeed > 0 ? system.Values.Ammo.Trajectory.DesiredSpeed : 1;
                ApplyProjectileForce((MyEntity)shield.CubeGrid, hitEnt.HitPos.Value, p.Direction, system.Values.Ammo.Mass * speed);
            }
            p.BaseDamagePool = 0;
        }

        private void DamageGrid(HitEntity hitEnt, Projectile p)
        {
            var grid = hitEnt.Entity as MyCubeGrid;
            var system = p.T.System;

            if (grid == null || grid.MarkedForClose || !hitEnt.HitPos.HasValue || hitEnt.Blocks == null)
            {
                //Log.Line($"grid something is null: gridNull:{grid == null} - gridMarked:{grid?.MarkedForClose} - noHitValue:{!hitEnt.HitPos.HasValue} - blocksNull:{hitEnt.Blocks == null}");
                hitEnt.Blocks?.Clear();
                return;
            }
            //Log.Line($"new hit: blockCnt:{grid.BlocksCount} - pool:{projectile.BaseDamagePool} - objHit:{projectile.ObjectsHit}");
            _destroyedSlims.Clear();
            //var cubes = SlimSpace[grid];
            //var sphere = new BoundingSphereD(hitEnt.HitPos.Value, areaRadius);
            var largeGrid = grid.GridSizeEnum == MyCubeSize.Large;
            var areaRadius = largeGrid ? system.AreaRadiusLarge : system.AreaRadiusSmall;
            var detonateRadius = largeGrid ? system.DetonateRadiusLarge : system.DetonateRadiusSmall;
            var maxObjects = p.T.System.MaxObjectsHit;
            var areaEffect = system.Values.Ammo.AreaEffect.AreaEffect;
            var explosive = areaEffect == AreaDamage.AreaEffectType.Explosive;
            var radiant = areaEffect == AreaDamage.AreaEffectType.Radiant;
            var detonateOnEnd = system.Values.Ammo.AreaEffect.Detonation.DetonateOnEnd;

            var areaEffectDmg = system.Values.Ammo.AreaEffect.AreaEffectDamage;
            var detonateDmg = system.Values.Ammo.AreaEffect.Detonation.DetonationDamage;
            var hitMass = system.Values.Ammo.Mass;
            if (p.IsShrapnel)
            {
                var shrapnel = system.Values.Ammo.Shrapnel;
                areaEffectDmg = areaEffectDmg > 0 ? areaEffectDmg / shrapnel.Fragments : 0;
                detonateDmg = detonateDmg > 0 ? detonateDmg / shrapnel.Fragments : 0;
                hitMass = hitMass > 0 ? hitMass / shrapnel.Fragments : 0;
                areaRadius = ModRadius(areaRadius, largeGrid);
                detonateRadius = ModRadius(detonateRadius, largeGrid);
            }

            var hasAreaDmg = areaEffectDmg > 0;
            var radiantCascade = radiant && !detonateOnEnd;
            var primeDamage = !radiantCascade || !hasAreaDmg;
            var radiantBomb = radiant && detonateOnEnd;
            var damageType = explosive || radiant ? MyDamageType.Explosion : MyDamageType.Bullet;

            var damagePool = p.BaseDamagePool;
            if (system.VirtualBeams)
            {
                var hits = p.DamageFrame.Hits;
                damagePool *= hits;
                areaEffectDmg *= hits;
            }
            var objectsHit = p.ObjectsHit;
            var countBlocksAsObjects = system.Values.Ammo.ObjectsHit.CountBlocks;

            List<Vector3I> radiatedBlocks = null;
            if (radiant) GetBlockSphereDb(grid, areaRadius, out radiatedBlocks);

            var done = false;
            var nova = false;
            var outOfPew = false;
            for (int i = 0; i < hitEnt.Blocks.Count; i++)
            {
                if (done || outOfPew && !nova) break;

                var rootBlock = hitEnt.Blocks[i];

                if (!nova)
                {
                    if (_destroyedSlims.Contains(rootBlock)) continue;
                    if (rootBlock.IsDestroyed)
                    {
                        _destroyedSlims.Add(rootBlock);
                        continue;
                    }
                }
                var radiate = radiantCascade || nova;
                var dmgCount = 1;
                if (radiate)
                {
                    if (nova) GetBlockSphereDb(grid, detonateRadius, out radiatedBlocks);
                    ShiftAndPruneBlockSphere(grid, rootBlock.Position, radiatedBlocks, _slimsSortedList);

                    done = nova;
                    dmgCount = _slimsSortedList.Count;
                }

                for (int j = 0; j < dmgCount; j++)
                {
                    var block = radiate ? _slimsSortedList[j].Slim : rootBlock;
                    var blockHp = block.Integrity;
                    float damageScale = 1;

                    if (system.DamageScaling)
                    {
                        var d = system.Values.DamageScales;
                        if (d.MaxIntegrity > 0 && blockHp > d.MaxIntegrity) continue;

                        if (d.Grids.Large >= 0 && largeGrid) damageScale *= d.Grids.Large;
                        else if (d.Grids.Small >= 0 && !largeGrid) damageScale *= d.Grids.Small;

                        MyDefinitionBase blockDef = null;
                        if (system.ArmorScaling)
                        {
                            blockDef = block.BlockDefinition;
                            var isArmor = AllArmorBaseDefinitions.Contains(blockDef);
                            if (isArmor && d.Armor.Armor >= 0) damageScale *= d.Armor.Armor;
                            else if (!isArmor && d.Armor.NonArmor >= 0) damageScale *= d.Armor.NonArmor;

                            if (isArmor && (d.Armor.Light >= 0 || d.Armor.Heavy >= 0))
                            {
                                var isHeavy = HeavyArmorBaseDefinitions.Contains(blockDef);
                                if (isHeavy && d.Armor.Heavy >= 0) damageScale *= d.Armor.Heavy;
                                else if (!isHeavy && d.Armor.Light >= 0) damageScale *= d.Armor.Light;
                            }
                        }
                        if (system.CustomDamageScales)
                        {
                            if (blockDef == null) blockDef = block.BlockDefinition;
                            float modifier;
                            var found = system.CustomBlockDefinitionBasesToScales.TryGetValue(blockDef, out modifier);

                            if (found) damageScale *= modifier;
                            else if (system.Values.DamageScales.Custom.IgnoreAllOthers) continue;
                        }
                    }

                    var blockIsRoot = block == rootBlock;
                    var primaryDamage = primeDamage || blockIsRoot;

                    if (damagePool <= 0 && primaryDamage || objectsHit >= maxObjects) break;

                    var scaledDamage = damagePool * damageScale;

                    if (primaryDamage)
                    {
                        if (countBlocksAsObjects) objectsHit++;

                        if (scaledDamage <= blockHp)
                        {
                            outOfPew = true;
                            damagePool = 0;
                        }
                        else
                        {
                            _destroyedSlims.Add(block);
                            damagePool -= blockHp;
                        }
                    }
                    else
                    {
                        scaledDamage = areaEffectDmg * damageScale;
                        if (scaledDamage >= blockHp) _destroyedSlims.Add(block);
                    }

                    //block.DoDamage(scaledDamage, damageType, true, null, p.T.FiringCube.EntityId);
                    var theEnd = damagePool <= 0 || objectsHit >= maxObjects;

                    if (explosive && !nova && ((!detonateOnEnd && blockIsRoot) || detonateOnEnd && theEnd))
                    {
                        var damage = detonateOnEnd && theEnd ? detonateDmg : areaEffectDmg;
                        var radius = detonateOnEnd && theEnd ? detonateRadius : areaRadius;
                        if (ExplosionReady) UtilsStatic.CreateMissileExplosion(damage, radius, hitEnt.HitPos.Value, p.Direction, p.T.FiringCube, grid, system);
                        else UtilsStatic.CreateMissileExplosion(damage, radius, hitEnt.HitPos.Value, p.Direction, p.T.FiringCube, grid, system, true);
                    }
                    else if (!nova)
                    {
                        if (hitMass > 0 && blockIsRoot)
                        {
                            var speed = system.Values.Ammo.Trajectory.DesiredSpeed > 0 ? system.Values.Ammo.Trajectory.DesiredSpeed : 1;
                            ApplyProjectileForce(grid, hitEnt.HitPos.Value, p.Direction, (hitMass * speed));
                        }

                        if (radiantBomb && theEnd)
                        {
                            nova = true;
                            i--;
                            p.BaseDamagePool = 0;
                            p.ObjectsHit = maxObjects;
                            objectsHit = int.MinValue;
                            var aInfo = system.Values.Ammo.AreaEffect;
                            var dInfo = aInfo.Detonation;

                            //sphere.Radius = dInfo.DetonationRadius;

                            if (dInfo.DetonationDamage > 0) damagePool = detonateDmg;
                            else if (aInfo.AreaEffectDamage > 0) damagePool = areaEffectDmg;
                            else damagePool = scaledDamage;
                            //Log.Line($"[raidant end] scaled:{scaledDamage} - area:{system.Values.Ammo.AreaEffect.AreaEffectDamage} - pool:{damagePool}({projectile.BaseDamagePool}) - objHit:{projectile.ObjectsHit} - gridBlocks:{grid.CubeBlocks.Count}({((MyCubeGrid)rootBlock.CubeGrid).BlocksCount}) - i:{i} j:{j}");
                            break;
                        }
                    }
                }
            }
            if (!countBlocksAsObjects) p.ObjectsHit += 1;
            if (!nova)
            {
                p.BaseDamagePool = damagePool;
                p.ObjectsHit = objectsHit;
                //Log.Line($"not end game: pool:{damagePool} - objHit:{objectsHit}" );
            }
            hitEnt.Blocks.Clear();
        }

        private void DamageDestObj(HitEntity hitEnt, Projectile projectile)
        {
            var entity = hitEnt.Entity;
            var destObj = hitEnt.Entity as IMyDestroyableObject;
            var system = projectile.T.System;
            if (destObj == null || entity == null) return;
            //projectile.ObjectsHit++;

            var objHp = destObj.Integrity;
            var integrityCheck = system.Values.DamageScales.MaxIntegrity > 0;
            if (integrityCheck && objHp > system.Values.DamageScales.MaxIntegrity) return;

            var character = hitEnt.Entity is IMyCharacter;
            float damageScale = 1;
            if (character && system.Values.DamageScales.Characters >= 0)
                damageScale *= system.Values.DamageScales.Characters;

            var scaledDamage = projectile.BaseDamagePool * damageScale;

            if (scaledDamage < objHp) projectile.BaseDamagePool = 0;
            else projectile.BaseDamagePool -= objHp;

            destObj.DoDamage(scaledDamage, MyDamageType.Bullet, true, null, projectile.T.FiringCube.EntityId);
            if (system.Values.Ammo.Mass > 0)
            {
                var speed = system.Values.Ammo.Trajectory.DesiredSpeed > 0 ? system.Values.Ammo.Trajectory.DesiredSpeed : 1;
                ApplyProjectileForce(entity, entity.PositionComp.WorldAABB.Center, projectile.Direction, (system.Values.Ammo.Mass * speed));
            }
        }

        private void DamageVoxel(HitEntity hitEnt, Projectile p)
        {
            var entity = hitEnt.Entity;
            var destObj = hitEnt.Entity as MyVoxelBase;
            var system = p.T.System;
            if (destObj == null || entity == null || !system.Values.DamageScales.DamageVoxels) return;

            var baseDamage = system.Values.Ammo.BaseDamage;
            var damage = baseDamage;
            p.ObjectsHit++; // add up voxel units

            //destObj.DoDamage(damage, MyDamageType.Bullet, true, null, dEvent.Attacker.EntityId);
        }

        private void ExplosionProximity(HitEntity hitEnt, Projectile projectile)
        {
            var system = projectile.T.System;
            projectile.BaseDamagePool = 0;
            var radius = system.Values.Ammo.AreaEffect.AreaEffectRadius;
            var damage = system.Values.Ammo.AreaEffect.AreaEffectDamage;

            if (hitEnt.HitPos.HasValue)
            {
                if (ExplosionReady)
                    UtilsStatic.CreateMissileExplosion(damage, radius, hitEnt.HitPos.Value, projectile.Direction, projectile.T.FiringCube, hitEnt.Entity, system, true);
                else
                    UtilsStatic.CreateMissileExplosion(damage, radius, hitEnt.HitPos.Value, projectile.Direction, projectile.T.FiringCube, hitEnt.Entity, system, true);
            }
            else if (!hitEnt.Hit == false && hitEnt.HitPos.HasValue)
                UtilsStatic.CreateFakeExplosion(radius, hitEnt.HitPos.Value, system);
        }

        public static void ApplyProjectileForce(MyEntity entity, Vector3D intersectionPosition, Vector3 normalizedDirection, float impulse)
        {
            if (entity.Physics == null || !entity.Physics.Enabled || entity.Physics.IsStatic || entity.Physics.Mass / impulse > 500)
                return;
            entity.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE, normalizedDirection * impulse, intersectionPosition, Vector3.Zero);
        }

        public void GetBlockSphereDb(MyCubeGrid grid, double areaRadius, out List<Vector3I> radiatedBlocks)
        {
            areaRadius = Math.Ceiling(areaRadius);

            if (grid.GridSizeEnum == MyCubeSize.Large)
            {
                if (areaRadius < 3) areaRadius = 3;
                LargeBlockSphereDb.TryGetValue(areaRadius, out radiatedBlocks);
            }
            else SmallBlockSphereDb.TryGetValue(areaRadius, out radiatedBlocks);
        }

        private void GenerateBlockSphere(MyCubeSize gridSizeEnum, double radiusInMeters)
        {
            var gridSizeInv = 2.0; // Assume small grid (1 / 0.5)
            if (gridSizeEnum == MyCubeSize.Large)
                gridSizeInv = 0.4; // Large grid (1 / 2.5)

            var radiusInBlocks = radiusInMeters * gridSizeInv;
            var radiusSq = radiusInBlocks * radiusInBlocks;
            var radiusCeil = (int)Math.Ceiling(radiusInBlocks);
            int i, j, k;
            var max = Vector3I.One * radiusCeil;
            var min = Vector3I.One * -radiusCeil;

            var blockSphereLst = _blockSpherePool.Get();
            for (i = min.X; i <= max.X; ++i)
                for (j = min.Y; j <= max.Y; ++j)
                    for (k = min.Z; k <= max.Z; ++k)
                        if (i * i + j * j + k * k < radiusSq)
                            blockSphereLst.Add(new Vector3I(i, j, k));

            blockSphereLst.Sort((a, b) => Vector3I.Dot(a, a).CompareTo(Vector3I.Dot(b, b)));
            if (gridSizeEnum == MyCubeSize.Large)
                LargeBlockSphereDb.Add(radiusInMeters, blockSphereLst);
            else
                SmallBlockSphereDb.Add(radiusInMeters, blockSphereLst);
        }

        private void ShiftAndPruneBlockSphere(MyCubeGrid grid, Vector3I center, List<Vector3I> sphereOfCubes, List<RadiatedBlock> slims)
        {
            slims.Clear(); // Ugly but super inlined V3I check
            var gMinX = grid.Min.X;
            var gMinY = grid.Min.Y;
            var gMinZ = grid.Min.Z;
            var gMaxX = grid.Max.X;
            var gMaxY = grid.Max.Y;
            var gMaxZ = grid.Max.Z;

            for (int i = 0; i < sphereOfCubes.Count; i++)
            {
                var v3ICheck = center + sphereOfCubes[i];
                var contained = gMinX <= v3ICheck.X && v3ICheck.X <= gMaxX && (gMinY <= v3ICheck.Y && v3ICheck.Y <= gMaxY) && (gMinZ <= v3ICheck.Z && v3ICheck.Z <= gMaxZ);
                if (!contained) continue;
                IMySlimBlock slim = grid.GetCubeBlock(v3ICheck);

                if (slim != null && slim.Position == v3ICheck)
                {
                    var radiatedBlock = new RadiatedBlock();
                    radiatedBlock.Center = center;
                    radiatedBlock.Slim = slim;
                    radiatedBlock.Position = v3ICheck;
                    slims.Add(radiatedBlock);
                }
            }
        }

        static void GetIntVectorsInSphere(MyCubeGrid grid, Vector3I center, double radius, List<RadiatedBlock> points)
        {
            points.Clear();
            radius *= grid.GridSizeR;
            double radiusSq = radius * radius;
            int radiusCeil = (int)Math.Ceiling(radius);
            int i, j, k;
            for (i = -radiusCeil; i <= radiusCeil; ++i)
            {
                for (j = -radiusCeil; j <= radiusCeil; ++j)
                {
                    for (k = -radiusCeil; k <= radiusCeil; ++k)
                    {
                        if (i * i + j * j + k * k < radiusSq)
                        {
                            var vector3I = center + new Vector3I(i, j, k);
                            IMySlimBlock slim = grid.GetCubeBlock(vector3I);

                            if (slim != null)
                            {
                                var radiatedBlock = new RadiatedBlock();
                                radiatedBlock.Center = center;
                                radiatedBlock.Slim = slim;
                                radiatedBlock.Position = vector3I;
                                points.Add(radiatedBlock);
                            }
                        }
                    }
                }
            }
        }

        private void GetIntVectorsInSphere2(MyCubeGrid grid, Vector3I center, double radius)
        {
            _slimsSortedList.Clear();
            radius *= grid.GridSizeR;
            var gridMin = grid.Min;
            var gridMax = grid.Max;
            double radiusSq = radius * radius;
            int radiusCeil = (int)Math.Ceiling(radius);
            int i, j, k;
            Vector3I max = Vector3I.Min(Vector3I.One * radiusCeil, gridMax - center);
            Vector3I min = Vector3I.Max(Vector3I.One * -radiusCeil, gridMin - center);

            for (i = min.X; i <= max.X; ++i)
            {
                for (j = min.Y; j <= max.Y; ++j)
                {
                    for (k = min.Z; k <= max.Z; ++k)
                    {
                        if (i * i + j * j + k * k < radiusSq)
                        {
                            var vector3I = center + new Vector3I(i, j, k);
                            IMySlimBlock slim = grid.GetCubeBlock(vector3I);

                            if (slim != null && slim.Position == vector3I)
                            {
                                var radiatedBlock = new RadiatedBlock();
                                radiatedBlock.Center = center;
                                radiatedBlock.Slim = slim;
                                radiatedBlock.Position = vector3I;
                                _slimsSortedList.Add(radiatedBlock);
                            }
                        }
                    }
                }
            }
            _slimsSortedList.Sort((a, b) => Vector3I.Dot(a.Position, a.Position).CompareTo(Vector3I.Dot(b.Position, b.Position)));
        }

        public void GetBlocksInsideSphere(MyCubeGrid grid, Dictionary<Vector3I, IMySlimBlock> cubes, ref BoundingSphereD sphere, bool sorted, Vector3I center, bool checkTriangles = false)
        {
            if (grid.PositionComp == null) return;

            if (sorted) _slimsSortedList.Clear();
            else _slimsSet.Clear();

            var matrixNormalizedInv = grid.PositionComp.WorldMatrixNormalizedInv;
            Vector3D result;
            Vector3D.Transform(ref sphere.Center, ref matrixNormalizedInv, out result);
            var localSphere = new BoundingSphere(result, (float)sphere.Radius);
            var fromSphere2 = BoundingBox.CreateFromSphere(localSphere);
            var min = (Vector3D)fromSphere2.Min;
            var max = (Vector3D)fromSphere2.Max;
            var vector3I1 = new Vector3I((int)Math.Round(min.X * grid.GridSizeR), (int)Math.Round(min.Y * grid.GridSizeR), (int)Math.Round(min.Z * grid.GridSizeR));
            var vector3I2 = new Vector3I((int)Math.Round(max.X * grid.GridSizeR), (int)Math.Round(max.Y * grid.GridSizeR), (int)Math.Round(max.Z * grid.GridSizeR));
            var start = Vector3I.Min(vector3I1, vector3I2);
            var end = Vector3I.Max(vector3I1, vector3I2);
            if ((end - start).Volume() < cubes.Count)
            {
                var vector3IRangeIterator = new Vector3I_RangeIterator(ref start, ref end);
                var next = vector3IRangeIterator.Current;
                while (vector3IRangeIterator.IsValid())
                {
                    IMySlimBlock cube;
                    if (cubes.TryGetValue(next, out cube))
                    {
                        if (new BoundingBox(cube.Min * grid.GridSize - grid.GridSizeHalf, cube.Max * grid.GridSize + grid.GridSizeHalf).Intersects(localSphere))
                        {
                            var radiatedBlock = new RadiatedBlock
                            {
                                Center = center,
                                Slim = cube,
                                Position = cube.Position,
                            };
                            if (sorted) _slimsSortedList.Add(radiatedBlock);
                            else _slimsSet.Add(cube);
                        }
                    }
                    vector3IRangeIterator.GetNext(out next);
                }
            }
            else
            {
                foreach (var cube in cubes.Values)
                {
                    if (new BoundingBox(cube.Min * grid.GridSize - grid.GridSizeHalf, cube.Max * grid.GridSize + grid.GridSizeHalf).Intersects(localSphere))
                    {
                        var radiatedBlock = new RadiatedBlock
                        {
                            Center = center,
                            Slim = cube,
                            Position = cube.Position,
                        };
                        if (sorted) _slimsSortedList.Add(radiatedBlock);
                        else _slimsSet.Add(cube);
                    }
                }
            }
            if (sorted)
                _slimsSortedList.Sort((x, y) => Vector3I.DistanceManhattan(x.Position, x.Slim.Position).CompareTo(Vector3I.DistanceManhattan(y.Position, y.Slim.Position)));
        }

        public void GetBlocksInsideSphereBrute(MyCubeGrid grid, Vector3I center, ref BoundingSphereD sphere, bool sorted)
        {
            if (grid.PositionComp == null) return;

            if (sorted) _slimsSortedList.Clear();
            else _slimsSet.Clear();

            var matrixNormalizedInv = grid.PositionComp.WorldMatrixNormalizedInv;
            Vector3D result;
            Vector3D.Transform(ref sphere.Center, ref matrixNormalizedInv, out result);
            var localSphere = new BoundingSphere(result, (float)sphere.Radius);
            foreach (IMySlimBlock cube in grid.CubeBlocks)
            {
                if (new BoundingBox(cube.Min * grid.GridSize - grid.GridSizeHalf, cube.Max * grid.GridSize + grid.GridSizeHalf).Intersects(localSphere))
                {
                    var radiatedBlock = new RadiatedBlock
                    {
                        Center = center,
                        Slim = cube,
                        Position = cube.Position,
                    };
                    if (sorted) _slimsSortedList.Add(radiatedBlock);
                    else _slimsSet.Add(cube);
                }
            }
            if (sorted)
                _slimsSortedList.Sort((x, y) => Vector3I.DistanceManhattan(x.Position, x.Slim.Position).CompareTo(Vector3I.DistanceManhattan(y.Position, y.Slim.Position)));
        }
    }
}

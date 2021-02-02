﻿using System;
using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.Game.Entities;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Support.UnitDefinition.ConsumableDef.TrajectoryDef;
using static WeaponCore.Support.UnitDefinition.ConsumableDef.AreaDamageDef;

namespace WeaponCore.Projectiles
{
    internal class Projectile
    {
        internal const float StepConst = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        internal ProjectileState State;
        internal EntityState ModelState;
        internal MyEntityQueryType PruneQuery;
        internal GuidanceType Guidance;
        internal Vector3D AccelDir;
        internal Vector3D Position;
        internal Vector3D LastPosition;
        internal Vector3D StartSpeed;
        internal Vector3D Velocity;
        internal Vector3D InitalStep;
        internal Vector3D AccelVelocity;
        internal Vector3D MaxVelocity;
        internal Vector3D TravelMagnitude;
        internal Vector3D LastEntityPos;
        internal Vector3D OriginTargetPos;
        internal Vector3D PredictedTargetPos;
        internal Vector3D PrevTargetPos;
        internal Vector3D TargetOffSet;
        internal Vector3D PrevTargetOffset;
        internal Vector3 PrevTargetVel;
        internal Vector3? LastHitEntVel;
        internal Vector3 Gravity;
        internal LineD Beam;
        internal BoundingSphereD PruneSphere;
        internal BoundingSphereD DeadSphere;
        internal double DeltaVelocityPerTick;
        internal double AccelInMetersPerSec;
        internal double DistanceToTravelSqr;
        internal double VelocityLengthSqr;
        internal double DistanceFromCameraSqr;
        internal double OffsetSqr;
        internal double MaxSpeedSqr;
        internal double MaxSpeed;
        internal double VisualStep;
        internal double MaxTrajectorySqr;
        internal double PrevEndPointToCenterSqr;
        internal float DesiredSpeed;
        internal int ChaseAge;
        internal int FieldTime;
        internal int EndStep;
        internal int ZombieLifeTime;
        internal int LastOffsetTime;
        internal int PruningProxyId = -1;
        internal int CachedId;
        internal int MaxChaseTime;
        internal int NewTargets;
        internal int SmartSlot;
        internal bool PickTarget;
        internal bool EnableAv;
        internal bool ConstantSpeed;
        internal bool PositionChecked;
        internal bool MoveToAndActivate;
        internal bool LockedTarget;
        internal bool DynamicGuidance;
        internal bool GenerateShrapnel;
        internal bool LinePlanetCheck;
        internal bool SmartsOn;
        internal bool MineSeeking;
        internal bool MineActivated;
        internal bool MineTriggered;
        internal bool CachedPlanetHit;
        internal bool AtMaxRange;
        internal bool ShieldBypassed;
        internal bool EarlyEnd;
        internal bool FeelsGravity;
        internal bool LineOrNotModel;
        internal bool EntitiesNear;
        internal bool FakeGravityNear;
        internal bool HadTarget;
        internal bool WasTracking;
        internal bool UseEntityCache;
        internal bool Intersecting;
        internal bool FinalizeIntersection;
        internal bool SphereCheck;
        internal bool LineCheck;
        internal enum CheckTypes
        {
            Ray,
            Sphere,
            CachedSphere,
            CachedRay,
        }

        internal CheckTypes CheckType;
        internal readonly ProInfo Info = new ProInfo();
        internal readonly List<MyLineSegmentOverlapResult<MyEntity>> MySegmentList = new List<MyLineSegmentOverlapResult<MyEntity>>();
        internal readonly List<MyEntity> MyEntityList = new List<MyEntity>();
        internal readonly List<ProInfo> VrPros = new List<ProInfo>();
        internal readonly List<Projectile> EwaredProjectiles = new List<Projectile>();
        internal readonly List<Ai> Watchers = new List<Ai>();
        internal readonly HashSet<Projectile> Seekers = new HashSet<Projectile>();

        #region Start
        internal void Start()
        {
            Position = Info.Origin;
            AccelDir = Info.Direction;
            var cameraStart = Info.System.Session.CameraPos;
            Vector3D.DistanceSquared(ref cameraStart, ref Info.Origin, out DistanceFromCameraSqr);
            GenerateShrapnel = Info.ConsumableDef.Const.ShrapnelId > -1;
            var probability = Info.ConsumableDef.AmmoGraphics.VisualProbability;
            EnableAv = !Info.ConsumableDef.Const.VirtualBeams && !Info.System.Session.DedicatedServer && DistanceFromCameraSqr <= Info.System.Session.SyncDistSqr && (probability >= 1 || probability >= MyUtils.GetRandomDouble(0.0f, 1f));
            ModelState = EntityState.None;
            LastEntityPos = Position;
            LastHitEntVel = null;
            Info.AvShot = null;
            Info.Age = -1;
            ChaseAge = 0;
            NewTargets = 0;
            ZombieLifeTime = 0;
            LastOffsetTime = 0;
            PruningProxyId = -1;
            EntitiesNear = false;
            CachedPlanetHit = false;
            PositionChecked = false;
            MineSeeking = false;
            MineActivated = false;
            MineTriggered = false;
            LinePlanetCheck = false;
            AtMaxRange = false;
            ShieldBypassed = false;
            FakeGravityNear = false;
            HadTarget = false;
            WasTracking = false;
            Intersecting = false;
            EndStep = 0;
            Info.PrevDistanceTraveled = 0;
            Info.DistanceTraveled = 0;
            PrevEndPointToCenterSqr = double.MaxValue;


            CachedId = Info.MuzzleId == -1 ? Info.WeaponCache.VirutalId : Info.MuzzleId;
            Guidance = Info.ConsumableDef.Trajectory.Guidance;
            DynamicGuidance = Guidance != GuidanceType.None && Guidance != GuidanceType.TravelTo && !Info.ConsumableDef.Const.IsBeamWeapon && Info.EnableGuidance;
            if (DynamicGuidance) DynTrees.RegisterProjectile(this);
            FeelsGravity = Info.ConsumableDef.Const.FeelsGravity;

            Info.MyPlanet = Info.Ai.MyPlanet;
            if (!Info.System.Session.VoxelCaches.TryGetValue(Info.UniqueMuzzleId, out Info.VoxelCache)) {
                Log.Line($"ProjectileStart VoxelCache Failure with Id:{Info.UniqueMuzzleId} BlockMarked:{Info.Target.CoreEntity?.MarkedForClose}, setting to default cache:");
                Info.VoxelCache = Info.System.Session.VoxelCaches[ulong.MaxValue];
            }
            if (Info.MyPlanet != null)
                Info.VoxelCache.PlanetSphere.Center = Info.Ai.ClosestPlanetCenter;

            Info.MyShield = Info.Ai.MyShield;
            Info.InPlanetGravity = Info.Ai.InPlanetGravity;
            Info.AiVersion = Info.Ai.Version;
            Info.Ai.ProjectileTicker = Info.Ai.Session.Tick;

            if (Guidance == GuidanceType.Smart && DynamicGuidance) {
                SmartsOn = true;
                MaxChaseTime = Info.ConsumableDef.Const.MaxChaseTime;
                SmartSlot = Info.WeaponRng.ClientProjectileRandom.Next(10);
                Info.WeaponRng.ClientProjectileCurrentCounter++;
            }
            else {
                MaxChaseTime = int.MaxValue;
                SmartsOn = false;
                SmartSlot = 0;
            }

            if (Info.Target.IsProjectile)
            {
                OriginTargetPos = Info.Target.Projectile.Position;
                Info.Target.Projectile.Seekers.Add(this);
            }
            else if (Info.Target.TargetEntity != null) OriginTargetPos = Info.Target.TargetEntity.PositionComp.WorldAABB.Center;
            else OriginTargetPos = Vector3D.Zero;
            LockedTarget = !Vector3D.IsZero(OriginTargetPos);

            if (SmartsOn && Info.ConsumableDef.Const.TargetOffSet && (LockedTarget || Info.Target.IsFakeTarget))
            {
                OffSetTarget();
                OffsetSqr = Info.ConsumableDef.Trajectory.Smarts.Inaccuracy * Info.ConsumableDef.Trajectory.Smarts.Inaccuracy;
            }
            else
            {
                TargetOffSet = Vector3D.Zero;
                OffsetSqr = 0;
            }
            PrevTargetOffset = Vector3D.Zero;

            var targetSpeed = (float)(!Info.ConsumableDef.Const.IsBeamWeapon ? Info.ConsumableDef.Trajectory.DesiredSpeed : Info.MaxTrajectory * MyEngineConstants.UPDATE_STEPS_PER_SECOND);
            if (Info.ConsumableDef.Const.SpeedVariance && !Info.ConsumableDef.Const.IsBeamWeapon)
            {
                var min = Info.ConsumableDef.Trajectory.SpeedVariance.Start;
                var max = Info.ConsumableDef.Trajectory.SpeedVariance.End;
                var speedVariance = (float)Info.WeaponRng.ClientProjectileRandom.NextDouble() * (max - min) + min;
                Info.WeaponRng.ClientProjectileCurrentCounter++;
                DesiredSpeed = targetSpeed + speedVariance;
            }
            else DesiredSpeed = targetSpeed;

            float variance = 0;
            if (Info.ConsumableDef.Const.RangeVariance)
            {
                var min = Info.ConsumableDef.Trajectory.RangeVariance.Start;
                var max = Info.ConsumableDef.Trajectory.RangeVariance.End;
                variance = (float)Info.WeaponRng.ClientProjectileRandom.NextDouble() * (max - min) + min;
                Info.MaxTrajectory -= variance;
                Info.WeaponRng.ClientProjectileCurrentCounter++;
            }

            if (Vector3D.IsZero(PredictedTargetPos)) PredictedTargetPos = Position + (AccelDir * Info.MaxTrajectory);
            PrevTargetPos = PredictedTargetPos;
            PrevTargetVel = Vector3D.Zero;
            Info.ObjectsHit = 0;
            Info.BaseHealthPool = Info.ConsumableDef.Health;
            Info.BaseEwarPool = Info.ConsumableDef.Health;
            Info.TracerLength = Info.ConsumableDef.Const.TracerLength <= Info.MaxTrajectory ? Info.ConsumableDef.Const.TracerLength : Info.MaxTrajectory;

            MaxTrajectorySqr = Info.MaxTrajectory * Info.MaxTrajectory;

            if (!Info.IsShrapnel) StartSpeed = Info.ShooterVel;

            MoveToAndActivate = LockedTarget && !Info.ConsumableDef.Const.IsBeamWeapon && Guidance == GuidanceType.TravelTo;

            if (MoveToAndActivate)
            {
                var distancePos = !Vector3D.IsZero(PredictedTargetPos) ? PredictedTargetPos : OriginTargetPos;
                if (variance > 0)
                {
                    var forward = Info.WeaponRng.ClientProjectileRandom.Next(100) < 50;
                    Info.WeaponRng.ClientProjectileCurrentCounter++;
                    distancePos = forward ? distancePos + (AccelDir * variance) : distancePos + (-AccelDir * variance);
                }
                Vector3D.DistanceSquared(ref Info.Origin, ref distancePos, out DistanceToTravelSqr);
            }
            else DistanceToTravelSqr = MaxTrajectorySqr;

            PickTarget = Info.ConsumableDef.Trajectory.Smarts.OverideTarget && !Info.Target.IsFakeTarget && !Info.LockOnFireState;
            if (PickTarget || LockedTarget) NewTargets++;

            var staticIsInRange = Info.Ai.ClosestStaticSqr * 0.5 < MaxTrajectorySqr;
            var pruneStaticCheck = Info.Ai.ClosestPlanetSqr * 0.5 < MaxTrajectorySqr || Info.Ai.StaticGridInRange;
            PruneQuery = (DynamicGuidance && pruneStaticCheck) || FeelsGravity && staticIsInRange || !DynamicGuidance && !FeelsGravity && staticIsInRange ? MyEntityQueryType.Both : MyEntityQueryType.Dynamic;

            if (Info.Ai.PlanetSurfaceInRange && Info.Ai.ClosestPlanetSqr <= MaxTrajectorySqr) {
                LinePlanetCheck = true;
                PruneQuery = MyEntityQueryType.Both;
            }

            if (DynamicGuidance && PruneQuery == MyEntityQueryType.Dynamic && staticIsInRange) CheckForNearVoxel(60);

            var accelPerSec = Info.ConsumableDef.Trajectory.AccelPerSec;
            ConstantSpeed = accelPerSec <= 0;
            AccelInMetersPerSec = accelPerSec > 0 ? accelPerSec : DesiredSpeed;
            var desiredSpeed = (AccelDir * DesiredSpeed);
            var relativeSpeedCap = StartSpeed + desiredSpeed;
            MaxVelocity = relativeSpeedCap;
            MaxSpeed = MaxVelocity.Length();
            MaxSpeedSqr = MaxSpeed * MaxSpeed;
            DeltaVelocityPerTick = accelPerSec * StepConst;
            AccelVelocity = (AccelDir * DeltaVelocityPerTick);

            if (ConstantSpeed)
            {
                Velocity = MaxVelocity;
                VelocityLengthSqr = MaxSpeed * MaxSpeed;
            }
            else Velocity = StartSpeed + AccelVelocity;
            
            if (Info.IsShrapnel) 
                Vector3D.Normalize(ref Velocity, out Info.Direction);

            InitalStep = !Info.IsShrapnel && ConstantSpeed ? desiredSpeed * StepConst : Velocity * StepConst;

            TravelMagnitude = Velocity * StepConst;
            FieldTime = Info.ConsumableDef.Const.Ewar || Info.ConsumableDef.Const.IsMine ? Info.ConsumableDef.Trajectory.FieldTime : 0;
            State = !Info.ConsumableDef.Const.IsBeamWeapon ? ProjectileState.Alive : ProjectileState.OneAndDone;

            if (EnableAv)
            {
                Info.AvShot = Info.System.Session.Av.AvShotPool.Get();
                Info.AvShot.Init(Info, AccelInMetersPerSec * StepConst, MaxSpeed, ref AccelDir);
                Info.AvShot.SetupSounds(DistanceFromCameraSqr); //Pool initted sounds per Projectile type... this is expensive
                if (Info.ConsumableDef.Const.HitParticle && !Info.ConsumableDef.Const.IsBeamWeapon || Info.ConsumableDef.Const.AreaEffect == AreaEffectType.Explosive && !Info.ConsumableDef.AreaEffect.Explosions.NoVisuals && Info.ConsumableDef.Const.AreaEffectSize > 0 && Info.ConsumableDef.Const.AreaEffectDamage > 0)
                {
                    var hitPlayChance = Info.ConsumableDef.AmmoGraphics.Particles.Hit.Extras.HitPlayChance;
                    Info.AvShot.HitParticleActive = hitPlayChance >= 1 || hitPlayChance >= MyUtils.GetRandomDouble(0.0f, 1f);
                }
                Info.AvShot.FakeExplosion = Info.AvShot.HitParticleActive && Info.ConsumableDef.Const.AreaEffect == AreaEffectType.Explosive && Info.ConsumableDef.AmmoGraphics.Particles.Hit.Name == string.Empty;
            }

            if (!Info.ConsumableDef.Const.PrimeModel && !Info.ConsumableDef.Const.TriggerModel) ModelState = EntityState.None;
            else
            {
                if (EnableAv)
                {
                    ModelState = EntityState.Exists;

                    double triggerModelSize = 0;
                    double primeModelSize = 0;
                    if (Info.ConsumableDef.Const.TriggerModel) triggerModelSize = Info.AvShot.TriggerEntity.PositionComp.WorldVolume.Radius;
                    if (Info.ConsumableDef.Const.PrimeModel) primeModelSize = Info.AvShot.PrimeEntity.PositionComp.WorldVolume.Radius;
                    var largestSize = triggerModelSize > primeModelSize ? triggerModelSize : primeModelSize;

                    Info.AvShot.ModelSphereCurrent.Radius = largestSize * 2;
                }
            }

            if (EnableAv)
            {
                LineOrNotModel = Info.ConsumableDef.Const.DrawLine || ModelState == EntityState.None && Info.ConsumableDef.Const.AmmoParticle;
                Info.AvShot.ModelOnly = !LineOrNotModel && ModelState == EntityState.Exists;
            }
        }

        #endregion

        #region Run
        internal void CouldHitPlanet(List<IHitInfo> hitInfos)
        {
            for (int i = 0; i < hitInfos.Count; i++)
            {
                var hit = hitInfos[i];
                var voxel = hit.HitEntity as MyVoxelBase;
                if (voxel?.RootVoxel is MyPlanet)
                {
                    LinePlanetCheck = true;
                    break;
                }
            }
            hitInfos.Clear();
        }

        internal void CheckForNearVoxel(uint steps)
        {
            var possiblePos = BoundingBoxD.CreateFromSphere(new BoundingSphereD(Position, ((MaxSpeed) * (steps + 1) * StepConst) + Info.ConsumableDef.Const.CollisionSize));
            if (MyGamePruningStructure.AnyVoxelMapInBox(ref possiblePos))
            {
                PruneQuery = MyEntityQueryType.Both;
            }
        }

        private void SpawnShrapnel()
        {
            var shrapnel = Info.System.Session.Projectiles.ShrapnelPool.Get();
            shrapnel.Init(this, Info.System.Session.Projectiles.FragmentPool);
            Info.System.Session.Projectiles.ShrapnelToSpawn.Add(shrapnel);
        }

        internal bool NewTarget()
        {
            var giveUp = HadTarget && ++NewTargets > Info.ConsumableDef.Const.MaxTargets && Info.ConsumableDef.Const.MaxTargets != 0;
            ChaseAge = Info.Age;
            PickTarget = false;
            if (giveUp || !Ai.ReacquireTarget(this)) {
                Info.Target.TargetEntity = null;
                if (Info.Target.IsProjectile) UnAssignProjectile(true);
                return false;
            }

            if (Info.Target.IsProjectile) UnAssignProjectile(false);
            return true;
        }

        internal void ForceNewTarget()
        {
            ChaseAge = Info.Age;
            PickTarget = false;
        }

        internal void ActivateMine()
        {
            var ent = Info.Target.TargetEntity;
            MineActivated = true;
            AtMaxRange = false;
            var targetPos = ent.PositionComp.WorldAABB.Center;
            var deltaPos = targetPos - Position;
            var targetVel = ent.Physics?.LinearVelocity ?? Vector3.Zero;
            var deltaVel = targetVel - Vector3.Zero;
            var timeToIntercept = MathFuncs.Intercept(deltaPos, deltaVel, DesiredSpeed);
            var predictedPos = targetPos + (float)timeToIntercept * deltaVel;
            PredictedTargetPos = predictedPos;
            PrevTargetPos = predictedPos;
            PrevTargetVel = targetVel;
            LockedTarget = true;

            if (Guidance == GuidanceType.DetectFixed) return;
            Vector3D.DistanceSquared(ref Info.Origin, ref predictedPos, out DistanceToTravelSqr);
            Info.DistanceTraveled = 0;
            Info.PrevDistanceTraveled = 0;

            Info.Direction = Vector3D.Normalize(predictedPos - Position);
            AccelDir = Info.Direction;
            VelocityLengthSqr = 0;

            MaxVelocity = (Info.Direction * DesiredSpeed);
            MaxSpeed = MaxVelocity.Length();
            MaxSpeedSqr = MaxSpeed * MaxSpeed;
            AccelVelocity = (Info.Direction * DeltaVelocityPerTick);

            if (ConstantSpeed) {
                Velocity = MaxVelocity;
                VelocityLengthSqr = MaxSpeed * MaxSpeed;
            }
            else Velocity = AccelVelocity;

            if (Guidance == GuidanceType.DetectSmart) {

                SmartsOn = true;
                MaxChaseTime = Info.ConsumableDef.Const.MaxChaseTime;

                if (SmartsOn && Info.ConsumableDef.Const.TargetOffSet && LockedTarget) {
                    OffSetTarget();
                    OffsetSqr = Info.ConsumableDef.Trajectory.Smarts.Inaccuracy * Info.ConsumableDef.Trajectory.Smarts.Inaccuracy;
                }
                else {
                    TargetOffSet = Vector3D.Zero;
                    OffsetSqr = 0;
                }
            }

            TravelMagnitude = Velocity * StepConst;
        }

        internal void TriggerMine(bool startTimer)
        {
            DistanceToTravelSqr = double.MinValue;
            if (Info.ConsumableDef.Const.Ewar) {
                Info.AvShot.Triggered = true;
                if (startTimer) FieldTime = Info.ConsumableDef.Trajectory.Mines.FieldTime;
            }
            else if (startTimer) FieldTime = 0;
            MineTriggered = true;
        }

        internal void RunSmart()
        {
            Vector3D newVel;
            if (DeltaVelocityPerTick <= 0 || Vector3D.DistanceSquared(Info.Origin, Position) >= Info.ConsumableDef.Const.SmartsDelayDistSqr) {

                var fake = Info.Target.IsFakeTarget;
                var gaveUpChase = !fake && Info.Age - ChaseAge > MaxChaseTime && HadTarget;
                var validTarget = fake || Info.Target.IsProjectile || Info.Target.TargetEntity != null && !Info.Target.TargetEntity.MarkedForClose;
                var isZombie = Info.ConsumableDef.Const.CanZombie && HadTarget && !fake && !validTarget && ZombieLifeTime > 0 && (ZombieLifeTime + SmartSlot) % 30 == 0;
                var seekFirstTarget = !HadTarget && !validTarget && Info.Age > 120 && (Info.Age + SmartSlot) % 30 == 0;

                if ((PickTarget && (Info.Age + SmartSlot) % 30 == 0 || gaveUpChase && validTarget || isZombie || seekFirstTarget) && NewTarget() || validTarget) {

                    HadTarget = true;
                    if (ZombieLifeTime > 0) {
                        ZombieLifeTime = 0;
                        OffSetTarget();
                    }
                    var targetPos = Vector3D.Zero;
                    if (fake)
                        targetPos = Info.DummyTarget.Position;
                    else if (Info.Target.IsProjectile) targetPos = Info.Target.Projectile.Position;
                    else if (Info.Target.TargetEntity != null) targetPos = Info.Target.TargetEntity.PositionComp.WorldAABB.Center;

                    if (Info.ConsumableDef.Const.TargetOffSet && WasTracking) 
                    {
                        if (Info.Age - LastOffsetTime > 300) {
                            double dist;
                            Vector3D.DistanceSquared(ref Position, ref targetPos, out dist);
                            if (dist < OffsetSqr + VelocityLengthSqr && Vector3.Dot(Info.Direction, Position - targetPos) > 0)
                                OffSetTarget();
                        }
                        targetPos += TargetOffSet;
                    }

                    PredictedTargetPos = targetPos;

                    var physics = Info.Target.TargetEntity?.Physics ?? Info.Target.TargetEntity?.Parent?.Physics;
                    if (!(Info.Target.IsProjectile || fake) && (physics == null || Vector3D.IsZero(targetPos)))
                        PrevTargetPos = PredictedTargetPos;
                    else
                        PrevTargetPos = targetPos;

                    var tVel = Vector3.Zero;
                    if (fake) tVel = Info.DummyTarget.LinearVelocity;
                    else if (Info.Target.IsProjectile) tVel = Info.Target.Projectile.Velocity;
                    else if (physics != null) tVel = physics.LinearVelocity;
                    if (Info.ConsumableDef.Const.TargetLossDegree > 0 && Vector3D.DistanceSquared(Info.Origin, Position) >= Info.ConsumableDef.Const.SmartsDelayDistSqr) {

                        if (WasTracking && (Info.System.Session.Tick20 || Vector3.Dot(Info.Direction, Position - targetPos) > 0) || !WasTracking) {
                            var targetDir = -Info.Direction;
                            var refDir = Vector3D.Normalize(Position - targetPos);
                            if (!MathFuncs.IsDotProductWithinTolerance(ref targetDir, ref refDir, Info.ConsumableDef.Const.TargetLossDegree)) {
                                if (WasTracking)
                                    PickTarget = true;
                            }
                            else if (!WasTracking)
                                WasTracking = true;
                        }
                    }

                    PrevTargetVel = tVel;
                }
                else {

                    var roam = Info.ConsumableDef.Trajectory.Smarts.Roam;
                    PrevTargetPos = roam ? PredictedTargetPos : Position + (Info.Direction * Info.MaxTrajectory);

                    if (ZombieLifeTime++ > Info.ConsumableDef.Const.TargetLossTime && !Info.ConsumableDef.Trajectory.Smarts.KeepAliveAfterTargetLoss && (Info.ConsumableDef.Trajectory.Smarts.NoTargetExpire || HadTarget)) {
                        DistanceToTravelSqr = Info.DistanceTraveled * Info.DistanceTraveled;
                        EarlyEnd = true;
                    }

                    if (roam && Info.Age - LastOffsetTime > 300 && HadTarget) {

                        double dist;
                        Vector3D.DistanceSquared(ref Position, ref PrevTargetPos, out dist);
                        if (dist < OffsetSqr + VelocityLengthSqr && Vector3.Dot(Info.Direction, Position - PrevTargetPos) > 0) {

                            OffSetTarget(true);
                            PrevTargetPos += TargetOffSet;
                            PredictedTargetPos = PrevTargetPos;
                        }
                    }
                }

                var missileToTarget = Vector3D.Normalize(PrevTargetPos - Position);
                var relativeVelocity = PrevTargetVel - Velocity;
                var normalMissileAcceleration = (relativeVelocity - (relativeVelocity.Dot(missileToTarget) * missileToTarget)) * Info.ConsumableDef.Trajectory.Smarts.Aggressiveness;

                Vector3D commandedAccel;
                if (Vector3D.IsZero(normalMissileAcceleration)) commandedAccel = (missileToTarget * AccelInMetersPerSec);
                else {

                    var maxLateralThrust = AccelInMetersPerSec * Math.Min(1, Math.Max(0, Info.ConsumableDef.Const.MaxLateralThrust));
                    if (normalMissileAcceleration.LengthSquared() > maxLateralThrust * maxLateralThrust) {
                        Vector3D.Normalize(ref normalMissileAcceleration, out normalMissileAcceleration);
                        normalMissileAcceleration *= maxLateralThrust;
                    }
                    commandedAccel = Math.Sqrt(Math.Max(0, AccelInMetersPerSec * AccelInMetersPerSec - normalMissileAcceleration.LengthSquared())) * missileToTarget + normalMissileAcceleration;
                }

                newVel = Velocity + (commandedAccel * StepConst);
                AccelDir = commandedAccel / AccelInMetersPerSec;
                Vector3D.Normalize(ref newVel, out Info.Direction);
            }
            else
                newVel = Velocity += (Info.Direction * DeltaVelocityPerTick);
            VelocityLengthSqr = newVel.LengthSquared();

            if (VelocityLengthSqr > MaxSpeedSqr) newVel = Info.Direction * MaxSpeed;
            Velocity = newVel;
        }

        internal void RunEwar()
        {
            if (Info.ConsumableDef.Const.Pulse && !Info.EwarAreaPulse && (VelocityLengthSqr <= 0 || AtMaxRange) && !Info.ConsumableDef.Const.IsMine)
            {
                Info.EwarAreaPulse = true;
                Velocity = Vector3D.Zero;
                DistanceToTravelSqr = Info.DistanceTraveled * Info.DistanceTraveled;
            }

            if (Info.EwarAreaPulse)
            {
                var areaSize = Info.ConsumableDef.Const.AreaEffectSize;
                var maxSteps = Info.ConsumableDef.Const.PulseGrowTime;
                if (Info.TriggerGrowthSteps < areaSize)
                {
                    var expansionPerTick = areaSize / maxSteps;
                    var nextSize = ++Info.TriggerGrowthSteps * expansionPerTick;
                    if (nextSize <= areaSize)
                    {
                        var nextRound = nextSize + 1;
                        if (nextRound > areaSize)
                        {
                            if (nextSize < areaSize)
                            {
                                nextSize = areaSize;
                                ++Info.TriggerGrowthSteps;
                            }
                        }
                        MatrixD.Rescale(ref Info.TriggerMatrix, nextSize);
                        if (EnableAv)
                        {
                            Info.AvShot.Triggered = true;
                            Info.AvShot.TriggerMatrix = Info.TriggerMatrix;
                        }
                    }
                }
            }

            if (!Info.ConsumableDef.Const.Pulse || Info.ConsumableDef.Const.Pulse && Info.Age % Info.ConsumableDef.Const.PulseInterval == 0)
                EwarEffects();
            else Info.EwarActive = false;
        }

        internal void EwarEffects()
        {
            switch (Info.ConsumableDef.Const.AreaEffect)
            {
                case AreaEffectType.AntiSmart:
                    var eWarSphere = new BoundingSphereD(Position, Info.ConsumableDef.Const.AreaEffectSize);
                    DynTrees.GetAllProjectilesInSphere(Info.System.Session, ref eWarSphere, EwaredProjectiles, false);
                    for (int j = 0; j < EwaredProjectiles.Count; j++)
                    {
                        var netted = EwaredProjectiles[j];
                        if (Info.Ai.IsGrid && netted.Info.Ai.IsGrid && Info.Ai.GridEntity.IsSameConstructAs(netted.Info.Ai.GridEntity) || netted.Info.Target.IsProjectile) continue;
                        if (Info.WeaponRng.ClientProjectileRandom.NextDouble() * 100f < Info.ConsumableDef.Const.PulseChance || !Info.ConsumableDef.Const.Pulse)
                        {
                            Info.BaseEwarPool -= Info.ConsumableDef.Const.AreaEffectDamage;
                            if (Info.BaseEwarPool <= 0)
                            {
                                Info.EwarActive = true;
                                netted.Info.Target.Projectile = this;
                                netted.Info.Target.IsProjectile = true;
                                Seekers.Add(netted);
                            }
                        }

                        Info.WeaponRng.ClientProjectileCurrentCounter++;
                    }
                    EwaredProjectiles.Clear();
                    return;
                case AreaEffectType.PushField:
                    if (Info.EwarAreaPulse && Info.WeaponRng.ClientProjectileRandom.NextDouble() * 100f <= Info.ConsumableDef.Const.PulseChance || !Info.ConsumableDef.Const.Pulse)
                        Info.EwarActive = true;
                    break;
                case AreaEffectType.PullField:
                    if (Info.EwarAreaPulse && Info.WeaponRng.ClientProjectileRandom.NextDouble() * 100f <= Info.ConsumableDef.Const.PulseChance || !Info.ConsumableDef.Const.Pulse)
                        Info.EwarActive = true;
                    break;
                case AreaEffectType.JumpNullField:
                    if (Info.EwarAreaPulse && Info.WeaponRng.ClientProjectileRandom.NextDouble() * 100f <= Info.ConsumableDef.Const.PulseChance || !Info.ConsumableDef.Const.Pulse)
                        Info.EwarActive = true;
                    break;
                case AreaEffectType.AnchorField:
                    if (Info.EwarAreaPulse && Info.WeaponRng.ClientProjectileRandom.NextDouble() * 100f <= Info.ConsumableDef.Const.PulseChance || !Info.ConsumableDef.Const.Pulse)
                        Info.EwarActive = true;
                    break;
                case AreaEffectType.EnergySinkField:
                    if (Info.EwarAreaPulse && Info.WeaponRng.ClientProjectileRandom.NextDouble() * 100f <= Info.ConsumableDef.Const.PulseChance || !Info.ConsumableDef.Const.Pulse)
                        Info.EwarActive = true;
                    break;
                case AreaEffectType.EmpField:
                    if (Info.EwarAreaPulse && Info.WeaponRng.ClientProjectileRandom.NextDouble() * 100f <= Info.ConsumableDef.Const.PulseChance || !Info.ConsumableDef.Const.Pulse)
                        Info.EwarActive = true;
                    break;
                case AreaEffectType.OffenseField:
                    if (Info.EwarAreaPulse && Info.WeaponRng.ClientProjectileRandom.NextDouble() * 100f <= Info.ConsumableDef.Const.PulseChance || !Info.ConsumableDef.Const.Pulse)
                        Info.EwarActive = true;
                    break;
                case AreaEffectType.NavField:
                    if (!Info.ConsumableDef.Const.Pulse || Info.EwarAreaPulse && Info.WeaponRng.ClientProjectileRandom.NextDouble() * 100f <= Info.ConsumableDef.Const.PulseChance)
                        Info.EwarActive = true;
                    break;
                case AreaEffectType.DotField:
                    if (Info.EwarAreaPulse && Info.WeaponRng.ClientProjectileRandom.NextDouble() * 100f <= Info.ConsumableDef.Const.PulseChance || !Info.ConsumableDef.Const.Pulse)
                    {
                        Info.EwarActive = true;
                    }
                    break;
            }
            Info.WeaponRng.ClientProjectileCurrentCounter++;
        }


        internal void SeekEnemy()
        {
            var mineInfo = Info.ConsumableDef.Trajectory.Mines;
            var detectRadius = mineInfo.DetectRadius;
            var deCloakRadius = mineInfo.DeCloakRadius;

            var wakeRadius = detectRadius > deCloakRadius ? detectRadius : deCloakRadius;
            PruneSphere = new BoundingSphereD(Position, wakeRadius);
            var inRange = false;
            var activate = false;
            var minDist = double.MaxValue;
            if (!MineActivated)
            {
                MyEntity closestEnt = null;
                MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref PruneSphere, MyEntityList, MyEntityQueryType.Dynamic);
                for (int i = 0; i < MyEntityList.Count; i++)
                {
                    var ent = MyEntityList[i];
                    var grid = ent as MyCubeGrid;
                    var character = ent as IMyCharacter;
                    if (grid == null && character == null || ent.MarkedForClose || !ent.InScene) continue;
                    Sandbox.ModAPI.Ingame.MyDetectedEntityInfo entInfo;
                    bool peace;
                    MyRelationsBetweenPlayerAndBlock newRelation;

                    if (!Info.Ai.CreateEntInfo(ent, Info.Ai.AiOwner, out entInfo)) continue;
                    switch (entInfo.Relationship)
                    {
                        case MyRelationsBetweenPlayerAndBlock.Owner:
                            continue;
                        case MyRelationsBetweenPlayerAndBlock.FactionShare:
                            continue;
                    }
                    var entSphere = ent.PositionComp.WorldVolume;
                    entSphere.Radius += Info.ConsumableDef.Const.CollisionSize;
                    var dist = MyUtils.GetSmallestDistanceToSphereAlwaysPositive(ref Position, ref entSphere);
                    if (dist >= minDist) continue;
                    minDist = dist;
                    closestEnt = ent;
                }
                MyEntityList.Clear();

                if (closestEnt != null)
                {
                    ForceNewTarget();
                    Info.Target.TargetEntity = closestEnt;
                }
            }
            else if (Info.Target.TargetEntity != null && !Info.Target.TargetEntity.MarkedForClose)
            {
                var entSphere = Info.Target.TargetEntity.PositionComp.WorldVolume;
                entSphere.Radius += Info.ConsumableDef.Const.CollisionSize;
                minDist = MyUtils.GetSmallestDistanceToSphereAlwaysPositive(ref Position, ref entSphere);
            }
            else
                TriggerMine(true);

            if (EnableAv) {
                if (Info.AvShot.Cloaked && minDist <= deCloakRadius) Info.AvShot.Cloaked = false;
                else if (Info.AvShot.ConsumableDef.Trajectory.Mines.Cloak && !Info.AvShot.Cloaked && minDist > deCloakRadius) Info.AvShot.Cloaked = true;
            }

            if (minDist <= Info.ConsumableDef.Const.CollisionSize) activate = true;
            if (minDist <= detectRadius) inRange = true;
            if (MineActivated)
            {
                if (!inRange)
                    TriggerMine(true);
            }
            else if (inRange) ActivateMine();

            if (activate)
            {
                TriggerMine(false);
                MyEntityList.Add(Info.Target.TargetEntity);
            }
        }

        internal void OffSetTarget(bool roam = false)
        {
            var randAzimuth = (Info.WeaponRng.TurretRandom.NextDouble() * 1) * 2 * Math.PI;
            var randElevation = ((Info.WeaponRng.TurretRandom.NextDouble() * 1) * 2 - 1) * 0.5 * Math.PI;

            Info.WeaponRng.TurretCurrentCounter += 2;

            var offsetAmount = roam ? 100 : Info.ConsumableDef.Trajectory.Smarts.Inaccuracy;
            Vector3D randomDirection;
            Vector3D.CreateFromAzimuthAndElevation(randAzimuth, randElevation, out randomDirection); // this is already normalized
            PrevTargetOffset = TargetOffSet;
            TargetOffSet = (randomDirection * offsetAmount);
            VisualStep = 0;
            if (Info.Age != 0) LastOffsetTime = Info.Age;
        }

        internal void DestroyProjectile()
        {
            if (State == ProjectileState.Destroy)
            {
                Info.Hit = new Hit { Block = null, Entity = null, SurfaceHit = Position, LastHit = Position, HitVelocity = Info.InPlanetGravity ? Velocity * 0.33f : Velocity, HitTick = Info.System.Session.Tick };
                if (EnableAv || Info.ConsumableDef.Const.VirtualBeams)
                {
                    Info.AvShot.ForceHitParticle = true;
                    Info.AvShot.Hit = Info.Hit;
                }

                Intersecting = true;
            }

            State = ProjectileState.Depleted;
        }

        internal void UnAssignProjectile(bool clear)
        {
            Info.Target.Projectile.Seekers.Remove(this);
            if (clear) Info.Target.Reset(Info.System.Session.Tick, Target.States.ProjectileClosed);
            else
            {
                Info.Target.IsProjectile = false;
                Info.Target.IsFakeTarget = false;
                Info.Target.Projectile = null;
            }
        }

        internal void ProjectileClose()
        {
            if (GenerateShrapnel && Info.Age >= Info.ConsumableDef.Const.MinArmingTime)
                SpawnShrapnel();

            for (int i = 0; i < Watchers.Count; i++) Watchers[i].DeadProjectiles.Add(this);
            Watchers.Clear();

            foreach (var seeker in Seekers) seeker.Info.Target.Reset(Info.System.Session.Tick, Target.States.ProjectileClosed);
            Seekers.Clear();

            if (EnableAv && Info.AvShot.ForceHitParticle)
                Info.AvShot.HitEffects(true);

            State = ProjectileState.Dead;

            var detInfo = Info.ConsumableDef.AreaEffect.Detonation;
            var afInfo = Info.ConsumableDef.AreaEffect;
            var detExp = !afInfo.Explosions.NoVisuals && (afInfo.AreaEffect == AreaEffectType.Explosive || afInfo.AreaEffect == AreaEffectType.Radiant && afInfo.Explosions.CustomParticle != string.Empty) && detInfo.DetonateOnEnd && (!detInfo.ArmOnlyOnHit || Info.ObjectsHit > 0);

            if (EnableAv)
            {
                if (ModelState == EntityState.Exists)
                    ModelState = EntityState.None;
                if (!Info.AvShot.Active)
                    Info.System.Session.Av.AvShotPool.Return(Info.AvShot);
                else Info.AvShot.EndState = new AvClose { EndPos = Position, Dirty = true, DetonateFakeExp = detExp };
            }
            else if (Info.ConsumableDef.Const.VirtualBeams)
            {
                for (int i = 0; i < VrPros.Count; i++)
                {
                    var vp = VrPros[i];
                    if (!vp.AvShot.Active)
                        Info.System.Session.Av.AvShotPool.Return(vp.AvShot);
                    else vp.AvShot.EndState = new AvClose { EndPos = Position, Dirty = true, DetonateFakeExp = detExp };

                    Info.System.Session.Projectiles.VirtInfoPool.Return(vp);
                }
                VrPros.Clear();
            }
            if (DynamicGuidance)
                DynTrees.UnregisterProjectile(this);

            PruningProxyId = -1;
            Info.Clean();
        }

        internal enum ProjectileState
        {
            Alive,
            Detonate,
            OneAndDone,
            Dead,
            Depleted,
            Destroy,
        }

        internal enum EntityState
        {
            Exists,
            None
        }
        #endregion
    }
}
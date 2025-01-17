﻿using System;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;
using static WeaponCore.Support.WeaponComponent.ShootActions;

namespace WeaponCore.Platform
{
    public partial class Weapon
    {

        internal void PositionChanged(MyPositionComponentBase pComp)
        {
            try
            {
                if (PosChangedTick != Comp.Session.Tick)
                    UpdatePivotPos();

                if (Comp.UserControlled) {
                    ReturingHome = false;
                    IsHome = false;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in PositionChanged: {ex}"); }
        }

        internal void TargetChanged()
        {
            EventTriggerStateChanged(EventTriggers.Tracking, Target.HasTarget);
            EventTriggerStateChanged(EventTriggers.StopTracking, !Target.HasTarget);

            if (!Target.HasTarget)
            {
                if (DrawingPower) {
                    Charging = false;
                    StopPowerDraw();
                }

                if (Comp.Session.MpActive && Comp.Session.IsServer)  {
                    TargetData.ClearTarget();
                    if (!Comp.FakeMode)
                        Target.PushTargetToClient(this);
                } 
            }

            Target.TargetChanged = false;
        }

        internal void EntPartClose(MyEntity obj)
        {
            obj.PositionComp.OnPositionChanged -= PositionChanged;
            obj.OnMarkForClose -= EntPartClose;
            if (Comp.MyCube.IsWorking)
                Comp.Status = WeaponComponent.Start.ReInit;
        }

        internal void UpdateRequiredPower()
        {
            if (ActiveAmmoDef.AmmoDef.Const.EnergyAmmo || ActiveAmmoDef.AmmoDef.Const.IsHybrid)
            {
                var rofPerSecond = RateOfFire / MyEngineConstants.UPDATE_STEPS_PER_SECOND;
                RequiredPower = ((ShotEnergyCost * (rofPerSecond * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS)) * System.Values.HardPoint.Loading.BarrelsPerShot) * System.Values.HardPoint.Loading.TrajectilesPerBarrel;
            }
            else
                RequiredPower = Comp.IdlePower;
        }

        internal void UpdateShotEnergy()
        {
            var ewar = (int)ActiveAmmoDef.AmmoDef.AreaEffect.AreaEffect > 3;
            ShotEnergyCost = ewar ? ActiveAmmoDef.AmmoDef.EnergyCost * ActiveAmmoDef.AmmoDef.Const.AreaEffectDamage : ActiveAmmoDef.AmmoDef.EnergyCost * BaseDamage;
        }

        internal void UpdateBarrelRotation()
        {
            const int loopCnt = 10;
            var interval = System.Values.HardPoint.Loading.DeterministicSpin ? (3600f / System.BarrelSpinRate) * (1f / _numModelBarrels) : (3600f / System.BarrelSpinRate) * ((float)Math.PI / _numModelBarrels);
            var steps = (360f / _numModelBarrels) / interval;
            _ticksBeforeSpinUp = (uint)interval / loopCnt;
            for (int i = 0; i < loopCnt; i++) {

                var multi = (float)(i + 1) / loopCnt;
                var angle = MathHelper.ToRadians(steps * multi);
                switch (System.Values.HardPoint.Other.RotateBarrelAxis) {

                    case 1:
                        BarrelRotationPerShot[i] = MuzzlePart.ToTransformation * Matrix.CreateRotationX(angle) * MuzzlePart.FromTransformation;
                        break;
                    case 2:
                        BarrelRotationPerShot[i] = MuzzlePart.ToTransformation * Matrix.CreateRotationY(angle) * MuzzlePart.FromTransformation;
                        break;
                    case 3:
                        BarrelRotationPerShot[i] = MuzzlePart.ToTransformation * Matrix.CreateRotationZ(angle) * MuzzlePart.FromTransformation;
                        break;
                }
            }
        }

        public void StartShooting()
        {
            if (FiringEmitter != null) StartFiringSound();
            if (!IsShooting && !System.DesignatorWeapon)
            {
                EventTriggerStateChanged(EventTriggers.StopFiring, false);
                Comp.CurrentDps += Dps;
                if ((ActiveAmmoDef.AmmoDef.Const.EnergyAmmo || ActiveAmmoDef.AmmoDef.Const.IsHybrid) && !ActiveAmmoDef.AmmoDef.Const.MustCharge && !Comp.UnlimitedPower && !DrawingPower)
                    DrawPower();
            }
            IsShooting = true;
        }

        public void StopShooting(bool power = true)
        {
            FireCounter = 0;
            CeaseFireDelayTick = uint.MaxValue / 2;
            _ticksUntilShoot = 0;
            FinishBurst = false;
            if (System.Session.IsServer)
                ShootOnce = false;

            if (PreFired)
                UnSetPreFire();

            if (IsShooting && !System.DesignatorWeapon)
            {
                EventTriggerStateChanged(EventTriggers.Firing, false);
                EventTriggerStateChanged(EventTriggers.StopFiring, true, _muzzlesFiring);
                Comp.CurrentDps = Comp.CurrentDps - Dps > 0 ? Comp.CurrentDps - Dps : 0;

                if (!ActiveAmmoDef.AmmoDef.Const.MustCharge && (ActiveAmmoDef.AmmoDef.Const.EnergyAmmo || ActiveAmmoDef.AmmoDef.Const.IsHybrid) && !Comp.UnlimitedPower && power && DrawingPower)
                    StopPowerDraw();

            }

            if (System.Session.HandlesInput)
                StopShootingAv(power);
            else IsShooting = false;
        }

        public void DrawPower(bool adapt = false)
        {
            if (DrawingPower && !adapt) return;

            var useableDif = adapt ? OldUseablePower - UseablePower : -UseablePower;
            DrawingPower = true;
            //yes they are the right signs, weird math at play :P
            Comp.Ai.CurrentWeaponsDraw -= useableDif;
            Comp.SinkPower -= useableDif;
            Comp.Ai.GridAvailablePower += useableDif;
            Comp.MyCube.ResourceSink.Update();
        }

        public void StopPowerDraw()
        {
            if (!DrawingPower) return;
            DrawingPower = false;
            RequestedPower = false;
            Comp.Ai.RequestedWeaponsDraw -= RequiredPower;
            Comp.Ai.CurrentWeaponsDraw -= UseablePower;
            Comp.SinkPower -= UseablePower;
            Comp.Ai.GridAvailablePower += UseablePower;

            ChargeDelayTicks = 0;
            if (Comp.SinkPower < Comp.IdlePower) Comp.SinkPower = Comp.IdlePower;
            Comp.MyCube.ResourceSink.Update();
        }

        internal void LostPowerIsThisEverUsed()
        {
            if (System.Session.IsServer)
            {
                State.WeaponMode(Comp, ShootOff);
                //w.Ammo.CurrentAmmo = 0;
                Log.Line($"power off set ammo to 0");
            }

            Reloading = false;
            FinishBurst = false;

            if (IsShooting)
                StopShooting();
        }


        internal bool ValidFakeTargetInfo(long playerId, out GridAi.FakeTarget.FakeWorldTargetInfo fakeTargetInfo, bool preferPainted = true)
        {
            fakeTargetInfo = null;
            GridAi.FakeTargets fakeTargets;
            if (Comp.Session.PlayerDummyTargets.TryGetValue(playerId, out fakeTargets))
            {
                var validManual = Comp.Data.Repo.Base.Set.Overrides.Control == GroupOverrides.ControlModes.Manual && Comp.Data.Repo.Base.State.TrackingReticle && fakeTargets.ManualTarget.FakeInfo.WorldPosition != Vector3D.Zero;
                var validPainter = Comp.Data.Repo.Base.Set.Overrides.Control == GroupOverrides.ControlModes.Painter && !fakeTargets.PaintedTarget.Dirty && fakeTargets.PaintedTarget.LocalPosition != Vector3D.Zero;
                var fakeTarget = validPainter && preferPainted ? fakeTargets.PaintedTarget : validManual ? fakeTargets.ManualTarget : null;
                if (fakeTarget == null || fakeTarget.Dirty)
                    return false;

                fakeTargetInfo = fakeTarget.LastInfoTick != System.Session.Tick ? fakeTarget.GetFakeTargetInfo(Comp.Ai) : fakeTarget.FakeInfo;
            }

            return fakeTargetInfo != null;
        }


        internal double GetMaxWeaponRange()
        {
            var ammoMax = ActiveAmmoDef.AmmoDef.Const.MaxTrajectory;
            var hardPointMax = System.Values.Targeting.MaxTargetDistance > 0 ? System.Values.Targeting.MaxTargetDistance : double.MaxValue;
            return Math.Min(hardPointMax, ammoMax);
        }

        internal void UpdateWeaponRange()
        {
            var hardPointMax = System.Values.Targeting.MaxTargetDistance > 0 ? System.Values.Targeting.MaxTargetDistance : double.MaxValue;
            var range = Comp.Data.Repo.Base.Set.Range < 0 ? hardPointMax : Comp.Data.Repo.Base.Set.Range; 
            var ammoMax = ActiveAmmoDef.AmmoDef.Const.MaxTrajectory;
            var weaponRange = Math.Min(hardPointMax, ammoMax);
            MaxTargetDistance = Math.Min(range, weaponRange);
            MaxTargetDistanceSqr = MaxTargetDistance * MaxTargetDistance;
            MinTargetDistance = System.Values.Targeting.MinTargetDistance;
            MinTargetDistanceSqr = MinTargetDistance * MinTargetDistance;
            
            var minBuffer = MinTargetDistance * 0.50;
            var minBufferSqr = (MinTargetDistance + minBuffer) * (MinTargetDistance + minBuffer);
            MinTargetDistanceBufferSqr = minBufferSqr;

            if (Comp.MaxTargetDistance < MaxTargetDistance) {
                Comp.MaxTargetDistance = MaxTargetDistance;
                Comp.MaxTargetDistanceSqr = MaxTargetDistanceSqr;
            }

            if (Comp.MinTargetDistance > MinTargetDistance) {
                Comp.MinTargetDistance = MinTargetDistance;
                Comp.MinTargetDistanceSqr = MinTargetDistanceSqr;
            }
        }

        internal void RayCallBackClean()
        {
            RayCallBack.Weapon = null;
            RayCallBack = null;
        }

        internal void WakeTargets()
        {
            LastTargetTick = Comp.Session.Tick;
            if (System.Session.IsServer && TrackTarget)
            {
                if (Acquire.Monitoring)
                    System.Session.AcqManager.Refresh(Acquire);
                else
                    System.Session.AcqManager.Monitor(Acquire);
            }

            ShortLoadId = Comp.Session.ShortLoadAssigner();
        }
    }
}

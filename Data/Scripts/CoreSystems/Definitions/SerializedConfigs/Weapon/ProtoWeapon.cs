﻿using System;
using System.ComponentModel;
using CoreSystems.Platform;
using CoreSystems.Support;
using ProtoBuf;
using VRageMath;
using static CoreSystems.Support.WeaponDefinition.TargetingDef;
using static CoreSystems.Support.CoreComponent;

namespace CoreSystems
{
    [ProtoContract]
    public class ProtoWeaponRepo : ProtoRepo
    {
        [ProtoMember(1)] public ProtoWeaponAmmo[] Ammos;
        [ProtoMember(2)] public ProtoWeaponComp Values;

        public void ResetToFreshLoadState()
        {
            //Values.Set.Overrides.Control = ProtoWeaponOverrides.ControlModes.Auto;
            //Values.PartState.Control = ProtoWeaponState.ControlMode.None;
            //Values.PartState.PlayerId = -1;
            Values.State.TrackingReticle = false;
            if (Values.State.TerminalAction == TriggerActions.TriggerOnce)
                Values.State.TerminalAction = TriggerActions.TriggerOff;
            for (int i = 0; i < Ammos.Length; i++)
            {
                var ws = Values.State.Weapons[i];
                var wr = Values.Reloads[i];
                var wa = Ammos[i];

                wa.AmmoCycleId = 0;
                ws.Heat = 0;
                ws.Overheated = false;
                if (ws.Action == TriggerActions.TriggerOnce)
                    ws.Action = TriggerActions.TriggerOff;
                wr.StartId = 0;
            }
            ResetCompBaseRevisions();
        }

        public void ResetCompBaseRevisions()
        {
            Values.Revision = 0;
            Values.State.Revision = 0;
            for (int i = 0; i < Ammos.Length; i++)
            {
                Values.Targets[i].Revision = 0;
                Values.Reloads[i].Revision = 0;
                Ammos[i].Revision = 0;
            }
        }
    }

    [ProtoContract]
    public class ProtoWeaponAmmo
    {
        [ProtoMember(1)] public uint Revision;
        [ProtoMember(2)] public int CurrentAmmo; //save
        [ProtoMember(3)] public float CurrentCharge; //save
        [ProtoMember(4)] public long CurrentMags; // save
        [ProtoMember(5)] public int AmmoTypeId; //save
        [ProtoMember(6)] public int AmmoCycleId; //save

        public void Sync(Weapon w, ProtoWeaponAmmo sync)
        {
            if (sync.Revision > Revision)
            {
                Revision = sync.Revision;
                CurrentAmmo = sync.CurrentAmmo;
                CurrentCharge = sync.CurrentCharge;

                if (sync.CurrentMags <= 0 && CurrentMags != sync.CurrentMags)
                    w.ClientReload(true);

                CurrentMags = sync.CurrentMags;
                AmmoTypeId = sync.AmmoTypeId;

                if (sync.AmmoCycleId > AmmoCycleId)
                    w.ChangeActiveAmmoClient();

                AmmoCycleId = sync.AmmoCycleId;
            }
        }
    }

    [ProtoContract]
    public class ProtoWeaponComp
    {
        [ProtoMember(1)] public uint Revision;
        [ProtoMember(2)] public ProtoWeaponSettings Set;
        [ProtoMember(3)] public ProtoWeaponState State;
        [ProtoMember(4)] public ProtoWeaponTransferTarget[] Targets;
        [ProtoMember(5)] public ProtoWeaponReload[] Reloads;

        public void Sync(Weapon.WeaponComponent comp, ProtoWeaponComp sync)
        {
            if (sync.Revision > Revision)
            {

                Revision = sync.Revision;
                Set.Sync(comp, sync.Set);
                State.Sync(comp, sync.State, ProtoWeaponState.Caller.CompData);

                for (int i = 0; i < Targets.Length; i++)
                {
                    var w = comp.Platform.Weapons[i];
                    sync.Targets[i].SyncTarget(w);
                    Reloads[i].Sync(w, sync.Reloads[i]);
                }
            }
            else Log.Line("CompDynamicValues older revision");

        }

        public void UpdateCompPacketInfo(Weapon.WeaponComponent comp, bool clean = false)
        {
            ++Revision;
            ++State.Revision;
            Session.PacketInfo info;
            if (clean && comp.Session.PrunedPacketsToClient.TryGetValue(comp.Data.Repo.Values.State, out info))
            {
                comp.Session.PrunedPacketsToClient.Remove(comp.Data.Repo.Values.State);
                comp.Session.PacketWeaponStatePool.Return((WeaponStatePacket)info.Packet);
            }

            for (int i = 0; i < Targets.Length; i++)
            {

                var t = Targets[i];
                var wr = Reloads[i];

                if (clean)
                {
                    if (comp.Session.PrunedPacketsToClient.TryGetValue(t, out info))
                    {
                        comp.Session.PrunedPacketsToClient.Remove(t);
                        comp.Session.PacketTargetPool.Return((TargetPacket)info.Packet);
                    }
                    if (comp.Session.PrunedPacketsToClient.TryGetValue(wr, out info))
                    {
                        comp.Session.PrunedPacketsToClient.Remove(wr);
                        comp.Session.PacketReloadPool.Return((WeaponReloadPacket)info.Packet);
                    }
                }
                ++wr.Revision;
                ++t.Revision;
                t.WeaponRandom.ReInitRandom();
            }
        }
    }

    [ProtoContract]
    public class ProtoWeaponReload
    {
        [ProtoMember(1)] public uint Revision;
        [ProtoMember(2)] public int StartId; //save
        [ProtoMember(3)] public int EndId; //save

        public void Sync(Weapon w, ProtoWeaponReload sync)
        {
            if (sync.Revision > Revision)
            {
                Revision = sync.Revision;
                StartId = sync.StartId;
                EndId = sync.EndId;

                w.ClientReload(true);
            }
        }
    }

    [ProtoContract]
    public class ProtoWeaponSettings
    {
        [ProtoMember(1), DefaultValue(true)] public bool Guidance = true;
        [ProtoMember(2), DefaultValue(1)] public int Overload = 1;
        [ProtoMember(3), DefaultValue(1)] public float DpsModifier = 1;
        [ProtoMember(4), DefaultValue(1)] public float RofModifier = 1;
        [ProtoMember(5), DefaultValue(100)] public float Range = 100;
        [ProtoMember(6)] public ProtoWeaponOverrides Overrides;


        public ProtoWeaponSettings()
        {
            Overrides = new ProtoWeaponOverrides();
        }

        public void Sync(Weapon.WeaponComponent comp, ProtoWeaponSettings sync)
        {
            Guidance = sync.Guidance;
            Range = sync.Range;
            Weapon.WeaponComponent.SetRange(comp);

            Overrides.Sync(sync.Overrides);

            var rofChange = Math.Abs(RofModifier - sync.RofModifier) > 0.0001f;
            var dpsChange = Math.Abs(DpsModifier - sync.DpsModifier) > 0.0001f;

            if (Overload != sync.Overload || rofChange || dpsChange)
            {
                Overload = sync.Overload;
                RofModifier = sync.RofModifier;
                DpsModifier = sync.DpsModifier;
                if (rofChange) Weapon.WeaponComponent.SetRof(comp);
            }
        }

    }

    [ProtoContract]
    public class ProtoWeaponState
    {
        public enum Caller
        {
            Direct,
            CompData,
        }

        public enum ControlMode
        {
            None,
            Ui,
            Toolbar,
            Camera
        }

        [ProtoMember(1)] public uint Revision;
        [ProtoMember(2)] public ProtoWeaponPartState[] Weapons;
        [ProtoMember(3)] public bool TrackingReticle; //don't save
        [ProtoMember(4), DefaultValue(-1)] public long PlayerId = -1;
        [ProtoMember(5), DefaultValue(ControlMode.None)] public ControlMode Control = ControlMode.None;
        [ProtoMember(6)] public TriggerActions TerminalAction;

        public void Sync(CoreComponent comp, ProtoWeaponState sync, Caller caller)
        {
            if (sync.Revision > Revision)
            {
                Revision = sync.Revision;
                TrackingReticle = sync.TrackingReticle;
                PlayerId = sync.PlayerId;
                Control = sync.Control;
                TerminalAction = sync.TerminalAction;
                for (int i = 0; i < sync.Weapons.Length; i++)
                    comp.Platform.Weapons[i].PartState.Sync(sync.Weapons[i]);
            }
            //else Log.Line($"ProtoWeaponState older revision: {sync.Revision} > {Revision} - caller:{caller}");
        }

        public void TerminalActionSetter(Weapon.WeaponComponent comp, TriggerActions action, bool syncWeapons = false, bool updateWeapons = true)
        {
            TerminalAction = action;

            if (updateWeapons)
            {
                for (int i = 0; i < Weapons.Length; i++)
                    Weapons[i].Action = action;
            }

            if (syncWeapons)
                comp.Session.SendState(comp);
        }
    }

    [ProtoContract]
    public class ProtoWeaponPartState
    {
        [ProtoMember(1)] public float Heat; // don't save
        [ProtoMember(2)] public bool Overheated; //don't save
        [ProtoMember(3), DefaultValue(TriggerActions.TriggerOff)] public TriggerActions Action = TriggerActions.TriggerOff; // save

        public void Sync(ProtoWeaponPartState sync)
        {
            Heat = sync.Heat;
            Overheated = sync.Overheated;
            Action = sync.Action;
        }

        public void WeaponMode(Weapon.WeaponComponent comp, TriggerActions action, bool resetTerminalAction = true, bool syncCompState = true)
        {
            if (resetTerminalAction)
                comp.Data.Repo.Values.State.TerminalAction = TriggerActions.TriggerOff;

            Action = action;
            if (comp.Session.MpActive && comp.Session.IsServer && syncCompState)
                comp.Session.SendState(comp);
        }

    }

    [ProtoContract]
    public class ProtoWeaponTransferTarget
    {
        [ProtoMember(1)] public uint Revision;
        [ProtoMember(2)] public long EntityId;
        [ProtoMember(3)] public Vector3 TargetPos;
        [ProtoMember(4)] public int PartId;
        [ProtoMember(5)] public WeaponRandomGenerator WeaponRandom; // save

        internal void SyncTarget(Weapon w)
        {
            if (Revision > w.TargetData.Revision)
            {
                w.TargetData.Revision = Revision;
                w.TargetData.EntityId = EntityId;
                w.TargetData.TargetPos = TargetPos;
                w.PartId = PartId;
                w.TargetData.WeaponRandom.Sync(WeaponRandom);

                var target = w.Target;
                target.IsProjectile = EntityId == -1;
                target.IsFakeTarget = EntityId == -2;
                target.TargetPos = TargetPos;
                target.ClientDirty = true;
            }
            //else Log.Line($"ProtoWeaponTransferTarget older revision:  {Revision}  > {w.TargetData.Revision}");
        }

        public void WeaponInit(Weapon w)
        {
            WeaponRandom.Init(w.UniqueId);

            var rand = WeaponRandom;
            rand.CurrentSeed = w.UniqueId;
            rand.ClientProjectileRandom = new Random(rand.CurrentSeed);

            rand.TurretRandom = new Random(rand.CurrentSeed);
            rand.AcquireRandom = new Random(rand.CurrentSeed);
        }

        public void PartRefreshClient(Weapon w)
        {
            try
            {
                var rand = WeaponRandom;

                rand.ClientProjectileRandom = new Random(rand.CurrentSeed);
                rand.TurretRandom = new Random(rand.CurrentSeed);
                rand.AcquireRandom = new Random(rand.CurrentSeed);

                for (int j = 0; j < rand.TurretCurrentCounter; j++)
                    rand.TurretRandom.Next();

                for (int j = 0; j < rand.ClientProjectileCurrentCounter; j++)
                    rand.ClientProjectileRandom.Next();

                for (int j = 0; j < rand.AcquireCurrentCounter; j++)
                    rand.AcquireRandom.Next();

                return;
            }
            catch (Exception e) { Log.Line("Client Weapon Values Failed To load re-initing... how?", null, true); }

            WeaponInit(w);
        }
        internal void ClearTarget()
        {
            ++Revision;
            EntityId = 0;
            TargetPos = Vector3.Zero;
        }
    }

    [ProtoContract]
    public class ProtoWeaponOverrides
    {
        public enum MoveModes
        {
            Any,
            Moving,
            Mobile,
            Moored,
        }

        public enum ControlModes
        {
            Auto,
            Manual,
            Painter,
        }

        [ProtoMember(1)] public bool Neutrals;
        [ProtoMember(2)] public bool Unowned;
        [ProtoMember(3)] public bool Friendly;
        [ProtoMember(4)] public bool FocusTargets;
        [ProtoMember(5)] public bool FocusSubSystem;
        [ProtoMember(6)] public int MinSize;
        [ProtoMember(7), DefaultValue(ControlModes.Auto)] public ControlModes Control = ControlModes.Auto;
        [ProtoMember(8), DefaultValue(BlockTypes.Any)] public BlockTypes SubSystem = BlockTypes.Any;
        [ProtoMember(9), DefaultValue(true)] public bool Meteors = true;
        [ProtoMember(10), DefaultValue(true)] public bool Biologicals = true;
        [ProtoMember(11), DefaultValue(true)] public bool Projectiles = true;
        [ProtoMember(12), DefaultValue(16384)] public int MaxSize = 16384;
        [ProtoMember(13), DefaultValue(MoveModes.Any)] public MoveModes MoveMode = MoveModes.Any;
        [ProtoMember(14), DefaultValue(true)] public bool Grids = true;
        [ProtoMember(15), DefaultValue(true)] public bool ArmorShowArea;

        public void Sync(ProtoWeaponOverrides syncFrom)
        {
            MoveMode = syncFrom.MoveMode;
            MaxSize = syncFrom.MaxSize;
            MinSize = syncFrom.MinSize;
            Neutrals = syncFrom.Neutrals;
            Unowned = syncFrom.Unowned;
            Friendly = syncFrom.Friendly;
            Control = syncFrom.Control;
            FocusTargets = syncFrom.FocusTargets;
            FocusSubSystem = syncFrom.FocusSubSystem;
            SubSystem = syncFrom.SubSystem;
            Meteors = syncFrom.Meteors;
            Grids = syncFrom.Grids;
            ArmorShowArea = syncFrom.ArmorShowArea;
            Biologicals = syncFrom.Biologicals;
            Projectiles = syncFrom.Projectiles;
        }
    }
}
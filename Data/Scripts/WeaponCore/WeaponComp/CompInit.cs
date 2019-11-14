﻿using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Utils;

namespace WeaponCore.Support
{
    public partial class WeaponComponent
    {
        private void PowerInit()
        {
            SinkInfo = new MyResourceSinkInfo()
            {
                ResourceTypeId = GId,
                MaxRequiredInput = 0f,
                RequiredInputFunc = () => SinkPower,
            };
            //MyCube.Components.Remove<MyResourceSinkComponent>();
            MyCube.Components.TryGet(out Sink);
            Sink.Init(MyStringHash.GetOrCompute("Charging"), SinkInfo);
            //Sink = new MyResourceSinkComponent() { TemporaryConnectedEntity = MyCube};
            //Sink.Init(MyStringHash.GetOrCompute("Charging"), resourceInfo);
            //Sink.AddType(ref resourceInfo);
            //MyCube.Components.Add(Sink);
            Sink.Update();
        }

        private bool EntityAlive()
        {
            if (Ai.MyGrid?.Physics == null) return false;
            //if (!_firstSync && _readyToSync) SaveAndSendAll();
            if (!_isDedicated && _count == 29) TerminalRefresh();

            if (!_allInited && !PostInit()) return false;

            if (ClientUiUpdate || SettingsUpdated) UpdateSettings();
            return true;
        }

        private bool PostInit()
        {
            if (!_isServer && _clientNotReady) return false;
            //Session.Instance.CreateLogicElements(Turret);
            //WepUi.CreateUi(Turret);
            if (_isServer && !IsFunctional) return false;

            if (_mpActive && _isServer) State.NetworkUpdate();

            _allInited = true;
            return true;
        }

        private void StorageSetup()
        {
            var isServer = MyAPIGateway.Multiplayer.IsServer;

            if (State == null)
            {
                //Log.Line($"State null");
                State = new LogicState(this);
            }

            if (IsSorterTurret)
            {
                if (SorterBase.Storage == null)
                {
                    //Log.Line("Storage null");
                    State.StorageInit();
                }
            }
            else
            {
                if (MissileBase.Storage == null)
                {
                    //Log.Line("Storage null");
                    State.StorageInit();
                }
            }

            if (Set == null)
            {
                //Log.Line($"Settings null");
                Set = new LogicSettings(this);
            }

            State.LoadState();

            Set.LoadSettings();
            if (!State.LoadState() && !isServer) _clientNotReady = true;
            UpdateSettings(Set.Value);
            if (isServer)
            {
                foreach (var w in State.Value.Weapons) {
                    w.Heat = 0;
                }
            }
        }
    }
}

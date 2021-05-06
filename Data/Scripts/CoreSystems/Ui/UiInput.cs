﻿using System;
using CoreSystems.Support;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Input;
using VRageMath;

namespace CoreSystems
{
    internal class UiInput
    {
        internal int PreviousWheel;
        internal int CurrentWheel;
        internal int ShiftTime;
        internal bool MouseButtonPressed;
        internal bool InputChanged;
        internal bool MouseButtonLeftWasPressed;
        internal bool MouseButtonMenuWasPressed;
        internal bool MouseButtonRightWasPressed;
        internal bool WasInMenu;
        internal bool WheelForward;
        internal bool WheelBackward;
        internal bool ShiftReleased;
        internal bool ShiftPressed;
        internal bool LongShift;
        internal bool AltPressed;
        internal bool ControlKeyPressed;
        internal bool ActionKeyPressed;
        internal bool ControlKeyReleased;
        internal bool ActionKeyReleased;
        internal bool BlackListActive1;
        internal bool CtrlPressed;
        internal bool AnyKeyPressed;
        internal bool KeyPrevPressed;
        internal bool UiKeyPressed;
        internal bool UiKeyWasPressed;
        internal bool PlayerCamera;
        internal bool FirstPersonView;
        internal bool Debug = true;
        internal bool MouseShootWasOn;
        internal bool MouseShootOn;
        internal LineD AimRay;
        private readonly Session _session;
        private uint _lastInputUpdate;
        internal readonly InputStateData ClientInputState;
        internal MyKeys ControlKey;
        internal MyKeys ActionKey;

        internal MyMouseButtonsEnum MouseButtonMenu;

        internal UiInput(Session session)
        {
            _session = session;
            ClientInputState = new InputStateData();
        }

        internal void UpdateInputState()
        {
            var s = _session;
            WheelForward = false;
            WheelBackward = false;
            AimRay = new LineD();

            if (!s.InGridAiBlock) s.UpdateLocalAiAndCockpit();

            if (s.InGridAiBlock && !s.InMenu)
            {
                MouseButtonPressed = MyAPIGateway.Input.IsAnyMousePressed();

                MouseButtonLeftWasPressed = ClientInputState.MouseButtonLeft;
                MouseButtonMenuWasPressed = ClientInputState.MouseButtonMenu;
                MouseButtonRightWasPressed = ClientInputState.MouseButtonRight;

                WasInMenu = ClientInputState.InMenu;
                ClientInputState.InMenu = _session.InMenu;

                if (MouseButtonPressed)
                {
                    ClientInputState.MouseButtonLeft = MyAPIGateway.Input.IsMousePressed(MyMouseButtonsEnum.Left);
                    ClientInputState.MouseButtonMenu = MyAPIGateway.Input.IsMousePressed(MouseButtonMenu);
                    ClientInputState.MouseButtonRight = MyAPIGateway.Input.IsMousePressed(MyMouseButtonsEnum.Right);
                }
                else
                {
                    ClientInputState.MouseButtonLeft = false;
                    ClientInputState.MouseButtonMenu = false;
                    ClientInputState.MouseButtonRight = false;
                }

                _session.PlayerMouseStates[_session.PlayerId] = ClientInputState;

                if (_session.MpActive)
                {
                    var shootButtonActive = ClientInputState.MouseButtonLeft || ClientInputState.MouseButtonRight;

                    MouseShootWasOn = MouseShootOn;
                    if ((_session.ManualShot || s.Tick - _lastInputUpdate >= 29) && shootButtonActive && !MouseShootOn)
                    {
                        _lastInputUpdate = s.Tick;
                        MouseShootOn = true;
                    }
                    else if (MouseShootOn && !shootButtonActive)
                        MouseShootOn = false;

                    InputChanged = MouseShootOn != MouseShootWasOn || WasInMenu != ClientInputState.InMenu;
                    _session.ManualShot = false;
                }

                ShiftReleased = MyAPIGateway.Input.IsNewKeyReleased(MyKeys.LeftShift);
                ShiftPressed = MyAPIGateway.Input.IsKeyPress(MyKeys.LeftShift);
                ControlKeyReleased = MyAPIGateway.Input.IsNewKeyReleased(ControlKey);

                if (ShiftPressed)
                {
                    ShiftTime++;
                    LongShift = ShiftTime > 59;
                }
                else
                {
                    if (LongShift) ShiftReleased = false;
                    ShiftTime = 0;
                    LongShift = false;
                }

                AltPressed = MyAPIGateway.Input.IsAnyAltKeyPressed();
                CtrlPressed = MyAPIGateway.Input.IsKeyPress(MyKeys.Control);
                KeyPrevPressed = AnyKeyPressed;
                AnyKeyPressed = MyAPIGateway.Input.IsAnyKeyPress();
                UiKeyWasPressed = UiKeyPressed;
                UiKeyPressed = CtrlPressed || AltPressed || ShiftPressed;
                PlayerCamera = MyAPIGateway.Session.IsCameraControlledObject;
                FirstPersonView = PlayerCamera && MyAPIGateway.Session.CameraController.IsInFirstPersonView;

                if ((!UiKeyPressed && !UiKeyWasPressed) || !AltPressed && CtrlPressed && !FirstPersonView)
                {
                    PreviousWheel = MyAPIGateway.Input.PreviousMouseScrollWheelValue();
                    CurrentWheel = MyAPIGateway.Input.MouseScrollWheelValue();
                }


            }
            else if (!s.InMenu)
            {
                CtrlPressed = MyAPIGateway.Input.IsKeyPress(MyKeys.Control);
                ControlKeyPressed = MyAPIGateway.Input.IsKeyPress(ControlKey);

                if (CtrlPressed && ControlKeyPressed && GetAimRay(s, out AimRay) && Debug)
                {
                    DsDebugDraw.DrawLine(AimRay, Color.Red, 0.1f);
                }
            }

            if (!s.InMenu)
            {
                //ActionKeyReleased = MyAPIGateway.Input.IsNewKeyReleased(ActionKey);
                ActionKeyPressed = MyAPIGateway.Input.IsKeyPress(ActionKey);
                if (ActionKeyPressed && _session.CanChangeHud)
                {

                    if (!BlackListActive1)
                        BlackList1(true);

                    var evenTicks = _session.Tick % 2 == 0;
                    if (evenTicks)
                    {

                        if (MyAPIGateway.Input.IsKeyPress(MyKeys.Up))
                        {
                            _session.Settings.ClientConfig.HudPos.Y += 0.01f;
                            _session.Settings.VersionControl.UpdateClientCfgFile();
                        }
                        else if (MyAPIGateway.Input.IsKeyPress(MyKeys.Down))
                        {
                            _session.Settings.ClientConfig.HudPos.Y -= 0.01f;
                            _session.Settings.VersionControl.UpdateClientCfgFile();
                        }
                        else if (MyAPIGateway.Input.IsKeyPress(MyKeys.Left))
                        {
                            _session.Settings.ClientConfig.HudPos.X -= 0.01f;
                            _session.Settings.VersionControl.UpdateClientCfgFile();
                        }
                        else if (MyAPIGateway.Input.IsKeyPress(MyKeys.Right))
                        {
                            _session.Settings.ClientConfig.HudPos.X += 0.01f;
                            _session.Settings.VersionControl.UpdateClientCfgFile();
                        }
                    }

                    if (_session.Tick10)
                    {
                        if (MyAPIGateway.Input.IsKeyPress(MyKeys.Add))
                        {
                            _session.Settings.ClientConfig.HudScale = MathHelper.Clamp(_session.Settings.ClientConfig.HudScale + 0.01f, 0.1f, 10f);
                            _session.Settings.VersionControl.UpdateClientCfgFile();
                        }
                        else if (MyAPIGateway.Input.IsKeyPress(MyKeys.Subtract))
                        {
                            _session.Settings.ClientConfig.HudScale = MathHelper.Clamp(_session.Settings.ClientConfig.HudScale - 0.01f, 0.1f, 10f);
                            _session.Settings.VersionControl.UpdateClientCfgFile();
                        }
                    }
                }
            }
            else
            {
                ActionKeyPressed = false;
            }

            if (_session.MpActive && !s.InGridAiBlock)
            {
                if (ClientInputState.InMenu || ClientInputState.MouseButtonRight || ClientInputState.MouseButtonMenu || ClientInputState.MouseButtonRight)
                {
                    ClientInputState.InMenu = false;
                    ClientInputState.MouseButtonLeft = false;
                    ClientInputState.MouseButtonMenu = false;
                    ClientInputState.MouseButtonRight = false;
                    InputChanged = true;
                }
            }

            if (CurrentWheel != PreviousWheel && CurrentWheel > PreviousWheel)
                WheelForward = true;
            else if (s.UiInput.CurrentWheel != s.UiInput.PreviousWheel)
                WheelBackward = true;

            if (!ActionKeyPressed && BlackListActive1)
                BlackList1(false);
        }

        internal bool GetAimRay(Session s, out LineD ray)
        {
            var character = MyAPIGateway.Session.Player.Character;
            if (character != null)
            {
                ray = new LineD(s.PlayerPos, s.PlayerPos + (character.WorldMatrix.Forward * 1000000));
                return true;
            }
            ray = new LineD();
            return false;
        }

        private void BlackList1(bool activate)
        {
            try
            {
                var upKey = MyAPIGateway.Input.GetControl(MyKeys.Up);
                var downKey = MyAPIGateway.Input.GetControl(MyKeys.Down);
                var leftKey = MyAPIGateway.Input.GetControl(MyKeys.Left);
                var rightkey = MyAPIGateway.Input.GetControl(MyKeys.Right);
                var addKey = MyAPIGateway.Input.GetControl(MyKeys.Add);
                var subKey = MyAPIGateway.Input.GetControl(MyKeys.Subtract);

                if (upKey != null)
                {
                    MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(upKey.GetGameControlEnum().String, _session.PlayerId, !activate);
                }
                if (downKey != null)
                {
                    MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(downKey.GetGameControlEnum().String, _session.PlayerId, !activate);
                }
                if (leftKey != null)
                {
                    MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(leftKey.GetGameControlEnum().String, _session.PlayerId, !activate);
                }
                if (rightkey != null)
                {
                    MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(rightkey.GetGameControlEnum().String, _session.PlayerId, !activate);
                }
                if (addKey != null)
                {
                    MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(addKey.GetGameControlEnum().String, _session.PlayerId, !activate);
                }
                if (subKey != null)
                {
                    MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(subKey.GetGameControlEnum().String, _session.PlayerId, !activate);
                }

                BlackListActive1 = activate;
            }
            catch (Exception ex) { Log.Line($"Exception in BlackList1: {ex}"); }
        }
    }
}

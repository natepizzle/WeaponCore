﻿using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Entity;
using VRageMath;

namespace WeaponCore.Support
{
    class ShieldTree
    {
        /*
        private MyDynamicAABBTreeD _aabbTree = new MyDynamicAABBTreeD(MyConstants.GAME_PRUNING_STRUCTURE_AABB_EXTENSION, 1.0);
        private Stack<int> _stack = new Stack<int>();

        public void AddShield(Controllers c)
        {
            if (c.DtreeProxyId != -1)
                return;
            BoundingBoxD worldAabb = c.WebBox;
            c.DtreeProxyId = _aabbTree.AddProxy(ref worldAabb, c, 0U, true);
        }

        public void RemoveShield(Controllers shield)
        {
            if (shield.DtreeProxyId == -1)
                return;
            _aabbTree.RemoveProxy(shield.DtreeProxyId);
            shield.DtreeProxyId = -1;
        }

        public void MoveShield(Controllers shield)
        {
            if (shield.DtreeProxyId == -1)
                return;
            BoundingBoxD worldAabb = shield.WebBox;
            _aabbTree.MoveProxy(shield.DtreeProxyId, ref worldAabb, Vector3.Zero);
        }

        public void Clear()
        {
            _aabbTree.Clear();
        }

        public void GetAllShieldsInSphere(BoundingSphereD sphere, List<Controllers> result)
        {
            _aabbTree.OverlapAllBoundingSphere(ref sphere, result, false);
        }

        public void GetAllShieldsInBox(BoundingBoxD box, List<Controllers> result)
        {
            _aabbTree.OverlapAllBoundingBox(ref box, result, 0U, false);
        }

        public void GetShieldsChangesInBox(int callerId, BoundingBoxD box, HashSet<Controllers> foundShields, HashSet<Controllers> lostShields, Dictionary<MyEntity, Controllers> compare)
        {
            var root = _aabbTree.GetRoot();
            var noneFound = true;

            if (root == -1)
                return;

            _stack.Clear();
            _stack.Push(root);

            while (_stack.Count > 0)
            {
                int id = _stack.Pop();
                var nodeBB = _aabbTree.GetAabb(id);

                if (!nodeBB.Intersects(box))
                    continue;

                int left;
                int right;
                _aabbTree.GetChildren(id, out left, out right); // GetBranches!!!

                if (left == -1) // is leaf
                {
                    if (id == callerId) continue;
                    noneFound = false;

                    var ds = _aabbTree.GetUserData<Controllers>(id);
                    if (!compare.ContainsKey(ds.Bus.Spine)) foundShields.Add(ds);
                    else lostShields.Add(ds);
                }
                else
                {
                    if (left >= 0)
                        _stack.Push(left);

                    if (right >= 0)
                        _stack.Push(right);
                }
            }

            foreach (var shield in compare.Values)
            {
                if (noneFound)
                {
                    lostShields.Add(shield);
                }
                else if (lostShields.Contains(shield)) lostShields.Remove(shield);
            }
        }

        public void GetAllShieldsInBoxDict(int callerId, BoundingBoxD box, Dictionary<MyEntity, Controllers> results)
        {
            var root = _aabbTree.GetRoot();

            if (root == -1)
                return;

            _stack.Clear();
            _stack.Push(root);

            while (_stack.Count > 0)
            {
                int id = _stack.Pop();
                var nodeBB = _aabbTree.GetAabb(id);

                if (!nodeBB.Intersects(box))
                    continue;

                int left;
                int right;
                _aabbTree.GetChildren(id, out left, out right); // GetBranches!!!

                if (left == -1) // is leaf
                {
                    if (id == callerId) continue;

                    var ds = _aabbTree.GetUserData<Controllers>(id);
                    results.Add(ds.Bus.Spine, ds);
                }
                else
                {
                    if (left >= 0)
                        _stack.Push(left);

                    if (right >= 0)
                        _stack.Push(right);
                }
            }
        }

        public int GetAllShieldInBoxCount(int callerId, BoundingBoxD box)
        {
            var root = _aabbTree.GetRoot();
            var count = 0;

            if (root == -1)
                return count;

            var stack = new Stack<int>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var id = stack.Pop();
                var nodeBB = _aabbTree.GetAabb(id);

                if (!nodeBB.Intersects(box))
                    continue;

                int left;
                int right;
                _aabbTree.GetChildren(id, out left, out right); // GetBranches!!!

                if (left == -1) // is leaf
                {
                    if (id == callerId) continue;
                    count++;
                }
                else
                {
                    if (left >= 0)
                        stack.Push(left);

                    if (right >= 0)
                        stack.Push(right);
                }
            }

            return count;
        }

        public BoundingBoxD GetAabb(int proxyId)
        {
            var aabb = _aabbTree.GetAabb(proxyId);
            return aabb;
        }

        public void GetAllShields(List<Controllers> result)
        {
            _aabbTree.GetAll(result, false, null);
        }
    */
    }
}

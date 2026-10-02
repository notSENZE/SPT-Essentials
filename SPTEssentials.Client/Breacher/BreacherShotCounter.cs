using System;
using System.Collections.Generic;

namespace SPTEssentials.Client.Breacher;

internal sealed class BreacherShotCounter
{
    private readonly int _requiredShots;
    private readonly Dictionary<int, DoorHitState> _doorHits = new Dictionary<int, DoorHitState>();

    internal BreacherShotCounter(int requiredShots)
    {
        if (requiredShots < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(requiredShots));
        }

        _requiredShots = requiredShots;
    }

    internal bool RegisterShot(int doorId, string weaponId, int fireIndex)
    {
        if (!_doorHits.TryGetValue(doorId, out var hitState))
        {
            hitState = new DoorHitState();
            _doorHits.Add(doorId, hitState);
        }

        if (!hitState.Shots.Add(new ShotKey(weaponId, fireIndex)))
        {
            return false;
        }

        hitState.ShotCount++;
        if (hitState.ShotCount < _requiredShots)
        {
            return false;
        }

        _doorHits.Remove(doorId);
        return true;
    }

    internal void Clear()
    {
        _doorHits.Clear();
    }

    private sealed class DoorHitState
    {
        internal int ShotCount;
        internal readonly HashSet<ShotKey> Shots = new HashSet<ShotKey>();
    }

    private readonly struct ShotKey : IEquatable<ShotKey>
    {
        private readonly string _weaponId;
        private readonly int _fireIndex;

        internal ShotKey(string weaponId, int fireIndex)
        {
            _weaponId = weaponId;
            _fireIndex = fireIndex;
        }

        public bool Equals(ShotKey other)
        {
            return _fireIndex == other._fireIndex
                && string.Equals(_weaponId, other._weaponId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ShotKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((_weaponId != null ? _weaponId.GetHashCode() : 0) * 397) ^ _fireIndex;
            }
        }
    }
}

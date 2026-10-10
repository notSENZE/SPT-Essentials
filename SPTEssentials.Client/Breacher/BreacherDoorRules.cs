using System;
using System.Collections.Generic;

namespace SPTEssentials.Client.Breacher;

internal static class BreacherDoorRules
{
    internal const string BreachingWeaponTemplateId = "6aca004f8ca7e3feaca39e3f";
    internal const string NormalDoorAmmoTemplateId = "6aca068e1d8673d4af3aad02";
    internal const string MarkedRoomAmmoTemplateId = "6aca07cd8b2dc0bac577e0e7";

    private const int NormalDoorShots = 4;
    private const int MarkedRoomShots = 6;

    private static readonly HashSet<string> MarkedRoomKeyTemplateIds = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "5780cf7f2459777de4559322",
        "5d80c60f86f77440373c4ece",
        "5d80c62a86f7744036212b3f",
        "5ede7a8229445733cb4c18e2",
        "62987dfc402c7f69bf010923",
        "63a3a93f8a56922e82001f5d",
        "64ccc25f95763a1ae376e447"
    };

    internal static bool TryGetRequiredShots(
        string keyTemplateId,
        string weaponTemplateId,
        string ammoTemplateId,
        out int requiredShots)
    {
        var markedRoom = !string.IsNullOrWhiteSpace(keyTemplateId)
            && MarkedRoomKeyTemplateIds.Contains(keyTemplateId);

        requiredShots = markedRoom ? MarkedRoomShots : NormalDoorShots;
        if (!IsBreachingWeapon(weaponTemplateId))
        {
            return false;
        }

        if (markedRoom)
        {
            return string.Equals(
                ammoTemplateId,
                MarkedRoomAmmoTemplateId,
                StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(
                ammoTemplateId,
                NormalDoorAmmoTemplateId,
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                ammoTemplateId,
                MarkedRoomAmmoTemplateId,
                StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsBreachingWeapon(string weaponTemplateId)
    {
        return string.Equals(
            weaponTemplateId,
            BreachingWeaponTemplateId,
            StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsBreachingAmmo(string ammoTemplateId)
    {
        return string.Equals(
                ammoTemplateId,
                NormalDoorAmmoTemplateId,
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                ammoTemplateId,
                MarkedRoomAmmoTemplateId,
                StringComparison.OrdinalIgnoreCase);
    }
}

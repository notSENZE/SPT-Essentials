using SPTEssentials.Client.Breacher;

var passed = 0;

void Check(bool condition, string name)
{
    if (!condition)
    {
        throw new Exception("FAIL: " + name);
    }

    passed++;
    Console.WriteLine("PASS: " + name);
}

var counter = new BreacherShotCounter();
Check(!counter.RegisterShot(10, "shotgun-a", 1, 2), "first shot does not breach");
Check(!counter.RegisterShot(10, "shotgun-a", 1, 2), "pellets from the same shot count once");
Check(counter.RegisterShot(10, "shotgun-a", 2, 2), "second distinct shot breaches");

counter = new BreacherShotCounter();
Check(!counter.RegisterShot(10, "shotgun-a", 1, 2), "first door keeps its own count");
Check(!counter.RegisterShot(20, "shotgun-a", 2, 2), "second door keeps a separate count");
Check(counter.RegisterShot(10, "shotgun-a", 3, 2), "first door breaches after its own second hit");
Check(counter.RegisterShot(20, "shotgun-a", 4, 2), "second door breaches after its own second hit");

counter = new BreacherShotCounter();
Check(!counter.RegisterShot(10, "shotgun-a", 1, 2), "weapon identity is part of a shot");
Check(counter.RegisterShot(10, "shotgun-b", 1, 2), "matching fire indexes from different weapons are distinct");

counter = new BreacherShotCounter();
Check(!counter.RegisterShot(10, "shotgun-a", 1, 2), "counter contains one shot before reset");
counter.Clear();
Check(!counter.RegisterShot(10, "shotgun-a", 2, 2), "raid reset clears earlier hits");

Check(
    BreacherDoorRules.TryGetRequiredShots(
        null,
        BreacherDoorRules.BreachingWeaponTemplateId,
        BreacherDoorRules.NormalDoorAmmoTemplateId,
        out var normalDoorShots)
    && normalDoorShots == 4,
    "normal doors require four door-breacher .50 BMG slugs");
Check(
    BreacherDoorRules.TryGetRequiredShots(
        null,
        BreacherDoorRules.BreachingWeaponTemplateId,
        BreacherDoorRules.MarkedRoomAmmoTemplateId,
        out var normalDoorAp20Shots)
    && normalDoorAp20Shots == 4,
    "AP-20 superslug breaches a normal door in four shots");

const string DormRoom314MarkedKey = "5780cf7f2459777de4559322";
Check(
    BreacherDoorRules.TryGetRequiredShots(
        DormRoom314MarkedKey,
        BreacherDoorRules.BreachingWeaponTemplateId,
        BreacherDoorRules.MarkedRoomAmmoTemplateId,
        out var markedRoomShots)
    && markedRoomShots == 6,
    "marked rooms require six AP-20 superslugs");
Check(
    !BreacherDoorRules.TryGetRequiredShots(
        DormRoom314MarkedKey,
        BreacherDoorRules.BreachingWeaponTemplateId,
        BreacherDoorRules.NormalDoorAmmoTemplateId,
        out _),
    "door-breacher .50 BMG does not breach a marked-room door");
Check(
    !BreacherDoorRules.TryGetRequiredShots(
        null,
        "5a7828548dc32e5a9c28b516",
        BreacherDoorRules.NormalDoorAmmoTemplateId,
        out _),
    "the original M870 cannot breach with the special ammunition");
Check(
    !BreacherDoorRules.TryGetRequiredShots(
        null,
        BreacherDoorRules.BreachingWeaponTemplateId,
        "5d6e68c4a4b9361b93413f79",
        out _),
    "the old makeshift .50 BMG slug no longer breaches doors");
Check(
    !BreacherDoorRules.TryGetRequiredShots(
        DormRoom314MarkedKey,
        BreacherDoorRules.BreachingWeaponTemplateId,
        "5d6e68a8a4b9360b6c0d54e2",
        out _),
    "the old AP-20 slug no longer breaches marked-room doors");

Check(
    ExplosiveBreacherRules.IsBreachingRocketLauncher(
        ExplosiveBreacherRules.Rshg2TemplateId),
    "the RShG-2 can breach doors");
Check(
    !ExplosiveBreacherRules.IsBreachingRocketLauncher("5a7828548dc32e5a9c28b516"),
    "ordinary weapons are not treated as rocket launchers");

Console.WriteLine($"{passed} checks passed. These are isolated counting tests, not an EFT runtime test.");

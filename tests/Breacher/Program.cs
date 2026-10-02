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

var counter = new BreacherShotCounter(2);
Check(!counter.RegisterShot(10, "shotgun-a", 1), "first shot does not breach");
Check(!counter.RegisterShot(10, "shotgun-a", 1), "pellets from the same shot count once");
Check(counter.RegisterShot(10, "shotgun-a", 2), "second distinct shot breaches");

counter = new BreacherShotCounter(2);
Check(!counter.RegisterShot(10, "shotgun-a", 1), "first door keeps its own count");
Check(!counter.RegisterShot(20, "shotgun-a", 2), "second door keeps a separate count");
Check(counter.RegisterShot(10, "shotgun-a", 3), "first door breaches after its own second hit");
Check(counter.RegisterShot(20, "shotgun-a", 4), "second door breaches after its own second hit");

counter = new BreacherShotCounter(2);
Check(!counter.RegisterShot(10, "shotgun-a", 1), "weapon identity is part of a shot");
Check(counter.RegisterShot(10, "shotgun-b", 1), "matching fire indexes from different weapons are distinct");

counter = new BreacherShotCounter(2);
Check(!counter.RegisterShot(10, "shotgun-a", 1), "counter contains one shot before reset");
counter.Clear();
Check(!counter.RegisterShot(10, "shotgun-a", 2), "raid reset clears earlier hits");

Console.WriteLine($"{passed} checks passed. These are isolated counting tests, not an EFT runtime test.");

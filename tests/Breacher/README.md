# Breacher isolated tests

Run with:

```powershell
dotnet run --project .\tests\Breacher\Breacher.Tests.csproj
```

These checks cover per-door hit counting, pellet deduplication, weapon identity, raid reset behavior, the required weapon template, the normal/marked-room ammunition rules and the explosive breaching TPL allowlists. They do not replace an in-game test of lock positions, door animation and interaction state.

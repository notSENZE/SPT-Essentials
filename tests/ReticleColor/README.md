# Custom Reticle Color checks

Run `dotnet run --project tests/ReticleColor/ReticleColor.Tests.csproj` with the .NET 10 SDK.

These checks compile the actual module against small Unity, EFT and BepInEx doubles. They cover ordinary material tinting, vertex-colored scope reticles, marked lens materials, active renderer binding, retained intensity and alpha, live setting changes, restoration and unsupported shaders. They do not prove how every vanilla or modded optic renders in game.

Before release, test in SPT 4.1.5:

- Equip several red-dot, reflex and holographic sights with different original colors.
- Check the HAMR, SIG TANGO6T and an EOTech HHS-1 in both modes.
- Change the global color while in the Hideout and during a raid; already loaded sights should update immediately.
- Change reticle mode, swap weapons and inspect the sight both normally and while aiming.
- Disable the module in F12; every sight should return to its original color.
- Start another raid and confirm the selected color is retained.
- Test with and without NVG Sight Dimmer enabled.
- Check additional magnified and hybrid optics for material-specific exceptions.
- If one still fails, collect the `Custom Reticle Color diagnostic` line from `BepInEx/LogOutput.log` after aiming through it once.

Version evidence: installed EFT 0.16.9.5.40743 / SPT 4.1.5. The local assembly exposes the collimator, optic-reticle and lens-renderer paths used by this module. Direct inspection of the installed assets confirms red vertex colors in the TANGO6T reticle mesh, while HAMR and the HHS-1 magnified mode use `_MarkTex` on their lens materials. Inactive hybrid modes are discovered through `ScopePrefabCache`.

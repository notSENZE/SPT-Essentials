## {.tabset}

### SPT ESSENTIALS

I built SPT Essentials as a single modular quality-of-life mod full of small improvements that are hard to give up once you have used them. It combines my own gameplay tweaks with carefully credited adaptations of two open-source mods and an independently made compact status display.

I made every module optional. You can enable or disable each one in the `F12` configuration menu and keep only the parts that suit your game.

I am preparing **SPT Essentials 1.1.0** for **SPT 4.1.5**. This is a test build; the new module still needs in-game testing before release. Later `4.1.x` versions may work, but I only claim support for versions I have tested.

#### 1.1.0 — in testing

- I added **Keep Tripwire Kit**: a successful multitool disarm leaves one installation kit beside the trap. The grenade still follows the game's normal behavior.
- I added **Custom Reticle Color**: choose one global color for supported collimator, hybrid and magnified-scope reticles.

#### 1.0.1 hotfix

- I fixed the Special Slots layout error that could interrupt the transit delivery screen.
- I fixed item icons disappearing from Special Slots after the layout was rebuilt, including when entering the Hideout.

### WHAT IS INCLUDED

- **Field Repair** adds the Field Armor Repair Kit. During a raid, drag it onto compatible damaged armor to repair it immediately. Outside raids, the normal repair screen is left alone.
- **Quickload Mag Saver** tries to place the old magazine in a free rig, pocket or inventory grid during a quick reload. When there is no room, the magazine still drops as usual.
- **Keep Tripwire Kit** leaves one Tripwire installation kit on the ground beside a trap after you successfully disarm it using a multitool. You pick it up normally, so the game's four-kit inventory limit remains in place. Disarming without a multitool, cancelling, explosions and expired traps do not return a kit. Recovered kits are not Found in Raid. I am targeting solo SPT; I have not verified Fika compatibility.
- **Raid Pause** freezes a solo raid, including the raid timer and time of day. The default key is `Down Arrow`.
- **Smart Air Filter** makes the Hideout air filter consume resources only while your PMC is in a raid.
- **Expanded Special Slots** adds three more Special Slots and arranges all six in a compact grid. Active SVM custom pocket layouts are supported.
- **Compatibility Rules** lets you extend item, container, Special Slot, weapon and attachment filters through one readable JSONC file.
- **Compact HUD** shows total health, energy, hydration and grenades that are ready in your rig or pockets. It can be moved, scaled and arranged horizontally or vertically.
- **Custom Reticle Color** gives supported red-dot, reflex, holographic, hybrid and magnified-scope reticles one global color. Pick it in the `F12` menu and the change is applied immediately.

### INSTALLATION AND SETTINGS

Copy the contents of the release into your main SPT folder and allow Windows to merge the folders.

Press `F12` in game to enable or disable individual modules and adjust Raid Pause, Compact HUD or the global reticle color. Compact HUD uses `F10` for edit mode and `F9` to restore its default position.

Field Repair, Smart Air Filter, Expanded Special Slots and Compatibility Rules also change server-side behavior. Restart both the SPT server and the game after changing one of those switches.

Compatibility rules live here:

```text
SPT_Runtime/user/mods/SPTEssentials/config/compat/compatibility.jsonc
```

The file contains commented examples. Add your template IDs, keep `REPLACE` set to `false` unless you intentionally want to clear an existing filter, then restart SPT.

If you previously installed my standalone Compact HUD, remove it before using SPT Essentials. I have included that functionality here, so running both versions would load it twice.

### SOURCE AND BUILDING

I publish my release source on [GitHub](https://github.com/notSENZE/SPT-Essentials). This local test build has not been published yet.

To build it yourself, point `SPTInstallPath` at a clean SPT 4.1.5 installation and build both projects:

```powershell
dotnet build .\SPTEssentials.Client\SPTEssentials.Client.csproj -c Release -p:SPTInstallPath="C:\Path\To\SPT"
dotnet build .\SPTEssentials.Server\SPTEssentials.Server.csproj -c Release -p:SPTInstallPath="C:\Path\To\SPT"
```

### LICENSE

I release my contributions to SPT Essentials under the [MIT License](LICENSE). You may use, modify and redistribute them as long as you keep the applicable copyright and license notices.

Raid Pause and Smart Air Filter include adaptations of separately copyrighted MIT-licensed work. I preserve those original notices in `THIRD-PARTY-NOTICES.md` and the `licenses` folder. The permissions I received from netVnum and Utjan are also recorded there.

### CREDITS

I adapted and reimplemented **Raid Pause** for SPT 4.1.5 based on the MIT-licensed [Pause](https://github.com/netvnum/Pause), currently maintained by **netVnum** and originally created by **swaffordm**. My implementation uses the same underlying pause and timer-correction ideas, but I restructured it for SPT Essentials and the current game version. I also received explicit permission from **netVnum** to adapt and include Pause in SPT Essentials.

I adapted and reimplemented **Smart Air Filter** for SPT 4.1.5 based on the MIT-licensed [AirFilterQOL](https://github.com/Utjan/AirFilterQOL) and [SPTAirFilterQOLClientMod](https://github.com/Utjan/SPTAirFilterQOLClientMod) by **Utjan**. My implementation follows their core approach of stopping air-filter drain outside a PMC raid and adds its own client-side safeguard. I also received explicit permission from **Utjan** to adapt and include this functionality in SPT Essentials. The AirFilterQOL license names **Jehree** as its copyright holder.

I wrote **Compact HUD** independently as a lightweight take on the general HUD concept popularized by [Game Panel HUD](https://github.com/kmyuhkyuk/GamePanelHUD) by **kmyuhkyuk**. I did not copy, modify or distribute any Game Panel HUD code, assets, localization or dependencies.

I wrote **Custom Reticle Color** independently for SPT 4.1.5 after looking at the general user-facing idea behind laser color mods such as [RGBLasers](https://github.com/kiobu/aki-rgblasers) by **kiobu**. I did not copy or adapt code from RGBLasers or Fontaine's Red Dot Tweaker.

I include the applicable MIT notices for Pause and AirFilterQOL in `THIRD-PARTY-NOTICES.md` and the `licenses` folder. Field Repair, Quickload Mag Saver and the remaining SPT Essentials modules are my own implementations.

### AI TRANSPARENCY

Just to be completely open about it: I’m not a trained software developer, and I don’t understand every single part of the code yet. I’m learning as I go.

A large part of the code was implemented with the help of Codex. It also helps me find the right classes and APIs, read telemetry logs, track down bugs and work on the documentation.

The idea behind the mod, its features, balancing, design decisions and the final say on what gets released all come from me. I don’t simply take generated code and throw it into a release.

New features are tested in-game, checked through logs and, whenever something is unclear, compared against the public SPT source code. If something cannot be properly verified or tested, it gets put aside for later.

That doesn’t mean mistakes can’t happen, especially while I’m still learning. At the end of the day, though, I’m responsible for the mod I publish and for the decisions made along the way, not the tool I used to help build it.

I know that AI-assisted mods are a touchy subject in parts of the community, and I understand why. That’s exactly why I want to be honest about how AI was used here.

The source code is publicly available, and constructive code reviews, specific suggestions and reproducible bug reports are always welcome.

### SUPPORT ME

If you enjoy my work and want to support it, you can find me on [Ko-fi](https://ko-fi.com/not_senze).

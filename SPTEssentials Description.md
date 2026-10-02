# IMPORTANT NOTE
My mods will no longer be available on The Forge.
According to the rules their, you won’t receive support if you’ve installed mods that weren’t published there. You’ll be advised to uninstall these (my) mods or be denied support altogether.
If you have any issues with my mods, please contact me directly on Discord.

**Discord Name:** *@obviously.senze*
**My Discord server:** https://discord.gg/PD5TPQFKpa


# SPT ESSENTIALS

“SPT Essentials” is a type of mod pack that includes many different, modular mods that can be turned on and off in the F12 menu,
allowing you to choose which ones should remain active.

**SPT version:** ~4.1.6
**Dependencies:** none

# WHAT IS INCLUDED

- **Field Repair** adds the Field Armor Repair Kit. During a raid, drag it onto compatible damaged armor to repair it immediately. Outside raids, the normal repair screen is left alone.
- **Quickload Mag Saver** tries to place the old magazine in a free rig, pocket or inventory grid during a quick reload. When there is no room, the magazine still drops as usual.
- **Raid Pause** freezes a solo raid, including the raid timer and time of day. The default key is `Down Arrow`.
- **Smart Air Filter** makes the Hideout air filter consume resources only while your PMC is in a raid.
- **Expanded Special Slots** adds three more Special Slots and arranges all six in a compact grid. Active SVM custom pocket layouts are supported.
- **Compatibility Rules** lets you extend item, container, Special Slot, weapon and attachment filters through one readable JSONC file.
- **Compact HUD** shows total health, energy, hydration and grenades that are ready in your rig or pockets. It can be moved, scaled and arranged horizontally or vertically.
[![CHUD.png](https://i.postimg.cc/SRffTmss/CHUD.png)](https://postimg.cc/tn767QFQ)
- **Custom Reticle Color** makes it possible to change the reticle of _some_ scopes.
[![Reticle-color.png](https://i.postimg.cc/QtVmL9mk/Reticle-color.png)](https://postimg.cc/2bRhh5PV)
- **Keep tripwire installation kit** gives you the possibilty to disarm a tripwire installation kit without losing it (works only with Leatherman Multitool).
- **Breacher** lets you blast open a regular hinged door with two shotgun shots. Keycard doors, sliding doors and other special door types are left untouched.

# INSTALLATION AND SETTINGS

Copy the contents of the release into your main SPT folder and allow Windows to merge the folders.

Press `F12` in game to enable or disable individual modules and adjust Raid Pause or Compact HUD. Compact HUD uses `F10` for edit mode and `F9` to restore its default position.

Field Repair, Smart Air Filter, Expanded Special Slots and Compatibility Rules also change server-side behavior. Restart both the SPT server and the game after changing one of those switches.

Compatibility rules live here:

```text
SPT_Runtime/user/mods/SPTEssentials/config/compat/compatibility.jsonc
```

The file contains commented examples. Add your template IDs, keep `REPLACE` set to `false` unless you intentionally want to clear an existing filter, then restart SPT.

If you previously installed my standalone Compact HUD, remove it before using SPT Essentials. I have included that functionality here, so running both versions would load it twice.

# LICENSE

I release my contributions to SPT Essentials under the [MIT License](LICENSE). You may use, modify and redistribute them as long as you keep the applicable copyright and license notices.

Raid Pause and Smart Air Filter include adaptations of separately copyrighted MIT-licensed work. I preserve those original notices in `THIRD-PARTY-NOTICES.md` and the `licenses` folder. The permissions I received from netVnum and Utjan are also recorded there.

# CREDITS

I adapted and reimplemented **Raid Pause** for SPT 4.1.5 based on the MIT-licensed [Pause](https://github.com/netvnum/Pause), currently maintained by **netVnum** and originally created by **swaffordm**. My implementation uses the same underlying pause and timer-correction ideas, but I restructured it for SPT Essentials and the current game version. I also received explicit permission from **netVnum** to adapt and include Pause in SPT Essentials.

I adapted and reimplemented **Smart Air Filter** for SPT 4.1.5 based on the MIT-licensed [AirFilterQOL](https://github.com/Utjan/AirFilterQOL) and [SPTAirFilterQOLClientMod](https://github.com/Utjan/SPTAirFilterQOLClientMod) by **Utjan**. My implementation follows their core approach of stopping air-filter drain outside a PMC raid and adds its own client-side safeguard. I also received explicit permission from **Utjan** to adapt and include this functionality in SPT Essentials. The AirFilterQOL license names **Jehree** as its copyright holder.

I wrote **Compact HUD** independently as a lightweight take on the general HUD concept popularized by [Game Panel HUD](https://github.com/kmyuhkyuk/GamePanelHUD) by **kmyuhkyuk**. I did not copy, modify or distribute any Game Panel HUD code, assets, localization or dependencies.

I wrote **Breacher** independently for SPT Essentials. It uses SPT's own hit and door behavior and contains no code from another door-breaching mod.

I include the applicable MIT notices for Pause and AirFilterQOL in `THIRD-PARTY-NOTICES.md` and the `licenses` folder. Field Repair, Quickload Mag Saver and the remaining SPT Essentials modules are my own implementations.

The module "Custom reticle color" is inspired by Acidphantasms [BrightLasers](https://sp-mod.com/mod/1358/brightlasers).

---

> INFO: I originally made Compact HUD primarily for my own use and never really intended to publish it. However, after seeing how popular [Game Panel HUD](https://sp-mod.com/mod/456/game-panel-hud) by [kmyuhkyuk](https://sp-mod.com/user/4974/kmyuhkyuk) had become, I decided to share my smaller take on the idea.

> Full credit for the original concept and inspiration goes to [kmyuhkyuk](https://sp-mod.com/user/4974/kmyuhkyuk). Compact HUD is an independent implementation and contains no code or assets from Game Panel HUD. If developer of the original mod resumes wants me to remove the module from the mod, I will do so immediately.

# AI TRANSPARENCY

Just to be completely open about it: I’m not a trained software developer, and I don’t understand every single part of the code yet. I’m learning as I go.

A large part of the code was implemented with the help of Codex. It also helps me find the right classes and APIs, read telemetry logs, track down bugs and work on the documentation.

The idea behind the mod, its features, balancing, design decisions and the final say on what gets released all come from me. I don’t simply take generated code and throw it into a release.

New features are tested in-game, checked through logs and, whenever something is unclear, compared against the public SPT source code. If something cannot be properly verified or tested, it gets put aside for later.

That doesn’t mean mistakes can’t happen, especially while I’m still learning. At the end of the day, though, I’m responsible for the mod I publish and for the decisions made along the way, not the tool I used to help build it.

I know that AI-assisted mods are a touchy subject in parts of the community, and I understand why. That’s exactly why I want to be honest about how AI was used here.

The source code is publicly available, and constructive code reviews, specific suggestions and reproducible bug reports are always welcome.

# SUPPORT ME

If you enjoy my work and want to support it, you can find me on [Ko-fi](https://ko-fi.com/not_senze).

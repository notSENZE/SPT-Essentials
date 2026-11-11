# ⚠️ IMPORTANT NOTE ⚠️
My mods will no longer be available on The Forge.
Under The Forge's rules, mods that aren't released there aren't supported.
This means that if you run into an issue that might be related to one of my mods, you may be denied support or advised to remove the mod.
If you experience any issues with my mods, please join my [Discord](https://discord.gg/PD5TPQFKpa) for support.

# ABOUT SPT ESSENTIALS
SPT Essentials is a kind of QoL modpack that introduces many small improvements and changes to the game to enhance your gaming experience.
All of the following modules can be turned on and off by pressing “F12.”

**SPT version:** `~4.1.6`  
**Dependencies:** [WTT - CommonLib](https://sp-mod.com/mod/2310/wtt-commonlib)  
**FIKA compatibility:** Unknown  
**Recommendations:** -  
**Known incompatibilities:** -  

# DETAILS
**FIELD REPAIR KIT**  
A small body armor repair kit that you can use during a raid to repair your ballistic plates.

**QUICKLOAD MAG SAVER**  
Are you tired of having to rely on Quickload during combat just to end up searching for your magazine afterward?
Quickload ensures that you can change magazines quickly and that it ends up in your inventory afterward.

**RAID PAUSE**  
You're in the middle of a battle and suddenly need to take a dump?
No more shitty buckets next to your gaming chair!
Press “arrow down” to pause your raid and take a dump like a normal person.

**SMART AIR FILTER**  
Air filters only consume resources when you're in a raid. 

**EXPANDED SPECIAL SLOTS**  
Do you need more special slots? Here are some more special slots.

**COMPACT HUD**  
A small HUD that constantly displays your health, energy, hydration, and grenades.

**CUSTOM RETICLE COLOR**  
Give your reticle a new color. You can also adjust how light or dark you want it to be.

**KEEP TRIPWIRE INSTALLATION KIT**  
You set a tripwire trap, but no one walks into it? Take it down and bring it back with you.
You can also disarm Partizan's traps and take them with you.
You'll need the Leatherman multitool for both.

**BREACHER**  
This requires the Door Breacher Shotgun.  
Normal locked doors take four hits with either 12/70 door breacher .50 BMG slugs or 12/70 AP-20 superslugs.  
Marked Rooms take six AP-20 superslugs. The RShG-2 opens a directly hit door with one explosive shot, without requiring a lock hit.  
The module also adds a Breaching Charge with a five-second fuse.  
It can be attached to regular, keycard and sliding doors, and removed again by holding F for one second while there is room in your inventory.  
Extraction doors and scripted map gates are deliberately left alone.  
The charge appears in suitable loot containers and on the Flea Market with a base price of 350,000 roubles.

**RAID-LONG PAIN RELIEF**  
Morphine or ibuprofen only last a few minutes? Bullshit. Now it lasts the whole raid.

**SPRINT WHILE HEALING/PACKING MAGS**  
You're the GOAT.  
Sure, you can run while healing yourself or reloading your magazines.  
But you can't have surgery and run at the same time. You're not *THE* GOAT.

**RAID MODDABLE**  
Of course, with a Leatherman multitool, you can take your entire weapon - and I mean the entire weapon - apart right in the middle of a raid.

**COMPAT RULES**  
Okay! This is a bit more complicated and only for advanced users.  
Under `SPT_Runtime\user\mods\SENZE - SPT Essentials.Server\config\compat`, there is a `compatibility.jsonc` file.  
Here’s what you can do:

Let’s say you have an AK-12, which by default only has a single dust cover.  
Using `COMPAT RULES`, you just need to find the item’s Tpl and can, for example, make other AK dust covers available for the AK-12.  

You can combine dozens of items with each other. 


# CREDITS
I adapted and reimplemented **Raid Pause** based on the MIT-licensed [Pause](https://github.com/netvnum/Pause), currently maintained by **netVnum** and originally created by **swaffordm**.  
My implementation uses the same underlying pause and timer-correction ideas, but I restructured it for SPT Essentials and the current game version.  
I also received explicit permission from **netVnum** to adapt and include Pause in SPT Essentials.

I adapted and reimplemented **Smart Air Filter** based on the MIT-licensed [AirFilterQOL](https://github.com/Utjan/AirFilterQOL) and [SPTAirFilterQOLClientMod](https://github.com/Utjan/SPTAirFilterQOLClientMod) by **Utjan**.  
My implementation follows their core approach of stopping air-filter drain outside a PMC raid and adds its own client-side safeguard.  
I also received explicit permission from **Utjan** to adapt and include this functionality in SPT Essentials.  
The AirFilterQOL license names **Jehree** as its copyright holder.

# BUG REPORTS
If you encounter a bug or experience any issues with my mods, please include the following information in your report:

- **Description:** What happened, and what were you doing when the issue occurred?
- **BepInEx logs**
- **Server logs**
- **Screenshots or video recordings**, if available

Please submit your bug reports in the dedicated channel [on my Discord server](https://discord.com/channels/1551226098354753576/1552431523658203307).

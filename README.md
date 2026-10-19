# Town of gHost

**Like Town of Host, but built for ranked settings — with an optional Jester role and the lowest vision ever played!**

Town of gHost is a **host-only** Among Us mod: only the host installs it. Your friends join with the normal, unmodded game and still get every rule.

It brings the ranked-mode settings into private lobbies — a task time limit for the crew, the time left posted in every meeting — and adds a Jester, an impostor secret chat and crewmate vision far below the game's own minimum.

> Not affiliated with Innersloth. Use it in private lobbies with friends. Innersloth's mod policy applies.

---

## Download

Get them from the **[Releases](../../releases)** page:

| File | What it is |
|---|---|
| **Town.Of.gHost.zip** | **Steam version** — ready to play. Extract and start the game. |
| **TownOfGHost.dll** | The plugin only, if you already have BepInEx 6 (IL2CPP) installed: put it in `BepInEx\plugins`. |
| **Source code (zip)** | **Source code** — for building the mod yourself. |

### Install (Steam version)
1. In Steam: right-click **Among Us → Manage → Browse local files**.
2. Extract **Town.Of.gHost.zip** into that folder, next to `Among Us.exe`.
3. Start the game. The **first launch is slower** while BepInEx sets itself up; after that it is normal.
4. Create a lobby and type **`/help`** in the chat.

After an Among Us update the mod must be rebuilt for the new version.

---

## Features

- **Ranked task time limit** — the crew must finish all tasks in time or the Impostor team wins (default 18 minutes).
- **Time left in every meeting** — "Tasks time left M:SS" is posted in the chat when a meeting starts.
- **Pause or no pause** — choose whether meetings stop the clock. If time runs out during a meeting, the loss is applied right **after** that meeting, so the crew can still win the vote.
- **Lowest vision ever played** — crewmate vision down to **0x (pitch black)**, far below the game's own minimum.
- **Lights-out vision** — choose how far crewmates see while the lights are sabotaged, separately from their normal vision. Impostors always keep full vision.
- **Jester** — a third team of one. One random crewmate is secretly the Jester and **wins by getting voted out**.
  - He sees **"Jester"** above his head and a **big J on his body** (still visible at the lowest vision). Nobody else sees it.
  - His tasks are **fake**: they never count toward the crew's task win.
  - When he wins, **everyone's end screen** shows him as the only winner, and the lobby chat reveals the Jester's and the Impostors' colors (Impostors who left are listed too, marked "(left)").
  - **Judge:** a Judge who overrules the Jester ejects **him** (he wins) instead of being ejected for a wrong judgment. If the Jester is the Judge, getting ejected by his own wrong overrule doesn't count as a win.
- **Impostor secret chat** — impostors talk privately in meetings. Only impostors and dead players can read it.
- **Lobby keep-alive** — automatically accepts the server's "extend lobby" offer so the lobby isn't closed for inactivity.
- **Everything is announced** — every rule change is posted in the lobby chat for everyone.
- **Town of gHost look** — the Town of gHost picture while the game loads (click to skip), the Town of gHost banner in the main menu, and the mod's name, version and website in the top-right corner under the ping. Only on your screen.

Everything is **OFF** in a new lobby. Your choices **stay on after each game** until you leave the lobby.

---

## Host commands

Type them in the chat. Only you, the host, can use them, and they are never sent to the other players as chat.

| Command | What it does |
|---|---|
| `/help` | Lists all commands (only you see it). |
| `/settings` | Posts every rule that is ON, with its details, for everyone. Works any time. |
| `/time on` / `/time off` | Turns the tasks time limit on or off. |
| `/time X` | Sets the time limit to X minutes (0.5–180) and turns it on. Example: `/time 18` |
| `/time pause on` / `/time pause off` | Should meetings pause the clock? |
| `/vision X` | Crewmate vision, from 0 (pitch black) to 0.5. Example: `/vision 0.05` |
| `/vision off` | Back to the vision set in the game's settings. |
| `/lightvision X` | Crewmate vision while the lights are out, from 0 (pitch black) to 0.5. Impostors keep full vision. Example: `/lightvision 0.02` |
| `/lightvision off` | Back to the game's own darkening when the lights are out. |
| `/jester on` / `/jester off` | Turns the Jester on or off. |
| `/impchat on` / `/impchat off` | Turns the impostor secret chat on or off. |

`/time`, `/vision`, `/lightvision`, `/jester` and `/impchat` work in the lobby, before the game starts.

### Impostor secret chat
- Impostors who are **not the host** type **`/cmd your message`** in a meeting.
- The host, when an impostor, types **`// your message`**.
- Don't type `//` if you're not the host: that goes to everyone.

---

## Settings file

`BepInEx\config\local.townofghost.cfg` — edit it with the game closed.

| Section | Setting | Default | What it does |
|---|---|---|---|
| General | `Enabled` | true | Master switch. The mod only acts while you host an online lobby. |
| General | `CloseConsoleAfterSeconds` | 3 | Close the console this many seconds after launch, and keep BepInEx's console off from the next launch (no window at all). -1 = keep the console (for debugging). |
| General | `CloseConsoleWhenHosting` | true | Also close the console when you create a lobby. |
| Network | `RegisterHostOnlyFlag` | true | Innersloth's host-only mod flag. Needed for the game end and the impostor chat. |
| Timer | `TimeLimitMinutes` | 18 | Minutes for the time limit (also changed by `/time X`). |
| Timer | `PauseDuringMeetings` | true | Meetings pause the clock (also changed by `/time pause`). |
| Timer | `ShowTimeInChat` | true | Post the time left at every meeting. |
| Timer | `LivingSenderWhenHostDead` | true | If the host is dead, post as a living player so everyone sees it. |
| Jester | `HostCanBeJester` | true | The host can be picked as the Jester too. |
| Jester | `BigJOnBody` | true | Show the big J on the Jester's body. |
| Jester | `BigJSizePercent` | 250 | Size of the big J. |
| Jester | `BigJOffsetEm` | -9 | Height of the big J (more negative = lower). |
| Jester | `SendPlayerInfoId` | true | Technical; leave it on. |
| Jester | `DebugLog` | false | Extra Jester logging. Can reveal the Jester — keep it off when playing. |
| Vision | `VisionMin` | 0 | Lowest vision `/vision` and `/lightvision` allow (0 = pitch black). |
| Vision | `VisionMax` | 0.5 | Highest vision `/vision` and `/lightvision` allow. |
| Lobby | `LobbyKeepAlive` | true | Auto-accept the server's lobby extension. |
| Lobby | `LobbyKeepAliveMaxMinutes` | 0 | Stop extending after this many minutes. 0 = unlimited (as long as the server offers). |
| Look | `ShowLoadingPicture` | true | Show the Town of gHost picture while the game loads. |
| Look | `ShowMenuBanner` | true | Show the Town of gHost banner in the main menu. |
| Look | `ShowCornerInfo` | true | Show the mod's name, version and website in the top-right corner. |

If another mod with its own loading screen, menu logo or corner text is installed too (for example GreaterAmongUs), that mod's are shown instead.

---

## Build from source

1. Install the **.NET SDK** (6 or newer).
2. Install **BepInEx 6 bleeding edge (Unity.IL2CPP, win-x64)** in a copy of Among Us and start the game once, so `BepInEx\interop` exists.
3. In the source folder run:
   ```
   dotnet build -c Release -p:AmongUsDir="C:\path\to\your\Among Us"
   ```
   `TownOfGHost.dll` is copied into `BepInEx\plugins` automatically.
4. Or run **`make-package.bat`** (Windows): it builds the plugin and makes the ready-to-play zip (BepInEx + the plugin) next to it.



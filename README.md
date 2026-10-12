# Town of gHost

**Like Town of Host, but built for ranked settings — with an optional Jester role and the lowest vision ever played!**

Town of gHost is a **host-only** Among Us mod: only the host installs it. Your friends join with the normal, unmodded game and still get every rule.

It brings the ranked-mode settings into private lobbies — a task time limit for the crew, the time left posted in every meeting — and adds a Jester, an impostor secret chat and crewmate vision far below the game's own minimum.

> Not affiliated with Innersloth. Use it in private lobbies with friends. Innersloth's mod policy applies.

---

## Download

Get both files from the **[Releases](../../releases)** page:

| File | What it is |
|---|---|
| **Town_Of_gHost.rar** | **Steam version** — ready to play. Extract and start the game. |
| **Town_Of_gHost.zip** | **Source code** — for building the mod yourself. |

### Install (Steam version)
1. In Steam: right-click **Among Us → Manage → Browse local files**.
2. Extract **Town_Of_gHost.rar** into that folder, next to `Among Us.exe`.
3. Start the game. The **first launch is slower** while BepInEx sets itself up; after that it is normal.
4. Create a lobby and type **`/help`** in the chat.

After an Among Us update the mod must be rebuilt for the new version.

---

## Features

- **Ranked task time limit** — the crew must finish all tasks in time or the Impostor team wins (default 18 minutes).
- **Time left in every meeting** — "Tasks time left M:SS" is posted in the chat when a meeting starts.
- **Pause or no pause** — choose whether meetings stop the clock. If time runs out during a meeting, the loss is applied right **after** that meeting, so the crew can still win the vote.
- **Lowest vision ever played** — crewmate vision down to **0.05x**, far below the game's own minimum.
- **Jester** — a third team of one. One random crewmate is secretly the Jester and **wins by getting voted out**.
  - He sees **"Jester"** above his head and a **big J on his body** (still visible at the lowest vision). Nobody else sees it.
  - His tasks are **fake**: they never count toward the crew's task win.
  - When he wins, **everyone's end screen** shows him as the only winner, and the lobby chat reveals the Jester's and the Impostors' colors.
- **Impostor secret chat** — impostors talk privately in meetings. Only impostors and dead players can read it.
- **Lobby keep-alive** — automatically accepts the server's "extend lobby" offer so the lobby isn't closed for inactivity.
- **Everything is announced** — every rule change is posted in the lobby chat for everyone.

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
| `/vision X` | Crewmate vision, from 0.05 to 0.5. Example: `/vision 0.05` |
| `/vision off` | Back to the vision set in the game's settings. |
| `/jester on` / `/jester off` | Turns the Jester on or off. |
| `/impchat on` / `/impchat off` | Turns the impostor secret chat on or off. |

`/time`, `/vision`, `/jester` and `/impchat` work in the lobby, before the game starts.

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
| General | `CloseConsoleAfterSeconds` | 3 | Close the console this many seconds after launch. -1 = never. |
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
| Vision | `VisionMin` | 0.05 | Lowest vision `/vision` allows. |
| Vision | `VisionMax` | 0.5 | Highest vision `/vision` allows. |
| Lobby | `LobbyKeepAlive` | true | Auto-accept the server's lobby extension. |
| Lobby | `LobbyKeepAliveMaxMinutes` | 0 | Stop extending after this many minutes. 0 = unlimited (as long as the server offers). |

---

## Build from source

1. Install the **.NET SDK** (6 or newer).
2. Install **BepInEx 6 bleeding edge (Unity.IL2CPP, win-x64)** in a copy of Among Us and start the game once, so `BepInEx\interop` exists.
3. In the source folder run:
   ```
   dotnet build -c Release -p:AmongUsDir="C:\path\to\your\Among Us"
   ```
   `TownOfGHost.dll` is copied into `BepInEx\plugins` automatically.

---

## Troubleshooting

- **Something went wrong?** Send `BepInEx\LogOutput.log`. Don't share the large console log: it contains account identifiers.
- **Kicked when hosting?** Look for `[net] disconnect` lines in `LogOutput.log`.

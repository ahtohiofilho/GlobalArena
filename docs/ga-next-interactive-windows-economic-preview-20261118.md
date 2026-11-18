# GA-Next — let the player SEE a working economy before final calibration

## Status and scope

The game's definitive numerical economic calibration is deliberately
**DEFERRED until the player can open and experience an interactive build**.
No activity-demand weights, relative cargo masses, tax rates, terrain
operating costs, opening cash, or infrastructure monetary prices have been
approved as final. Prior v15 QA fixture numbers remain only laboratory
examples. No change to the universal `P(Q)=sqrt(D/Q)` law, immediate
sold-and-consumed production, node-local demand for real workers, shared
civilization treasury, or money-only infrastructure investment.

This improvement is a real hands-on visual acceptance entry point,
NOT an adoption of test coefficients or a full release.

## Launch for Windows — safe worktree

The repository includes
`tools/GlobalArena.Godot/Launchers/GA-Next-PR432-Abrir-Visual.ps1`.
Use **Windows PowerShell 5.1+**, a local
`C:\Users\<user>\source\repos\GlobalArena` clone, Git and .NET 10.
The launcher:

1. Confirms the clone's origin is
   `ahtohiofilho/GlobalArena` and fetches `refs/pull/432/head`
   (Draft PR, **no merge to main**).
2. Checks that PR #431 is an ancestor and verifies exact Git blob SHA-1
   identities of the game Main, the interactive-disclosure banner, the
   explicit QA profile, the authoritative sales settlement and fiscal
   bootstrap, and the source of real transport visualization.
3. Opens a **detached worktree with the commit hash in the directory
   name**, never overwriting the main branch, an existing unrelated
   worktree, or any local user changes.
4. Builds the actual Godot C# client. Finds Godot 4.7.2 Mono or
   downloads the official .NET build with its recorded SHA-256.
5. Clears inherited automated-smoke flags before starting the window.
   In the default `-Mode EconomyLab` mode sets only
   `GA_NEXT_INTERACTIVE_ECONOMY_PREVIEW=1`, which does NOT auto-press
   buttons, auto-close turns, or quit the window.
6. `-Mode Workers` runs the existing standard v11 visual worker demo,
   without using QA financial settings at all.

### One-shot commands

```powershell
Invoke-WebRequest -UseBasicParsing -Uri "https://raw.githubusercontent.com/ahtohiofilho/GlobalArena/feature/ga-next-interactive-economy-visual-preview-20261118/tools/GlobalArena.Godot/Launchers/GA-Next-PR432-Abrir-Visual.ps1" -OutFile "$env:USERPROFILE\Downloads\GA-Next-PR432-Abrir-Visual.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$env:USERPROFILE\Downloads\GA-Next-PR432-Abrir-Visual.ps1"
```

For the old worker-only gameplay:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$env:USERPROFILE\Downloads\GA-Next-PR432-Abrir-Visual.ps1" -Mode Workers
```

## Honest hands-on experiment

The interactive EconomyLab intentionally reuses the exact six-real-worker
QA profile already verified in #430 (3 red/3 blue) and declares initial
civilization balances of **zero**. It labels the top of the actual
interactive window `EXPERIMENTAL / NOT FINAL BALANCE`. A persistent
indicator shows the **authoritative** cash of the human civilization
(displayed to six decimal places, with all internal 1e16-scale precision
unchanged), completed economic cycles and private participated trade
crossings, not invented global traffic. The bottom status also identifies
the experimental mode.

Press **E** manually: the all-ready human+AI turn closes, existing worker
production is fully sold and consumed, exact money is credited to the
actual treasury and the participant-allowed last-cycle freight bands
appear on the fine tactical graph. Press E again to see next-cycle
economic results and lagged congestion evolve. **TAB** opens operations;
**M** switches to planetary map; select an inhabited tile and workers,
then **G** moves, **P** resumes productive work, **A/V/N** change occupation,
and **X** cancels a pending order. The new preview never runs the
automated screenshot smoke or closes itself.

`LAST SETTLED TRADE ROUTES` toggles presentation only, not simulation,
wallet or worker state. Empty local charts simply have no visible freight
lines even if the last-cycle overall participant view contains other
crossings elsewhere. Lines represent **only actual participated
transactions**, not all global routes or someone else's hidden commerce.

## Automated acceptance

- Existing v10/v11 and QA v15 journal/visual smokes continue to run
  unchanged, including two **automated** E keys only in the separate CI
  mode `GA_NEXT_SETTLED_TRADE_VISUAL_SMOKE=1`.
- Added headless **interactive** startup check with
  `GA_NEXT_INTERACTIVE_ECONOMY_PREVIEW=1`, verifies the private
  experimental startup marker and zero opening cash, and **rejects
  any auto-committed E-key sales or automated QA completion marker**.
  The CI's explicit `--quit-after` is ONLY in the CI process; the
  Windows launcher never passes it.
- Windows C# validation parses the PowerShell launcher source without
  executing any fetch, download or process command.
- Other C# economic regressions, StateHash, kernel and freight benchmarks
  remain active.
- The exact launcher branch source blob identities are pinned; if the
  relevant PR sources change, the script refuses to run and will require
  an updated launcher and a new approval cycle.

This is the practical player-feedback checkpoint ahead of final
calibration. The next product step is gather actual UI/gameplay
observations, not commit arbitrary economic coefficient defaults.

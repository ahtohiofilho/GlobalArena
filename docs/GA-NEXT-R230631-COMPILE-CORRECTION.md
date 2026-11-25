# R230.6.3.1 — Compile correction

PR #665 HEAD 73d672a: all five jobs failed at Godot compilation with CS0103 in Main.HudMapSelection.cs: FoundationWorkerOrderKind not found. Import GlobalArena.Runtime.PersistentOrders, matching existing Main.PersistentOrders.cs. Also refresh HUD command presentation when no worker is selected, including the disabled City action, and assert this at neutral startup. No change to queue semantics, economics, map input rules, or CI coverage. Runtime and Windows visual validation remain pending. Draft, no merge.

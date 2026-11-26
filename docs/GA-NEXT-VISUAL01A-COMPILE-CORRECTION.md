# Visual 01.1 — Qualify environment lookup

PR #668 HEAD 0b31981: Persistent Godot UI job 139600341973 failed Godot build with CS0104 (Godot.Environment vs System.Environment) at GaHudMotion.cs:11. Qualify the existing reduced-motion environment variable lookup with System.Environment. No behavior, material, animation, selection, test or economy changes. Cartography PR #667 remains independent and passed both workflows at 14af97d. New CI and Windows visual/performance review pending; draft, no merge.

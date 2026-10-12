# Global Arena - production Godot client

This is the production application shell governed by ADR-054. It is not a diagnostic viewer.
It references `GlobalArena.World` and does not move simulation state into Godot.

QA of this checkpoint confirms a production menu root and build/integration plumbing.
It does NOT certify world rendering, tile picking, visual quality, performance or gameplay.

Launch after QA: open `tools/GlobalArena.Godot/project.godot` in Godot .NET 4.7.2, or run from this directory with Godot.
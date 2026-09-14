# ADR-004 — Authoritative Multiplayer

**Status:** Accepted
**Date:** 2026-09-14

## Context

Global Arena terá multiplayer.

Permitir que clientes determinem resultados aumentaria:

- cheating;
- divergência;
- inconsistência;
- dificuldade de diagnóstico.

## Decision

O multiplayer utilizará servidor autoritativo.

Clientes enviam Commands.

O servidor:

- valida;
- executa;
- consolida;
- distribui o estado autorizado.

As regras do jogo permanecem dentro da Simulation, não dentro do Networking.

## Consequences

Positive:

- autoridade única;
- menor superfície para cheats;
- diagnóstico mais claro;
- compatibilidade com replay e snapshots.

Negative:

- necessidade de infraestrutura de servidor;
- latência;
- custo operacional futuro;
- mecanismos de reconexão e sincronização.

## Constraint

Single-player e multiplayer deverão utilizar as mesmas regras de Simulation.

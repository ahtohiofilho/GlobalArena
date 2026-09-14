# ADR-001 — Headless Deterministic Simulation

**Status:** Accepted
**Date:** 2026-09-14

## Context

Global Arena deverá suportar:

- single-player;
- multiplayer;
- replay;
- testes automatizados;
- benchmarks;
- simulações massivas.

Acoplar as regras à Unity dificultaria essas capacidades.

## Decision

O núcleo da simulação será independente da engine gráfica.

A Simulation deverá poder executar sem:

- Unity;
- GPU;
- UI;
- áudio;
- input humano.

A execução deverá buscar determinismo reproduzível.

Entrada conceitual:

WorldState + Commands + Seed

Saída:

NewWorldState + EventLog

## Consequences

Positive:

- testes headless;
- servidor sem renderização;
- replay;
- debugging determinístico;
- benchmarking;
- independência da engine.

Negative:

- necessidade de separar tipos de domínio dos tipos da Unity;
- maior disciplina arquitetural;
- algumas conversões entre Simulation e Presentation.

## Review

Reavaliar apenas se evidência técnica demonstrar inviabilidade.

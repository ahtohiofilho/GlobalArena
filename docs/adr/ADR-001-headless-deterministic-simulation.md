# ADR-001 — Headless Deterministic Simulation

**Status:** Accepted
**Date:** 2026-09-14
**Last revised:** 2026-09-19

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

O núcleo de domínio e simulação também será cross-platform por design.

O mesmo código autoritativo deverá poder executar em Windows, Linux e macOS sem alterar as regras da simulação.

Dependências específicas de sistema operacional deverão permanecer nas bordas de infraestrutura, integração ou apresentação.

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
- independência da engine;
- portabilidade do núcleo entre sistemas operacionais;
- liberdade para desenvolver em uma plataforma e hospedar o servidor autoritativo em outra;
- regressões específicas de plataforma detectáveis por CI.

Negative:

- necessidade de separar tipos de domínio dos tipos da Unity;
- maior disciplina arquitetural;
- algumas conversões entre Simulation e Presentation.

## Review

M1 foi fechado em 2026-09-19 com regressão cross-platform completa do Simulation Kernel:

- Ubuntu / x64: 126/126;
- Windows / x64: 126/126;
- macOS / arm64: 126/126;
- total: 378 execuções aprovadas, 0 falhas.

Run de referência: `35444833011`.

A decisão permanece aceita.

Reavaliar apenas se evidência técnica demonstrar inviabilidade ou se uma dependência futura exigir comportamento autoritativo específico de plataforma.

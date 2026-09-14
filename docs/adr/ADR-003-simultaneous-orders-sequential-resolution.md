# ADR-003 — Simultaneous Orders with Sequential Resolution

**Status:** Accepted
**Date:** 2026-09-14

## Context

Resolver várias ordens realmente simultâneas cria grande explosão combinatória.

Tentativas anteriores demonstraram alto custo de implementação e dificuldade de prever todas as interações.

## Decision

Os jogadores enviarão ordens durante a mesma janela.

Após o fechamento:

1. comandos são bloqueados;
2. comandos válidos geram eventos;
3. eventos são embaralhados deterministicamente;
4. eventos são executados um por vez;
5. cada evento é revalidado no momento da execução;
6. o estado é consolidado.

## Example

A ataca X
B ataca X
C ataca X

Ordem sorteada:

B → X
C → X
A → X

Se C destruir X, o evento de A será revalidado e poderá ser cancelado.

## Consequences

Positive:

- resolução simples;
- menos combinações especiais;
- forte capacidade de replay;
- incerteza estratégica natural;
- adequado a multiplayer.

Negative:

- ordem de resolução influencia resultados;
- jogadores precisam compreender essa incerteza;
- algumas classes de eventos poderão exigir fases específicas.

## Constraint

O shuffle deve ser reproduzível através de seed.

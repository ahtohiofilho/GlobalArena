# ADR-005 — Modular Monolith First

**Status:** Accepted
**Date:** 2026-09-14

## Context

Global Arena terá muitos subsistemas:

- world;
- worldgen;
- economy;
- warfare;
- civilizations;
- diplomacy;
- AI;
- networking;
- persistence;
- presentation.

Separar tudo imediatamente em processos independentes aumentaria a complexidade operacional antes de existir necessidade real.

Colocar tudo sem fronteiras em um único módulo criaria acoplamento excessivo.

## Decision

O projeto começará como monólito modular.

Os módulos terão:

- ownership de dados;
- contratos claros;
- dependências direcionais;
- testes independentes.

A separação física em processos ou serviços ocorrerá apenas quando houver benefício demonstrado.

## Consequences

Positive:

- baixa complexidade operacional inicial;
- desenvolvimento rápido;
- fronteiras arquiteturais preservadas;
- possibilidade de extração futura.

Negative:

- exige disciplina para impedir acesso indevido entre módulos;
- separação conceitual não é automaticamente garantida pela infraestrutura.

## Rule

Não criar microserviços apenas para representar módulos conceituais.

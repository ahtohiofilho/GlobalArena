# ADR-015 — Scaled Goldberg Refinement Compatibility Contract

**Status:** Accepted
**Date:** 2026-09-20

## Context

M2.3 encerrou o modelo lógico de tactical regions e shared borders.

O início de M2.4 precisa responder uma pergunta diferente:

quando duas topologias Goldberg válidas podem ser tratadas pelo projeto como uma relação coarse→fine suportada?

O audit read-only mostrou que:

- Class I, Class II e Class III já podem ser geradas individualmente;
- nenhuma estrutura de production code representa refinement entre duas resoluções;
- IDs estratégicos são locais à topologia;
- o minimal tactical region continua sendo reference-only;
- shared border incidence é lógica, não física;
- nenhuma regra universal de Goldberg refinement foi provada.

Portanto, M2.4 não pode deduzir refinement apenas de `TriangulationNumber`, contagens ou tamanho relativo.

## Decision

### 1. Refinement compatibility is directional

Uma relação possui:

- coarse parameters;
- fine parameters;
- scale.

Coarse e fine não são intercambiáveis.

### 2. Supported V1 baseline is exact integer scaling

O primeiro subconjunto oficialmente suportado exige um inteiro:

`scale >= 2`

e:

`fine.M == coarse.M * scale`

`fine.N == coarse.N * scale`

A validação usa aritmética inteira e `checked` quando necessário.

### 3. Meaning of the rule

A regra de escala:

- preserva a direção do vetor Goldberg;
- preserva Class I no mesmo eixo;
- preserva Class II;
- preserva orientação/quiralidade de Class III;
- garante `fine.T == coarse.T * scale^2`.

A igualdade do triangulation number multiplicado é uma consequência validada, não o critério primário de compatibilidade.

### 4. Unsupported does not mean impossible

Pares válidos de `GoldbergParameters` que não atendem ao scaling contract serão rejeitados pelo contrato de M2.4.1.

Isso significa apenas:

`not supported by the current refinement contract`

e não:

`mathematically impossible`.

O conjunto suportado poderá ser ampliado por ADR futuro com prova executável.

### 5. Production type

M2.4.1 introduzirá:

`GoldbergScaledRefinement`

como classe imutável.

Public surface:

- `GoldbergParameters CoarseParameters`;
- `GoldbergParameters FineParameters`;
- `int Scale`.

Construction:

`new GoldbergScaledRefinement(coarseParameters, fineParameters)`

Behavior:

- coarse `default`/inválido → `ArgumentException`;
- fine `default`/inválido → `ArgumentException`;
- pair válido mas fora do scaling contract → `NotSupportedException`;
- pair suportado → calcula e congela `Scale`.

### 6. Scale derivation

Se `coarse.M > 0`, o scale candidato é derivado de `fine.M / coarse.M` e deve ser inteiro exato.

Se `coarse.M == 0`, `coarse.N > 0` e o scale candidato é derivado de `fine.N / coarse.N`.

Depois de derivado, ambas as componentes devem satisfazer exatamente:

`fine.M == coarse.M * scale`

`fine.N == coarse.N * scale`.

O scale deve ser pelo menos 2.

### 7. No topology generation inside the relation

`GoldbergScaledRefinement` não:

- chama `GoldbergStrategicTopologyGenerator`;
- armazena `StrategicTopology`;
- compara `StrategicCellId`;
- cria parent-child mapping;
- cria geometry;
- usa floating point.

O objeto é apenas um witness de compatibilidade de parâmetros.

### 8. IDs remain topology-local

Mesmo em um pair suportado:

`StrategicCellId(1)` no coarse

não é automaticamente parent de:

`StrategicCellId(1)` no fine.

A relação de identidade/provenance será projetada em M2.4.2.

### 9. Tactical and border semantics remain unchanged

M2.4.1 não modifica:

- `TacticalCellId`;
- `TacticalCell`;
- `TacticalRegion`;
- `MinimalTacticalRegionGraphGenerator`;
- `SharedBorderElementId`;
- `SharedBorderBand`;
- `SharedBorderIncidence`;
- `StrategicTacticalBorderAggregate`.

`SharedBorderElement` não é reinterpretado como `TacticalCell`.

### 10. Validation matrix

Tests:

`GlobalArena.Tests/GoldbergScaledRefinementTests.cs`

7 Facts:

1. invalid coarse parameters are rejected;
2. invalid fine parameters are rejected;
3. equal coarse/fine pair is rejected;
4. reversed refinement direction is rejected;
5. non-collinear valid pair is rejected;
6. Class I axis swap is rejected;
7. Class III chirality swap is rejected.

One Theory with five executions:

8. `G(1,0) -> G(2,0)` gives scale 2;
9. `G(0,2) -> G(0,6)` gives scale 3;
10. `G(1,1) -> G(2,2)` gives scale 2;
11. `G(2,1) -> G(4,2)` gives scale 2;
12. `G(1,2) -> G(3,6)` gives scale 3.

For every supported pair the test also validates:

`FineParameters.TriangulationNumber == CoarseParameters.TriangulationNumber * Scale * Scale`.

Baseline:

`301`

Expected after implementation:

`313`

### 11. Stage decomposition

M2.4 is decomposed into:

- M2.4.1 — Scaled Refinement Compatibility Contract;
- M2.4.2 — Canonical Construction Provenance & Reference Mapping;
- M2.4.3 — Shared Border Refinement Continuity;
- M2.4.4 — Class I/II/III Scaled Refinement Validation;
- M2.4.5 — Refinement Stage Validation & M2.4 Close.

### 12. Maturity and GPP

M2.4.1 design and compatibility implementation do not promote GPP.

`Strategic ↔ tactical hierarchy/refinement mapping`

remains:

`Inexistente — fator 0.00`

until an executable parent-child mapping exists.

## Implementation evidence

M2.4.1 foi implementado no commit:

`4de075510904c03b261555ad62173599d233537d`

Arquivos:

- `GlobalArena.World/GoldbergScaledRefinement.cs`;
- `GlobalArena.Tests/GoldbergScaledRefinementTests.cs`.

O contrato executável confirma:

- coarse e fine válidos são obrigatórios;
- `Scale` é derivado por razão inteira exata;
- `Scale >= 2`;
- ambas as componentes precisam satisfazer exatamente a mesma escala;
- same-resolution, reverse direction, non-collinear pairs, Class I axis swap e Class III chirality swap são rejeitados;
- supported pairs preservam `fine.T == coarse.T * Scale^2`.

Cobertura de referência:

- `G(1,0) -> G(2,0)` → scale 2;
- `G(0,2) -> G(0,6)` → scale 3;
- `G(1,1) -> G(2,2)` → scale 2;
- `G(2,1) -> G(4,2)` → scale 2;
- `G(1,2) -> G(3,6)` → scale 3.

Validação local:

- Release build: 0 warnings, 0 errors;
- suíte: 313/313;
- failures: 0;
- skipped: 0.

Cross-platform:

- workflow: `Cross-Platform Kernel Regression Validation`;
- run ID: `35499086790`;
- Ubuntu: `success`;
- Windows: `success`;
- macOS: `success`;
- Ubuntu artifact: ID `10601344453`, SHA-256 `9d51be3e4247413de3738410e01b87fa3976377896150a456887385ea1e699ae`;
- Windows artifact: ID `10601529472`, SHA-256 `9fb99db6fa2254ce143b05186864ea83680df73007d8636b4ec9a820d4afd8b3`;
- macOS artifact: ID `10601259733`, SHA-256 `d8777dcf4c6d38c0794c192a16645bc0d2f1c6a29f66cab715de8c4eedf6d227`.

M2.4.1 está concluído.

Nenhuma maturidade/GPP é promovida neste subcheckpoint porque o contrato representa compatibilidade de parâmetros, não parent-child mapping executável.

O próximo subcheckpoint é:

`M2.4.2 — Canonical Construction Provenance & Reference Mapping`

## Consequences

Positive:

- converts an open universal hypothesis into a conservative executable subset;
- preserves Class III chirality instead of silently normalizing it;
- gives later mapping work a precise coarse/fine relation;
- keeps compatibility independent from geometry and rendering;
- allows unsupported pairs to fail explicitly.

Negative:

- valid non-scaled Goldberg refinement relations, if any, remain unsupported;
- no parent-child entity mapping exists after M2.4.1;
- physical tactical boundaries remain unresolved;
- the stage remains on the critical path.

## Invariant

A pair may be described by production code as a supported scaled Goldberg refinement only when one exact integer `scale >= 2` multiplies both coarse Goldberg components to the fine components. No identity lineage or physical mapping is implied by that compatibility alone.

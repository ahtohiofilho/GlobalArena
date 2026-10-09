# Global Arena — Roadmap

**Versão:** 0.1
**Status:** Baseline V1 congelado
**Milestone atual:** M3 — Procedural World

---

# 1. Objetivo

Este documento mantém o Global Arena inteiro em uma única escala de evolução.

Ele deve permitir responder continuamente:

- quanto do V1 já foi concluído;
- quanto ainda falta;
- qual milestone está em execução;
- qual é o caminho crítico;
- quais áreas estão maduras;
- quais áreas ainda possuem grande incerteza;
- se o projeto está avançando ou apenas expandindo escopo.

O roadmap não deve ser redefinido sempre que novas tarefas forem descobertas.

---

# 2. Unidade de progresso

O V1 possui um orçamento fixo inicial de:

**1000 Global Progress Points — GPP**

Esses pontos representam 100% do escopo V1 atualmente aprovado.

1 GPP = 0,1% do V1.

Exemplo:

100 GPP = 10%
500 GPP = 50%
1000 GPP = 100%

Novas tarefas descobertas dentro de um domínio dividem o orçamento existente daquele domínio.

Elas não aumentam automaticamente o total de 1000 GPP.

---

# 3. Baseline por domínio

| Domínio | GPP | Peso |
|---|---:|---:|
| Foundation / Simulation Kernel | 70 | 7% |
| Planet Topology / Goldberg | 90 | 9% |
| World Generation / biomas / recursos | 100 | 10% |
| Economy / comércio / logística | 140 | 14% |
| Warfare / unidades / combate | 140 | 14% |
| Civilizations / Diplomacy | 70 | 7% |
| Artificial Intelligence | 80 | 8% |
| Multiplayer / Networking | 90 | 9% |
| Persistence / Save / Replay | 60 | 6% |
| Rendering / Interaction / UI | 100 | 10% |
| Performance / QA / Release | 60 | 6% |
| **TOTAL** | **1000** | **100%** |

Esse é o baseline inicial do V1.

Alterações nesse total exigem Scope Change Record explícito.

## 3.1 Decomposição inicial — Foundation / Simulation Kernel

O orçamento de 70 GPP de Foundation / Simulation Kernel fica congelado inicialmente com a seguinte decomposição:

| Capability | GPP |
|---|---:|
| Fundação modular, solução e infraestrutura de testes | 10 |
| Tempo lógico, seed e PRNG determinístico | 8 |
| Identidade e contratos de Command/Event | 8 |
| SimulationContext | 5 |
| WorldState mínimo | 5 |
| Contratos de entrada/saída da resolução | 5 |
| Orquestração mínima do TurnResolver | 4 |
| Command → Event, validação e execução sequencial | 8 |
| Deterministic shuffle / ordering | 5 |
| EventLog / base de replay | 5 |
| Turn policies | 3 |
| Determinismo end-to-end / simulação headless automatizada | 4 |
| **TOTAL** | **70** |

Essa decomposição não altera o orçamento global de 1000 GPP.

Detalhamento futuro dentro dessas capabilities redistribui o orçamento existente e não aumenta automaticamente o escopo do V1.

### Baseline de maturidade no fechamento do M0

| Capability | GPP | Maturidade | Fator | GPP ganhos |
|---|---:|---|---:|---:|
| Fundação modular, solução e infraestrutura de testes | 10 | Validada | 0.85 | 8.50 |
| Tempo lógico, seed e PRNG determinístico | 8 | Validada | 0.85 | 6.80 |
| Identidade e contratos de Command/Event | 8 | Validada | 0.85 | 6.80 |
| SimulationContext | 5 | Validada | 0.85 | 4.25 |
| WorldState mínimo | 5 | Validada | 0.85 | 4.25 |
| Contratos de entrada/saída da resolução | 5 | Validada | 0.85 | 4.25 |
| Orquestração mínima do TurnResolver | 4 | Validada | 0.85 | 3.40 |
| Command → Event, validação e execução sequencial | 8 | Especificada | 0.20 | 1.60 |
| Deterministic shuffle / ordering | 5 | Especificada | 0.20 | 1.00 |
| EventLog / base de replay | 5 | Especificada | 0.20 | 1.00 |
| Turn policies | 3 | Especificada | 0.20 | 0.60 |
| Determinismo end-to-end / simulação headless automatizada | 4 | Especificada | 0.20 | 0.80 |
| **TOTAL** | **70** |  |  | **43.25** |

No fechamento do M0:

**Foundation / Simulation Kernel: 43.25 / 70 GPP = 61.8%**

**Global Progress: 43.25 / 1000 GPP = 4.325%**

O dashboard exibe o percentual global arredondado para uma casa decimal:

**Official Progress: 4.3%**

Os demais domínios permanecem em 0 GPP conquistado neste baseline.

Descrições conceituais de alto nível, sem contratos ou capabilities suficientemente definidos, não qualificam por si só uma capability como `Especificada`.

---

# 4. Modelo de maturidade

Cada capability possui um orçamento próprio dentro de seu domínio.

O progresso da capability é calculado por maturidade.

| Maturidade | Fator |
|---|---:|
| Não iniciada | 0.00 |
| Especificada | 0.20 |
| Funcional isoladamente | 0.50 |
| Integrada | 0.70 |
| Validada | 0.85 |
| Definition of Done atendida | 1.00 |

Exemplo:

Uma capability vale 20 GPP.

Se estiver Integrada:

20 × 0.70 = 14 GPP conquistados.

---

# 5. Progresso global

Fórmula:

**Global Progress = GPP conquistados / 1000**

Exemplo:

374 GPP conquistados:

**37,4% do V1**

O percentual global não deve ser alterado manualmente.

Ele é consequência da maturidade das capabilities.

---

# 6. Confidence

Progress e Confidence são métricas diferentes.

## Progress

Quanto do produto foi efetivamente concluído.

## Scope Confidence

Quanto do caminho restante é conhecido com confiança.

Escala:

0% — quase todo o projeto ainda é desconhecido
25% — grandes áreas ainda são conceituais
50% — arquitetura e principais sistemas conhecidos
75% — maioria dos riscos estruturais conhecida
90% — escopo altamente previsível
100% — trabalho restante essencialmente executivo

Um projeto pode estar:

Progress: 60%
Scope Confidence: 40%

Isso significa que muito foi implementado, mas ainda existem grandes incógnitas.

---

# 7. Risk Level

Risco técnico global será classificado como:

LOW
MODERATE
HIGH
CRITICAL

O Risk Register será a fonte dessa classificação.

---

# 8. Milestones

Milestones são gates de maturidade.

Eles não substituem o cálculo por GPP.

---

## M0 — Project Baseline

Objetivo:

estabelecer fundação técnica, arquitetural e gerencial.

Definition of Done:

- PROJECT_COMPASS criado;
- V1_DEFINITION_OF_DONE criado;
- ARCHITECTURE criado;
- ROADMAP criado;
- PROGRESS_LEDGER criado;
- RISK_REGISTER criado;
- ADRs fundamentais registrados;
- estrutura inicial da solução criada;
- infraestrutura de testes criada;
- kernel mínimo compilando;
- primeiro baseline de progresso calculado.

---

## M1 — Deterministic Simulation Kernel

Objetivo:

provar que o coração da simulação é reproduzível.

Inclui:

- IDs estáveis;
- SimulationContext;
- WorldState mínimo;
- Command;
- Event;
- TurnResolver;
- PRNG determinístico;
- seeds;
- deterministic shuffle;
- event log;
- turn policies;
- testes de determinismo;
- simulação automatizada headless.

Gate:

mesmo estado + mesmas ordens + mesma seed = mesmo resultado.

Status:

**concluído em 2026-09-19**

Evidência de fechamento:

- suíte completa `GlobalArena.Tests`: 126/126;
- Ubuntu, Windows e macOS;
- 378 execuções cross-platform;
- 0 falhas;
- resolução determinística com resultados de referência fixos;
- state hash canônico;
- replay e EventLog validados;
- execução headless.

---



## M2 — Planet Topology

**Status: concluído em 2026-09-29**

Objetivo:

estabelecer o planeta lógico.

Inclui:

- topologia Goldberg `G(m,n)`;
- grafo estratégico global;
- StrategicCell;
- StrategicEdge;
- StrategicVertex;
- região/tabuleiro tático por StrategicCell;
- TacticalCell;
- adjacências;
- pertencimento pai-filho;
- conectividade entre tabuleiros vizinhos;
- mapeamento determinístico de fronteiras;
- refinamento hierárquico;
- validação da hipótese de refinamento nas famílias Goldberg relevantes;
- testes topológicos;
- validação de escalabilidade.

Gate:

o planeta forma uma topologia fechada e navegável sem depender da Unity, e a relação estratégico → tático está demonstrada para o conjunto de famílias Goldberg oficialmente suportado.

Stages planejados:

**M2.1 — Goldberg Topology Foundation**

- M2.1.1 — Goldberg Hierarchy Invariants Audit;
- M2.1.2 — Goldberg Parameter & Count Contract;
- M2.1.3 — Strategic Topology Identity Contract;
- M2.1.4 — Minimal G(1,0) Strategic Topology;
- M2.1.5 — General Icosahedral Goldberg Generation.

**M2.2 — Tactical Region Topology**

- M2.2.1 — Tactical Identity & Region Contract;
- M2.2.2 — Minimal Tactical Region Graph;
- M2.2.3 — Strategic-to-Tactical Region Materialization;
- M2.2.4 — Tactical Region Validation & M2.2 Close.

**M2.3 — Shared Border Bands & Strategic/Tactical Mapping**

**M2.4 — Goldberg Family & Refinement Validation**

- M2.4.1 — Scaled Refinement Compatibility Contract;
- M2.4.2 — Canonical Construction Provenance & Reference Mapping;
- M2.4.3 — Shared Border Refinement Continuity;
- M2.4.4 — Class I/II/III Scaled Refinement Validation;
- M2.4.5 — Refinement Stage Validation & M2.4 Close.

**M2.5 — Scalability, Cross-Platform Regression & M2 Exit Gate**

- M2.5.1 — Exit Gate Requirements & Performance Budget Contract;
- M2.5.2 — Strategic/Tactical Physical Boundary Attachment & Cross-Region Traversal;
- M2.5.3 — Hierarchy/Refinement Coverage Decision for Officially Supported Goldberg Families;
- M2.5.4 — Headless Scalability Benchmark Harness & Baseline;
- M2.5.5 — Cross-Platform Regression, Exit Audit & M2 Formal Close.

M2.1.1 está concluído em 2026-09-19.

M2.1.2 está concluído em 2026-09-19.

Evidência de M2.1.2:

- contrato `GoldbergParameters` implementado;
- domínio `m >= 0`, `n >= 0`, exceto `(0,0)`;
- aritmética `checked`;
- vetores de referência `G(1,0)`, `G(1,1)` e `G(2,1)`;
- Euler verificado;
- 139/139 testes por plataforma;
- 417 execuções cross-platform;
- 0 falhas.

M2.1.3 está concluído em 2026-09-19.

Evidência de M2.1.3:

- `StrategicCellId`, `StrategicEdgeId` e `StrategicVertexId` implementados;
- IDs fortemente tipados e locais à topologia;
- ordinal canônico one-based congelado;
- `Value > 0` para identidade válida;
- estado `default` tratado explicitamente como sentinela inválida;
- identidade independente de coordenadas e ordem incidental de criação;
- 154/154 testes por plataforma;
- 462 execuções cross-platform;
- 0 falhas.

GPP adicional de M2.1.3:

**+0.00 GPP**

A capability agregada de strategic graph ainda não atinge `Especificada`, pois incidência e adjacência permanecem abertas.

M2.1.4 está concluído em 2026-09-19.

Evidência de M2.1.4:

- primeira topologia estratégica completa materializada;
- `G(1,0)` com 12 cells, 30 edges e 20 vertices;
- 12 pentágonos com grau 5;
- incidência 2 por edge e 3 por vertex;
- reciprocidade cell-edge, cell-vertex e edge-vertex;
- uma única edge por par adjacente;
- ausência de self-loop e duplicatas;
- conectividade global;
- Euler = 2;
- IDs canônicos one-based;
- geração repetida reproduz a mesma topologia;
- 171/171 testes por plataforma;
- 513 execuções cross-platform;
- 0 falhas.

Promoção de maturidade:

`Strategic graph: identidade, incidência e adjacência`

**Inexistente — 0.00 → Implementação funcional isolada — 0.50**

GPP adicional de M2.1.4:

**+6.00 GPP**

M2.1.5.A está concluído em 2026-09-19.

Evidência de M2.1.5.A:

- gerador estratégico generalizado para Class I `G(m,0)` e `G(0,n)`;
- `G(2,0)` validado com 42 cells, 120 edges, 80 vertices e 30 hexágonos;
- `G(0,2)` validado com as mesmas contagens de Class I de frequência 2;
- `G(3,0)` validado com 92 cells, 270 edges, 180 vertices e 80 hexágonos;
- exatamente 12 pentágonos preservados;
- grau 5 para pentágonos e grau 6 para hexágonos;
- incidência 2 por edge e 3 por vertex preservada;
- reciprocidade, conectividade e Euler = 2 preservados;
- IDs canônicos one-based derivados de ordenação determinística;
- geração repetida de `G(2,0)` reproduz a mesma assinatura;
- Class II `G(1,1)` e Class III `G(2,1)` permanecem explicitamente não suportados nesta tranche;
- build Release local com 0 warnings e 0 errors;
- suíte local: 179/179, 0 falhas, 0 skipped;
- workflow `Cross-Platform Kernel Regression Validation`, run `35455738612`, concluído com sucesso em Ubuntu, Windows e macOS;
- commit validado: `22ba5cf7f6b4604d3356ecb786dbd6a181e15f1f`.

M2.1.5.A fecha somente a tranche Class I.

A capability agregada `Goldberg parameterization e geração estratégica` permanece:

**Especificada — fator 0.20 — 3.20 GPP**

GPP adicional de M2.1.5.A:

**+0.00 GPP**

A promoção para `Implementação funcional isolada — fator 0.50` permanece bloqueada até a generalização necessária de M2.1.5 cobrir as famílias adicionais oficialmente exigidas.

M2.1.5.B está concluído em 2026-09-19.

Evidência de M2.1.5.B:

- suporte Class II `G(k,k)` materializado;
- `G(1,1)` validado com 32 cells, 90 edges, 60 vertices e 20 hexágonos;
- `G(2,2)` validado com 122 cells, 360 edges, 240 vertices e 110 hexágonos;
- exatamente 12 pentágonos preservados;
- grau 5 para pentágonos e grau 6 para hexágonos;
- incidência 2 por edge e 3 por vertex preservada;
- reciprocidade, conectividade e Euler = 2 preservados;
- geração repetida de `G(1,1)` reproduz a mesma assinatura canônica;
- Class I permanece suportada;
- Class III permanece explicitamente não suportada;
- build Release local com 0 warnings e 0 errors;
- suíte local: 185/185, 0 falhas, 0 skipped;
- workflow `Cross-Platform Kernel Regression Validation`, run `35457406818`, concluído com sucesso em Ubuntu, Windows e macOS;
- commit validado: `5897e929d46e964ef544404c2f6b14b2dc3fc436`.

M2.1.5.B fecha somente a tranche Class II.

A capability agregada `Goldberg parameterization e geração estratégica` permanece:

**Especificada — fator 0.20 — 3.20 GPP**

GPP adicional de M2.1.5.B:

**+0.00 GPP**

A promoção para `Implementação funcional isolada — fator 0.50` permanece bloqueada até o fechamento da generalização necessária de M2.1.5.

M2.1.5.C está concluído em 2026-09-19.

Evidência de M2.1.5.C:

- suporte Class III para `m > 0`, `n > 0`, `m != n`;
- `G(2,1)` validado com 72 cells, 210 edges, 140 vertices e 60 hexágonos;
- `G(1,2)` validado com as mesmas contagens e assinatura quiral distinta;
- `G(3,1)` validado com 132 cells, 390 edges, 260 vertices e 120 hexágonos;
- `G(3,2)` validado com 192 cells, 570 edges, 380 vertices e 180 hexágonos;
- exatamente 12 pentágonos preservados;
- grau 5 para pentágonos e grau 6 para hexágonos;
- incidência 2 por edge e 3 por vertex preservada;
- reciprocidade, conectividade e Euler = 2 preservados;
- IDs canônicos contíguos one-based explicitamente testados em `G(2,1)`;
- geração repetida de `G(2,1)` reproduz a mesma assinatura canônica;
- `G(2,1)` e `G(1,2)` possuem assinaturas distintas, preservando a distinção quiral;
- Class I e Class II permanecem suportadas sem regressão;
- build Release local com 0 warnings e 0 errors;
- suíte local: 193/193, 0 falhas, 0 skipped;
- workflow `Cross-Platform Kernel Regression Validation`, run `35458533092`, concluído com sucesso em Ubuntu, Windows e macOS;
- commit validado: `eda066cd45d1c16f3504a3d120b7e2500193d8f9`.

M2.1.5 está concluído com suporte funcional isolado às famílias Class I, Class II e Class III.

Promoção de maturidade:

`Goldberg parameterization e geração estratégica`

**Especificada — 0.20 → Implementação funcional isolada — 0.50**

GPP adicional de M2.1.5.C:

**+4.80 GPP**

GPP após o fechamento:

**84.00 / 1000**

Global Progress:

**8.4%**

Topologia planetária / Goldberg:

**14.00 / 90 GPP — 15.6%**

M2.1 — Goldberg Topology Foundation está concluído em 2026-09-19.

M2.2.1 está concluído em 2026-09-20.

Evidência de M2.2.1:

- `TacticalCellId` implementado como `ParentStrategicCellId + LocalOrdinal`;
- parent estratégico inválido é rejeitado;
- ordinal local zero é rejeitado;
- `default(TacticalCellId)` é sentinela inválida;
- igualdade de identidade preserva pai e ordinal;
- `TacticalCell` rejeita self-loop, duplicatas e adjacência cross-region;
- adjacências locais são ordenadas canonicamente;
- `TacticalRegion` é identificado diretamente pelo `StrategicCellId` pai;
- não existe `TacticalRegionId` redundante;
- região vazia, pais mistos, IDs duplicados, referência pendente, não reciprocidade e desconectividade são rejeitados;
- um grafo local conectado e canonicamente ordenado é aceito;
- shared border bands e adjacência cross-region permanecem explicitamente fora do escopo até M2.3;
- build Release local com 0 warnings e 0 errors;
- suíte local: 211/211, 0 falhas, 0 skipped;
- workflow `Cross-Platform Kernel Regression Validation`, run `35489047998`, concluído com sucesso em Ubuntu, Windows e macOS;
- commit validado: `1c834e4dcc14deaf01422dd7ab102534aae92307`.

Promoção de maturidade:

`Tactical region topology`

**Inexistente — 0.00 → Especificada — 0.20**

GPP adicional de M2.2.1:

**+2.80 GPP**

GPP após o fechamento:

**86.80 / 1000**

Global Progress:

**8.7%**

Topologia planetária / Goldberg:

**16.80 / 90 GPP — 18.7%**

M2.2.2 está concluído em 2026-09-20.

Evidência de M2.2.2:

- `MinimalTacticalRegionGraphGenerator` implementado;
- entrada por `StrategicCellId` válido;
- saída por `TacticalRegion`;
- reference graph canônico com três células;
- ordinais locais `1`, `2`, `3`;
- conectividade `1 <-> 2 <-> 3`;
- parent estratégico preservado em todas as identidades;
- ausência de self-loop e adjacência duplicada;
- reciprocidade preservada;
- grafo conectado;
- geração repetida reproduz a mesma assinatura canônica;
- nenhuma geometria física final foi congelada;
- shared border bands e adjacência cross-region permanecem fora do escopo;
- build Release local com 0 warnings e 0 errors;
- suíte local: 221/221, 0 falhas, 0 skipped;
- workflow `Cross-Platform Kernel Regression Validation`, run `35489984263`, concluído com sucesso em Ubuntu, Windows e macOS;
- commit validado: `cf5e3d5093c21bfc9a67b38e6c16ce35d35d70c2`.

Promoção de maturidade:

`Tactical region topology`

**Especificada — 0.20 → Implementação funcional isolada — 0.50**

GPP adicional de M2.2.2:

**+4.20 GPP**

GPP após o fechamento:

**91.00 / 1000**

Global Progress:

**9.1%**

Topologia planetária / Goldberg:

**21.00 / 90 GPP — 23.3%**

M2.2.3 está concluído em 2026-09-20.

Evidência de M2.2.3:

- `StrategicTacticalRegionMaterializer` implementado;
- entrada por `StrategicTopology`;
- saída por `IReadOnlyList<TacticalRegion>`;
- input nulo rejeitado;
- exatamente uma região materializada por `StrategicCell`;
- ordem canônica de `StrategicTopology.Cells` preservada;
- todos os parents estratégicos aparecem exatamente uma vez;
- cada região preserva o reference graph validado de M2.2.2;
- coleção retornada é somente leitura;
- materialização repetida reproduz a mesma assinatura canônica;
- nenhuma adjacência local cruza parent estratégico;
- casos representativos Class I `G(1,0)`, Class II `G(1,1)` e Class III `G(2,1)` validados;
- nenhum aggregate tático cross-region foi introduzido;
- shared border bands e adjacência cross-region permanecem fora do escopo até M2.3;
- build Release local com 0 warnings e 0 errors;
- suíte local: 232/232, 0 falhas, 0 skipped;
- workflow `Cross-Platform Kernel Regression Validation`, run `35491174076`, concluído com sucesso em Ubuntu, Windows e macOS;
- commit validado: `69472665f629218fcd789bcde21e159844b0e1fd`.

Promoção de maturidade:

`Tactical region topology`

**Implementação funcional isolada — 0.50 → Integrada ao sistema — 0.70**

GPP adicional de M2.2.3:

**+2.80 GPP**

GPP após o fechamento:

**93.80 / 1000**

Global Progress:

**9.4%**

Topologia planetária / Goldberg:

**23.80 / 90 GPP — 26.4%**

M2.2.4 está concluído em 2026-09-20.

Evidência de M2.2.4:

- tranche executada como validation-only;
- nenhum production type foi criado ou alterado;
- `TacticalCell.AdjacentCellIds` validado como snapshot somente leitura;
- `TacticalRegion.Cells` validado como snapshot somente leitura;
- `G(0,2)` validado com 42 regiões;
- `G(2,2)` validado com 122 regiões;
- `G(1,2)` validado com 72 regiões;
- `G(3,1)` validado com 132 regiões;
- `G(3,2)` validado com 192 regiões;
- `G(3,2)` produziu 576 `TacticalCellId` globalmente únicos no reference graph atual;
- toda adjacency materializada em `G(3,2)` permaneceu parent-local;
- duas materializações independentes de `G(3,2)` reproduziram a mesma assinatura canônica completa;
- nenhum threshold de wall-clock foi introduzido;
- build Release local com 0 warnings e 0 errors;
- suíte local: 241/241, 0 falhas, 0 skipped;
- workflow `Cross-Platform Kernel Regression Validation`, run `35492380549`, concluído com sucesso em Ubuntu, Windows e macOS;
- commit validado: `66f10e0bbf5c986ef7dd380df73079f5d5037324`.

Promoção de maturidade:

`Tactical region topology`

**Integrada ao sistema — 0.70 → Validada — 0.85**

GPP adicional de M2.2.4:

**+2.10 GPP**

GPP após o fechamento:

**95.90 / 1000**

Global Progress:

**9.6%**

Topologia planetária / Goldberg:

**25.90 / 90 GPP — 28.8%**

M2.2 — Tactical Region Topology está concluído em 2026-09-20.

O fechamento de M2.2 prova a topologia intra-região, a materialização um-para-um a partir de `StrategicTopology`, canonicalização, ownership local, determinismo e validação cross-platform.

M2.2 não prova ainda:

- shared border bands;
- ownership multi-região;
- adjacência tática cross-region;
- mapping tático por `StrategicEdge`;
- geometria tática final;
- refinamento hierárquico Goldberg.

Stage atual:

**M2.3 — Shared Border Bands & Strategic/Tactical Mapping**

Subcheckpoint atual:

**M2.3.3 — StrategicEdge-to-Border Materialization**

O audit read-only confirmou:

- não existe hoje production type de shared border;
- `StrategicEdge` já representa canonicamente uma fronteira estratégica;
- cada `StrategicEdge` possui exatamente duas `StrategicCell` incidentes e dois `StrategicVertex` incidentes;
- incidence e ordering estratégicos já são determinísticos;
- `TacticalCellId` é deliberadamente region-owned e rejeita adjacency cross-region;
- existe exatamente uma `TacticalRegion` por `StrategicCell`;
- o reference graph tático `1 <-> 2 <-> 3` não representa geometria final;
- nenhum aggregate cross-region existe;
- refinamento Goldberg universal continua não demonstrado.

Design congelado:

- haverá exatamente um `SharedBorderBand` lógico por `StrategicEdge`;
- a identidade do band será o próprio `StrategicEdgeId`;
- não haverá `SharedBorderBandId` redundante;
- elementos compartilhados usarão `SharedBorderElementId = StrategicEdgeId + LocalOrdinal`;
- `LocalOrdinal` será one-based;
- a orientação canônica da sequência seguirá `min(IncidentVertexIds) -> max(IncidentVertexIds)`;
- `SharedBorderBand` exigirá elementos não vazios, IDs pertencentes ao mesmo edge, ordinais únicos e contíguos e exposição somente leitura;
- os dois parents estratégicos serão derivados do `StrategicEdge`, não duplicados como fonte independente dentro do band;
- `TacticalCellId` continuará exclusivo de elementos region-owned;
- M2.3 não representará um elemento compartilhado como dois `TacticalCellId`;
- nenhuma adjacency direta entre `TacticalCell` de regiões diferentes será introduzida no contrato inicial;
- a quantidade e geometria final de elementos por border band continuam abertas;
- o aggregate cross-region será projetado somente depois que band identity e materialization existirem.

Decomposição de M2.3:

- **M2.3.1 — Shared Border Contract Audit & Design**;
- **M2.3.2 — Shared Border Identity & Band Contract**;
- **M2.3.3 — StrategicEdge-to-Border Materialization**;
- **M2.3.4 — Cross-Region Aggregate & Derived Incidence**;
- **M2.3.5 — Shared Border Validation & M2.3 Close**.

M2.3.1 é somente audit/design e não promove GPP.

M2.3.2 está concluído em 2026-09-20.

Evidência de M2.3.2:

- `SharedBorderElementId` implementado como `StrategicEdgeId + LocalOrdinal`;
- edge inválido é rejeitado;
- ordinal local zero é rejeitado;
- `default(SharedBorderElementId)` é sentinela inválida;
- igualdade de identidade preserva edge e ordinal;
- `SharedBorderElement` exige identidade válida;
- `SharedBorderBand` usa diretamente `StrategicEdgeId` como identidade do band;
- não existe `SharedBorderBandId` redundante;
- coleção nula ou vazia é rejeitada;
- elementos nulos são rejeitados;
- todos os elementos devem pertencer ao mesmo `StrategicEdgeId`;
- IDs duplicados são rejeitados;
- ordinais devem formar sequência canônica one-based contígua;
- entrada fora de ordem é canonicalizada por `LocalOrdinal`;
- `Elements` preserva snapshot somente leitura;
- nenhum `TacticalCellId` foi usado no contrato;
- incidência regional e orientação por vértices não foram duplicadas dentro do band;
- nenhuma adjacency tática cross-region foi introduzida;
- quantidade final de elementos e geometria física permanecem abertas;
- build Release local com 0 warnings e 0 errors;
- suíte local: 259/259, 0 falhas, 0 skipped;
- workflow `Cross-Platform Kernel Regression Validation`, run `35494073354`, concluído com sucesso em Ubuntu, Windows e macOS;
- commit validado: `d7d16f0d1dd54d9d71b8163329950d87b2771b92`.

Promoção de maturidade:

`Shared subtile border bands`

**Inexistente — 0.00 → Especificada — 0.20**

GPP adicional de M2.3.2:

**+3.20 GPP**

GPP após o fechamento:

**99.10 / 1000**

Global Progress:

**9.9%**

Topologia planetária / Goldberg:

**29.10 / 90 GPP — 32.3%**

M2.3 continua aberto.

O contrato local de identidade e invariantes do band está congelado e implementado, mas ainda não existe materialização de um band para cada `StrategicEdge`.

Subcheckpoint atual:

**M2.3.3 — StrategicEdge-to-Border Materialization**

O audit read-only confirmou:

- não existe materializador de shared border;
- `StrategicTopology.Edges` é a coleção canônica e somente leitura de edges;
- `StrategicEdgeId` é contiguous canonical one-based;
- cada edge possui exatamente duas células e dois vértices incidentes;
- `SharedBorderBand` já possui identidade por `StrategicEdgeId`;
- `SharedBorderElementId` já usa `StrategicEdgeId + LocalOrdinal`;
- a quantidade final de elementos por border band continua aberta;
- nenhum aggregate cross-region foi introduzido antecipadamente.

Design congelado para M2.3.3:

- criar `StrategicEdgeSharedBorderBandMaterializer`;
- API: `Materialize(StrategicTopology) -> IReadOnlyList<SharedBorderBand>`;
- rejeitar topologia nula;
- usar exclusivamente a `StrategicTopology` fornecida como fonte da verdade;
- não receber `GoldbergParameters` adicionais;
- não regenerar topologia;
- não depender de `TacticalRegion`;
- criar exatamente um `SharedBorderBand` por `StrategicEdge`;
- preservar a ordem canônica de `StrategicTopology.Edges`;
- preservar `band.StrategicEdgeId == edge.Id` posição a posição;
- usar exatamente um `SharedBorderElement` de referência por band nesta tranche;
- usar `SharedBorderElementId(edge.Id, 1)` como identidade de referência;
- tratar o elemento único como artefato lógico não geométrico;
- devolver snapshot somente leitura;
- validar cobertura exata de edges;
- validar unicidade global de IDs de elementos;
- validar determinismo por assinatura canônica repetida;
- validar representantes Class I `G(2,0)`, Class II `G(2,2)` e Class III `G(3,2)`;
- não duplicar `StrategicCellId`, `StrategicVertexId` ou `TacticalCellId`;
- não introduzir aggregate cross-region;
- não introduzir geometria, mesh ou adjacency tática cross-region.

Arquivos de implementação previstos:

- `GlobalArena.World/StrategicEdgeSharedBorderBandMaterializer.cs`;
- `GlobalArena.Tests/StrategicEdgeSharedBorderBandMaterializerTests.cs`.

Matriz prevista:

- 9 Facts;
- 3 casos de Theory;
- 12 casos executados adicionais;
- baseline: 259 testes;
- esperado após implementação: 271 testes.

M2.3.3 está concluído em 2026-09-20.

Evidência de M2.3.3:

- `StrategicEdgeSharedBorderBandMaterializer` implementado;
- entrada autoritativa: `StrategicTopology`;
- topologia nula é rejeitada;
- exatamente um `SharedBorderBand` é materializado por `StrategicEdge`;
- a ordem de `StrategicTopology.Edges` é preservada;
- `band.StrategicEdgeId == edge.Id` posição a posição;
- cada edge aparece exatamente uma vez;
- cada band usa exatamente um `SharedBorderElement` de referência;
- reference ID = `SharedBorderElementId(edge.Id, 1)`;
- o elemento de referência continua explicitamente não geométrico;
- IDs dos reference elements são globalmente únicos;
- coleção retornada é snapshot somente leitura;
- materializações repetidas produzem assinatura canônica idêntica;
- Class I `G(2,0)` validada com 120 bands;
- Class II `G(2,2)` validada com 360 bands;
- Class III `G(3,2)` validada com 570 bands;
- nenhum `GoldbergParameters` separado é recebido;
- nenhuma topologia é regenerada;
- não existe dependência de `TacticalRegion`;
- nenhum `StrategicCellId`, `StrategicVertexId`, `TacticalCellId` ou `SharedBorderBandId` foi duplicado no materializador;
- nenhum aggregate cross-region foi introduzido;
- nenhuma geometria, mesh ou adjacency tática cross-region foi introduzida;
- build Release local com 0 warnings e 0 errors;
- suíte local: 271/271, 0 falhas, 0 skipped;
- workflow `Cross-Platform Kernel Regression Validation`, run `35495093016`, concluído com sucesso em Ubuntu, Windows e macOS;
- commit validado: `3007738fc66d7fa9b8dcff415207f0e6fe9aad93`.

Promoção de maturidade:

`Shared subtile border bands`

**Especificada — 0.20 → Implementação funcional isolada — 0.50**

GPP adicional de M2.3.3:

**+4.80 GPP**

GPP após o fechamento:

**103.90 / 1000**

Global Progress:

**10.4%**

Topologia planetária / Goldberg:

**33.90 / 90 GPP — 37.7%**

M2.3 continua aberto.

A identidade e a materialização 1:1 de border bands por `StrategicEdge` agora são funcionais e determinísticas. A integração cross-region conjunta com `TacticalRegion` ainda não existe.

Subcheckpoint atual:

**M2.3.4 — Cross-Region Aggregate & Derived Incidence**

O audit read-only confirmou:

- não existe production aggregate cross-region;
- `StrategicTopology` permanece o aggregate estratégico autoritativo;
- existe exatamente uma `TacticalRegion` por `StrategicCell` quando usada a materialização validada;
- existe exatamente um `SharedBorderBand` por `StrategicEdge` quando usada a materialização validada;
- `StrategicEdge.IncidentCellIds` já fornece exatamente duas regiões estratégicas em ordem canônica;
- `TacticalCell` continua rejeitando adjacency cross-region direta;
- não existe production code de geometry/mesh/bridge tático cross-region;
- o único match do guard de adjacency foi o teste que confirma a rejeição desse comportamento;
- nenhuma derived incidence view existe ainda.

Design congelado para M2.3.4:

- criar `StrategicTacticalBorderAggregate`;
- constructor input: `StrategicTopology`, `IEnumerable<TacticalRegion>`, `IEnumerable<SharedBorderBand>`;
- não materializar regiões ou bands internamente;
- manter `StrategicTopology` como fonte estratégica autoritativa;
- validar exatamente uma região por `StrategicCell`;
- rejeitar região nula, parent duplicado, parent ausente ou parent estrangeiro;
- canonicalizar `TacticalRegions` pela ordem de `StrategicTopology.Cells`;
- validar exatamente um band por `StrategicEdge`;
- rejeitar band nulo, edge duplicado, edge ausente ou edge estrangeiro;
- canonicalizar `SharedBorderBands` pela ordem de `StrategicTopology.Edges`;
- criar `SharedBorderIncidence`;
- identity da incidence = `StrategicEdge.Id`, sem novo ID;
- cada incidence referencia o `StrategicEdge` autoritativo;
- cada incidence referencia o `SharedBorderBand` correspondente;
- cada incidence resolve exatamente duas `TacticalRegion` por `StrategicEdge.IncidentCellIds`;
- ordem das regiões incidentes = ordem canônica de `IncidentCellIds`;
- não copiar incident `StrategicCellId` como segunda fonte de verdade;
- criar exatamente uma incidence por `StrategicEdge`;
- canonicalizar incidences pela ordem de `StrategicTopology.Edges`;
- expor topology, regions, bands e incidences como snapshots somente leitura;
- não criar lookup APIs adicionais nesta tranche;
- validar determinismo de assinatura em construções repetidas;
- preservar referências aos objetos region/band fornecidos, sem clonar domain entities;
- não introduzir mapping físico de `TacticalCell` para border elements;
- não introduzir adjacency tática cross-region;
- não introduzir geometria, coordinates ou mesh.

Production files previstos:

- `GlobalArena.World/StrategicTacticalBorderAggregate.cs`;
- `GlobalArena.World/SharedBorderIncidence.cs`.

Tests previstos:

- `GlobalArena.Tests/StrategicTacticalBorderAggregateTests.cs`.

Matriz prevista:

- 17 Facts;
- 3 casos de uma Theory representativa;
- 20 casos executados adicionais;
- baseline: 271 testes;
- esperado após implementação: 291 testes.

Representantes:

- Class I `G(2,0)` → 42 regiões / 120 bands / 120 incidences;
- Class II `G(2,2)` → 122 regiões / 360 bands / 360 incidences;
- Class III `G(3,2)` → 192 regiões / 570 bands / 570 incidences.

M2.3.4 está concluído em 2026-09-20.

Evidência de M2.3.4:

- `StrategicTacticalBorderAggregate` implementado;
- `SharedBorderIncidence` implementado;
- `StrategicTopology` permanece a fonte autoritativa;
- input nulo de topology/regions/bands é rejeitado;
- elementos nulos são rejeitados;
- cobertura de regions é exatamente uma por `StrategicCell`;
- missing, duplicate e foreign region parents são rejeitados;
- cobertura de bands é exatamente um por `StrategicEdge`;
- missing, duplicate e foreign band edges são rejeitados;
- inputs fora de ordem são canonicalizados por `StrategicTopology.Cells` e `StrategicTopology.Edges`;
- existe exatamente uma derived incidence por strategic edge;
- cada incidence referencia o `StrategicEdge` autoritativo;
- cada incidence referencia o band correspondente;
- cada incidence resolve exatamente duas `TacticalRegion` na ordem de `IncidentCellIds`;
- nenhuma cópia independente de incident `StrategicCellId` foi introduzida;
- referências aos domain objects fornecidos são preservadas;
- coleções públicas e incident regions são snapshots somente leitura;
- construções repetidas sobre inputs semanticamente equivalentes produzem assinatura canônica idêntica;
- Class I `G(2,0)` validada com 42 regions / 120 bands / 120 incidences;
- Class II `G(2,2)` validada com 122 regions / 360 bands / 360 incidences;
- Class III `G(3,2)` validada com 192 regions / 570 bands / 570 incidences;
- nenhuma rematerialização interna foi introduzida;
- nenhum `SharedBorderIncidenceId` foi criado;
- nenhum mapping físico, geometry, mesh ou adjacency tática cross-region foi introduzido;
- build Release local com 0 warnings e 0 errors;
- suíte local: 291/291, 0 falhas, 0 skipped;
- workflow `Cross-Platform Kernel Regression Validation`, run `35496490603`, concluído com sucesso em Ubuntu, Windows e macOS;
- commit validado: `75b6929004a2149195a46772a6e7aae3ad509211`.

Promoção de maturidade:

`Shared subtile border bands`

**Implementação funcional isolada — 0.50 → Integrada ao sistema — 0.70**

GPP adicional de M2.3.4:

**+3.20 GPP**

GPP após o fechamento:

**107.10 / 1000**

Global Progress:

**10.7%**

Topologia planetária / Goldberg:

**37.10 / 90 GPP — 41.2%**

M2.3 continua aberto.

A identidade, materialização por edge e incidence cross-region derivada estão agora integradas. Ainda faltam validação acumulada do stage e fechamento de M2.3.

Subcheckpoint atual:

**M2.3.5 — Shared Border Validation & M2.3 Close**

O audit read-only confirmou:

- M2.3.4 está formalmente fechado;
- baseline local permanece em 291/291 testes;
- não existe arquivo de validação acumulada específico para M2.3;
- os contratos públicos existentes são suficientes para executar o gate sem alterar production code;
- `StrategicCell.IncidentEdgeIds` já permite validar continuidade lógica cell↔edge↔incidence;
- `StrategicEdge.IncidentCellIds` já mantém exatamente dois parents canônicos;
- o aggregate já expõe regions, bands e incidences como snapshots somente leitura;
- malformed input, canonicalização e snapshot mutation já possuem testes diretos em M2.3.4;
- não existe production mapping físico de `TacticalCell` para border element;
- não existe adjacency tática cross-region;
- o único match do guard físico continua sendo o teste que confirma rejeição de adjacency cross-region.

Design congelado para M2.3.5:

- validation-only;
- nenhum production file alterado;
- criar somente `GlobalArena.Tests/SharedBorderStageValidationTests.cs`;
- validar continuidade lógica de cada `StrategicCell` contra `IncidentEdgeIds`;
- validar grau derivado 5 para pentágonos e 6 para hexágonos;
- validar handshake global: soma de graus regionais = `2 * Edges.Count`;
- validar que cada incidence é observada por exatamente suas duas regions;
- validar unicidade global de `SharedBorderElementId` no aggregate;
- validar edge-locality de todos os border elements;
- validar presença do reference element ordinal `1` em cada band sem congelar quantidade física final;
- validar adjacency tática estritamente parent-local em todas as regions do aggregate;
- validar determinismo de duas pipelines independentes desde `GoldbergParameters`;
- adicionar cobertura `G(0,2)`, `G(1,2)`, `G(2,1)` e `G(3,1)`;
- não duplicar malformed-input tests já existentes;
- não duplicar snapshot-mutation tests já existentes;
- não introduzir mapping físico, geometry, mesh, pathfinding ou adjacency tática cross-region;
- não alegar refinamento Goldberg universal.

Matriz prevista:

- 6 Facts;
- 4 casos de uma Theory;
- 10 casos executados adicionais;
- baseline: 291;
- esperado após validation-only implementation: 301.

M2.3.5 está concluído em 2026-09-20.

Evidência de M2.3.5:

- validation-only;
- nenhum production file alterado;
- arquivo adicionado: `GlobalArena.Tests/SharedBorderStageValidationTests.cs`;
- exact cell-to-incidence edge sets validados contra `StrategicCell.IncidentEdgeIds`;
- regions de parents pentagonais validadas com grau 5;
- regions de parents hexagonais validadas com grau 6;
- handshake global validado: soma dos graus regionais = `2 * Edges.Count`;
- cada incidence observada por exatamente duas regions;
- `SharedBorderElementId` globalmente único no aggregate;
- border elements permanecem edge-local;
- reference element ordinal `1` validado sem congelar cardinalidade física final;
- adjacency tática permanece estritamente parent-local;
- duas pipelines independentes desde `GoldbergParameters` produzem assinatura canônica idêntica;
- `G(0,2)` validado com 42 regions / 120 bands / 120 incidences;
- `G(1,2)` validado com 72 regions / 210 bands / 210 incidences;
- `G(2,1)` validado com 72 regions / 210 bands / 210 incidences;
- `G(3,1)` validado com 132 regions / 390 bands / 390 incidences;
- build Release local com 0 warnings e 0 errors;
- suíte local: 301/301, 0 falhas, 0 skipped;
- workflow `Cross-Platform Kernel Regression Validation`, run `35497801912`, concluído com sucesso em Ubuntu, Windows e macOS;
- commit validado: `799ea942e2b81f99bbc0d8f560a73041e86a0e14`.

Promoção de maturidade:

`Shared subtile border bands`

**Integrada ao sistema — 0.70 → Validada — 0.85**

GPP adicional de M2.3.5:

**+2.40 GPP**

GPP após o fechamento:

**109.50 / 1000**

Global Progress:

**11.0%**

Topologia planetária / Goldberg:

**39.50 / 90 GPP — 43.9%**

M2.3 está concluído em 2026-09-20.

O stage fecha a identidade, materialização, aggregate cross-region e validação acumulada de shared borders no nível lógico. Não fecha mapping físico, geometry, traversal tático cross-region ou refinamento Goldberg universal.

`Strategic ↔ tactical hierarchy/refinement mapping` permanece:

**Inexistente — 0.00**

Stage atual:

**M2.4 — Goldberg Family & Refinement Validation**

Próximo gate:

**READ_ONLY_AUDIT**

---

## M2.4 — Goldberg Family & Refinement Validation

M2.4 iniciou em 2026-09-20 com audit read-only sobre o baseline formalmente fechado de M2.3.

O audit confirmou:

- baseline local em 301/301 testes;
- geração estratégica existente para Class I, Class II e Class III;
- `TacticalRegion` ainda usa o minimal reference graph de M2.2;
- shared border lógico e incidence cross-region estão validados;
- nenhum production type de refinement/mapping coarse→fine existe;
- nenhuma physical tactical-border mapping existe;
- final border cardinality permanece aberta;
- universal Goldberg refinement permanece não provado.

M2.4.1 está concluído em 2026-09-20.

Evidência de M2.4.1:

- production type `GlobalArena.World/GoldbergScaledRefinement.cs`;
- tests `GlobalArena.Tests/GoldbergScaledRefinementTests.cs`;
- compatibilidade suportada somente para `fine=(coarse.M*scale, coarse.N*scale)`, com `scale >= 2`;
- parâmetros `default` rejeitados;
- mesma resolução rejeitada;
- direção coarse→fine invertida rejeitada;
- pares não colineares rejeitados;
- troca de eixo Class I rejeitada;
- troca de quiralidade Class III rejeitada;
- Class I `G(1,0) -> G(2,0)` validada com scale 2;
- Class I invertida `G(0,2) -> G(0,6)` validada com scale 3;
- Class II `G(1,1) -> G(2,2)` validada com scale 2;
- Class III `G(2,1) -> G(4,2)` validada com scale 2;
- Class III `G(1,2) -> G(3,6)` validada com scale 3;
- relação `fine.T == coarse.T * scale^2` validada como consequência;
- nenhum `StrategicTopology` é materializado pelo contrato;
- nenhum parent-child mapping é criado;
- nenhum ID local é reutilizado como lineage;
- `TacticalRegion`, `SharedBorderBand` e aggregate permanecem inalterados;
- build Release local com 0 warnings e 0 errors;
- suíte local: 313/313, 0 falhas, 0 skipped;
- workflow `Cross-Platform Kernel Regression Validation`, run `35499086790`, concluído com sucesso em Ubuntu, Windows e macOS;
- commit validado: `4de075510904c03b261555ad62173599d233537d`.

M2.4.1 não promove GPP.

`Strategic ↔ tactical hierarchy/refinement mapping` permanece:

**Inexistente — 0.00**

GPP permanece:

**109.50 / 1000**

Global Progress permanece:

**11.0%**

Topologia planetária / Goldberg permanece:

**39.50 / 90 GPP — 43.9%**

M2.4.2 está concluído em 2026-09-20.

Evidência de M2.4.2:

- `IcosahedronSeedVertexId` implementado com domínio `1..12`;
- `GoldbergScaledCellReference` implementado;
- `GoldbergScaledRefinementReferenceMap` implementado;
- `GoldbergScaledRefinementReferenceMapper` implementado;
- `GoldbergStrategicTopologyGenerator` passou a expor provenance interna reutilizável no caminho triangular sem alterar seu public contract;
- reference pair suportado nesta tranche: `G(1,0) -> G(2,0)`, scale 2;
- exatamente 12 references materializadas;
- todas as 12 coarse cells cobertas uma vez;
- 12 fine reference IDs únicos;
- as 12 fine cells referenciadas são os 12 pentágonos;
- canonical vector validado: `1:1->6, 2:2->11, 3:3->15, 4:4->19, 5:5->23, 6:6->26, 7:7->30, 8:8->33, 9:9->36, 10:10->39, 11:11->41, 12:12->42`;
- materialização repetida canônica;
- coleção read-only;
- Class I scale 3 permanece unsupported nesta tranche;
- Class II permanece unsupported nesta tranche;
- Class III permanece unsupported nesta tranche;
- nenhum lineage é inferido por igualdade ou aritmética de `StrategicCellId`;
- build Release local com 0 warnings e 0 errors;
- suíte local: 327/327, 0 falhas;
- workflow `Cross-Platform Kernel Regression Validation`, run `35500232516`, concluído com sucesso em Ubuntu, Windows e macOS;
- commit validado: `d76128d4d0528979c3c1a5c1d5d59a745a10c868`.

M2.4.2 não promove GPP.

`Strategic ↔ tactical hierarchy/refinement mapping` permanece:

**Inexistente — 0.00**

GPP permanece:

**109.50 / 1000**

Global Progress permanece:

**11.0%**

Topologia planetária / Goldberg permanece:

**39.50 / 90 GPP — 43.9%**

M2.4.3 está concluído em 2026-09-20.

Evidência de M2.4.3:

- `GoldbergScaledSharedBorderReference` implementado;
- `GoldbergScaledSharedBorderContinuityMap` implementado;
- `GoldbergScaledSharedBorderContinuityMapper` implementado;
- input autoritativo preservado como `GoldbergScaledRefinementReferenceMap`;
- reference pair: `G(1,0) -> G(2,0)`, scale 2;
- 30/30 coarse edges cobertos;
- exatamente duas fine edges por chain;
- 30 middle fine cells únicos;
- middle fine cells = todos os 30 hexágonos de `G(2,0)`;
- 60 fine edge IDs globalmente únicos;
- fine edge coverage: 60/120;
- coarse band coverage: 30/30;
- fine band coverage: 60/120;
- current single-element band semantics preservada;
- canonical continuity vector validado;
- repeated materialization determinística;
- collections read-only;
- nenhum lineage derivado por equality/arithmetic de edge IDs;
- nenhum hard-coded oracle usado como production source;
- nenhuma physical geometry introduzida;
- build Release local com 0 warnings e 0 errors;
- suíte local: 340/340, 0 falhas;
- workflow `Cross-Platform Kernel Regression Validation`, run `35501841291`, concluído com sucesso em Ubuntu, Windows e macOS;
- commit validado: `ed71d86e58967cd2e9b1484200d07c57821d196f`.

M2.4.3 não promove GPP.

`Strategic ↔ tactical hierarchy/refinement mapping` permanece:

**Inexistente — 0.00**

GPP permanece:

**109.50 / 1000**

Global Progress permanece:

**11.0%**

Topologia planetária / Goldberg permanece:

**39.50 / 90 GPP — 43.9%**

M2.4.4 está concluído em 2026-09-20.

Evidência de M2.4.4:

- implementation kind: validation-only;
- production files alterados: 0;
- test file: `GoldbergScaledRefinementFamilyValidationTests.cs`;
- 14 Facts;
- 11 representative pairs;
- Class I normal e inverted axis;
- Class II;
- Class III right e left chirality;
- scales 2 e 3;
- public scaled invariants validados;
- Euler validado;
- 12 pentágonos preservados;
- deterministic canonical topology signatures validadas;
- current reference mapper scope preservado em `G(1,0) -> G(2,0)`;
- current continuity mapper scope preservado no mesmo reference pair;
- nenhum private generator detail promovido a public lineage;
- build Release local com 0 warnings e 0 errors;
- suíte local: 354/354, 0 falhas;
- workflow `Cross-Platform Kernel Regression Validation`, run `35503057993`, concluído com sucesso em Ubuntu, Windows e macOS;
- commit validado: `b9ef26225e6413e3ae5ec07e6fee3b59e6424ca6`.

M2.4.4 não promove GPP.

GPP permanece:

**109.50 / 1000**

Global Progress permanece:

**11.0%**

Topologia planetária / Goldberg permanece:

**39.50 / 90 GPP — 43.9%**

`Strategic ↔ tactical hierarchy/refinement mapping` permanece:

**Inexistente — 0.00**

Subcheckpoint atual:

**M2.4.5 — Refinement Stage Validation & M2.4 Close**

O audit read-only acumulado de M2.4.5 confirmou:

- ancestry válida dos formal closes M2.4.1–M2.4.4;
- build Release: 0 warnings, 0 errors;
- suíte: 354/354;
- 11 representative scaled pairs em Class I/II/III;
- scales 2 e 3;
- scaled count relations, Euler, 12 pentagons e determinism aprovados;
- reference map do first pair com 12 canonical seed references;
- continuity map do first pair com 30 coarse-edge references;
- 30 unique middle fine cells = todos os 30 fine hexagons;
- 60 unique mapped fine edges;
- fine-edge coverage: 60/120;
- mapped bands existentes;
- current single-element band semantics preservada;
- 10 non-reference representative pairs rejeitados pelo current reference mapper;
- physical tactical mapping continua ausente.

Design congelado para M2.4.5:

- validation-only;
- nenhuma mudança em production;
- novo test file: `GoldbergRefinementStageValidationTests.cs`;
- 12 Facts cumulativos;
- somente public contracts;
- sem reflection/private generator details;
- expected suite: 366;
- M2.4 pode fechar após implementação + cross-platform PASS;
- stage close não implica multi-family lineage;
- stage close não implica physical tactical-border mapping;
- stage close não implica universal Goldberg refinement.

M2.4.5 não promove GPP.

GPP permanece:

**109.50 / 1000**

Global Progress permanece:

**11.0%**

Topologia planetária / Goldberg permanece:

**39.50 / 90 GPP — 43.9%**

`Strategic ↔ tactical hierarchy/refinement mapping` permanece:

**Inexistente — 0.00**

`RISK-003` permanece:

**HIGH**

M2.4.5 está concluído em 2026-09-20.

Evidência final:

- implementation kind: validation-only;
- production files alterados: 0;
- test file: `GoldbergRefinementStageValidationTests.cs`;
- 12 Facts cumulativos;
- suíte local: 366/366;
- build Release: 0 warnings, 0 errors;
- 11 representative scaled pairs;
- deterministic signatures;
- Euler e 12 pentagons;
- 12 canonical seed references no first reference pair;
- 30 continuity references;
- 30 middle fine cells = 30 fine hexagons;
- 60 globally unique mapped fine edges;
- fine-edge coverage: 60/120;
- ordered two-edge chains válidos;
- mapped shared-border bands presentes;
- single-element band semantics preservada;
- exposed collections read-only;
- workflow `Cross-Platform Kernel Regression Validation`, run `35504415048`, concluído com sucesso em Ubuntu, Windows e macOS;
- commit validado: `01583a55f24119e398296c2c620cd76ab64277ca`.

M2.4 — Goldberg Family & Refinement Validation está concluído em 2026-09-20.

O stage fecha sem promover GPP e sem alegar multi-family lineage, physical tactical-border mapping ou universal Goldberg refinement.

GPP permanece:

**109.50 / 1000**

Global Progress permanece:

**11.0%**

Topologia planetária / Goldberg permanece:

**39.50 / 90 GPP — 43.9%**

`Strategic ↔ tactical hierarchy/refinement mapping` permanece:

**Inexistente — 0.00**

`RISK-003` permanece:

**HIGH**

Stage atual:

**M2.5 — Scalability, Cross-Platform Regression & M2 Exit Gate**

O entry audit read-only de M2.5 foi aprovado no baseline:

`e9e50c0ff957b286186e16cc7e9de86a01100256`.

Resultado:

**PASS_READY_FOR_M2_5_1_DESIGN**

O audit confirmou:

- 366/366 testes;
- Release build com 0 warnings e 0 errors;
- 38/38 snapshot hashes válidos;
- benchmark project ainda placeholder;
- current regression workflow sem benchmark;
- tactical reference graph ainda com três células;
- physical tactical-border mapping ausente;
- dedicated cross-region navigation contract ausente;
- multi-family durable lineage ausente;
- M2 exit ready: no.

Probe observacional sem threshold:

- Class I `G(16,0)`: median 84.784 ms, max 116.604 ms, median allocation 63,876,136 bytes;
- Class II `G(10,10)`: median 65.171 ms, max 70.425 ms, median allocation 75,541,888 bytes;
- Class III `G(12,7)`: median 58.798 ms, max 82.909 ms, median allocation 62,493,848 bytes.

M2.5.1 congela o seguinte first M2 topology performance budget para o workload final aceito após M2.5.2/M2.5.3:

- 1 warmup + 5 measured samples por canonical load case;
- median elapsed `<= 1000 ms`;
- max elapsed sample `<= 2000 ms`;
- median managed allocation `<= 192 MiB`;
- max managed allocation sample `<= 256 MiB`;
- Class I `G(16,0)`;
- Class II `G(10,10)`;
- Class III `G(12,7)`.

O budget é M2-specific e não define o planet size final do V1.

Nenhuma promoção de GPP ocorre no design.

Subcheckpoint atual:

**M2.5.1 — Exit Gate Requirements & Performance Budget Contract**

Próximo gate:

**M2.5.1 DESIGN AUDIT**

---




### M2.5.2 — Vertex-aware physical tactical mesh design

O audit read-only M2.5.2-A foi concluído em 2026-09-20 no baseline:

`714cc1f06bbb1dc411fdb1bfcda2c3a19f8a9acd`

Resultado:

**PASS_READY_FOR_M2_5_2_VERTEX_AWARE_DESIGN**

Evidência:

- Release build: 0 warnings, 0 errors;
- suíte: 366/366;
- tracked hash drift: 0;
- worktree clean;
- `SharedBorderElementId` confirmado como edge-scoped logical/non-geometric contract;
- `StrategicVertex` confirmado com incidência estratégica 3-way;
- global physical tactical identity ausente;
- physical boundary attachment ausente;
- cross-region tactical traversal ausente.

Design proposto em ADR-021:

- a malha tática física é uma topologia Goldberg fina contínua;
- cada fine topology cell é um único physical tactical tile;
- identidade proposta: `PhysicalTacticalTileId = FineGoldbergParameters + FineStrategicCellId`;
- coarse incidence explícita classifica tiles em 1-cell, 2-cell ou 3-cell incidence;
- 2-cell incidence deriva uma `StrategicEdge`;
- 3-cell incidence deriva um `StrategicVertex`;
- edge/vertex tiles nunca são duplicados por strategic region;
- `TacticalCellId` permanece reference/region-owned;
- `SharedBorderElementId` permanece logical/edge-scoped;
- traversal físico usa a adjacency da fine Goldberg topology;
- audit de provenance M2.5.2-C: `G(1,0) -> G(3,0)`, scale 3, é vertex-aware apenas (`72 interior + 0 edge + 20 vertex = 92`);
- primeiro target de materialização com edge + vertex simultaneamente: `G(1,0) -> G(6,0)`, scale 6;
- decomposição auditada do novo target: `312 interior + 30 edge-shared + 20 vertex-shared = 362`;
- a materialização production `G(1,0) -> G(6,0)` foi concluída no M2.5.2-C com cobertura `362/362` e assinatura `312/30/20`.

Decomposição de M2.5.2:

- M2.5.2-A — Vertex-Aware Physical Boundary Read-Only Audit — concluído;
- M2.5.2-B — Global Physical Tactical Identity & Coarse Incidence Contract — concluído;
- M2.5.2-C — Vertex-Aware Physical Incidence Materialization — concluído;
- M2.5.2-D — Fine-Topology Cross-Region Traversal Contract & Validation — concluído;
- M2.5.2-E — Accumulated Validation & M2.5.2 Close — concluído;`n- M2.5.2 — Strategic/Tactical Physical Boundary Attachment & Cross-Region Traversal — concluído.

Nenhuma promoção de GPP ocorre no design.

Próximo gate após design audit:

**M2.5.2-B IMPLEMENTATION — CONCLUÍDO**

#### M2.5.2-B implementation close

Implementados e auditados:

- `PhysicalTacticalTileId`;
- `PhysicalTacticalTileIncidence`;
- canonicalização read-only da incidência coarse;
- validação de incidência 1/2/3;
- validação estrutural edge-aware e vertex-aware;
- 23 novos testes;
- suíte acumulada: 389/389.

Evidência:

`GlobalArena-Evidence-M2.5.2-B-IMPLEMENTATION-R2-20260928-083834.zip`

O materializador coarse→fine permanece deliberadamente ausente nesta tranche.

Próximo gate histórico:

**M2.5.2-C — Vertex-Aware Physical Incidence Materialization**

### M2.5.4 — Headless Scalability Benchmark Harness & Baseline

Decomposição:

- M2.5.4-A — Final Workload Performance Spike & Bottleneck Diagnosis — concluído;
- M2.5.4-B — Product/Stress Scale Envelope Audit — concluído;
- M2.5.4-C — Benchmark Contract & Scale Envelope Design Freeze — concluído;
- M2.5.4-D — Permanent Headless Benchmark Harness Implementation — concluído;
- M2.5.4-E — Baseline Validation & M2.5.4 Close — concluído.

M2.5.4-A evidence established:

- the historical full physical `G(16,0) -> G(96,0)` workload completes but exceeds all four original blocking budgets;
- R4 decomposition isolates fine `G(96,0)` topology generation as the dominant cost;
- fine topology accounts for approximately `87.74%` of physical-pipeline managed allocation and `75.34%` of elapsed time;
- the performance failure is not a topology-correctness failure.

M2.5.4-B evidence established:

- provisional strategic product hypothesis: `m+n <= 15`;
- provisional tactical product hypothesis: up to roughly 12 rings, normally less;
- final V1 scale remains unfrozen pending visual/gameplay validation;
- `G(15,0)` has `2252` strategic cells;
- scale-6 physical `G(15,0) -> G(90,0)` has `81002` fine cells;
- area-equivalent scale near 12 hex rings is about `21.656`;
- `G(15,0)` at scale 21 would approach `992252` fine cells;
- allocation per fine tile remained approximately linear across the measured scale-6 sweep;
- measured allocation-per-tile spread: `1.010`;
- measured time-per-tile spread: `1.696`.

M2.5.4-C freezes the separation between product acceptance and engineering stress without freezing final V1 scale.

Blocking M2 benchmark cases after the design freeze:

- Class I strategic/logical: `G(15,0)`;
- Class II strategic/logical: `G(7,7)`;
- Class III strategic/logical: `G(14,1)`;
- Class I physical scale-6 semantic acceptance: `G(4,0) -> G(24,0)`.

Non-blocking stress baseline:

- full physical `G(16,0) -> G(96,0)`.

The M2.5.1 hard thresholds remain unchanged for blocking acceptance cases:

- median elapsed `<= 1000 ms`;
- max elapsed `<= 2000 ms`;
- median managed allocation `<= 192 MiB`;
- max managed allocation `<= 256 MiB`.

No GPP promotion occurs in the design freeze.

M2.5.4-D and M2.5.4-E subsequently established the permanent harness and the committed accumulated performance baseline.

M2.5.4 status:

**CLOSED**

M2.5.5 — Cross-Platform Regression, Exit Audit & M2 Formal Close:

**concluído em 2026-09-29**

M2 exit audit evidence:

`GlobalArena-Evidence-M2.5.5-R1-ACCUMULATED-M2-EXIT-AUDIT-20260929-152623.zip`

SHA-256:

`931dd69bf4f15b40cf3a9ea31c5ddaa6ec9ab001062266386de02db973bea719`

Exit result:

- GA-SRP `1.4`: `19/19`;
- Release build: PASS;
- local suite: `435/435`;
- permanent benchmark harness: PASS;
- blocking performance budgets: PASS;
- cross-platform regression: Ubuntu `435/435`, Windows `435/435`, macOS `435/435`;
- cross-platform-relevant drift after validated commit: `0`;
- M2 exit requirements: `10/10`.

M2 — Planet Topology:

**CLOSED**

M2 final maturity:

**76.50 / 90 GPP — 85.0%**

GPP promotion at M2 close:

**+21.60 GPP**

Global total:

**146.50 / 1000 — 14.7%**

The remaining 15% of the topology budget is not silently claimed by milestone closure. Factor `1.00` remains reserved for V1 Definition of Done and later final-scale/consumer integration evidence.

Próximo milestone:

**M3 — Procedural World**

Próximo gate:

**M3 entry audit — world-generation contracts, risks and stage decomposition**

---
## M3 — Procedural World

**Status: atual desde 2026-09-29**

Objetivo:

transformar a topologia validada de M2 em um mundo físico procedural, reproduzível e consumível por sistemas posteriores.

M3 contract:

- `WorldSeed` is distinct from `SimulationSeed`;
- generator semantics are identified by `WorldGenerationVersion`;
- M2 remains the authoritative source of Goldberg strategic topology and physical incidence semantics;
- physical scalar fields are primary where practical;
- tactical detail is materialized only when a stage requires it;
- strategic systems consume aggregates rather than routinely traversing all tactical detail;
- resources are world potentials/properties, not Economy runtime inventories;
- civilization placement produces deterministic start-site suitability/candidates, not live Civilization runtime state;
- same request + seed + generation version must reproduce the same semantic world.

### M3.1 — World Generation Contracts, Seed/Versioning & Pipeline — 10 GPP

- M3 entry audit — concluded;
- M3.1-A — Architecture, Contract & GPP Design Freeze — concluded;
- M3.1-B — Executable World Generation Identity & Pipeline Contracts — concluded;
- M3.1-C — Deterministic Pipeline Skeleton & Contract Validation — concluded;
- M3.1-D — Accumulated M3.1 Validation & Close — concluded.

### M3.2 — Strategic Geometry Bridge, Elevation & Land/Water — 26 GPP

- M3.2-A — Strategic Geometry & Physical-Field Contract Freeze — concluded;
- M3.2-B — Executable Strategic Surface Graph & Scalar Field Substrate — concluded;
- M3.2-C — Deterministic Elevation, Relief & Land/Water Foundation — concluded;
- M3.2-D — Accumulated M3.2 Validation & Close — concluded.

Stage envelope:

- strategic geometry bridge;
- macro physical-field substrate;
- elevation / relief;
- initial land/water foundation.

M3.2-A freezes the data and determinism boundary only and awards no GPP.

M3.2 closes after accumulated local and cross-platform validation at `22.10 / 26 GPP — 85.0%`. The remaining `3.90 GPP` is reserved for later V1 Definition of Done evidence.

### M3.3 — Climate Inputs & Cross-Scale Physical Refinement — 28 GPP

- M3.3-A — Climate & Cross-Scale Physical Contract Freeze — concluded;
- M3.3-B — Executable Strategic Temperature, Moisture & Water Availability — concluded;
- M3.3-C — Bounded Tactical Refinement & Strategic Aggregation — concluded;
- M3.3-D — Accumulated M3.3 Validation & Close — concluded.

Stage envelope:

- strategic temperature / climate inputs;
- strategic moisture / water availability;
- explicit strategic boundary conditions;
- bounded tactical physical refinement using M2 physical identity;
- deterministic strategic aggregation.

M3.3-A freezes ownership, deterministic random domains, physical tactical identity, bounded materialization and aggregation boundaries only and awards no GPP.

M3.3 closes after accumulated local, cross-platform and bounded-path performance validation at `23.80 / 28 GPP — 85.0%`. The remaining `4.20 GPP` is reserved for later V1 Definition of Done evidence. The retained tactical patch is bounded; transient construction-time global M2 physical incidence materialization remains an open memory/scalability limitation.

### M3.4 — Hydrology & Derived Biomes — 20 GPP

- M3.4-A — Hydrology & Derived Biome Contract Freeze — concluded;
- M3.4-B — Executable Strategic Drainage & Flow Accumulation — concluded;
- M3.4-C — Executable Derived Strategic Biome Classification — concluded;
- M3.4-D — Accumulated M3.4 Validation & Close — concluded.

Stage envelope:

- hydrology derived from accepted terrain and water-availability inputs;
- deterministic strategic drainage;
- explicit inland sinks and water outlets;
- deterministic strategic flow accumulation;
- derived strategic biome classification;
- strategic-first residency with no required global tactical hydrology array.

M3.4-A freezes the hydrology/biome ownership and deterministic contracts only and awards no GPP.

M3.4 closes at `17.00 / 20 GPP — 85.0%` after accumulated local and cross-platform validation. Hydrology and Derived biomes are both `Validada — fator 0.85`. The remaining `3.00 GPP` is reserved for later V1 Definition of Done evidence.
### M3.5 — Resources, Habitability & Civilization Placement Inputs — 12 GPP

- M3.5-A — Resources, Habitability & Placement Policy Freeze — concluded;
- M3.5-B — Executable Strategic Resource Potential — concluded;
- M3.5-C — Executable Habitability & Civilization Placement Suitability — concluded;
- M3.5-D — Accumulated M3.5 Validation & Close — concluded.

Stage envelope:

- strategic resource potential/distribution as generated-world data;
- habitability as a separate derived environmental suitability signal;
- civilization-placement suitability/candidates as a distinct decision layer;
- deterministic domain-separated variation where explicitly used;
- placement policy capable of balancing local suitability with spatial dispersion;
- centralized/versioned calibration boundary for tunable weights and thresholds;
- strategic-first placement without global tactical pathfinding.

M3.5-A freezes ownership, separation of responsibilities and calibration boundaries only. It does not freeze the final resource catalogue, habitability coefficients, water/land traversal costs, start-distribution algorithm or civilization-count formula, and awards no GPP.

M3.5-B adds the first executable strategic resource-potential field: one normalized deterministic `GeneralPotential` value per strategic node using the `Resources` random domain. It deliberately does not freeze the final resource catalogue or Economy behavior.
M3.5-C adds executable habitability, local placement suitability and a deterministic strategic candidate selector. The selector can combine local quality with spatial dispersion, while water/land costs, exact weights and civilization count remain calibratable and unfrozen.
M3.5 closes at `10.20 / 12 GPP — 85.0%` after accumulated local and cross-platform validation. `Resources` and `Habitability & civilization-placement suitability` are both `Validada — fator 0.85`. The remaining `1.80 GPP` is reserved for later V1 Definition of Done evidence.
### M3.6 — Determinism, Performance, Cross-Platform Validation & M3 Exit — 4 GPP

- M3.6-A — World Generation Exit, Signature & Performance Contract Freeze — concluded;
- M3.6-B — Canonical Generated-World Signature & Regression Vectors — concluded;
- M3.6-C — World Generation Performance & Memory Acceptance — concluded;
- M3.6-D — Accumulated Cross-Platform M3 Exit Validation & Close — concluded.

Stage envelope:

- canonical SHA-256 generated-world signature with an explicit independent format version;
- explicit canonical binary serialization of request identity and authoritative `WorldGenerationResult` layers;
- known Class I/II/III regression digests;
- Windows/Ubuntu/macOS digest equivalence;
- complete `IWorldGenerator.Generate(request)` performance workload;
- explicit Release time/allocation benchmark contract;
- evidence-driven numeric budget calibration;
- accumulated M3 exit audit over one accepted source tree.

M3.6-A freezes the exit/signature/measurement contract only and awards no GPP.

Planned maturity progression:

- M3.6-B — factor `0.50` — `2.00 GPP`;
- M3.6-C — factor `0.70` — `2.80 GPP`;
- M3.6-D — factor `0.85` — `3.40 GPP`.

M3.6 closes at `85.0%` maturity, reserving `0.60 GPP` for V1 Definition of Done.

Total M3 budget:

**100 GPP**

Gate:

a seed and generation version, together with the same generation request, produce a functionally equivalent physical world across supported platforms, with coherent strategic/tactical scale boundaries and performance compatible with the approved V1 product envelope.

The final V1 strategic/tactical scale is not frozen by M3.1-A.

---

M3 formal close:

- M3 / World Generation: `85.00 / 100 GPP — 85.0%`;
- M3.6: `3.40 / 4 GPP — Validada — factor 0.85`;
- `15.00 GPP` of World Generation remain reserved for V1 Definition of Done evidence;
- final accumulated local gate: `717/717`;
- cross-platform exit source tree: `717/717` on Windows, Ubuntu and macOS with zero compiler warnings/errors;
- canonical generated-world signatures and blocking performance/memory budgets accepted.

M3 closed on `2026-09-30`.
## M4 — Systemic Vertical Slice

Objetivo:

validar o primeiro loop sistêmico principal headless.

Deve ser possível:

- gerar mundo;
- instanciar civilizações runtime;
- estabelecer propriedade/controle territorial;
- produzir e consumir ao menos uma mercadoria runtime;
- estabelecer comércio por rota estratégica explícita;
- emitir ordens militares;
- movimentar unidades;
- combater deterministicamente;
- alterar controle territorial e/ou acesso de aresta estratégica;
- invalidar somente rotas econômicas dependentes da mudança;
- recalcular a economia afetada;
- avançar o turno pela pipeline determinística existente.

Operational decomposition:

- M4.1 — Systemic Runtime Contracts & Ownership Foundation;
- M4.2 — Runtime Civilizations, Territory & Baseline Diplomacy;
- M4.3 — Economic Points, Workforce, Production & Demand;
- M4.4 — Markets, Trade, Routes & Flow Allocation;
- M4.5 — Units, Orders & Strategic Movement;
- M4.6 — Combat, Control Transfer & Edge Blocking;
- M4.7 — End-to-End Turn Coupling & Systemic Vertical Slice;
- M4.8 — Accumulated Determinism, Performance, Cross-Platform Validation & M4 Exit.

Current subcheckpoint:

**M4.5-D — Human Visibility Gate & Warfare Design Review**

Revised M4.5 decomposition under ADR-049:

- M4.5-A — Units, Orders & Strategic Movement Contract Freeze — closed;
- M4.5-B — Military Order Identity & Validation — closed;
- M4.5-C — Minimal Deterministic Strategic Movement Execution;
- M4.5-D — Human Visibility Gate & Warfare Design Review;
- M4.5-E — Accumulated Integration Validation & M4.5 Close.

M4.5-C scope guard:

- implement only deterministic one-edge event/execution substrate;
- preserve expected source, destination and traversed edge intent;
- revalidate authoritative unit state at execution time;
- deterministically reject stale same-unit movement events;
- mutate only unit strategic location plus normal WorldState revision;
- do not introduce multi-edge routing, movement points, unit classes, terrain mobility, supply, morale, formations, zones of control, hostile occupancy blocking or combat.

M4.5-D human gate:

- expose the real strategic map/topology through a low-cost replaceable viewer;
- show ownership and military units;
- allow identification/selection of a unit;
- highlight valid movement destinations using real rules;
- execute or step real movement and show before/after state;
- gather explicit human Warfare design decisions before detailed M4.6 decomposition.

M4.6 detailed implementation is blocked until M4.5-D is accepted.

M4.5-C — Minimal Deterministic Strategic Movement Execution: closed.

M4.5-C executable close:

- `MilitaryMoveEvent` records deterministic event identity, issuer/unit, expected authoritative source, destination and traversed strategic edge;
- `MilitaryMoveCommandProcessor` derives movement intent from authoritative `WorldState` and the shared strategic topology;
- execution-time revalidation rejects wrong world/turn, missing unit, owner mismatch, stale source, invalid destination and wrong/non-adjacent traversed edge;
- stale same-unit events are rejected after an earlier ordered event moves the unit;
- co-location remains allowed and creates no combat semantics;
- executor replaces only the moved unit location while preserving identity/owner and advances WorldState revision once;
- real `TurnResolver` pipeline behavior is covered;
- deterministic equivalent input/context behavior is covered;
- focused regression: `18/18`;
- full local Release regression: `1044/1044`;
- compiler warnings/errors: `0/0`;
- forbidden deeper Warfare scope remains absent;
- `Strategic movement`: `9.00 / 18 GPP — factor 0.50`;
- M4.5-C GPP delta: `+9.00`;
- M4.5 total: `21.00 / 42 GPP — 50.0%`;
- Warfare total: `21.00 / 140 — 15.0%`;
- Project total: `346.30 / 1000 — 34.6%`;
- next mandatory checkpoint: `M4.5-D — Human Visibility Gate & Warfare Design Review`;
- M4.6 remains blocked by ADR-049.
M4.5-B — Military Order Identity & Validation: closed.


M4.5-B executable close:

- `MilitaryMoveCommand` carries CommandId, issuer, MilitaryUnitId and destination StrategicCellId;
- authoritative source is read from `WorldState.Warfare`;
- command-authored source and traversed edge remain forbidden;
- validator is tied to the generated-world identity through RuntimeWorldBinding;
- unknown units, owner/issuer mismatch, foreign destinations and non-neighbor moves are rejected;
- direct-neighbor moves are accepted;
- co-location remains allowed;
- movement event, state mutation and execution-time revalidation remain deferred to M4.5-C;
- focused regression: `15/15`;
- full local Release regression: `1026/1026`;
- compiler warnings/errors: `0/0`;
- `Military orders / validation`: `6.00 / 12 GPP — factor 0.50`;
- M4.5-B GPP delta: `+6.00`;
- M4.5 total: `12.00 / 42 GPP — 28.6%`;
- Warfare total: `12.00 / 140 — 8.6%`;
- Project total: `337.30 / 1000 — 33.7%`.

M4.5-A — Units, Orders & Strategic Movement Contract Freeze: closed.

M4.5-A contract freeze:

- existing unit runtime authority remains MilitaryUnitId + Owner + StrategicCellId;
- WarfareRuntimeState remains the canonical unit roster;
- one baseline movement order traverses exactly one strategic edge;
- command-authored source location is forbidden; source comes from WorldState;
- movement planning derives expected source, destination and traversed StrategicEdgeId;
- planning validation and execution-time event revalidation are both required;
- stale same-unit events fail after an earlier movement changes the unit source;
- unit co-location is allowed during M4.5;
- combat, hostile occupancy, control transfer and strategic-edge blocking are deferred to M4.6;
- routine movement does not use tactical pathfinding;
- Economy route-cache authority is not reused for military movement;
- M4.5 capability budget: `42 GPP`;
- current M4.5 credit remains `6.00 / 42`;
- M4.5-A GPP delta: `0.00`.

M4.4 — Markets, Trade, Routes & Flow Allocation: closed.

M4.4-F — Accumulated Validation & M4.4 Close: closed.

M4.4-F integrated close:

- direct integrated pipeline: route cache -> route resolution -> transfer signal -> market allocation -> settlement;
- strategic-edge closure selectively invalidates the affected route and changes transfer economics;
- higher transfer cost reduces net settlement return;
- reopening restores the canonical route and original settlement;
- unreachable cached route suppresses trade until a queried frontier edge reopens;
- same-point exchange remains zero-hop and zero-transfer-cost;
- repeated end-to-end execution is deterministic;
- production code delta: none;
- direct integration regression: `6/6`;
- accumulated M4.4 regression: `125/125`;
- full local Release regression: `1011/1011`;
- M4.4 capability budget: `54 GPP`;
- maturity promotion: `0.50 -> 0.70`;
- M4.4 credit: `37.80 / 54 GPP`;
- M4.4-F promotion delta: `+10.80 GPP`;
- Economy total: `58.80 / 140 — 42.0%`;
- Project total: `331.30 / 1000 — 33.1%`;
- factor `0.85` remains reserved for later M4 exit evidence.

M4.4-E — Route Dependency Cache & Incremental Invalidation: closed.

M4.4-E executable close:

- final route traversal dependencies remain distinct from search dependencies;
- deterministic detailed BFS records every queried edge-access dependency;
- closed queried non-traversed edges participate in reopening invalidation;
- reachable and unreachable route results are cacheable;
- endpoint StrategicCellId anchor drift forces local recomputation;
- deleted EconomicPoints never return stale cached routes;
- access snapshot updates use symmetric-difference selective invalidation;
- sparse reverse StrategicEdgeId-to-route-key dependency index;
- explicit dependency invalidation returns canonical affected route keys;
- same-point zero-edge routes carry zero search dependencies;
- cache hit/miss is observability-only;
- no dense all-pairs matrix, persistent cache, market cache, transfer cache or weighted routing;
- targeted regression: `24/24`;
- full local Release regression: `1005/1005`;
- `Route dependency cache & invalidation`: `7.00 / 14 GPP — factor 0.50`;
- M4.4-E GPP delta: `+7.00`;
- Economy total: `48.00 / 140 — 34.3%`;
- Project total: `320.50 / 1000 — 32.1%`.

M4.4-D — Deterministic Market Allocation & Settlement Output: closed.

M4.4-D executable close:

- explicit fixed-point market allocation policy with bounded demand/transfer bands;
- transient supply/demand and transfer signals remain the input boundary;
- allocation partitions independently by CommodityId in the M4 baseline;
- finite residual network enforces supply, demand and route capacity;
- deterministic successive shortest augmenting path with Bellman-Ford relaxation;
- Int128 signed path economics;
- residual reverse edges permit correction of earlier assignments;
- only strictly positive unit-net-return trade is emitted;
- source Agriculture/Mining activity attribution is preserved;
- deterministic gross/transfer/net settlement output;
- canonical result ordering is input-order independent;
- no persistent inventory, resident treasury, weighted rerouting or route cache;
- targeted regression: `39/39`;
- full local Release regression: `981/981`;
- `Market allocation & financial settlement`: `7.00 / 14 GPP — factor 0.50`;
- M4.4-D GPP delta: `+7.00`;
- Economy total: `41.00 / 140 — 29.3%`;
- Project total: `313.50 / 1000 — 31.4%`.

M4.4-C — Transfer Cost & Capacity Signal: closed.

M4.4-C executable close:

- explicit immutable fixed-point `EconomicTransferPolicy`;
- positive base route capacity even with zero TradeLogistics workforce;
- only endpoint TradeLogistics workforce increases effective capacity;
- same EconomicPoint endpoint logistics workforce is counted once;
- base strategic unit cost derives from route hop count;
- zero-hop local route has zero strategic base cost;
- deterministic linear congestion marginal-cost signal;
- proposed load above capacity is rejected;
- UInt128 intermediates with fail-fast UInt64 bounds;
- no float/double/decimal simulation arithmetic;
- transfer state remains derived and outside WorldState;
- targeted regression: `30/30`;
- full local Release regression: `942/942`;
- `Transport/transfer cost & capacity signal`: `5.00 / 10 GPP — factor 0.50`;
- M4.4-C GPP delta: `+5.00`;
- Economy total: `34.00 / 140 — 24.3%`;
- Project total: `306.50 / 1000 — 30.7%`.

M4.4-B — Deterministic Strategic Route Identity, Reachability & Access: closed.

M4.4-B executable close:

- directional `StrategicEconomicRouteKey`;
- shared `StrategicSurfaceGraph` routing substrate;
- sparse explicit `StrategicEdgeAccessSnapshot`;
- same-point/co-located zero-edge routes;
- deterministic unweighted shortest-hop BFS;
- canonical equal-hop tie-breaking;
- ordered `StrategicEdgeId` route dependencies;
- closed-edge avoidance and explicit unreachable result;
- route state remains derived and outside WorldState;
- targeted regression: `26/26`;
- full local Release regression: `912/912`;
- `Strategic route identity & pathfinding`: `8.00 / 16 GPP — factor 0.50`;
- M4.4-B GPP delta: `+8.00`;
- Economy total: `29.00 / 140 — 20.7%`;
- Project total: `301.50 / 1000 — 30.2%`.

M4.4-A — Markets, Trade, Routes & Flow Allocation Contract Freeze: closed.

M4.4-A frozen direction:

- M4.3 transient supply/demand remain the upstream per-cycle input;
- no mandatory Market/City/Hub entity;
- `StrategicSurfaceGraph` is the shared global routing substrate;
- `StrategicEdgeId` is the route dependency identity;
- strategic access uses an explicit edge overlay decoupled from Warfare;
- same-point exchange is zero-edge/zero-distance;
- equal-cost route ties use canonical stable IDs;
- transfer cost/capacity policies are fixed-point and versioned;
- market allocation resolves independently per CommodityId;
- literal micro-packet water filling is not required;
- price/value formula remains calibratable;
- deterministic settlement is an output seam, not a full treasury model;
- route cache is derived/non-authoritative and selectively invalidated by edge dependency;
- M4.4 capability budget: `54 GPP`;
- M4.4-A GPP delta: `0.00`;
- project remains `293.50 / 1000 — 29.4%`.

M4.4 decomposition:

- M4.4-A — Markets, Trade, Routes & Flow Allocation Contract Freeze;
- M4.4-B — Deterministic Strategic Route Identity, Reachability & Access;
- M4.4-C — Transfer Cost & Capacity Signal;
- M4.4-D — Deterministic Market Allocation & Settlement Output;
- M4.4-E — Route Dependency Cache & Incremental Invalidation;
- M4.4-F — Accumulated Validation & M4.4 Close.

M4.3 — Economic Points, Workforce, Production & Demand: closed.

M4.3-D — Accumulated Validation & M4.3 Close: closed.

M4.3 accumulated close:

- resident EconomicPoint/workforce/activity substrate preserved;
- deterministic transient Agriculture/Mining supply and recurring demand preserved;
- canonical WorldState hash remains `7`;
- accumulated M4.3 targeted regression: `113/113`;
- full local Release regression: `886/886`;
- cross-platform full regression on M4.3-C source commit: `886/886` on Windows, Ubuntu and macOS;
- persistent commodity inventory remains absent;
- combined M4.3 Economy capability budget: `30 GPP`;
- integrated factor: `0.70`;
- M4.3 capability credit: `21.00 / 30`;
- M4.3-D GPP delta: `+6.00`;
- Economy total: `21.00 / 140 — 15.0%`;
- Project total: `293.50 / 1000 — 29.4%`.

M4.3-C — Deterministic Production & Demand Flow Resolution: closed.

M4.3-C executable close:

- explicit fixed-point EconomicProductionPotential and EconomicDemandProfile inputs;
- deterministic transient EconomicSupplyFlow and EconomicDemandFlow outputs;
- Agriculture/Mining supply uses allocated activity workforce;
- demand uses total localized workforce;
- equivalent input ordering yields canonical equivalent flow ordering;
- UInt128 intermediate arithmetic and fail-fast overflow;
- no float/double/decimal simulation arithmetic;
- no WorldState mutation and hash remains format `7`;
- targeted regression: `28/28`;
- full local Release regression: `886/886`;
- `Workforce, production & demand`: `9.00 / 18 GPP — factor 0.50`;
- M4.3-C GPP delta: `+9.00`.

M4.3-B — Economic Point & Localized Workforce/Activity Substrate: closed.

M4.3-B executable close:

- EconomicPointId and localized EconomicPoint runtime state are authoritative;
- workforce is localized with sparse canonical Agriculture / Mining / TradeLogistics allocations;
- StrategicStockEntry persistent inventory semantics are retired from active V1 state;
- CommodityId and WorldState Economy aggregate boundary are preserved;
- point owners and bound-world strategic anchors are validated;
- canonical WorldState hash format is `7`;
- targeted regression: `85/85`;
- full local Release regression: `858/858`;
- existing Economy `6.00 GPP` is confirmed, not duplicated;
- M4.3-B GPP delta: `0.00`.

M4.3-A — Economic Points, Workforce, Production & Demand Contract Freeze: closed.

M4.3 decomposition:

- M4.3-A — Economic Points, Workforce, Production & Demand Contract Freeze;
- M4.3-B — Economic Point & Localized Workforce/Activity Substrate;
- M4.3-C — Deterministic Production & Demand Flow Resolution;
- M4.3-D — Accumulated Validation & M4.3 Close.

M4.3-A freezes the flow-based Economy boundary before implementation:

- economic points replace mandatory cities as the baseline spatial abstraction;
- workforce is localized;
- Agriculture, Mining and Trade / Logistics are the three baseline economic activities;
- Agriculture uses diffuse productive potential;
- Mining depends on concentrated deposits;
- Trade / Logistics uses workforce and infrastructure to raise transfer capacity;
- production is a recurring supply flow rather than durable physical inventory;
- consumption is represented as recurring demand;
- market activity settles primarily into financial return;
- hub-like centers emerge through logistics specialization rather than a mandatory Hub entity;
- population growth, diminishing-return formulas and logistics-capacity formulas remain open;
- water-filling, market prices, routes and inter-point allocation remain for M4.4.

M4.3-A awards `0.00 GPP`.

M4.2-E closed ownership-link integration and accumulated validation:

- the representative bound state integrates materialized civilizations, initial territory, baseline diplomacy, Economy ownership and Warfare ownership/location references;
- invalid Economy/Warfare owners remain rejected by WorldState invariants;
- no production code is added by M4.2-E;
- canonical WorldState hash format remains `6`;
- accumulated M4.2 tests pass `79/79`;
- M4.2 closes at `35.00 / 50 GPP — 70.0%`;
- M4.2-E GPP delta: `+18.00`.

M4.2 — Runtime Civilizations, Territory & Baseline Diplomacy: closed.

M4.2-D closed baseline diplomacy state:

- Neutral is implicit and is not persisted;
- Enemy and Ally are sparse canonical pair overrides;
- pair identity and lookup are symmetric;
- self-relations are not persisted;
- every persisted diplomacy participant must exist in the civilization roster;
- canonical WorldState hash format is `6`;
- M4.2-D GPP delta: `+4.00`.

M4.2-C closed strategic territory control:

- territory is sparse authoritative Runtime state;
- absence of an entry means unowned;
- one strategic cell has at most one controlling civilization;
- initial control derives from each materialized civilization `StartCellId`;
- controller identities and bound-world cells are validated;
- canonical WorldState hash format is `5`;
- M4.2-C GPP delta: `+5.00`.

M4.2-B closed deterministic runtime civilization materialization:

- explicit civilization count remains the runtime input;
- M3.5 placement selector remains authoritative;
- runtime land eligibility is enforced while preserving selector order;
- civilization identities are canonical `1..N`;
- start cells are unique and bound-world validated;
- canonical WorldState hash format is `4`;
- M4.2-B GPP delta: `+4.00`.

M4.2-A closed the Civilization/Territory/Diplomacy contract freeze with ADR-032 Accepted and `0.00 GPP` awarded.


M4.2-A freezes the Civilization/Diplomacy runtime contract before implementation.

Baseline decisions:

- civilization count is an explicit runtime materialization input; the final count formula remains open;
- generated-world placement suitability and the accepted deterministic candidate selector remain authoritative for starts;
- runtime civilization starts must be strategic land;
- civilization IDs are assigned `1..N` in deterministic selected-start order;
- each civilization initially controls its own start cell;
- all other strategic cells begin unowned;
- territorial control is sparse and one-owner-per-cell;
- diplomacy baseline is symmetric `Enemy / Neutral / Ally`;
- Neutral is implicit/default and only non-neutral overrides require persistence.

M4.2 decomposition:

- M4.2-A — contract freeze;
- M4.2-B — deterministic runtime civilization materialization;
- M4.2-C — strategic territory control;
- M4.2-D — baseline diplomacy state;
- M4.2-E — ownership-link integration, accumulated validation and close.

M4.2-A awards no GPP. Project progress remains `247.50 / 1000 — 24.8%`.


M4.1-C closed the runtime foundation:

- runtime state is bound to canonical generated-world identity;
- cross-domain Civilization/Economy/Warfare ownership invariants are enforced;
- bound unit locations must belong to the generated world;
- canonical WorldState hash format is `3`;
- M4.1-C GPP delta: `+9.60`;
- M4.1 — Systemic Runtime Contracts & Ownership Foundation: concluded.

M4.1-B closed with the first executable authoritative runtime-state contracts:

- `GlobalArena.Runtime` owns `WorldState`;
- Civilization roster, strategic stocks and military unit state are canonicalized;
- canonical WorldState hash format is `2`;
- M4.1-B GPP delta: `+6.40`.

GPP rule:

M4 uses a `254 GPP` tranche drawn from existing Civilizations/Diplomacy, Economy and Warfare budgets. It does not add scope to the `1000 GPP` baseline.

M4.1-A awards no GPP.

Gate:

warfare and economy interfere with each other through explicit contracts inside a deterministic headless turn, without direct mutation of each other's internals.

Presentation, multiplayer, AI decision-making and final balance are outside M4.

---

## M5 — Playable 3D Client

Objetivo:

tornar a simulação controlável visualmente.

Inclui:

- pseudoesfera 3D;
- câmera;
- seleção;
- visualização estratégica;
- visualização tática;
- emissão de comandos;
- UI operacional;
- read models.

Gate:

uma partida pode ser controlada sem ferramentas de desenvolvimento.

---

## M6 — Multiplayer

Objetivo:

executar a simulação através de autoridade remota.

Inclui:

- servidor autoritativo;
- transporte de commands;
- snapshots;
- synchronization;
- reconnect;
- protocol version;
- desync diagnostics.

Gate:

dois ou mais jogadores conseguem compartilhar uma partida válida.

---

## M7 — Alpha

Objetivo:

todos os sistemas fundamentais do V1 existem.

Foco:

integração sistêmica.

Gate:

core loop completo do V1 funcional.

---

## M8 — Beta

Objetivo:

feature complete.

Foco:

- performance;
- estabilidade;
- UX;
- balanceamento;
- conteúdo;
- networking;
- saves.

Novas features V1 deixam de entrar sem aprovação excepcional.

---

## V1.0

Objetivo:

produto lançável.

Gate:

V1_DEFINITION_OF_DONE integralmente atendida.

---

# 9. Critical Path

O Critical Path representa o elemento que mais limita o avanço do projeto naquele momento.

Somente um ou poucos itens devem ser classificados como Critical Path.

Status atual:

**M4.5-D human visibility gate and Warfare design review**

---

# 10. Classificação de trabalho

Toda nova proposta deve ser classificada como:

## V1 REQUIRED

Necessária para Definition of Done.

## V1 OPTIONAL

Desejável, mas removível sem quebrar a proposta do V1.

## POST-V1

Planejada para depois do lançamento.

## EXPERIMENTAL

Hipótese ainda não aprovada.

---

# 11. Scope Change

Mudanças importantes do baseline exigem registro.

Exemplos:

- novo sistema obrigatório;
- remoção de capability V1;
- mudança de plataforma;
- mudança estrutural no multiplayer;
- alteração relevante da proposta de produto.

Mudanças de escopo devem responder:

1. O que mudou?
2. Por que mudou?
3. Qual domínio é afetado?
4. Quantos GPP precisam ser redistribuídos?
5. O total de 1000 GPP muda?
6. Qual impacto esperado no roadmap?

---

# 12. Regra anti-horizonte-móvel

Descobrir mais detalhes de uma capability não significa automaticamente aumentar o tamanho do projeto.

Exemplo:

Economy possui 140 GPP.

Se inicialmente havia:

- Production
- Markets
- Trade

e depois forem descobertos:

- Capacity
- Transport Costs
- Warehousing
- Route Cache
- Embargoes

os 140 GPP são redistribuídos entre essas capabilities.

O horizonte permanece fixo.

---

# 13. Revisão do roadmap

O roadmap deve ser revisado:

- no fechamento de cada milestone;
- após spike técnico importante;
- após alteração de escopo;
- quando surgir risco estrutural;
- quando o Critical Path mudar.

Revisar não significa apagar a história.

Mudanças devem ser registradas no PROGRESS_LEDGER.

---

# 14. Dashboard

O estado do projeto deve poder ser resumido assim:

Global Arena

Progress: XX.X%
GPP Earned: XXX / 1000
Scope Confidence: XX%
Current Milestone: MX
Critical Path: ...
Technical Risk: ...
Open Critical Risks: X
Last Baseline Review: YYYY-MM-DD

---

# 15. Status atual

Current Milestone:

**M4 — Systemic Vertical Slice**

Current Stage:

**M4 — Systemic Vertical Slice**

Current Subcheckpoint:

**M4.5-D — Human Visibility Gate & Warfare Design Review**

M3 Entry Audit:

**concluded on 2026-09-29**

M3.1 — World Generation Contracts, Seed/Versioning & Pipeline:

**concluded on 2026-09-29**

M3.1-A — Architecture, Contract & GPP Design Freeze:

**concluded on 2026-09-29**

M3.1-B — Executable World Generation Identity & Pipeline Contracts:

**concluded on 2026-09-29**

M3.1-C — Deterministic Pipeline Skeleton & Contract Validation:

**concluded on 2026-09-29**

M3.1-D — Accumulated M3.1 Validation & Close:

**concluded on 2026-09-29**

Official Progress:

**34.6%**

GPP Earned:

**346.30 / 1000**

Foundation / Simulation Kernel:

**70.00 / 70 GPP — 100.0%**

Planet Topology / Goldberg:

**76.50 / 90 GPP — 85.0%**

World Generation / biomes / resources:

**85.00 / 100 GPP — 85.0%**

Economy / trade / logistics:

**58.80 / 140 GPP — 42.0%**

Warfare / units / combat:

**21.00 / 140 GPP — 15.0%**

Civilizations / Diplomacy:

**35.00 / 70 GPP — 50.0%**

Scope Confidence:

**50%**

Technical Risk:

**HIGH**

Active Critical Risks:

**7**

Critical Path:

**M4.5-D human visibility gate and Warfare design review**

Entry gate:

**M4.5-C independent audit accepted; M4.5-D mandatory under ADR-049 before deeper M4.6 Warfare design**

Mandatory human gate before M4.6:

**M4.5-D — Human Visibility Gate & Warfare Design Review**

Last Baseline Review:

**2026-10-06**

#### M2.5.2-C materializer close

Materialização física concluída no reference target `G(1,0) -> G(6,0)`:

- cobertura física: `362/362`;
- interior: `312`;
- edge-shared: `30`;
- vertex-shared: `20`;
- todos os 30 coarse edges cobertos exatamente uma vez;
- todos os 20 coarse vertices cobertos exatamente uma vez;
- determinismo e snapshots read-only validados;
- suíte acumulada: `402/402`.

Evidence:

`GlobalArena-Evidence-M2.5.2-C-MATERIALIZER-IMPLEMENTATION-R1-20260928-093748.zip`

M2.5.2-C está concluído.

Traversal físico cross-region permanece fora desta tranche e passa a ser o foco do M2.5.2-D.
#### M2.5.2-D traversal close

Traversal físico cross-region concluído no reference target `G(1,0) -> G(6,0)`:

- source graph: fine `StrategicTopology`;
- adjacency source: `StrategicCell.AdjacentCellIds`;
- algoritmo: deterministic BFS shortest path;
- edge-shared traversal: PASS;
- vertex-shared traversal: PASS;
- synthetic cross-region adjacency: False;
- accumulated tests: `417/417`.

Evidence:

`GlobalArena-Evidence-M2.5.2-D-TRAVERSAL-IMPLEMENTATION-R2-20260928-101812.zip`

M2.5.2-D está concluído.

#### M2.5.2-E accumulated validation and M2.5.2 close

Validação acumulada concluída sem alteração de production code:

- Release build: PASS;
- compiler warnings/errors: `0/0`;
- suíte: `417/417`;
- fine tile coverage: `362/362`;
- incidence signature: `312 interior / 30 edge / 20 vertex`;
- coarse edge coverage: `30/30`;
- coarse vertex coverage: `20/20`;
- fine edges: `1080`, com reciprocidade integral;
- shortest paths: `65.703/65.703` pares validados contra BFS independente;
- cross-ownership endpoint pairs: `61.441`;
- maximum shortest distance no reference target: `18`;
- synthetic cross-region adjacency: `False`;
- tracked hash drift: `0`.

Evidence:

`GlobalArena-Evidence-M2.5.2-E-R2-READONLY-ACCUMULATED-VALIDATION-20260928-120824.zip`

M2.5.2 está concluído.

### M2.5.3 — Hierarchy/Refinement Coverage Decision

M2.5.3-A concluiu o audit read-only de feasibility/coverage.

Evidência:

`GlobalArena-Evidence-M2.5.3-A-R2-READONLY-HIERARCHY-COVERAGE-AUDIT-20260928-131956.zip`

SHA-256:

`b06095778ecae5f160a538d78504a47dedfab5f32dde290d81e138b3b7d48dd4`

Achados:

- strategic topology generation suporta Class I, II e III;
- `GoldbergScaledRefinement` aceita representative scaled pairs das três classes;
- `PhysicalTacticalIncidenceMapper` suporta somente `G(1,0) -> G(6,0)`;
- Class I unit-base possui provenance suficiente no mecanismo atual;
- Class I non-unit coarse requer lineage além dos 12 seed vertices;
- Class II requer lineage além do mapa atual de 12 seeds;
- Class III não expõe a provenance necessária ao physical mapper;
- universal physical refinement não está provado.

Decisão proposta para freeze em M2.5.3-B:

- suporte estratégico Class I/II/III permanece;
- physical strategic-to-tactical hierarchy oficial de M2: Class I;
- formas oficiais: `G(k,0) -> G(6k,0)` e `G(0,k) -> G(0,6k)`;
- `k >= 1`, sujeito aos limites de implementação e ao performance envelope do M2.5.4;
- scale físico oficial de M2: `6`;
- Class II/III physical hierarchy fica fora do scope oficial de M2;
- M2.5.3-C deve implementar durable Class I lineage e remover a limitação física ao coarse unit-base;
- M2.5.3-D fará accumulated validation e fechamento do stage.

Decomposição:

- M2.5.3-A — Hierarchy/Refinement Coverage Feasibility Audit — concluído;
- M2.5.3-B — Official Coverage Contract & Design Freeze — concluído;
- M2.5.3-C — Class I Scale-6 Durable Lineage & Physical Mapping — concluído;
- M2.5.3-D — Accumulated Coverage Validation & M2.5.3 Close — concluído;
- M2.5.3 — Hierarchy/Refinement Coverage Decision for Officially Supported Goldberg Families — concluído.

M2.5.3-C implementation evidence:

- durable Class I lineage derives exact fine anchors by scaling canonical integer construction keys;
- official physical mapper accepts only Class I scale 6;
- `G(1,0) -> G(6,0)` remains compatible;
- `G(0,1) -> G(0,6)`, `G(2,0) -> G(12,0)`, `G(0,2) -> G(0,12)` and `G(3,0) -> G(18,0)` are covered;
- generalized signatures `312/30/20/362`, `1242/120/80/1442` and `2792/270/180/3242` are enforced;
- every 2-way incidence validates against an authoritative coarse edge;
- every 3-way incidence validates against an authoritative coarse vertex;
- physical traversal remains based on authoritative fine adjacency;
- Class II/III physical hierarchy remains outside the official M2 support envelope;
- suite accumulated after implementation: `429/429`.

M2.5.3-D accumulated validation evidence:

- Release build: PASS;
- compiler warnings/errors: `0/0`;
- suite: `429/429`;
- `k=1`, `k=2`, `k=3` and out-of-sample `k=4` validated on both Class I axes;
- exact generalized count formulas: PASS;
- authoritative coarse-edge coverage: PASS;
- authoritative coarse-vertex coverage: PASS;
- exact fine-tile coverage: PASS;
- repeated `k=4` determinism on both axes: PASS;
- cross-owner fine-topology traversal at `k=4`: PASS;
- wrong-scale Class I rejection: PASS;
- Class II physical rejection: PASS;
- Class III physical rejection: PASS;
- repository mutation: False.

Maturity remains:

`Strategic ↔ tactical hierarchy/refinement mapping`

**Integrada ao sistema — fator 0.70 — 11.20 GPP**

No additional GPP is awarded by M2.5.3-D because `Validada — 0.85` still requires performance minimum and the applicable later exit regression.

Current totals remain:

- GPP: `124.90 / 1000`;
- Global Progress: `12.5%`;
- Planet Topology: `54.90 / 90 — 61.0%`.

Próximo gate:

**M2.5.4 — Headless Scalability Benchmark Harness & Baseline**

## 2026-10-07 — World MVP critical-path pivot

The first M4.5-D viewer passed technical QA but failed the human design-sufficiency gate because its arbitrary layout could not support decisions about real Goldberg geometry, strategic scale, tactical density or movement cadence.

Revised Critical Path:

**M4.5-D — Geometric World MVP & Human Design Gate**

1. **M4.5-D.1 — Goldberg Geometric World MVP Feasibility Audit & Contract Freeze**
2. **M4.5-D.2 — Canonical Spherical Strategic Geometry**
3. **M4.5-D.3 — Strategic Worldgen 3D MVP & Human Calibration Surface**
4. **M4.5-D.4 — Continuous Tactical Mesh Visualization & 1/2/3 Incidence Gate**
5. **M4.5-D.5 — Human World-Scale Acceptance & System Reintegration Direction**

Retained contracts: strategic Goldberg topology; ADR-021 continuous fine physical topology; canonical tactical incidence `1/2/3`; edge-shared tile belongs to 2 strategic cells; vertex-shared tile belongs to 3; no shared physical identity duplication.

Current Class I scale 6 is not final product density.

M4.6 remains blocked through D.5. Existing M4.4 Economy and M4.5-C movement substrate are retained.

GPP delta `0.00`; official progress remains **346.30 / 1000 — 34.6%**.

Decision authority: **ADR-051**.

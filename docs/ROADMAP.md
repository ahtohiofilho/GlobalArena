# Global Arena — Roadmap

**Versão:** 0.1
**Status:** Baseline V1 congelado
**Milestone atual:** M2 — Planet Topology

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

Subcheckpoint atual:

**M2.4.4 — Class I/II/III Scaled Refinement Validation**

O audit read-only de M2.4.4 confirmou:

- 340/340 testes no baseline;
- 7 audit pairs cobrindo Class I, Class II e Class III;
- public count scaling em todos os audit pairs;
- canonical determinism em todos os audit pairs;
- 12 pentágonos em coarse/fine em todos os audit pairs;
- scale-homogeneous Class I/II construction keys;
- scale-homogeneous Class III local lattice coordinates;
- current cell reference mapper suporta somente um pair;
- current border continuity mapper suporta somente um pair;
- Class III durable global provenance continua ausente;
- raw DSU roots continuam implementation details.

Design congelado para M2.4.4:

- validation-only;
- nenhuma mudança em production;
- novo test file: `GoldbergScaledRefinementFamilyValidationTests.cs`;
- 14 Facts;
- representative pairs Class I/II/III;
- scale 2 e scale 3;
- Class I normal e inverted axis;
- Class III ambas as chiralities;
- somente public invariants;
- sem reflection no test suite;
- sem generalização prematura de provenance;
- current reference/continuity scope explicitamente preservado.

Baseline:

`340`.

Expected after implementation:

`354`.

M2.4.4 não promove GPP.

GPP permanece:

**109.50 / 1000**

Global Progress permanece:

**11.0%**

Topologia planetária / Goldberg permanece:

**39.50 / 90 GPP — 43.9%**

`Strategic ↔ tactical hierarchy/refinement mapping` permanece:

**Inexistente — 0.00**

Próximo gate:

**M2.4.4 DESIGN AUDIT**

---



## M3 — Procedural World

Objetivo:

transformar topologia em mundo.

Inclui:

- geração multiescala;
- campos físicos macroscópicos;
- elevation;
- moisture / water availability;
- temperature / climate;
- condições de contorno entre regiões;
- refinamento físico tático;
- hydrology;
- biomes derivados;
- resources;
- habitability;
- agregação tático → estratégico;
- civilization placement;
- generation versions;
- seed reproducibility.

Gate:

uma seed gera um planeta físico funcional e reproduzível, com coerência entre escalas e sem exigir que os sistemas estratégicos recorrentes percorram diretamente toda a resolução tática.

---

## M4 — Systemic Vertical Slice

Objetivo:

validar o loop sistêmico principal.

Deve ser possível:

- gerar mundo;
- criar civilizações;
- produzir;
- consumir;
- criar comércio;
- calcular rotas;
- emitir ordens;
- movimentar unidades;
- combater;
- alterar controle;
- bloquear aresta;
- invalidar rota;
- recalcular economia;
- avançar turno.

Gate:

guerra e economia interferem uma na outra através dos contratos arquiteturais.

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

**Goldberg family/refinement validation e mapeamento físico estratégico/tático**

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

**M2 — Planet Topology**

Current Stage:

**M2.4 — Goldberg Family & Refinement Validation**

Current Subcheckpoint:

**M2.4.4 — Class I/II/III Scaled Refinement Validation**

Official Progress:

**11.0%**

GPP Earned:

**109.50 / 1000**

Foundation / Simulation Kernel:

**70.00 / 70 GPP — 100.0%**

Planet Topology / Goldberg:

**39.50 / 90 GPP — 43.9%**

Scope Confidence:

**50%**

Technical Risk:

**HIGH**

Active Critical Risks:

**7**

Critical Path:

**Goldberg family/refinement validation e mapeamento físico estratégico/tático**

Last Baseline Review:

**2026-09-20**

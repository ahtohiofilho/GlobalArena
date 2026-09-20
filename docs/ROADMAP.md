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

Etapa atual:

**M2.2 — Tactical Region Topology**

Subcheckpoint atual:

**M2.2.1 — Tactical Identity & Region Contract**

O audit read-only inicial confirmou:

- nenhum tipo `Tactical*` existe ainda no código;
- `StrategicCellId` já fornece a identidade canônica do pai;
- a arquitetura exige uma região tática por `StrategicCell`;
- shared border bands pertencem ao escopo de M2.3 e não devem ser antecipadas silenciosamente em M2.2.

Contrato congelado para M2.2.1:

- não criar `TacticalRegionId` redundante;
- `TacticalRegion` é identificado pelo `StrategicCellId` pai;
- `TacticalCellId` para células region-owned usa `ParentStrategicCellId + LocalOrdinal`;
- topologia de M2.2 é estritamente intra-região;
- adjacências locais devem ser recíprocas, sem self-loop, sem duplicatas e conectadas;
- shared border entities não podem ser duplicadas entre regiões e permanecem para M2.3;
- Unity, mesh, coordenadas, terreno e conteúdo físico continuam fora da fonte de verdade.

Próximo gate:

implementar o contrato executável de M2.2.1 e validá-lo antes de materializar o primeiro grafo tático local.

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

**Project Baseline / arquitetura inicial**

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

**M2.2 — Tactical Region Topology**

Current Subcheckpoint:

**M2.2 — Tactical Region Topology**

Official Progress:

**8.4%**

GPP Earned:

**84.00 / 1000**

Foundation / Simulation Kernel:

**70.00 / 70 GPP — 100.0%**

Planet Topology / Goldberg:

**14.00 / 90 GPP — 15.6%**

Scope Confidence:

**50%**

Technical Risk:

**HIGH**

Active Critical Risks:

**7**

Critical Path:

**Goldberg hierarchy mapping e prova da hierarquia estratégico/tático**

Last Baseline Review:

**2026-09-19**

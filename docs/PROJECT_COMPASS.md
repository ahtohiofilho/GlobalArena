# Global Arena — Project Compass

## 1. Identidade do projeto

**Nome:** Global Arena
**Plataforma inicial:** PC
**Modelo:** jogo de estratégia e tática em escala planetária
**Estado atual:** M3 — Procedural World
**Versão deste documento:** 0.1

Global Arena é um jogo de estratégia em escala planetária com mundos procedurais, economia viva, civilizações, guerra tática, diplomacia e multiplayer.

A arquitetura deve permitir grande escala sem obrigar todos os subsistemas a operar na mesma granularidade.

---

## 2. Princípios estruturais

### 2.1 Mundo multiescala

O planeta possui duas representações principais:

**Camada estratégica**
- poliedro de Goldberg;
- cada face funciona como um nó do grafo estratégico;
- arestas representam conexões entre territórios;
- utilizada por economia, comércio, logística, diplomacia, IA estratégica e movimentação macro.

**Camada tática**
- malha de alta resolução;
- usada para combate, posicionamento, terreno, ocupação e movimentação detalhada;
- as arestas entre territórios estratégicos possuem uma fileira compartilhada de hexágonos.

O aumento da resolução tática não deve provocar aumento proporcional do custo da simulação estratégica.

---

## 3. Geometria do planeta

O planeta será visualizado em 3D como uma pseudoesfera formada por um poliedro de Goldberg de alta resolução.

A geometria renderizada e a topologia lógica são conceitos separados.

Para a simulação:

- territórios estratégicos são nós;
- conexões são arestas;
- subtiles constituem a malha tática;
- pentágonos estruturais do Goldberg são aceitos;
- hexágonos compõem a maioria da superfície;
- tiles compartilhados nas fronteiras pertencem logicamente às arestas estratégicas.

A Unity não será a fonte de verdade da geometria lógica.

---

## 4. Simulação

A simulação deve ser independente da interface gráfica.

Entrada:

WorldState
+
Commands
+
SimulationContext

No baseline atual, `SimulationContext` contém o `TurnNumber` e a `SimulationSeed` necessários para contextualizar deterministicamente uma resolução.

Saída:

NewWorldState
+
EventLog

O mesmo núcleo deverá poder rodar em:

- single-player;
- multiplayer;
- servidor;
- console de testes;
- replay;
- benchmarks;
- simulações automatizadas.

---

## 5. Sistema de turnos

Os jogadores tomam decisões simultaneamente.

Quando a janela de ordens fecha:

1. as ordens são bloqueadas;
2. comandos válidos geram eventos;
3. os eventos são ordenados de forma pseudoaleatória;
4. a ordem é determinada por uma seed reproduzível;
5. os eventos são resolvidos sequencialmente;
6. cada evento é revalidado no momento da execução;
7. o estado resultante é consolidado.

O sistema evita tentar resolver combinatoriamente todos os comandos simultâneos.

---

## 6. Modos temporais previstos

O núcleo não deve depender de um único modelo de duração do turno.

Possibilidades:

### Tempo contínuo por janelas
O turno fecha automaticamente após determinado intervalo.

### Correspondência
Cada jogador possui um período maior para enviar suas ordens.

### Manual
O turno avança quando todos os participantes estão prontos.

Todos utilizam o mesmo pipeline de resolução.

---

## 7. Multiplayer

Multiplayer é requisito arquitetural desde o início.

Modelo pretendido:

- servidor autoritativo;
- clientes enviam intenções/comandos;
- servidor valida;
- servidor executa a simulação;
- clientes recebem o estado autorizado.

O código da simulação não deve depender de networking.

---

## 8. World Generation

Cada planeta será procedural.

Pipeline inicial previsto:

Geometry
→ Elevation
→ Water/Hydrology
→ Climate
→ Biomes
→ Resources
→ Habitability
→ Civilizations
→ Initial Economy

Cada estágio deverá permanecer substituível.

A geração deverá utilizar seeds reproduzíveis.

---

## 9. Civilizações

O número de civilizações será derivado das características do mundo, produzindo um valor sugerido ao jogador.

Não haverá limite estrutural baseado em uma lista fixa de cores.

Identidade visual de uma civilização poderá utilizar:

- cor;
- símbolo;
- padrão;
- nome.

A geração de cores deverá buscar distinção visual entre civilizações.

---

## 10. Diplomacia

Modelo inicial:

- Enemy
- Neutral
- Ally

A implementação deve permitir expansão futura sem exigir reestruturação do modelo básico.

---

## 11. Economia

A economia será um subsistema independente e potencialmente um dos maiores consumidores de CPU.

Princípios:

- operar prioritariamente sobre o grafo estratégico;
- não executar pathfinding global na malha tática;
- manter cache de rotas;
- invalidar somente rotas afetadas por mudanças;
- permitir diferentes cadências de atualização;
- utilizar estruturas de dados adequadas para processamento em lote.

Exemplo:

mudança tática
→ estado da aresta muda
→ rotas dependentes são marcadas como dirty
→ apenas rotas afetadas são recalculadas.

---

## 12. Guerra

A guerra possui escala estratégica e resolução tática.

O módulo de Warfare deverá ser desacoplado de:

- UI;
- economia;
- networking;
- renderização.

Interações com outros sistemas devem ocorrer através de contratos ou eventos bem definidos.

---

## 13. UI

A interface será sofisticada e deverá permitir navegar por:

- planeta;
- territórios;
- civilizações;
- economia;
- rotas;
- exércitos;
- guerras;
- diplomacia;
- estatísticas.

A UI será consumidora da simulação, nunca fonte de verdade.

Read models específicos poderão ser criados para apresentação.

---

## 14. Princípio de modularidade

Global Arena será inicialmente um monólito modular.

Objetivos:

- baixo acoplamento;
- alta coesão interna;
- responsabilidades claras;
- substituição de subsistemas;
- blast radius controlado;
- testes independentes;
- possibilidade futura de separar processos quando necessário.

Regra:

**simplicidade dentro dos módulos; contratos fortes entre os módulos.**

---

# 15. Mapa macro do V1

Os pesos abaixo representam o orçamento funcional inicial do V1.

| Área | Peso |
|---|---:|
| Fundação / Simulation Kernel | 7% |
| Topologia planetária / Goldberg | 9% |
| World Generation / biomas / recursos | 10% |
| Economia / comércio / logística | 14% |
| Warfare / unidades / combate | 14% |
| Civilizações / diplomacia | 7% |
| Inteligência Artificial | 8% |
| Multiplayer / networking | 9% |
| Persistência / save / replay | 6% |
| Renderização 3D / interação / UI | 10% |
| Performance / QA / release | 6% |
| **TOTAL** | **100%** |

## 15.1 Decomposição de Topologia planetária / Goldberg

O orçamento congelado da área é:

**90 GPP**

A decomposição operacional inicial de M2 é:

| Capability | GPP |
|---|---:|
| Goldberg parameterization e geração estratégica | 16 |
| Strategic graph: identidade, incidência e adjacência | 12 |
| Tactical region topology | 14 |
| Shared subtile border bands | 16 |
| Strategic ↔ tactical hierarchy/refinement mapping | 16 |
| Canonical deterministic topology generation | 6 |
| Topological validation e navigability | 6 |
| Scalability / headless performance baseline | 4 |
| **TOTAL** | **90** |

Estado de maturidade após o fechamento formal de M2:

| Capability | GPP | Maturidade | Fator | GPP ganhos |
|---|---:|---|---:|---:|
| Goldberg parameterization e geração estratégica | 16 | Validada | 0.85 | 13.60 |
| Strategic graph: identidade, incidência e adjacência | 12 | Validada | 0.85 | 10.20 |
| Tactical region topology | 14 | Validada | 0.85 | 11.90 |
| Shared subtile border bands | 16 | Validada | 0.85 | 13.60 |
| Strategic ↔ tactical hierarchy/refinement mapping | 16 | Validada | 0.85 | 13.60 |
| Canonical deterministic topology generation | 6 | Validada | 0.85 | 5.10 |
| Topological validation e navigability | 6 | Validada | 0.85 | 5.10 |
| Scalability / headless performance baseline | 4 | Validada | 0.85 | 3.40 |
| **TOTAL** | **90** |  |  | **76.50** |

O fechamento de M2 promove somente capabilities que agora possuem evidência acumulada de testes, edge cases, performance mínima aplicável e regressão cross-platform. Nenhuma capability de Topologia planetária / Goldberg é promovida a fator `1.00` neste milestone: o fator `1.00` permanece reservado à Definition of Done do V1, incluindo decisões finais de escala e integração com consumidores posteriores.

Promoções de M2.5.5:

- `Goldberg parameterization e geração estratégica`: `0.50 -> 0.85`, `+5.60 GPP`;
- `Strategic graph: identidade, incidência e adjacência`: `0.50 -> 0.85`, `+4.20 GPP`;
- `Strategic ↔ tactical hierarchy/refinement mapping`: `0.70 -> 0.85`, `+2.40 GPP`;
- `Canonical deterministic topology generation`: `0.00 -> 0.85`, `+5.10 GPP`;
- `Topological validation e navigability`: `0.70 -> 0.85`, `+0.90 GPP`;
- `Scalability / headless performance baseline`: `0.00 -> 0.85`, `+3.40 GPP`.

GPP adicional no fechamento de M2:

**+21.60 GPP**

GPP global após M2:

**146.50 / 1000**

Global Progress:

**14.7%**

Topologia planetária / Goldberg:

**76.50 / 90 GPP — 85.0%**

M2.1.1 foi um audit arquitetural e não promoveu maturidade por si só.

M2.1.2 promoveu `Goldberg parameterization e geração estratégica` para `Especificada — fator 0.20` porque o contrato executável de parâmetros, domínio válido, fórmulas, contagens esperadas e vetores de referência foi congelado e validado cross-platform.

M2.1.3 fechou o subcontrato de identidade, sem promover isoladamente a capability agregada de strategic graph.

M2.1.4 materializou a primeira topologia estratégica completa em `G(1,0)`, com entidades, identidades, incidência e adjacência funcionais, reciprocidade, conectividade e invariantes topológicos validados.

Por isso, `Strategic graph: identidade, incidência e adjacência` passa diretamente para `Implementação funcional isolada — fator 0.50`.

M2.1.5.A generalizou a geração para a família Class I `G(m,0)` / `G(0,n)` e validou `G(2,0)`, `G(0,2)` e `G(3,0)` de forma determinística e cross-platform.

M2.1.5.B adicionou suporte determinístico à família Class II `G(k,k)` e validou `G(1,1)` e `G(2,2)` cross-platform, preservando a família Class I.

M2.1.5.C adicionou a família Class III para parâmetros positivos desiguais e validou `G(2,1)`, `G(1,2)`, `G(3,1)` e `G(3,2)`, incluindo orientação/chiralidade, determinismo e invariantes topológicos.

Com as três classes icosaédricas cobertas por implementação funcional isolada e validação cross-platform representativa, M2.1.5 é encerrado e `Goldberg parameterization e geração estratégica` é promovida para `Implementação funcional isolada — fator 0.50`.

M2.2.1 congelou e implementou o contrato executável de identidade e invariantes locais para `TacticalRegion` e `TacticalCell`, com validação cross-platform.

Por isso, `Tactical region topology` foi promovida de `Inexistente — fator 0.00` para `Especificada — fator 0.20`.

M2.2.2 materializou o primeiro grafo tático local por meio de `MinimalTacticalRegionGraphGenerator`, com reference graph canônico `1 <-> 2 <-> 3`, invariantes estruturais preservados e geração repetida determinística validada cross-platform.

Por isso, `Tactical region topology` foi promovida de `Especificada — fator 0.20` para `Implementação funcional isolada — fator 0.50`.

M2.2.3 integrou a topologia tática à topologia estratégica por meio de `StrategicTacticalRegionMaterializer`, materializando exatamente uma `TacticalRegion` por `StrategicCell`, preservando ordem canônica, identidade pai-filho e determinismo em casos representativos Class I, Class II e Class III.

Por isso, `Tactical region topology` foi promovida de `Implementação funcional isolada — fator 0.50` para `Integrada ao sistema — fator 0.70`.

M2.2.4 fechou as lacunas de validação acumulada sem alterar production code: snapshot/read-only semantics foram provadas diretamente, variantes Goldberg adicionais foram materializadas, `G(3,2)` validou 192 regiões e 576 identidades táticas únicas, e duas materializações completas reproduziram a mesma assinatura canônica.

Com 241/241 testes locais e regressão cross-platform aprovada em Ubuntu, Windows e macOS, `Tactical region topology` é promovida de `Integrada ao sistema — fator 0.70` para `Validada — fator 0.85`.

M2.2 é encerrado sem antecipar shared border bands, ownership multi-região ou conectividade tática cross-region, que permanecem para M2.3.

M2.3.1 congelou o contrato de identidade e ownership de fronteira compartilhada: um `SharedBorderBand` lógico por `StrategicEdge`, identidade de elemento `StrategicEdgeId + LocalOrdinal`, incidência regional derivada da edge e preservação de `TacticalCellId` como region-owned.

M2.3.2 implementou `SharedBorderElementId`, `SharedBorderElement` e `SharedBorderBand`, com invariantes de edge válido, coleção não vazia, pertencimento ao mesmo edge, ordinais one-based contíguos, ordenação canônica e snapshot somente leitura. A suíte passou para 259/259 e a regressão cross-platform foi aprovada em Ubuntu, Windows e macOS.

Por isso, `Shared subtile border bands` é promovida de `Inexistente — fator 0.00` para `Especificada — fator 0.20`.

M2.3.3 adicionou `StrategicEdgeSharedBorderBandMaterializer`, materializando exatamente um `SharedBorderBand` por `StrategicEdge` em ordem canônica, usando um único `SharedBorderElement(edge.Id, 1)` de referência por band. Cobertura, identidade global, ordering, snapshot somente leitura e determinismo foram validados em Class I, Class II e Class III, com 271/271 testes locais e regressão cross-platform aprovada em Ubuntu, Windows e macOS.

Por isso, `Shared subtile border bands` é promovida de `Especificada — fator 0.20` para `Implementação funcional isolada — fator 0.50`.

M2.3.4 introduziu `StrategicTacticalBorderAggregate` e `SharedBorderIncidence`, validando conjuntamente `StrategicTopology`, `TacticalRegion` e `SharedBorderBand`. O aggregate exige cobertura exata de strategic cells e edges, canonicaliza as coleções pela topologia autoritativa e deriva exatamente duas regiões incidentes por `StrategicEdge.IncidentCellIds`, preservando snapshots somente leitura e determinismo.

Com 291/291 testes locais e regressão cross-platform aprovada em Ubuntu, Windows e macOS, `Shared subtile border bands` é promovida de `Implementação funcional isolada — fator 0.50` para `Integrada ao sistema — fator 0.70`.

M2.3.5 fechou a validação acumulada de shared borders sem alterar production code. O gate provou continuidade `StrategicCell.IncidentEdgeIds` ↔ derived incidences, graus 5/6 para pentágonos/hexágonos, handshake global, exatamente duas regions por incidence, unicidade e edge-locality de border elements, adjacency tática estritamente parent-local e determinismo de duas pipelines completas independentes.

A cobertura adicional validou `G(0,2)`, `G(1,2)`, `G(2,1)` e `G(3,1)`. Com 301/301 testes locais e regressão cross-platform aprovada em Ubuntu, Windows e macOS, `Shared subtile border bands` é promovida de `Integrada ao sistema — fator 0.70` para `Validada — fator 0.85`.

M2.3 é encerrado. `Strategic ↔ tactical hierarchy/refinement mapping` permanece sem promoção porque mapping físico, traversal tático cross-region e universal Goldberg refinement continuam abertos.

M2.4 estabelece scaled refinement compatibility, representative multi-family regression coverage e first-pair provenance/edge-chain continuity. O stage fecha após M2.4.5 com 366/366 testes locais e regressão cross-platform aprovada em Ubuntu, Windows e macOS no run `35504415048`.

A capability `Strategic ↔ tactical hierarchy/refinement mapping` permanece em `Inexistente — fator 0.00`, porque multi-family durable lineage, full parent-child ownership e physical tactical-border mapping continuam ausentes. Esses requisitos seguem para M2.5 e para o M2 exit gate.

O entry audit de M2.5 confirmou que o milestone não pode ser encerrado por uma regressão de performance isolada. O exit gate ainda exige physical boundary attachment, cross-region navigability, decisão explícita sobre hierarchy/refinement coverage e um benchmark headless quantitativo sobre o workload final aceito para M2.

M2.5.1 congela os requisitos do exit gate, a decomposição operacional de M2.5 e o primeiro budget quantitativo de M2 topology baseline. Nenhuma maturity capability é promovida por esse design.

M2.5.2-B introduz a identidade física global e o contrato de incidência coarse 1/2/3. M2.5.2-C materializa integralmente o primeiro target edge-and-vertex-aware `G(1,0) -> G(6,0)`, cobrindo 362/362 tiles físicos com assinatura 312 interior / 30 edge-shared / 20 vertex-shared, incluindo cobertura exata de todos os coarse edges e vertices. Por existir agora uma implementação física funcional, porém ainda restrita ao reference target e sem traversal/family coverage universal, `Strategic ↔ tactical hierarchy/refinement mapping` é promovida de `Inexistente — fator 0.00` para `Implementação funcional isolada — fator 0.50`.

M2.5.2-D implementa traversal determinístico de menor caminho diretamente sobre a adjacency autoritativa da fine topology. M2.5.2-E fecha a validação acumulada do reference target sem alterar production code: confirma `362/362` tiles, assinatura `312/30/20`, cobertura `30/30` de coarse edges, cobertura `20/20` de coarse vertices, reciprocidade das `1080` fine edges e valida exaustivamente os `65.703/65.703` pares de shortest path contra uma BFS independente. Como mapping, incidence e navigability agora operam em conjunto no mesmo physical mesh e o contrato integrado foi exaustivamente provado no reference target, `Topological validation e navigability` é promovida de `Implementação funcional isolada — fator 0.50` para `Integrada ao sistema — fator 0.70`. A promoção para `Validada — 0.85` permanece condicionada ao gate cross-platform/final de M2.5.5.

M2.5.3-A prova que o suporte estratégico Class I/II/III, o contrato `GoldbergScaledRefinement` e o physical mapper possuem envelopes diferentes. M2.5.3-B congela o physical hierarchy oficial de M2 como Class I em scale 6, preservando Class II/III como suporte estratégico sem alegar physical lineage universal.

M2.5.3-C implementa durable Class I construction lineage usando as mesmas integer/combinatorial `SubdivisionLatticeVertexKey` da geração autoritativa. Cada coarse Class I cell recebe um fine anchor exato pela multiplicação da construction key pelo scale 6. O `PhysicalTacticalIncidenceMapper` passa a aceitar as formas oficiais `G(k,0) -> G(6k,0)` e `G(0,k) -> G(0,6k)`, deriva ownership por multi-source BFS sobre a adjacency da fine topology, valida toda incidência 2-way contra coarse edges e toda incidência 3-way contra coarse vertices e aplica a assinatura generalizada congelada. A implementação preserva o reference target e valida `k=1,2,3`, incluindo o eixo Class I invertido, sem floating-point ownership, sem igualdade de ordinal local como lineage e sem adjacency cross-region sintética.

Como durable lineage, authoritative topology generation, physical incidence materialization e fine-topology traversal agora operam conjuntamente dentro do physical hierarchy oficial, `Strategic ↔ tactical hierarchy/refinement mapping` é promovida de `Implementação funcional isolada — fator 0.50 — 8.00 GPP` para `Integrada ao sistema — fator 0.70 — 11.20 GPP`.

M2.5.3-D fecha a validação acumulada do physical hierarchy oficial sem alterar production code. O gate valida `k=1..4` nos dois eixos Class I, incluindo `k=4` como caso fora da amostra de implementação; confirma as fórmulas generalizadas, cobertura exata de coarse edges e vertices, cobertura integral das fine tiles, determinismo repetido em `k=4`, traversal cross-owner sobre a fine topology autoritativa e rejeição de scale incorreto, Class II e Class III no physical mapper.

M2.5.3 está encerrado. A capability `Strategic ↔ tactical hierarchy/refinement mapping` permanece em `Integrada ao sistema — fator 0.70` neste fechamento porque o nível `Validada — fator 0.85` exige também performance mínima e regressão de saída aplicável. Esses gates pertencem a M2.5.4 e M2.5.5.

M2.5.5 fecha o exit gate acumulado de M2. A evidência read-only confirma `435/435` testes no HEAD atual, permanent benchmark harness aprovado, os quatro workloads bloqueantes dentro dos budgets, stress topology correctness preservada e regressão cross-platform de `435/435` em Ubuntu, Windows e macOS no run `36557455921`. Entre o commit cross-platform validado `296977c9f0260c7ddaa01efa50319337178fb046` e o baseline final de M2 não existe drift em código de produção, testes, benchmark ou workflow cross-platform.

Os 10 requisitos de saída congelados em ADR-020 estão atendidos: execução headless, determinismo, fechamento topológico, navegabilidade, semântica pai-filho, attachment físico de fronteira, traversal cross-region, scope oficial de refinement/famílias explícito, evidência quantitativa de escalabilidade e regressão cross-platform.

M2 é encerrado em `Validada — fator 0.85` para a área de Topologia planetária / Goldberg. O `RISK-003 — Goldberg hierarchy mapping` passa de `MITIGATING` para `WATCHING`; `RISK-002 — Tactical resolution scalability` e `RISK-012 — Memory footprint` permanecem abertos porque o tamanho final do produto e a densidade tática final ainda dependem de validação posterior.

Esses pesos constituem o baseline inicial.

Novas tarefas descobertas dentro de uma área não aumentam automaticamente o peso do V1.

Alterações relevantes de escopo exigem decisão explícita.

---

# 16. Modelo de maturidade

Cada capability será avaliada aproximadamente pelos seguintes níveis:

0% — inexistente
20% — especificação e contratos definidos
50% — implementação funcional isolada
70% — integrada ao sistema
85% — testes, edge cases e performance mínima atendidos
100% — Definition of Done do V1 atendida

Progresso global = soma da maturidade ponderada de cada domínio.

---

# 17. Indicadores do projeto

Acompanharemos separadamente:

**Progress**
Quanto do V1 efetivamente foi concluído.

**Scope Confidence**
Quanto do caminho restante é conhecido com confiança.

**Technical Risk**
Quantidade e impacto de incógnitas técnicas.

**Current Milestone**
Marco atual de execução.

**Critical Path**
Subsistema que atualmente condiciona mais o avanço global.

---

# 18. Regra de escopo

Toda nova ideia deverá receber uma classificação:

- V1 REQUIRED
- V1 OPTIONAL
- POST-V1
- EXPERIMENTAL

Funcionalidades não entram silenciosamente no escopo do V1.

Mudanças relevantes exigem registro explícito.

---

# 19. Milestones iniciais

## M0 — Project Baseline

Objetivo:
criar fundação técnica e gerencial.

Critérios:

- Project Compass criado;
- escopo V1 definido;
- arquitetura inicial registrada;
- ADRs fundamentais registrados;
- Risk Register criado;
- solução modular criada;
- infraestrutura de testes criada;
- kernel mínimo compilando.

---

## M1 — Deterministic Simulation Kernel

**Status:** concluído em 2026-09-19

Objetivo:
validar o modelo fundamental da simulação.

Inclui:

- IDs;
- WorldState mínimo;
- Commands;
- Events;
- seeds;
- RNG determinístico;
- turn policy;
- resolução sequencial;
- event log;
- testes de determinismo;
- simulações automatizadas.

---

## M2 — Planet Topology

**Status: concluído em 2026-09-29**

Objetivo:
estabelecer a representação lógica do planeta.

Inclui:

- geração Goldberg;
- células estratégicas;
- arestas;
- malha tática;
- tiles compartilhados;
- adjacência;
- pertencimento;
- testes topológicos.

---

## M3 — Procedural World

**Status: concluído em 2026-09-30**

Objetivo:
gerar um planeta jogável procedimentalmente.

Inclui:

- elevação;
- água;
- clima;
- biomas;
- recursos;
- habitabilidade;
- spawn inicial de civilizações.

---

## M4 — Systemic Vertical Slice

**Status: atual desde 2026-09-30**

Objetivo:
primeiro ciclo sistêmico completo.

Deverá ser possível:

- gerar planeta;
- criar civilizações;
- produzir recursos;
- estabelecer comércio;
- emitir ordens;
- movimentar unidades;
- combater;
- alterar controle territorial;
- impactar rotas econômicas;
- avançar turnos.

Ainda sem necessidade de apresentação final.

---

## M5 — Playable Client

Objetivo:
representar e controlar a vertical slice através da Unity.

Inclui:

- planeta 3D;
- câmera;
- seleção;
- UI operacional;
- visualização de estados;
- interação.

---

## M6 — Multiplayer

Objetivo:
executar a mesma simulação através de autoridade remota.

---

## M7 — Alpha

Objetivo:
todos os sistemas fundamentais do V1 presentes e integrados.

---

## M8 — Beta

Objetivo:
feature complete para V1.

Foco:
performance, balanceamento, UX, estabilidade e conteúdo.

---

## V1.0

Critérios definidos em documento próprio.

---

## 15.2 M2.5.4 performance-scale envelope

M2.5.4 separates three distinct concerns:

- semantic support — what the topology model can represent correctly;
- product acceptance — what M2 must execute inside the blocking performance budget;
- engineering stress — larger valid workloads used to measure robustness headroom without defining final V1 size.

Current product-scale hypotheses are intentionally provisional:

- strategic Goldberg size is expected to remain around `m+n <= 15`;
- tactical density may reach roughly 12 rings in some contexts and normally less;
- neither value is a frozen V1 contract;
- final product scale depends on later visual and gameplay validation.

The two provisional maxima are not independent. A large strategic topology combined with a dense tactical realization can multiply the physical tile count dramatically. M2.5.4 therefore owns a combined scale envelope rather than two unrelated maxima.

The official Class I scale-6 hierarchy remains the M2 semantic/refinement contract. It is not a declaration that scale 6 is the final tactical density of the game.

Measured evidence shows the current Class I physical pipeline scales approximately linearly in managed allocation per fine tile, but the constant allocation cost is high. `G(16,0) -> G(96,0)` is retained as a stress-baseline candidate rather than a product-performance requirement.

The blocking M2.5.4 benchmark contract is revised to use:

- strategic/logical Class I: `G(15,0)`;
- strategic/logical Class II: `G(7,7)`;
- strategic/logical Class III: `G(14,1)`;
- physical Class I semantic acceptance: `G(4,0) -> G(24,0)`, scale 6.

The historical full physical `G(16,0) -> G(96,0)` workload remains a non-blocking stress case. Topology correctness still applies; only its performance numbers are non-blocking.

The hard M2.5.1 thresholds themselves remain unchanged.

## 15.3 Decomposição de World Generation / biomas / recursos

O orçamento congelado da área permanece:

**100 GPP**

M3.1-A congela a decomposição operacional sem alterar o baseline global de `1000 GPP`:

| Capability | GPP | M3 stage owner |
|---|---:|---|
| World generation identity, seed/versioning & deterministic pipeline contracts | 10 | M3.1 |
| Strategic geometry bridge & macro physical-field substrate | 12 | M3.2 |
| Elevation, relief & land/water foundation | 14 | M3.2 |
| Temperature, climate, moisture & water availability | 14 | M3.3 |
| Cross-scale boundary conditions, tactical refinement & strategic aggregation | 14 | M3.3 |
| Hydrology | 12 | M3.4 |
| Derived biomes | 8 | M3.4 |
| Resources | 7 | M3.5 |
| Habitability & civilization-placement suitability | 5 | M3.5 |
| Determinism, cross-platform validation, performance baseline & M3 exit | 4 | M3.6 |
| **TOTAL** | **100** |  |

Current M3 maturity after closed subcheckpoints:

| Capability | GPP | Maturity | Factor | GPP earned |
|---|---:|---|---:|---:|
| World generation identity, seed/versioning & deterministic pipeline contracts | 10 | Validada | 0.85 | 8.50 |
| Strategic geometry bridge & macro physical-field substrate | 12 | Validada | 0.85 | 10.20 |
| Elevation, relief & land/water foundation | 14 | Validada | 0.85 | 11.90 |
| Temperature, climate, moisture & water availability | 14 | Validada | 0.85 | 11.90 |
| Cross-scale boundary conditions, tactical refinement & strategic aggregation | 14 | Validada | 0.85 | 11.90 |
| Hydrology | 12 | Validada | 0.85 | 10.20 |
| Derived biomes | 8 | Validada | 0.85 | 6.80 |
| Resources | 7 | Validada | 0.85 | 5.95 |
| Habitability & civilization-placement suitability | 5 | Validada | 0.85 | 4.25 |
| Determinism, cross-platform validation, performance baseline & M3 exit | 4 | Validada | 0.85 | 3.40 |
| **TOTAL** | **100** |  |  | **85.00** |

M3.1-A is a governance/design freeze. It does not promote implementation maturity and therefore awards no GPP.

M3.1-B materializes the executable seed/version/request/result/interface boundary and promotes `World generation identity, seed/versioning & deterministic pipeline contracts` to `Especificada — fator 0.20`, adding `2.00 GPP`.

M3.1-C adds the concrete deterministic generator, explicit supported generation version, domain-separated random streams and a fixed deterministic regression vector. This promotes the same capability to `Funcional isoladamente — fator 0.50`, adding another `3.00 GPP`.

M3.1-D closes accumulated validation without production-code mutation. The accepted gate revalidates the `475/475` Release suite locally, validates the `40/40` M3.1-specific slice and consumes cross-platform run `36623662001`, where the same accepted tree passed `475/475` on Ubuntu, Windows and macOS with zero compiler warnings/errors and artifacts uploaded on all three platforms. The capability is therefore promoted to `Validada — fator 0.85`, adding `3.50 GPP`.

M3.1 closes at `8.50 / 10 GPP`. The remaining `1.50 GPP` is not awarded merely for stage completion; `100%` remains reserved for V1 Definition of Done evidence.

Stage allocation:

- M3.1 — `10 GPP`;
- M3.2 — `26 GPP`;
- M3.3 — `28 GPP`;
- M3.4 — `20 GPP`;
- M3.5 — `12 GPP`;
- M3.6 — `4 GPP`.

The decomposition redistributes the already approved `100 GPP` World Generation budget and is not a Scope Change.

### 15.4 M3.2 strategic geometry and physical-field decomposition

M3.2 consumes the authoritative M2 `StrategicTopology` without duplicating Goldberg generation.

The stage is decomposed into:

- M3.2-A — Strategic Geometry & Physical-Field Contract Freeze;
- M3.2-B — Executable Strategic Surface Graph & Scalar Field Substrate;
- M3.2-C — Deterministic Elevation, Relief & Land/Water Foundation;
- M3.2-D — Accumulated M3.2 Validation & Close.

Frozen M3.2 data direction:

`StrategicTopology`
→ `StrategicSurfaceGraph`
→ `StrategicScalarField`
→ deterministic elevation
→ derived relief
→ explicit sea-level classification
→ land/water foundation

The strategic surface graph is a derived data-oriented view of M2 topology, not a second topology source.

The initial macro scalar substrate uses signed `Int64` fixed-point raw values with denominator `1_000_000`.

No tactical global materialization is introduced by M3.2-A.

No GPP is awarded by the design freeze. M3.2-B subsequently materializes the executable `StrategicSurfaceGraph`, immutable `StrategicScalarField` and generation-result integration. This promotes `Strategic geometry bridge & macro physical-field substrate` to `Funcional isoladamente — fator 0.50`, adding `6.00 GPP`.

M3.2-C adds deterministic fixed-point strategic elevation, derived relief, explicit sea-level classification and integrated land/water fields. This promotes `Elevation, relief & land/water foundation` to `Funcional isoladamente — fator 0.50`, adding `7.00 GPP`.

M3.2-D closes accumulated validation without production-code mutation. The accepted gate revalidates the `64/64` M3.2-specific slice and the `539/539` full Release suite locally, and consumes cross-platform run `36710766790`, where the same accepted tree passed `539/539` on Windows, macOS and Ubuntu with zero compiler warnings/errors and artifacts uploaded on all three platforms.

Both M3.2 capabilities are therefore promoted to `Validada — fator 0.85`. M3.2 closes at `22.10 / 26 GPP — 85.0%`. The remaining `3.90 GPP` is not awarded merely for stage completion; factor `1.00` remains reserved for V1 Definition of Done evidence.
### 15.5 M3.3 climate and cross-scale physical decomposition

M3.3 owns the remaining pre-hydrology climate and cross-scale physical foundation:

- `Temperature, climate, moisture & water availability` — `14 GPP`;
- `Cross-scale boundary conditions, tactical refinement & strategic aggregation` — `14 GPP`.

Operational decomposition:

- M3.3-A — Climate & Cross-Scale Physical Contract Freeze;
- M3.3-B — Executable Strategic Temperature, Moisture & Water Availability;
- M3.3-C — Bounded Tactical Refinement & Strategic Aggregation;
- M3.3-D — Accumulated M3.3 Validation & Close.

Frozen direction:

- strategic temperature uses only the `Temperature` world-generation random domain;
- strategic moisture uses only the `Moisture` world-generation random domain;
- water availability is derived and receives no independent random domain;
- physical tactical identity reuses M2 `PhysicalTacticalTileId` / physical hierarchy contracts;
- logical `TacticalCellId` is not promoted into physical high-resolution identity;
- tactical physical fields are bounded/on-demand rather than globally resident by default;
- stochastic tactical residuals belong only to `TacticalRefinement` and must be order-independent by stable scope identity;
- strategic boundary conditions are explicit;
- strategic aggregation is deterministic and canonical;
- hydrology, biomes and resources remain later stages.

M3.3-A is a governance/design freeze and awards no GPP.

M3.3-B materializes the strategic climate layer with deterministic fixed-point temperature, deterministic fixed-point moisture, derived water availability and `WorldGenerationResult` integration. This promotes `Temperature, climate, moisture & water availability` to `Funcional isoladamente — fator 0.50`, adding `7.00 GPP`.

M3.3-C materializes a bounded retained tactical scalar patch using M2 physical identity, explicit deterministic incident-strategic boundary values and deterministic strategic aggregation with explicit `6/3/2` interior/edge/vertex weights. This promotes `Cross-scale boundary conditions, tactical refinement & strategic aggregation` to `Funcional isoladamente — fator 0.50`, adding `7.00 GPP`.

The retained patch does not keep the global M2 incidence map, but the current construction path still materializes that map transiently before projection. `RISK-012` and `RISK-002` therefore remain open; this close must not be interpreted as final tactical-memory or product-scale validation.
M3.3-D closes accumulated validation without production-code mutation. The accepted gate revalidates the `70/70` M3.3-specific slice and the `609/609` full Release suite locally, consumes cross-platform run `36724972469` with `609/609` on Windows, macOS and Ubuntu, and accepts the bounded-path performance probe against the existing M2 blocking physical budget.

Both M3.3 capabilities are therefore promoted to `Validada — fator 0.85`. M3.3 closes at `23.80 / 28 GPP — 85.0%`. The remaining `4.20 GPP` is not awarded merely for stage completion; factor `1.00` remains reserved for V1 Definition of Done evidence.

The retained patch is bounded, but transient global M2 physical incidence materialization remains an acknowledged construction-memory/scalability limitation and keeps `RISK-012` and `RISK-002` open.
### 15.6 M3.4 hydrology and derived biome decomposition

M3.4 owns:

- `Hydrology` — `12 GPP`;
- `Derived biomes` — `8 GPP`.

Operational decomposition:

- M3.4-A — Hydrology & Derived Biome Contract Freeze;
- M3.4-B — Executable Strategic Drainage & Flow Accumulation;
- M3.4-C — Executable Derived Strategic Biome Classification;
- M3.4-D — Accumulated M3.4 Validation & Close.

Frozen direction:

- hydrology is derived from accepted strategic terrain and water availability;
- the baseline hydrology path consumes no `Hydrology` RNG;
- land drainage may target only a strictly lower adjacent strategic cell;
- lowest elevation wins, then canonical node index resolves ties;
- water nodes are terminal outlets;
- land nodes without a lower neighbor are explicit inland sink/basin terminals;
- strict elevation descent makes the drainage graph acyclic by construction;
- land runoff derives from strategic water availability;
- flow accumulation is deterministic integer/fixed-point arithmetic;
- biomes remain derived classifications;
- water classification has precedence;
- terrestrial classification uses explicit fixed-point thresholds and deterministic precedence;
- the baseline biome classifier consumes no `Biomes` RNG;
- tactical hydrology is not globally resident by default;
- resources and habitability remain later stages.

M3.4-A is a governance/design freeze and awards no GPP.

Project progress therefore remains `200.90 / 1000 — 20.1%` until executable M3.4 evidence is accepted.
M3.4-B adds executable strategic drainage and flow accumulation. Land nodes drain only to strictly lower adjacent elevation; lowest elevation wins and canonical node index breaks ties. Water cells are terminal outlets, local land minima are explicit inland sinks, and strict descent excludes cycles by construction.

Local land runoff is the accepted strategic water-availability value. Water nodes add no new local runoff but receive upstream accumulation. Accumulation propagates in descending elevation order using deterministic integer arithmetic. The baseline consumes no Hydrology random stream.

This promotes `Hydrology` to `Funcional isoladamente — fator 0.50`, adding `6.00 GPP`. `Derived biomes` remains at `0.00` until executable M3.4-C evidence is accepted.
M3.4-C adds deterministic derived strategic biome classification over the accepted physical, climate and hydrology fields. The executable taxonomy is finite and table-driven with fixed-point thresholds and explicit precedence.

Water classification has precedence. Terrestrial ordering is `PolarIce`, `Tundra`, `Highland`, `Wetland`, `Desert`, `Rainforest`, `Forest`, then `Grassland`. Wetland classification composes accepted hydrology through `InlandSink`, water availability and flow accumulation. No Biomes random stream is consumed.

This promotes `Derived biomes` to `Funcional isoladamente — fator 0.50`, adding `4.00 GPP`.
M3.4-D closes accumulated validation without production-code mutation. The accepted gate revalidates the `44/44` M3.4-specific slice and the `653/653` full Release suite locally, consumes cross-platform run `36742871415` with `653/653` on Windows, macOS and Ubuntu, and confirms zero compiler warnings/errors with artifacts uploaded on all three platforms.

Hydrology and Derived biomes are therefore promoted to `Validada — fator 0.85`. M3.4 closes at `17.00 / 20 GPP — 85.0%`. The remaining `3.00 GPP` is reserved for later V1 Definition of Done evidence.
### 15.7 M3.5 resources, habitability and civilization-placement decomposition

M3.5 owns:

- `Resources` — `7 GPP`;
- `Habitability & civilization-placement suitability` — `5 GPP`.

Operational decomposition:

- M3.5-A — Resources, Habitability & Placement Policy Freeze;
- M3.5-B — Executable Strategic Resource Potential;
- M3.5-C — Executable Habitability & Civilization Placement Suitability;
- M3.5-D — Accumulated M3.5 Validation & Close.

Frozen direction:

- generated resource potential belongs to World Generation, not Economy runtime;
- resource potential remains strategic-first and may be represented by one or more compact strategic channels;
- final resource catalogue, abundance curves and tactical deposit geometry remain open;
- habitability is a derived environmental suitability signal distinct from resource richness;
- civilization placement is a separate layer from habitability;
- placement must be able to balance local suitability and separation from already selected starts;
- high suitability is a tendency rather than an absolute highest-score requirement;
- exact placement algorithm, first-reference policy, distance metric and quality-versus-distance weights remain open;
- water eligibility, water weight, coast/island handling and land-water transition costs remain open;
- tunable weights/thresholds must be centralized behind a coherent versioned calibration boundary;
- `Resources`, `Habitability` and `CivilizationPlacement` random domains remain distinct; consumption is optional, but any stochastic residual must use only its owned domain;
- M3.5 remains strategic-first and does not require global tactical pathfinding for placement;
- candidate/suitability generation does not instantiate Civilization runtime state.

M3.5-A is a governance/design freeze and awards no GPP.

Project progress remains `217.90 / 1000 — 21.8%` and World Generation remains `71.40 / 100 — 71.4%` until executable M3.5 evidence is accepted.
M3.5-B adds a deterministic strategic resource-potential substrate. The executable baseline exposes one normalized `GeneralPotential` scalar per strategic node, generated exclusively from the `Resources` world-generation domain and integrated into `WorldGenerationResult`.

This promotes `Resources` to `Funcional isoladamente — fator 0.50`, adding `3.50 GPP`. The final resource catalogue, abundance curves, physical-field coefficients and Economy runtime semantics remain deliberately unfrozen.
M3.5-C adds executable strategic habitability and civilization-placement suitability while keeping placement policy explicitly calibratable. Environmental habitability, local placement suitability and multi-start dispersion remain separate concerns.

The baseline selector uses a deterministic `CivilizationPlacement` reference and strategic unweighted graph distance, but water treatment, transition costs, quality-versus-dispersion weights, initial-reference behavior and civilization count remain deliberately open behind versioned policies.

This promotes `Habitability & civilization-placement suitability` to `Funcional isoladamente — fator 0.50`, adding `2.50 GPP`.
M3.5-D closes accumulated resources, habitability and civilization-placement validation. The accepted ReadOnly gate revalidates `43/43` M3.5-specific tests and the `696/696` Release suite locally, and consumes cross-platform run `36759069345`, where the same accepted source tree passed `696/696` on Windows, Ubuntu and macOS with zero compiler warnings/errors and artifacts uploaded on all three platforms.

`Resources` and `Habitability & civilization-placement suitability` are therefore promoted to `Validada — fator 0.85`. M3.5 closes at `10.20 / 12 GPP — 85.0%`; the remaining `1.80 GPP` is reserved for later V1 Definition of Done evidence.
### 15.8 M3.6 deterministic exit, signature and performance decomposition

M3.6 owns the final `4 GPP` World Generation capability:

`Determinism, cross-platform validation, performance baseline & M3 exit`.

Operational decomposition:

- M3.6-A — World Generation Exit, Signature & Performance Contract Freeze;
- M3.6-B — Canonical Generated-World Signature & Regression Vectors;
- M3.6-C — World Generation Performance & Memory Acceptance;
- M3.6-D — Accumulated Cross-Platform M3 Exit Validation & Close.

Frozen direction:

- M3.6 adds proof/acceptance infrastructure, not another gameplay layer;
- the generated world receives an explicit canonical SHA-256 signature;
- signature-format versioning is separate from `WorldGenerationVersion`;
- canonical serialization is explicit binary, big-endian, framed and order-stable;
- the signature covers request identity plus every authoritative strategic layer resident in `WorldGenerationResult`;
- Class I/II/III known regression vectors must match on Windows, Ubuntu and macOS;
- complete `IWorldGenerator.Generate(request)` is the M3 performance workload;
- benchmark methodology follows the established M2 discipline;
- exact numeric time/allocation thresholds require a separate ReadOnly observational calibration before M3.6-C acceptance;
- hosted CI timing is non-blocking, while cross-platform digest correctness is blocking;
- the final M3.6-D gate must be ReadOnly over production code.

Planned maturity progression:

- M3.6-A: no GPP;
- M3.6-B: `Funcional isoladamente — 0.50 — 2.00 GPP`;
- M3.6-C: `Integrada — 0.70 — 2.80 GPP`;
- M3.6-D: `Validada — 0.85 — 3.40 GPP`.

Projected M3 close after M3.6-D:

- World Generation: `85.00 / 100 GPP — 85.0%`;
- M3.6 V1 DoD reserve: `0.60 GPP`.

M3.6-A awards no GPP.

Project progress remains `228.10 / 1000 — 22.8%` and World Generation remains `81.60 / 100 — 81.6%` until executable M3.6 evidence is accepted.

## 15.9 M4 Systemic Vertical Slice decomposition

M4 does not create a new GPP domain.

It operates across already frozen V1 budgets:

- Civilizations / Diplomacy: `70 GPP`;
- Economy / comércio / logística: `140 GPP`;
- Warfare / unidades / combate: `140 GPP`.

ADR-029 freezes the full operational decomposition of those three domains and identifies the M4-owned vertical-slice tranche.

### M4-owned tranche

| Domain | Existing V1 budget | M4-owned tranche |
|---|---:|---:|
| Civilizations / Diplomacy | 70 | 50 |
| Economy / comércio / logística | 140 | 96 |
| Warfare / unidades / combate | 140 | 108 |
| **TOTAL** | **350** | **254** |

The `254 GPP` is not additional scope. It is a cross-domain ownership view over existing baseline points.

M4.1-A is a governance/design freeze and awards:

**0.00 GPP**

No capability is promoted merely because its decomposition is documented.

Executable contracts and behavior are required before any capability reaches `Especificada` or above.

### Operational stages

- M4.1 — Systemic Runtime Contracts & Ownership Foundation;
- M4.2 — Runtime Civilizations, Territory & Baseline Diplomacy;
- M4.3 — Economic Points, Workforce, Production & Demand;
- M4.4 — Markets, Trade, Routes & Flow Allocation;
- M4.5 — Units, Orders & Strategic Movement;
- M4.6 — Combat, Control Transfer & Edge Blocking;
- M4.7 — End-to-End Turn Coupling & Systemic Vertical Slice;
- M4.8 — Accumulated Determinism, Performance, Cross-Platform Validation & M4 Exit.

### Slice rule

M4 proves breadth of systemic interaction before depth of content.

The first slice may use minimal runtime commodities and minimal deterministic combat semantics.

It does not freeze final:

- commodity catalogue;
- production/consumption coefficients;
- price/market-clearing model;
- unit catalogue;
- combat formula;
- morale/supply model;
- diplomacy depth;
- civilization-count formula;
- UI/presentation.


### M4 maturity after M4.1-B

M4.1-B promotes only the first executable state/identity contracts.

| Domain | Capability | GPP | Maturidade | Fator | GPP ganhos |
|---|---|---:|---|---:|---:|
| Civilizations / Diplomacy | Runtime civilization identity & roster | 8 | Especificada | 0.20 | 1.60 |
| Economy | Strategic stocks/inventories | 12 | Especificada | 0.20 | 2.40 |
| Warfare | Unit identity/ownership/state | 12 | Especificada | 0.20 | 2.40 |
| **TOTAL M4.1-B** |  | **32** |  |  | **6.40** |

No credit is assigned yet to:

- world-to-runtime civilization instantiation;
- territory/control;
- diplomacy;
- production/consumption;
- trade/routes;
- military orders;
- movement;
- combat.

The M4.1-B close therefore changes project progress by exactly:

**+6.40 GPP**


### M4 maturity after M4.1-C

M4.1-C integrates the first three runtime-state capabilities with generated-world identity and cross-domain invariants.

| Domain | Capability | GPP | Maturidade | Fator | GPP ganhos |
|---|---|---:|---|---:|---:|
| Civilizations / Diplomacy | Runtime civilization identity & roster | 8 | Funcional isoladamente | 0.50 | 4.00 |
| Economy | Strategic stocks/inventories | 12 | Funcional isoladamente | 0.50 | 6.00 |
| Warfare | Unit identity/ownership/state | 12 | Funcional isoladamente | 0.50 | 6.00 |
| **TOTAL M4.1-C** |  | **32** |  |  | **16.00** |

Increment over M4.1-B:

**+9.60 GPP**

No credit is assigned yet to:

- world-to-runtime civilization instantiation;
- territory/control;
- diplomacy;
- production/consumption;
- trade/routes;
- military orders;
- movement;
- combat.

M4.1 is closed after M4.1-C subject to post-commit cross-platform attestation of the accepted source tree.


### M4.2 contract freeze

M4.2 owns the executable Civilization/Diplomacy portion of the systemic vertical slice.

Operational decomposition:

- M4.2-A — Runtime Civilizations, Territory & Baseline Diplomacy Contract Freeze;
- M4.2-B — Deterministic Runtime Civilization Materialization;
- M4.2-C — Strategic Territory Control;
- M4.2-D — Baseline Diplomacy State;
- M4.2-E — Ownership-Link Integration, Accumulated Validation & M4.2 Close.

Frozen baseline direction:

- civilization count remains an explicit positive runtime input;
- the final civilization-count formula remains open;
- M3.5 placement suitability and selector remain the placement authority;
- runtime start cells must be strategic land;
- identities are assigned deterministically as `CivilizationId(1..N)` in selected-start order;
- each civilization initially controls exactly its own start cell;
- all other strategic cells begin unowned;
- territorial ownership is sparse and one-owner-per-cell;
- diplomacy vocabulary is `Enemy`, `Neutral`, `Ally`;
- diplomacy is symmetric in the baseline;
- Neutral is implicit/default;
- only non-neutral pair overrides need persistence;
- no city, population, tactical ownership, diplomacy AI, treaty depth, production, trade or combat behavior is introduced by the freeze.

M4.2-A awards:

**0.00 GPP**

Project progress therefore remains:

**247.50 / 1000 — 24.8%**

until executable M4.2 evidence is accepted.


### M4.2 maturity after M4.2-B

M4.2-B materializes deterministic runtime civilizations from the generated world.

| Capability | GPP | Maturidade | Fator | GPP ganhos |
|---|---:|---|---:|---:|
| Runtime civilization identity & roster | 8 | Funcional isoladamente | 0.50 | 4.00 |
| World-to-runtime civilization instantiation | 8 | Funcional isoladamente | 0.50 | 4.00 |
| Territory/control ownership state | 10 | Não existente | 0.00 | 0.00 |
| Baseline diplomacy relation state | 8 | Não existente | 0.00 | 0.00 |
| Economic/military ownership links | 6 | Não existente | 0.00 | 0.00 |
| Validation/observability/systemic integration | 10 | Não existente | 0.00 | 0.00 |
| **TOTAL M4.2 tranche** | **50** |  |  | **8.00** |

M4.2-B increment:

**+4.00 GPP**

Project:

**251.50 / 1000 — 25.2%**

Civilizations / Diplomacy:

**8.00 / 70 — 11.4%**

The next implementation target is explicit strategic territorial control. No territory or diplomacy GPP is credited by M4.2-B.


### M4.2 maturity after M4.2-C

M4.2-C makes strategic territorial control executable as sparse authoritative Runtime state.

| Capability | GPP | Maturidade | Fator | GPP ganhos |
|---|---:|---|---:|---:|
| Runtime civilization identity & roster | 8 | Funcional isoladamente | 0.50 | 4.00 |
| World-to-runtime civilization instantiation | 8 | Funcional isoladamente | 0.50 | 4.00 |
| Territory/control ownership state | 10 | Funcional isoladamente | 0.50 | 5.00 |
| Baseline diplomacy relation state | 8 | Não existente | 0.00 | 0.00 |
| Economic/military ownership links | 6 | Não existente | 0.00 | 0.00 |
| Validation/observability/systemic integration | 10 | Não existente | 0.00 | 0.00 |
| **TOTAL M4.2 tranche** | **50** |  |  | **13.00** |

M4.2-C increment:

**+5.00 GPP**

Project:

**256.50 / 1000 — 25.7%**

Civilizations / Diplomacy:

**13.00 / 70 — 18.6%**

The next implementation target is baseline diplomacy state. No diplomacy or later ownership-link GPP is credited by M4.2-C.


### M4.2 maturity after M4.2-D

M4.2-D makes the baseline symmetric diplomacy relation state executable as sparse authoritative Runtime state.

| Capability | GPP | Maturidade | Fator | GPP ganhos |
|---|---:|---|---:|---:|
| Runtime civilization identity & roster | 8 | Funcional isoladamente | 0.50 | 4.00 |
| World-to-runtime civilization instantiation | 8 | Funcional isoladamente | 0.50 | 4.00 |
| Territory/control ownership state | 10 | Funcional isoladamente | 0.50 | 5.00 |
| Baseline diplomacy relation state | 8 | Funcional isoladamente | 0.50 | 4.00 |
| Economic/military ownership links | 6 | Não existente | 0.00 | 0.00 |
| Validation/observability/systemic integration | 10 | Não existente | 0.00 | 0.00 |
| **TOTAL M4.2 tranche** | **50** |  |  | **17.00** |

M4.2-D increment:

**+4.00 GPP**

Project:

**260.50 / 1000 — 26.1%**

Civilizations / Diplomacy:

**17.00 / 70 — 24.3%**

The next implementation target is ownership-link integration and accumulated M4.2 validation. No ownership-link or systemic-integration GPP is credited by M4.2-D.


### M4.2 maturity after M4.2-E close

M4.2-E integrates the complete M4.2 runtime civilization slice with the existing Economy and Warfare ownership references without adding later gameplay behavior.

| Capability | GPP | Maturidade | Fator | GPP ganhos |
|---|---:|---|---:|---:|
| Runtime civilization identity & roster | 8 | Integrada ao sistema | 0.70 | 5.60 |
| World-to-runtime civilization instantiation | 8 | Integrada ao sistema | 0.70 | 5.60 |
| Territory/control ownership state | 10 | Integrada ao sistema | 0.70 | 7.00 |
| Baseline diplomacy relation state | 8 | Integrada ao sistema | 0.70 | 5.60 |
| Economic/military ownership links | 6 | Integrada ao sistema | 0.70 | 4.20 |
| Validation/observability/systemic integration | 10 | Integrada ao sistema | 0.70 | 7.00 |
| **TOTAL M4.2 tranche** | **50** |  |  | **35.00** |

M4.2-E increment:

**+18.00 GPP**

M4.2 closes at:

**35.00 / 50 — 70.0%**

Project:

**278.50 / 1000 — 27.9%**

Civilizations / Diplomacy:

**35.00 / 70 — 50.0%**

The `0.85` validated factor remains reserved for broader accumulated M4 validation and later consumer evidence.

Next implementation stage after post-commit cross-platform attestation:

**M4.3 — Economic Points, Workforce, Production & Demand**

### M4.3 contract baseline before implementation

M4.3 refines the Economy direction from persistent inventory semantics toward localized recurring economic flows.

The fundamental spatial abstraction is an economic point, not a mandatory city.

The three baseline activities are:

- Agriculture;
- Mining;
- Trade / Logistics.

Agriculture is based on diffuse productive potential.

Mining depends on concentrated deposits.

Trade / Logistics consumes infrastructure and workforce to raise transfer capacity and support large economic flows.

Production and demand are resolved as flows for each economic cycle. Goods are not required to survive as physical inventory into later cycles.

Financial return is the important persistent outcome of market activity.

The exact population-growth model, workforce-allocation UX, diminishing-return function, commodity catalogue, logistics-capacity function, market-clearing algorithm and price function remain open.

M4.3 decomposition:

- M4.3-A — Economic Points, Workforce, Production & Demand Contract Freeze;
- M4.3-B — Economic Point & Localized Workforce/Activity Substrate;
- M4.3-C — Deterministic Production & Demand Flow Resolution;
- M4.3-D — Accumulated Validation & M4.3 Close.

M4.4 is refined to:

**Markets, Trade, Routes & Flow Allocation**

Water-filling remains the leading allocation hypothesis for M4.4, while literal packet iteration is not frozen.

The `Commodity identity, economic points/locality & flow substrate` capability remains at functional-isolated factor `0.50`, contributing the previously confirmed `6.00 GPP`.

M4.3-C now provides deterministic isolated production/demand behavior through explicit fixed-point transient flow contracts.

The refined `Workforce, production & demand` capability has an `18 GPP` budget and advances to functional-isolated factor `0.50`:

**9.00 GPP**

M4.3-C awards:

**+9.00 GPP**

Project therefore becomes:

**287.50 / 1000 — 28.8%**

Economy / trade / logistics becomes:

**15.00 / 140 — 10.7%**

Production/demand flows remain transient and canonical WorldState hash format remains `7`.

### Runtime authority

`WorldGenerationResult` remains immutable procedural input.

Runtime mutable state belongs to `WorldState`.

Authoritative changes continue through:

`Command -> Event -> WorldState`

The existing `TurnResolver` orchestrates ordering and execution but does not own domain business logic.

### M4 exit target

M4 exit requires a deterministic headless scenario in which production/trade and warfare interact through explicit state/event contracts, a warfare-caused control/access change invalidates only affected economic route dependencies, economy recomputes the affected state, and the turn completes reproducibly.

Representative cross-platform regression and applicable performance/memory evidence remain mandatory before M4 exit.

# 20. Status atual

Milestone:

**M4 — Systemic Vertical Slice**

Etapa atual:

**M4 — Systemic Vertical Slice**

Subetapa atual:

**M4.4-A — Markets, Trade, Routes & Flow Allocation Contract Freeze**

M4.3 — Economic Points, Workforce, Production & Demand:

**concluído em 2026-10-06**

M4.3-D — Accumulated Validation & M4.3 Close:

**concluído em 2026-10-06**

M4.3-C — Deterministic Production & Demand Flow Resolution:

**concluído em 2026-10-05**

M4.3-B — Economic Point & Localized Workforce/Activity Substrate:

**concluído em 2026-10-02**

M4.3-A — Economic Points, Workforce, Production & Demand Contract Freeze:

**concluído em 2026-10-02**

M4.2 — Runtime Civilizations, Territory & Baseline Diplomacy:

**concluído em 2026-10-01**

M4.2-E — Ownership-Link Integration, Accumulated Validation & M4.2 Close:

**concluído em 2026-10-01**

M4.2-D — Baseline Diplomacy State:

**concluído em 2026-10-01**

M4.2-C — Strategic Territory Control:

**concluído em 2026-10-01**

M4.2-B — Deterministic Runtime Civilization Materialization:

**concluído em 2026-10-01**

M4.2-A — Runtime Civilizations, Territory & Baseline Diplomacy Contract Freeze:

**concluído em 2026-10-01**

M4.1-A — Systemic Vertical Slice Architecture, Domain Ownership & GPP Freeze:

**concluído em 2026-09-30**

M4.1-B — Executable Runtime State & Domain Identity Contracts:

**concluído em 2026-10-01**

M4.1-C — Runtime World Binding & Cross-Domain Invariant Contracts:

**concluído em 2026-10-01**

M4.1 — Systemic Runtime Contracts & Ownership Foundation:

**concluído em 2026-10-01**

M3 entry audit:

**concluído em 2026-09-29**

M3.1 — World Generation Contracts, Seed/Versioning & Pipeline:

**concluído em 2026-09-29**

M3.1-A — Architecture, Contract & GPP Design Freeze:

**concluído em 2026-09-29**

M3.1-B — Executable World Generation Identity & Pipeline Contracts:

**concluído em 2026-09-29**

M3.1-C — Deterministic Pipeline Skeleton & Contract Validation:

**concluído em 2026-09-29**

M3.1-D — Accumulated M3.1 Validation & Close:

**concluído em 2026-09-29**

M3.2 — Strategic Geometry Bridge, Elevation & Land/Water:

**concluído em 2026-09-30**

M3.2-D — Accumulated M3.2 Validation & Close:

**concluído em 2026-09-30**

M3.3 — Climate Inputs & Cross-Scale Physical Refinement:

**concluído em 2026-09-30**

M3.3-D — Accumulated M3.3 Validation & Close:

**concluído em 2026-09-30**

M3.4-A — Hydrology & Derived Biome Contract Freeze:

**concluído em 2026-09-30**

M3.4-B — Executable Strategic Drainage & Flow Accumulation:

**concluído em 2026-09-30**

M3.4-C — Executable Derived Strategic Biome Classification:

**concluído em 2026-09-30**

M3.4-D — Accumulated M3.4 Validation & Close:

**concluído em 2026-09-30**

M3.4 — Hydrology & Derived Biomes:

**concluído em 2026-09-30**

M3.5-A — Resources, Habitability & Placement Policy Freeze:

**concluído em 2026-09-30**

M3.5-B — Executable Strategic Resource Potential:

**concluído em 2026-09-30**

M3.5-C — Executable Habitability & Civilization Placement Suitability:

**concluído em 2026-09-30**

M3.5-D — Accumulated M3.5 Validation & Close:

**concluído em 2026-09-30**

M3.5 — Resources, Habitability & Civilization Placement Inputs:

**concluído em 2026-09-30**

M3.6-A — World Generation Exit, Signature & Performance Contract Freeze:

**concluído em 2026-09-30**

M3.6-B — Canonical Generated-World Signature & Regression Vectors:

**concluído em 2026-09-30**

M3.6-C — World Generation Performance & Memory Acceptance:

**concluído em 2026-09-30**

M3.6-D — Accumulated Cross-Platform M3 Exit Validation & Close:

**concluído em 2026-09-30**

M3.6 — Determinism, Performance, Cross-Platform Validation & M3 Exit:

**concluído em 2026-09-30**

M3 — Procedural World:

**concluído em 2026-09-30**

M2 — Planet Topology:

**concluído em 2026-09-29**

M1 — Deterministic Simulation Kernel:

**concluído em 2026-09-19**

M0 — Project Baseline:

**concluído em 2026-09-18**

Baseline V1:

**congelado em 2026-09-18**

Progresso oficial:

**29,4%**

GPP conquistados:

**293,50 / 1000**

Foundation / Simulation Kernel:

**70,00 / 70 GPP — 100,0%**

Topologia planetária / Goldberg:

**76,50 / 90 GPP — 85,0%**

World Generation / biomas / recursos:

**85,00 / 100 GPP — 85,0%**

Economia / comércio / logística:

**21,00 / 140 GPP — 15,0%**

Warfare / unidades / combate:

**6,00 / 140 GPP — 4,3%**

Civilizações / diplomacia:

**35,00 / 70 GPP — 50,0%**

Scope Confidence:

**50%**

Technical Risk:

**HIGH**

Riscos críticos ativos:

**7**

Critical Path atual:

**M4.4-A markets, trade, routes and flow allocation contract freeze**

Entry gate:

**M4.3 accepted accumulated validation and integrated Economy foundation**

Última revisão de baseline:

**2026-10-06**

---
# 21. Política de atualização

Este documento deverá ser revisado quando:

- um milestone for iniciado ou concluído;
- ocorrer alteração relevante de escopo;
- uma decisão arquitetural mudar;
- surgir risco estrutural relevante;
- houver mudança significativa no Critical Path.

Mudanças históricas não devem ser apagadas do Progress Ledger.

# Global Arena — Project Compass

## 1. Identidade do projeto

**Nome:** Global Arena
**Plataforma inicial:** PC
**Modelo:** jogo de estratégia e tática em escala planetária
**Estado atual:** M2 — Planet Topology
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

Estado de maturidade após M2.3.5 / fechamento de M2.3:

| Capability | GPP | Maturidade | Fator | GPP ganhos |
|---|---:|---|---:|---:|
| Goldberg parameterization e geração estratégica | 16 | Implementação funcional isolada | 0.50 | 8.00 |
| Strategic graph: identidade, incidência e adjacência | 12 | Implementação funcional isolada | 0.50 | 6.00 |
| Tactical region topology | 14 | Validada | 0.85 | 11.90 |
| Shared subtile border bands | 16 | Validada | 0.85 | 13.60 |
| Strategic ↔ tactical hierarchy/refinement mapping | 16 | Inexistente | 0.00 | 0.00 |
| Canonical deterministic topology generation | 6 | Inexistente | 0.00 | 0.00 |
| Topological validation e navigability | 6 | Inexistente | 0.00 | 0.00 |
| Scalability / headless performance baseline | 4 | Inexistente | 0.00 | 0.00 |
| **TOTAL** | **90** |  |  | **39.50** |

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

M2.3 é encerrado. `Strategic ↔ tactical hierarchy/refinement mapping` permanece sem promoção porque mapping físico, traversal tático cross-region e universal Goldberg refinement continuam abertos para M2.4.

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

## M4 — Simulation Vertical Slice

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

# 20. Status atual

Milestone:

**M2 — Planet Topology**

Etapa:

**M2.4 — Goldberg Family & Refinement Validation**

Subetapa atual:

**M2.4.2 — Canonical Construction Provenance & Reference Mapping**

M0 — Project Baseline:

**concluído em 2026-09-18**

M1 — Deterministic Simulation Kernel:

**concluído em 2026-09-19**

M2.1 — Goldberg Topology Foundation:

**concluído em 2026-09-19**

M2.1.2 — Goldberg Parameter & Count Contract:

**concluído em 2026-09-19**

M2.1.3 — Strategic Topology Identity Contract:

**concluído em 2026-09-19**

M2.1.4 — Minimal G(1,0) Strategic Topology:

**concluído em 2026-09-19**

M2.1.5.A — Class I Goldberg Generalization:

**concluído em 2026-09-19**

M2.1.5.B — Class II Goldberg Generalization:

**concluído em 2026-09-19**

M2.1.5.C — Class III Goldberg Generalization:

**concluído em 2026-09-19**

M2.2.1 — Tactical Identity & Region Contract:

**concluído em 2026-09-20**

M2.2.2 — Minimal Tactical Region Graph:

**concluído em 2026-09-20**

M2.2.3 — Strategic-to-Tactical Region Materialization:

**concluído em 2026-09-20**

M2.2.4 — Tactical Region Validation & M2.2 Close:

**concluído em 2026-09-20**

M2.2 — Tactical Region Topology:

**concluído em 2026-09-20**

M2.3.1 — Shared Border Contract Audit & Design:

**concluído em 2026-09-20**

M2.3.2 — Shared Border Identity & Band Contract:

**concluído em 2026-09-20**

M2.3.3 — StrategicEdge-to-Border Materialization:

**concluído em 2026-09-20**

M2.3.4 — Cross-Region Aggregate & Derived Incidence:

**concluído em 2026-09-20**

M2.3.5 — Shared Border Validation & M2.3 Close:

**concluído em 2026-09-20**

M2.3 — Shared Border Bands & Strategic/Tactical Mapping:

**concluído em 2026-09-20**

M2.4.1 — Scaled Refinement Compatibility Contract:

**concluído em 2026-09-20**

Baseline V1:

**congelado em 2026-09-18**

Progresso oficial:

**11,0%**

GPP conquistados:

**109,50 / 1000**

Foundation / Simulation Kernel:

**70,00 / 70 GPP — 100,0%**

Topologia planetária / Goldberg:

**39,50 / 90 GPP — 43,9%**

Scope Confidence:

**50%**

Technical Risk:

**HIGH**

Riscos críticos ativos:

**7**

Critical Path atual:

**Goldberg family/refinement validation e mapeamento físico estratégico/tático**

Última revisão de baseline:

**2026-09-20**

---

# 21. Política de atualização

Este documento deverá ser revisado quando:

- um milestone for iniciado ou concluído;
- ocorrer alteração relevante de escopo;
- uma decisão arquitetural mudar;
- surgir risco estrutural relevante;
- houver mudança significativa no Critical Path.

Mudanças históricas não devem ser apagadas do Progress Ledger.

# Global Arena — Risk Register

**Versão:** 0.1
**Milestone:** M3 — Procedural World
**Status:** Ativo
**Última revisão formal:** 2026-09-29
**Baseline V1 relacionado:** 0.1 — congelado

---

# 1. Objetivo

Este documento registra riscos capazes de afetar:

- arquitetura;
- performance;
- escopo;
- prazo;
- estabilidade;
- multiplayer;
- experiência do jogador;
- capacidade de evolução do projeto.

Risco conhecido não deve permanecer implícito.

---

# 2. Modelo de avaliação

Cada risco possui:

Probability:
1 — baixa
2 — moderada
3 — alta
4 — muito alta

Impact:
1 — baixo
2 — moderado
3 — alto
4 — crítico

Risk Score:

Probability × Impact

Classificação:

1–3   LOW
4–7   MODERATE
8–11  HIGH
12–16 CRITICAL

---

# 3. Estados

OPEN

Risco identificado e ainda relevante.

MITIGATING

Ações de mitigação em andamento.

WATCHING

Risco controlado, mas ainda monitorado.

CLOSED

Risco eliminado ou tornado irrelevante.

ACCEPTED

Risco conscientemente aceito.

---

# 4. Registro

## RISK-001 — Economy scalability

Status:

OPEN

Probability:

4

Impact:

4

Score:

16 — CRITICAL

Descrição:

A economia viva poderá se tornar um dos maiores consumidores de CPU do Global Arena, especialmente com múltiplas civilizações, mercados, recursos, rotas e grandes planetas.

Mitigação planejada:

- operar prioritariamente no grafo estratégico;
- evitar pathfinding econômico na malha tática;
- usar cache de rotas;
- invalidar apenas dependências afetadas;
- utilizar diferentes cadências de atualização;
- criar benchmarks sintéticos cedo;
- evitar um objeto pesado por agente econômico.

Trigger:

tempo de atualização econômica cresce além do budget definido para o tamanho de mundo alvo.

Próxima ação:

performance spike econômico antes da implementação completa.

---

## RISK-002 — Tactical resolution scalability

Status:

OPEN

Probability:

3

Impact:

4

Score:

12 — CRITICAL

Descrição:

A alta resolução tática pode gerar uso excessivo de memória e CPU se cada subtile for modelado como objeto independente pesado.

M2.5.4 evidence:

- full physical `G(16,0) -> G(96,0)` completes but exceeds the original product-performance budget;
- fine `G(96,0)` topology generation is the dominant measured cost;
- scale-6 allocation per fine tile is approximately linear across `k=4,8,12,15`;
- observed allocation-per-tile spread is `1.010`;
- observed time-per-tile spread is `1.696`;
- the main current issue is a high constant cost per fine tile rather than demonstrated combinatorial explosion.

Mitigação:

- data-oriented design;
- estruturas compactas;
- tiles como dados;
- carregamento e processamento seletivo;
- evitar GameObject por tile;
- benchmarks de memória;
- níveis de atividade;
- separar blocking product acceptance de non-blocking stress scale;
- manter escala final do produto dependente de validação visual/gameplay.

Próxima ação:

manter o permanent benchmark harness como regression baseline e revisitar o envelope quando M3/M5 congelarem densidade tática e tamanho de mundo do produto.

---

## RISK-003 — Goldberg hierarchy mapping

Status:

WATCHING

Probability:

2

Impact:

4

Score:

8 — HIGH

Descrição:

A relação entre Goldberg estratégico e regiões táticas precisa preservar:

- adjacência;
- pertencimento pai-filho;
- continuidade entre tabuleiros vizinhos;
- consistência topológica;
- capacidade de navegação;
- mapeamento determinístico de fronteiras.

A hipótese atual assume que o refinamento hierárquico poderá funcionar de forma geral sobre poliedros Goldberg `G(m,n)`.

Essa universalidade ainda não foi demonstrada.

Casos das diferentes famílias Goldberg podem introduzir limitações de orientação, fronteira ou pertencimento que inviabilizem uma subdivisão estritamente hierárquica em todos os casos.

Erros nessa camada contaminariam diversos subsistemas.

Mitigação:

- módulo de geometria independente;
- testes topológicos;
- geração headless;
- validação de Euler e invariantes;
- contrato executável de parâmetros e contagens Goldberg;
- vetores de referência validados cross-platform;
- IDs estratégicos fortemente tipados e estáveis;
- estado `default` dos IDs tratado como sentinela inválida;
- contrato de identidade validado cross-platform;
- topologia estratégica mínima `G(1,0)` materializada;
- incidência, adjacência, conectividade e Euler validados cross-platform;
- IDs canônicos determinísticos provados no caso mínimo;
- geração estratégica Class I `G(m,0)` / `G(0,n)` materializada;
- `G(2,0)`, `G(0,2)` e `G(3,0)` validados com contagens, graus e invariantes esperados;
- determinismo de identidade e assinatura provado para a tranche Class I;
- geração estratégica Class II `G(k,k)` materializada;
- `G(1,1)` e `G(2,2)` validados com contagens, graus e invariantes esperados;
- determinismo de identidade e assinatura provado para a tranche Class II;
- geração estratégica Class III para parâmetros positivos desiguais materializada;
- `G(2,1)`, `G(1,2)`, `G(3,1)` e `G(3,2)` validados com contagens, graus e invariantes esperados;
- orientação/chiralidade Class III tratada combinatoriamente e validada por assinaturas distintas de `G(2,1)` e `G(1,2)`;
- suporte estratégico funcional isolado agora cobre Class I, Class II e Class III;
- contrato tático M2.2.1 materializado com `TacticalCellId`, `TacticalCell` e `TacticalRegion`;
- identidade tática region-owned ancorada em `StrategicCellId + LocalOrdinal`;
- ownership regional e invariantes locais de adjacência/conectividade validados cross-platform;
- shared border bands e adjacência cross-region permanecem explicitamente fora de M2.2.1;
- `MinimalTacticalRegionGraphGenerator` materializa um reference graph local canônico;
- ordinais `1`, `2`, `3`, reciprocidade, conectividade e determinismo do grafo tático mínimo foram validados cross-platform;
- o reference graph não congela geometria física, resolução final ou semântica cross-region;
- `StrategicTacticalRegionMaterializer` integra `StrategicTopology` ao conjunto de regiões táticas;
- correspondência um-para-um `StrategicCell` → `TacticalRegion`, ordem canônica e determinismo foram validados cross-platform;
- casos representativos Class I, Class II e Class III materializam corretamente;
- nenhum aggregate ou adjacency cross-region foi inventado antes dos contratos de M2.3;
- M2.2.4 validou snapshot/read-only semantics diretamente sem alterar production code;
- `G(0,2)`, `G(2,2)`, `G(1,2)`, `G(3,1)` e `G(3,2)` foram materializados no gate acumulado;
- `G(3,2)` validou 192 regiões, 576 identidades táticas globalmente únicas e adjacency estritamente parent-local;
- duas materializações completas de `G(3,2)` reproduziram a mesma assinatura canônica;
- regressão cross-platform final de M2.2 passou em Ubuntu, Windows e macOS;
- M2.3.1 congelou `StrategicEdgeId` como identidade do `SharedBorderBand`;
- M2.3.1 congelou `SharedBorderElementId = StrategicEdgeId + LocalOrdinal`;
- M2.3.2 implementou identidade e invariantes locais de shared border sem reutilizar `TacticalCellId`;
- `SharedBorderBand` rejeita edge inválido, coleção vazia, elementos de outro edge, IDs duplicados e gaps de ordinal;
- elementos são canonicalizados por ordinal e expostos como snapshot somente leitura;
- incidência regional e orientação permanecem derivadas do `StrategicEdge`, evitando segunda fonte de verdade;
- regressão cross-platform de M2.3.2 passou em Ubuntu, Windows e macOS;
- M2.3.3 materializou exatamente um `SharedBorderBand` por `StrategicEdge`;
- ordering de `StrategicTopology.Edges` foi preservado na coleção de bands;
- o reference element `SharedBorderElementId(edge.Id, 1)` provou identidade e cobertura sem congelar geometria;
- IDs de reference elements foram validados como globalmente únicos;
- materialização repetida produziu assinatura canônica idêntica;
- Class I `G(2,0)`, Class II `G(2,2)` e Class III `G(3,2)` validaram 120, 360 e 570 bands respectivamente;
- regressão cross-platform de M2.3.3 passou em Ubuntu, Windows e macOS;
- M2.3.4 introduziu `StrategicTacticalBorderAggregate` como aggregate cross-region mínimo;
- cobertura de `TacticalRegion` agora é validada exatamente contra `StrategicTopology.Cells`;
- cobertura de `SharedBorderBand` agora é validada exatamente contra `StrategicTopology.Edges`;
- missing, duplicate e foreign region/band entries são rejeitados;
- `SharedBorderIncidence` deriva exatamente duas regiões por `StrategicEdge.IncidentCellIds`;
- ordering de regions, bands e incidences é canonicalizado pela topologia autoritativa;
- snapshots read-only e preservação de referências dos domain objects foram validados;
- construção repetida do aggregate produziu assinatura canônica idêntica;
- Class I `G(2,0)`, Class II `G(2,2)` e Class III `G(3,2)` validaram joint coverage de 42/120/120, 122/360/360 e 192/570/570;
- regressão cross-platform de M2.3.4 passou em Ubuntu, Windows e macOS;
- testes explícitos de pertencimento pai-filho;
- testes de continuidade entre regiões;
- validação de casos representativos das famílias Goldberg;
- permitir redução do conjunto de famílias suportadas caso a hipótese geral não se sustente.

M2.3.5 fechou o stage com validation-only e regressão cross-platform aprovada:

- continuidade lógica `StrategicCell.IncidentEdgeIds` ↔ derived incidences validada;
- graus pentagonais e hexagonais validados em 5 e 6;
- handshake global validado;
- exatamente duas regions por incidence confirmado;
- border element IDs globalmente únicos e edge-local;
- adjacency tática parent-local preservada;
- duas pipelines completas independentes produziram assinatura canônica idêntica;
- `G(0,2)`, `G(1,2)`, `G(2,1)` e `G(3,1)` adicionados à cobertura acumulada;
- regressão cross-platform de M2.3.5 passou em Ubuntu, Windows e macOS.

M2.4 iniciou com audit read-only sobre o gap de refinement.

O audit confirmou:

- não existe production mapping coarse→fine;
- `StrategicCellId`, `StrategicEdgeId` e `StrategicVertexId` são locais à topologia e não podem ser usados como lineage entre resoluções;
- geração Class I, II e III prova topologias individuais, não relações de refinement;
- `MinimalTacticalRegionGraphGenerator` continua reference-only;
- shared border lógico está validado, mas physical border cardinality e tactical-border mapping permanecem abertos;
- nenhuma fórmula universal de refinement está provada.

M2.4.1 foi implementado e validado cross-platform.

O contrato `GoldbergScaledRefinement` agora:

- reconhece somente pares de escala inteira exata;
- preserva orientação/quiralidade;
- rejeita relações unsupported explicitamente;
- não cria parent-child mapping;
- não usa IDs locais como lineage;
- não introduz geometry ou floating point.

A incerteza de compatibilidade de parâmetros foi reduzida, mas o núcleo do RISK-003 permanece aberto: ainda não existe provenance/mapping executável entre entidades coarse e fine.

O audit read-only de M2.4.2 confirmou que a provenance necessária não está ausente; ela está encapsulada dentro dos geradores.

Class I/II preservam provenance por `SubdivisionLatticeVertexKey` antes de converter construction keys em `StrategicCellId`.

Class III preserva provenance por lattice local, orientação de seed faces e stitching via `DisjointSet`, mas os roots atuais são implementation details e não podem ser publicados como identidade.

Decisão de redução de risco:

M2.4.2 começará somente pelo reference pair `G(1,0) -> G(2,0)`, onde os 12 coarse cells correspondem exatamente aos 12 vertices canônicos do icosaedro base.

A provenance pública mínima será `IcosahedronSeedVertexId(1..12)` e o reference mapping associará cada seed vertex a um coarse `StrategicCellId` e a um fine `StrategicCellId`.

Isso cria uma primeira correspondência cross-resolution baseada em construction provenance sem ainda forçar ownership das fine cells intermediárias.

M2.4.2 foi implementado e validado cross-platform.

O projeto agora possui uma primeira correspondência cross-resolution baseada em provenance de construção para `G(1,0) -> G(2,0)`:

- 12 seed vertices canônicos;
- 12 coarse cells cobertas;
- 12 fine seed-vertex cells referenciadas;
- nenhum lineage derivado de ordinal de `StrategicCellId`.

Isso reduz RISK-003, mas não resolve o núcleo de continuidade de fronteira.

Ainda permanecem abertos:

- as 30 fine cells intermediárias;
- coarse-edge para fine-edge-chain mapping;
- coarse-vertex junction mapping;
- Class II mapping;
- Class III mapping;
- full hierarchy coverage.

O audit read-only de M2.4.3 confirmou uma estrutura de continuidade lógica forte no reference pair `G(1,0) -> G(2,0)`.

Para cada um dos 30 coarse edges:

- os dois fine anchor cells não são diretamente adjacentes;
- existe exatamente um common fine neighbor;
- esse neighbor é um hexágono;
- a chain anchor -> middle -> anchor usa exatamente dois fine edges.

Globalmente:

- existem 30 middle fine cells únicos;
- eles cobrem exatamente os 30 hexágonos de `G(2,0)`;
- existem 60 fine edges únicos nas chains;
- metade dos 120 fine edges permanece fora das coarse-edge chains;
- cada mapped fine edge possui seu próprio `SharedBorderBand`;
- a cardinalidade atual dos bands permanece single-element.

Isso reduz RISK-003 porque a continuidade de coarse edges possui agora um reference pattern executável e determinístico.

Ainda permanecem abertos:

- ownership de middle fine cells;
- coarse-vertex para fine junction mapping;
- os outros 60 fine edges;
- physical tactical-border mapping;
- final border geometry/cardinality;
- Class II continuity;
- Class III continuity;
- full hierarchy coverage.

M2.4.3 foi implementado e validado cross-platform.

O projeto agora possui continuidade lógica executável de shared borders para `G(1,0) -> G(2,0)`:

- 30/30 coarse edges cobertos;
- 30/30 coarse bands cobertos;
- 30 unique middle fine cells;
- os 30 middle cells são todos os fine hexagons;
- 60/120 fine edges cobertos;
- 60/120 fine bands cobertos;
- ordering determinística anchor -> middle -> anchor;
- current single-element band semantics preservada.

Isso reduz novamente RISK-003 porque a relação coarse-edge -> fine-edge-chain deixou de ser apenas um probe e passou a existir como production contract executável.

Ainda permanecem abertos:

- ownership das middle fine cells;
- coarse-vertex para fine-junction mapping;
- os outros 60 fine edges;
- physical tactical-border mapping;
- final border geometry/cardinality;
- Class II continuity;
- Class III continuity;
- full hierarchy coverage.

O audit read-only de M2.4.4 confirmou que scaled compatibility, count scaling e deterministic topology generation se mantêm nos representative pairs de Class I, Class II e Class III.

Também confirmou:

- Class I/II possuem construction keys scale-homogeneous;
- Class III local lattice coordinates são scale-homogeneous;
- isso não prova durable global Class III lineage;
- raw DSU roots não são identidade estável;
- multi-family cell reference mapping permanece ausente;
- multi-family border continuity mapping permanece ausente.

Decisão de redução de risco:

M2.4.4 será validation-only.

O checkpoint congelará executable regression evidence sobre public multi-family invariants sem promover private implementation details a public identity.

Isso evita aumentar RISK-003 com um provenance contract prematuro.

M2.4.4 foi implementado e validado cross-platform como validation-only.

A regressão agora cobre public scaled behavior em:

- Class I normal;
- Class I inverted axis;
- Class II;
- Class III right chirality;
- Class III left chirality;
- scales 2 e 3.

Isso reduz o risco de regressão multi-family sem criar um provenance contract prematuro.

O current reference/continuity scope permanece deliberadamente narrow:

- cell reference mapping: apenas `G(1,0) -> G(2,0)`;
- shared-border continuity: apenas o mesmo reference pair.

Continuam abertos no RISK-003:

- multi-family durable cell provenance;
- Class III global stitched provenance;
- multi-family edge continuity;
- parent-child ownership;
- coarse-vertex junction mapping;
- physical tactical-border mapping;
- universal Goldberg refinement.

O audit read-only acumulado de M2.4.5 confirmou que M2.4 pode avançar para um final validation gate sem alterar production.

A evidência acumulada suporta:

- scaled compatibility;
- representative multi-family topology regression;
- first-pair canonical seed provenance;
- first-pair logical shared-border edge-chain continuity.

A evidência acumulada não suporta:

- multi-family durable cell lineage;
- Class III global stitched provenance;
- multi-family edge continuity;
- full parent-child ownership;
- coarse-vertex fine-junction mapping;
- physical tactical-border mapping;
- universal Goldberg refinement.

Decisão de redução de risco:

M2.4.5 será validation-only e consolidará 12 Facts cumulativos usando somente public contracts.

Se o gate final passar cross-platform, M2.4 poderá ser fechado, mas RISK-003 continuará HIGH e a capability `Strategic ↔ tactical hierarchy/refinement mapping` continuará em fator 0.00.

M2.4.5 foi implementado e validado cross-platform no commit `01583a55f24119e398296c2c620cd76ab64277ca`, com 366/366 testes e run `35504415048` aprovado em Ubuntu, Windows e macOS.

M2.4 está formalmente concluído, mas o fechamento não remove as lacunas centrais de RISK-003.

Continuam abertos:

- multi-family durable cell lineage;
- Class III global stitched provenance;
- multi-family edge continuity;
- full parent-child ownership;
- coarse-vertex fine-junction mapping;
- physical tactical-border mapping;
- final physical border geometry/cardinality;
- universal Goldberg refinement no sentido forte de lineage/hierarchy.

`Strategic ↔ tactical hierarchy/refinement mapping` permanece em fator 0.00.

`RISK-003` permanece HIGH.

O entry audit read-only de M2.5 confirmou:

- M2 exit ready: no;
- physical tactical-border mapping permanece ausente;
- cross-region tactical navigability permanece ausente;
- multi-family durable lineage permanece ausente;
- o tactical graph atual continua sendo o reference graph de três células;
- o benchmark project continua placeholder;
- a regressão cross-platform atual não mede performance.

O probe observacional mostrou que o workload atual de referência constrói os canonical large cases com ampla margem frente ao primeiro budget proposto, mas isso não constitui acceptance evidence porque M2.5.2/M2.5.3 ainda podem alterar o workload obrigatório.

M2.5.1 congela um budget inicial de M2 topology baseline:

- median elapsed `<= 1000 ms`;
- max elapsed sample `<= 2000 ms`;
- median managed allocation `<= 192 MiB`;
- max managed allocation sample `<= 256 MiB`;
- 1 warmup + 5 measured samples;
- canonical cases Class I `G(16,0)`, Class II `G(10,10)` e Class III `G(12,7)`.

Esse budget não fecha `RISK-002` nem `RISK-012`, não representa high-resolution tactical performance e não reduz `RISK-003` por si só.

`RISK-003` permanece HIGH.

M2.5.3-C generalized Class I physical hierarchy implementation passed its implementation audit:

- canonical integer construction keys now provide durable lineage for every coarse Class I strategic cell;
- coarse construction keys scale exactly to distinct authoritative fine anchors;
- physical incidence materialization now accepts the official Class I scale-6 family instead of only the unit-base reference pair;
- representative `k=1`, `k=2` and `k=3` cases passed, including the inverted Class I axis;
- frozen generalized count signatures are enforced at runtime;
- every observed 2-way incidence must equal one authoritative coarse edge signature;
- every observed 3-way incidence must equal one authoritative coarse vertex signature;
- ownership remains independent of floating-point coordinates and local ordinal equality;
- physical traversal remains on authoritative fine adjacency;
- Class II/III remain outside official M2 physical hierarchy scope.

M2.5.3-D then closed accumulated coverage validation without production mutation:

- `k=1..4` passed on both Class I axes;
- `k=4` provided an out-of-sample generalized case;
- exact generalized count formulas passed;
- every authoritative coarse edge and coarse vertex was represented exactly once;
- exact fine-tile coverage passed;
- repeated `k=4` materialization was deterministic on both axes;
- cross-owner traversal used authoritative fine adjacency;
- wrong-scale Class I, Class II physical and Class III physical cases remained rejected;
- `429/429` tests passed with `0/0` compiler warnings/errors.

This closes the official-family/refinement coverage uncertainty owned by M2.5.3. `RISK-003` remains HIGH under the current governance until measured performance and the later M2 exit gates validate the accepted workload in its final execution envelope.

Próxima ação:

monitorar durante M3 e nos consumidores posteriores se novos requisitos de escala ou família exigem ampliar o physical hierarchy além do scope oficial de M2.

---

## RISK-004 — Determinism failure

Status:

WATCHING

Probability:

2

Impact:

4

Score:

8 — HIGH

Descrição:

Diferenças de resultado entre execuções idênticas podem comprometer:

- multiplayer;
- replay;
- debugging;
- testes;
- sincronização.

Possíveis fontes:

- random não controlado;
- ordem instável de coleções;
- ponto flutuante;
- concorrência;
- dependência de relógio real;
- plataforma.

Mitigação:

- PRNG próprio/versionado;
- seeds explícitas;
- ordem estável;
- testes repetidos;
- evitar estado global;
- command/event log;
- validação repetida do fluxo integrado de Turn Policies;
- hash de estado;
- validação cross-platform automatizada do state hash em Windows, Linux e macOS;
- avaliar uso de matemática determinística onde necessário.

Próxima ação:

manter a regressão cross-platform como gate contínuo e ampliar vetores determinísticos sempre que o estado autoritativo ou as regras evoluírem.

---

## RISK-005 — Multiplayer desynchronization

Status:

OPEN

Probability:

3

Impact:

4

Score:

12 — CRITICAL

Descrição:

Clientes podem divergir ou receber estado inconsistente.

Mitigação:

- servidor autoritativo;
- Simulation executada no servidor;
- snapshots;
- versionamento de protocolo;
- state hashes;
- logs reproduzíveis;
- clientes não determinam estado final.

Dependência:

RISK-004.

---

## RISK-006 — Scope expansion

Status:

MITIGATING

Probability:

4

Impact:

4

Score:

16 — CRITICAL

Descrição:

A natureza sistêmica do projeto facilita expansão contínua de features e crescimento invisível do horizonte.

Mitigação:

- baseline de 1000 GPP;
- V1 Definition of Done;
- V1 REQUIRED / OPTIONAL / POST-V1 / EXPERIMENTAL;
- Scope Change Records;
- roadmap fixo;
- Progress Ledger.

Indicador:

crescimento frequente do escopo obrigatório sem remoção ou redistribuição.

---

## RISK-007 — Architecture overengineering

Status:

OPEN

Probability:

3

Impact:

3

Score:

9 — HIGH

Descrição:

A preocupação com escalabilidade pode gerar abstrações prematuras, excesso de projetos, event buses genéricos ou infraestrutura sem necessidade comprovada.

Mitigação:

- monólito modular;
- poucos projetos inicialmente;
- extração progressiva;
- abstrações justificadas por uso real;
- ADR para mudanças estruturais relevantes.

---

## RISK-008 — Architecture underengineering

Status:

OPEN

Probability:

3

Impact:

4

Score:

12 — CRITICAL

Descrição:

Uma implementação rápida demais pode produzir forte acoplamento entre:

- Unity;
- economia;
- guerra;
- networking;
- worldgen;
- UI.

Isso aumentaria drasticamente o custo de evolução.

Mitigação:

- Simulation headless;
- ownership de dados;
- contratos explícitos;
- dependências direcionais;
- testes sem Unity;
- revisão arquitetural por milestone.

---

## RISK-009 — UI complexity

Status:

OPEN

Probability:

4

Impact:

3

Score:

12 — CRITICAL

Descrição:

A quantidade de informação do Global Arena pode tornar a interface difícil de compreender e operar.

Mitigação:

- read models;
- hierarquia de informação;
- zoom semântico;
- filtros;
- overlays;
- dashboards;
- prototipagem precoce;
- UX tratada como subsistema próprio.

---

## RISK-010 — AI cost and complexity

Status:

OPEN

Probability:

3

Impact:

3

Score:

9 — HIGH

Descrição:

IA para economia, guerra e diplomacia pode consumir CPU excessiva ou se tornar difícil de manter.

Mitigação:

- IA em camadas;
- decisões estratégicas em baixa frequência;
- uso do mesmo grafo agregado;
- budgets computacionais;
- perfis diferentes de atualização;
- interfaces públicas da simulação.

---

## RISK-011 — Save compatibility

Status:

OPEN

Probability:

3

Impact:

3

Score:

9 — HIGH

Descrição:

Evolução de WorldGeneration, Ruleset e estrutura de dados pode tornar saves antigos incompatíveis.

Mitigação:

- SaveSchemaVersion;
- WorldGenerationVersion;
- RulesetVersion;
- migrações explícitas;
- snapshots;
- testes de carregamento.

---

## RISK-012 — Memory footprint

Status:

OPEN

Probability:

3

Impact:

4

Score:

12 — CRITICAL

Descrição:

Planetas gigantes com centenas de milhares ou milhões de subtiles podem exceder budgets de memória se os dados forem representados de forma ingênua.

M2.5.4 evidence:

- `G(15,0) -> G(90,0)` scale 6 implies `81002` fine cells;
- a scale near the area of a 12-ring hex region is roughly `21.656`;
- `G(15,0)` at scale 21 approaches `992252` fine cells;
- `G(15,0)` at scale 22 exceeds one million fine cells;
- observed scale-6 managed allocation is approximately linear per fine tile;
- the current representation allocates roughly 22 KB per fine tile across the measured sweep.

Mitigação:

- estruturas compactas;
- IDs numéricos;
- arrays contíguos;
- bit fields quando apropriado;
- dados derivados não persistidos quando barato recalcular;
- streaming;
- profiling;
- não materializar simultaneamente uma combinação de máximos estratégicos e táticos sem evidência de produto que a justifique;
- manter stress cases para detectar perda de robustez sem transformar stress scale em requisito de produto.

Próxima ação:

recalibrar o memory budget quando o tamanho estratégico e a densidade tática de produto forem congelados por evidência de world generation e client.

---

# 5. Riscos globais atuais

Critical:

- Economy scalability
- Tactical resolution scalability
- Multiplayer synchronization
- Scope expansion
- Architecture underengineering
- UI complexity
- Memory footprint

High:

- Goldberg hierarchy
- Determinism
- Architecture overengineering
- AI complexity
- Save compatibility

---

# 6. Estado global

Risk Level atual:

**HIGH**

Embora existam riscos individuais classificados como CRITICAL, muitos ainda são hipóteses arquiteturais controláveis.

O risco global só deverá ser classificado como CRITICAL se existir ameaça imediata ao viability do projeto ou ao Critical Path atual.

## 6.1 Revisão de fechamento do M0

A revisão formal realizada no fechamento do M0 não identificou motivo técnico suficiente para encerrar ou reclassificar individualmente os riscos existentes.

O baseline permanece com:

- 8 riscos ativos classificados individualmente como CRITICAL;
- 4 riscos ativos classificados individualmente como HIGH;
- Risk Level global: HIGH.

A diferença entre os riscos individuais CRITICAL e o Risk Level global HIGH é deliberada.

Os riscos individuais representam impacto potencial caso se materializem.

O nível global representa a ameaça técnica atual ao projeto e ao Critical Path.

No início do M1, o risco mais diretamente associado ao Critical Path é:

**RISK-004 — Determinism failure**

O M1 deverá produzir evidência executável de que:

- uma resolução não vazia pode ser reproduzida;
- a mesma entrada e seed produzem o mesmo resultado;
- ordering e aleatoriedade permanecem controlados;
- Commands e Events mantêm sequência determinística;
- divergências podem ser diagnosticadas.

`RISK-006 — Scope expansion` permanece em `MITIGATING`.

O congelamento do baseline V1 de 1000 GPP, da V1 Definition of Done e da decomposição inicial de Foundation / Simulation Kernel constitui mitigação ativa, mas ainda não justifica encerramento do risco.

Nenhum novo risco estrutural foi identificado no fechamento do M0.

A próxima revisão obrigatória ocorrerá no fechamento do M1 ou antes disso caso:

- o determinismo end-to-end falhe;
- um benchmark revele limitação estrutural;
- ocorra mudança arquitetural relevante;
- surja novo risco capaz de afetar o Critical Path.

---

# 7. Política de revisão

Revisar este documento:

- no fechamento de milestone;
- após spike técnico;
- após benchmark relevante;
- após mudança arquitetural;
- após incidente grave;
- quando um risco for encerrado;
- quando surgir risco novo.

Riscos encerrados não devem ser apagados.

---

# 8. Revisão formal — fechamento do M1

**Data:** 2026-09-19

O M1 foi encerrado após a suíte completa do Simulation Kernel passar em Windows, Linux e macOS.

`RISK-004 — Determinism failure` foi reclassificado de `CRITICAL` para `HIGH` e de `MITIGATING` para `WATCHING`.

A probabilidade foi reduzida de 3 para 2 porque o projeto agora possui:

- PRNG e ordering determinísticos com vetores de referência;
- resolução end-to-end reproduzível;
- EventLog e replay automatizados;
- state hash canônico;
- regressão cross-platform completa;
- evidência em x64 e arm64.

O impacto permanece 4 porque uma regressão futura de determinismo continuaria capaz de comprometer multiplayer, replay e diagnóstico.

Após essa revisão:

- 7 riscos ativos permanecem classificados como CRITICAL;
- 5 riscos ativos permanecem classificados como HIGH;
- Risk Level global permanece HIGH.

O Critical Path deixa o determinismo mínimo e passa para:

**RISK-003 — Goldberg hierarchy mapping**

A prioridade de M2 é reduzir a incerteza sobre a hierarquia estratégico/tático antes de consolidar estruturas de produção.

---

# 9. M2.5.2 vertex-aware physical mapping review

**Data:** 2026-09-20

The M2.5.2-A read-only audit isolated a previously implicit three-way physical-incidence requirement at each coarse `StrategicVertex`.

No new risk ID is created.

The finding remains inside:

**RISK-003 — Goldberg hierarchy mapping**

Current classification remains:

**HIGH**

The mitigation path is refined to require:

- one continuous fine Goldberg physical topology;
- canonical physical tile identity independent of observation side;
- complete coarse→fine incidence with counts 1, 2 or 3;
- derivation of edge and vertex anchors from the authoritative coarse topology;
- explicit proof that a vertex-shared tile is one canonical physical tile;
- cross-region traversal through fine-topology adjacency;
- family/scale coverage decision in M2.5.3.

The first vertex-aware mapping target is `G(1,0) -> G(3,0)` scale 3.

The count decomposition `12 + 60 + 20 = 92` is a design target only and does not reduce RISK-003 until the provenance/mapping is implemented and validated.

`RISK-002 — Tactical resolution scalability` remains open.

`RISK-012 — Memory footprint` remains open.

Global Risk Level remains:

**HIGH**
---

# 10. M2.5.2-C provenance target correction

**Date:** 2026-09-28

Evidence:

`GlobalArena-Evidence-M2.5.2-C-R3-READONLY-PROVENANCE-INCIDENCE-AUDIT-20260928-090905.zip`

Result:

**PASS_M2_5_2_C_TARGET_CORRECTION_REQUIRED**

The previous section 9 target `G(1,0) -> G(3,0)` is superseded as the first complete physical-incidence target.

Canonical provenance classification established:

- scale 2: edge-aware only — `12/30/0`;
- scale 3: vertex-aware only — `72/0/20`;
- scale 6: edge-and-vertex-aware — `312/30/20`.

M2.5.2-C therefore moves its first materialization target to:

`G(1,0) -> G(6,0)`

This does not reduce `RISK-003` yet because the production materializer and complete coverage proof remain absent.

`RISK-003 — Goldberg hierarchy mapping` remains:

**HIGH**

`RISK-002 — Tactical resolution scalability` remains open.

`RISK-012 — Memory footprint` remains open.

Global Risk Level remains:

**HIGH**
---

# 11. M2.5.2-C physical incidence materializer close

**Date:** 2026-09-28

Evidence:

`GlobalArena-Evidence-M2.5.2-C-MATERIALIZER-IMPLEMENTATION-R1-20260928-093748.zip`

Result:

**PASS_READY_FOR_M2_5_2_C_MATERIALIZER_FORMAL_CLOSE**

The corrected reference target `G(1,0) -> G(6,0)` is now materialized in production code.

Validated:

- fine physical tile coverage: `362/362`;
- interior incidence: `312`;
- edge-shared incidence: `30`;
- vertex-shared incidence: `20`;
- all 30 authoritative coarse strategic edges represented exactly once;
- all 20 authoritative coarse strategic vertices represented exactly once;
- deterministic repeated materialization;
- read-only incidence map;
- 402/402 tests;
- 0 compiler warnings/errors.

Risk effect:

The reference physical coarse-to-fine mapping is no longer absent. `RISK-003` is narrowed from absence of any physical mapping to lack of generalized family/scale coverage and durable lineage beyond the accepted reference target.

`RISK-003 — Goldberg hierarchy mapping` remains:

**HIGH**

Reason:

- physical materialization is currently reference-target-specific;
- M2.5.3 must still decide and validate official family/refinement coverage;
- M2.5.2-D must still prove cross-region traversal over the fine topology.

`RISK-002 — Tactical resolution scalability` remains open.

`RISK-012 — Memory footprint` remains open.

Global Risk Level remains:

**HIGH**
---

# 12. M2.5.2-D fine-topology traversal close

**Date:** 2026-09-28

Evidence:

`GlobalArena-Evidence-M2.5.2-D-TRAVERSAL-IMPLEMENTATION-R2-20260928-101812.zip`

Validated:

- fine-topology BFS traversal implemented;
- canonical physical path identity;
- edge-shared cross-coarse traversal: PASS;
- vertex-shared cross-coarse traversal: PASS;
- distant distinct-coarse-ownership traversal: PASS;
- synthetic cross-region adjacency: False;
- 417/417 tests;
- 0 compiler warnings/errors.

`RISK-003 — Goldberg hierarchy mapping` remains **HIGH** because generalized family/refinement coverage remains open and M2.5.2-E accumulated validation is still pending.

`RISK-002 — Tactical resolution scalability` remains open.

`RISK-012 — Memory footprint` remains open.

Global Risk Level remains **HIGH**.
---

# 13. M2.5.2-E accumulated validation and close

**Date:** 2026-09-28

Evidence:

`GlobalArena-Evidence-M2.5.2-E-R2-READONLY-ACCUMULATED-VALIDATION-20260928-120824.zip`

Result:

**PASS_READY_FOR_M2_5_2_E_FORMAL_CLOSE**

Accumulated validation confirms:

- `362/362` physical fine tiles;
- incidence signature `312/30/20`;
- coarse edge coverage `30/30`;
- coarse vertex coverage `20/20`;
- `1080` reciprocal fine edges;
- `65.703/65.703` shortest-path pairs validated against an independent BFS oracle;
- no synthetic cross-region adjacency;
- deterministic traversal;
- `417/417` automated tests;
- compiler warnings/errors `0/0`;
- tracked hash drift `0`.

Risk effect:

The physical boundary attachment and reference-target cross-region navigability requirements of M2.5.2 are now closed.

`RISK-003 — Goldberg hierarchy mapping` remains **HIGH**, but its remaining uncertainty is narrowed to official family/refinement coverage and generalization beyond the accepted reference target. That decision moves to M2.5.3.

`RISK-002 — Tactical resolution scalability` remains open and proceeds toward M2.5.4.

`RISK-012 — Memory footprint` remains open.

Global Risk Level remains **HIGH**.
---

# 14. M2.5.3-A hierarchy/refinement coverage feasibility audit

**Date:** 2026-09-28

Evidence:

`GlobalArena-Evidence-M2.5.3-A-R2-READONLY-HIERARCHY-COVERAGE-AUDIT-20260928-131956.zip`

Result:

**PASS_M2_5_3_COVERAGE_DECISION_INPUT_READY**

Validated:

- public scaled-refinement compatibility accepts representative Class I/II/III pairs;
- strategic topology generation supports Class I/II/III;
- physical incidence mapper supports only `G(1,0) -> G(6,0)`;
- current Class I unit-base 12-seed provenance is sufficient for the reference pair;
- non-unit Class I coarse topology is not covered by the 12-seed lineage primitive;
- Class II dominant provenance includes seed identities outside the exposed 12-seed map;
- Class III dominant provenance is unavailable to the physical mapper;
- universal physical refinement is not proved;
- 417/417 tests;
- 0 compiler warnings/errors;
- tracked hash drift 0;
- repository mutation False.

Design response:

M2.5.3-B freezes the official M2 physical hierarchy scope as Class I scale 6, with durable lineage required for every coarse Class I cell.

`RISK-003 — Goldberg hierarchy mapping` remains **HIGH**.

Reason:

The coverage boundary is now explicit, but the generalized Class I physical mapping is not implemented yet.

`RISK-002 — Tactical resolution scalability` remains open and becomes more relevant because the Class I `G(16,0)` M2 load case implies a scale-6 fine topology `G(96,0)` if full physical hierarchy construction is benchmarked.

`RISK-012 — Memory footprint` remains open for the same reason.

Global Risk Level remains **HIGH**.
---

# 15. M2.5.4-D permanent benchmark harness review

**Date:** 2026-09-29

Evidence:

`GlobalArena-Evidence-M2.5.4-D-R2-PERMANENT-BENCHMARK-HARNESS-20260929-062457.zip`

Result:

**PASS_READY_FOR_M2_5_4_D_FORMAL_AUDIT**

Validated:

- the permanent headless harness encodes the frozen M2.5.4-C acceptance/stress split;
- all four blocking product-acceptance workloads pass all four hard budgets;
- `G(16,0) -> G(96,0)` remains structurally correct and deterministic enough for the current stress lane, while its performance remains outside the blocking budgets;
- stress median elapsed is approximately `3.818 s`;
- stress max elapsed is approximately `7.985 s`;
- stress managed allocation is approximately `2.045 GB` per measured run;
- automated tests: `435/435`;
- compiler warnings/errors: `0/0`.

Risk effect:

`RISK-002 — Tactical resolution scalability` remains open.

The blocking M2 product-acceptance lane now has executable passing performance evidence, but the larger engineering-stress lane continues to demonstrate substantial headroom cost.

`RISK-012 — Memory footprint` remains open.

The approximately `2.045 GB` allocation observed in the `G(16,0) -> G(96,0)` stress case confirms that simultaneous large-scale physical materialization remains a real memory concern and should not be inferred as a V1 product requirement.

`RISK-003 — Goldberg hierarchy mapping` remains **HIGH** pending accumulated M2.5.4 baseline validation and the later M2.5.5 exit regression.

Global Risk Level remains:

**HIGH**

---

# 16. M2.5.4-E accumulated performance-baseline review

**Date:** 2026-09-29

Evidence:

`GlobalArena-Evidence-M2.5.4-E-R1-BASELINE-VALIDATION-20260929-075531.zip`

Result:

**PASS_READY_FOR_M2_5_4_E_FORMAL_CLOSE**

Validated:

- committed permanent benchmark harness baseline;
- three independent full harness executions;
- four blocking product-acceptance cases passed every hard elapsed/allocation budget in every run;
- full physical `G(16,0) -> G(96,0)` stress correctness passed in every run;
- stress median elapsed remained approximately `2.55–2.66 s`;
- stress managed allocation remained approximately `2.045 GB`;
- Release build: PASS;
- automated tests: `435/435`;
- repository mutation: False.

Risk effect:

`RISK-002 — Tactical resolution scalability` remains open.

The M2 blocking product envelope is now quantitatively stable on the committed Windows baseline, but final V1 scale and broader tactical density remain deliberately unfrozen.

`RISK-012 — Memory footprint` remains open.

The stress baseline continues to show approximately `2.045 GB` of managed allocation per run, reinforcing the requirement to avoid assuming simultaneous strategic/tactical maxima as a V1 product envelope.

`RISK-003 — Goldberg hierarchy mapping` remains **HIGH** until M2.5.5 completes cross-platform regression and the accumulated M2 exit audit.

M2.5.4 no longer blocks M2 on local committed-baseline scalability evidence.

Global Risk Level remains:

**HIGH**
---

# 17. M2.5.5 accumulated exit audit and M2 formal close risk review

**Date:** 2026-09-29

Evidence:

`GlobalArena-Evidence-M2.5.5-R1-ACCUMULATED-M2-EXIT-AUDIT-20260929-152623.zip`

SHA-256:

`931dd69bf4f15b40cf3a9ea31c5ddaa6ec9ab001062266386de02db973bea719`

Result:

**PASS_READY_FOR_M2_FORMAL_CLOSE**

Validated:

- M2 exit requirements: `10/10`;
- local Release suite: `435/435`;
- permanent benchmark harness: PASS;
- all blocking performance budgets: PASS;
- cross-platform suite: Ubuntu `435/435`, Windows `435/435`, macOS `435/435`;
- current cross-platform-relevant source/test/workflow drift: `0`;
- official Class I scale-6 physical hierarchy and Class I/II/III strategic support boundaries remain explicit.

Risk effect:

`RISK-003 — Goldberg hierarchy mapping` moves from `MITIGATING` to `WATCHING`.

Probability remains `2`, impact remains `4`, score remains `8 — HIGH`.

Reason:

- the accepted M2 physical hierarchy scope is explicit;
- durable Class I lineage is implemented;
- coarse edge/vertex physical incidence is validated;
- cross-region traversal uses authoritative fine adjacency;
- accumulated performance gates pass for the blocking product envelope;
- the applicable production/test tree has passed the full suite on Ubuntu, Windows and macOS.

The risk is not closed because later final-scale decisions or later topology consumers may require extensions outside the currently accepted M2 physical scope.

`RISK-002 — Tactical resolution scalability` remains `OPEN / CRITICAL`.

Its next decision point moves from M2 benchmark implementation to later product-scale validation. The full physical `G(16,0) -> G(96,0)` stress lane remains useful headroom evidence but is not a V1 product requirement.

`RISK-012 — Memory footprint` remains `OPEN / CRITICAL`.

The approximately `2.045 GB` managed allocation observed for the full physical stress case remains a warning against simultaneous materialization of independent strategic and tactical maxima.

Active CRITICAL risks remain:

**7**

Active HIGH risks remain:

**5**

Global Risk Level remains:

**HIGH**

The Critical Path moves from M2 topology exit semantics to the M3 procedural-world foundation.
---

# 18. M3 entry and M3.1-A world-generation design-freeze risk review

**Date:** 2026-09-29

Evidence:

`GlobalArena-Evidence-M3-ENTRY-AUDIT-R2-20260929-155250.zip`

SHA-256:

`400dfbb0b8cd5f4d05cfa787953c5e7aa5d26d09cbe96493a5f3b945033c51a6`

Result:

**PASS_READY_FOR_M3_1_CONTRACT_AND_DESIGN_FREEZE**

Risk assessment:

No new structural risk ID is introduced at M3 entry. The identified risk classes are already covered by the existing register.

`RISK-004 — Determinism failure` remains `WATCHING / HIGH`.

M3 response:

- separate `WorldSeed` from `SimulationSeed`;
- version generator semantics;
- forbid ambient randomness and wall-clock-dependent generation;
- require stable ordering and later cross-platform generated-world signatures.

`RISK-008 — Architecture underengineering` remains `OPEN / CRITICAL`.

M3 response:

- freeze request/result boundaries before physical algorithms;
- reuse M2 topology as an authoritative dependency;
- keep generated layers outside ad hoc `WorldState` mutation;
- preserve logical module ownership inside the modular monolith.

`RISK-006 — Scope expansion` remains `MITIGATING / CRITICAL`.

M3 response:

- World Generation budget remains exactly `100 GPP`;
- resources mean generated world properties/potentials, not Economy implementation;
- civilization placement means start-site suitability/candidates, not Civilizations runtime implementation;
- no new V1 capability is added by M3.1-A.

`RISK-002 — Tactical resolution scalability` remains `OPEN / CRITICAL`.

M3 response:

- tactical materialization is stage-specific;
- the validated M2 scale-6 hierarchy is not treated as final product density;
- recurring strategic systems consume aggregates.

`RISK-012 — Memory footprint` remains `OPEN / CRITICAL`.

M3 response:

- no requirement to keep full tactical detail globally resident;
- field representation should remain data-oriented and keyed by canonical identities;
- M3.6 must establish generation-time and memory budgets before M3 closes.

Global Risk Level remains:

**HIGH**

Active CRITICAL risks remain:

**7**

Next risk review gate:

**M3.1 executable contracts and deterministic pipeline skeleton**
---

# 19. M3.1-B executable world-generation contract risk review

**Date:** 2026-09-29

Evidence:

`GlobalArena-Evidence-M3.1-B-R1-EXECUTABLE-WORLDGEN-CONTRACTS-20260929-162926.zip`

SHA-256:

`2fdc52f81c9c87a4c96a2d88cdfa369cc97123274d878a56a992cd55e8fcbca9`

Result:

**PASS_READY_FOR_M3_1_B_FORMAL_CLOSE**

Validated:

- explicit `WorldSeed` domain;
- explicit positive `WorldGenerationVersion`;
- immutable request/result boundaries;
- `IWorldGenerator` executable interface;
- authoritative M2 strategic topology reuse;
- local Release suite `455/455`;
- zero compiler warnings/errors.

Risk effect:

`RISK-004 — Determinism failure` remains `WATCHING / HIGH`.

The seed/version boundary is executable, but deterministic sub-stream derivation, concrete pipeline behavior and cross-platform generated-world equivalence remain for M3.1-C / M3.1-D.

`RISK-008 — Architecture underengineering` remains `OPEN / CRITICAL`.

The first request/result/interface boundary now exists and reduces ambiguity, but the pipeline composition model has not yet been exercised by a concrete generator.

`RISK-006 — Scope expansion` remains `MITIGATING / CRITICAL`.

The implementation stays inside the frozen M3.1-A boundary: no Economy runtime state, Civilization runtime state, UI or networking responsibility was added.

`RISK-002 — Tactical resolution scalability` and `RISK-012 — Memory footprint` remain unchanged.

M3.1-B materializes no tactical physical field and therefore does not change the existing scale/memory envelope.

Global Risk Level remains:

**HIGH**

Active CRITICAL risks remain:

**7**

Next risk review gate:

**M3.1-C deterministic pipeline skeleton and contract validation**
---

# 20. M3.1-C deterministic pipeline risk review

**Date:** 2026-09-29

Evidence:

`GlobalArena-Evidence-M3.1-C-R1-DETERMINISTIC-PIPELINE-SKELETON-20260929-164625.zip`

SHA-256:

`a70d4adace51bfcbb87f51b7119180cac70714672d8fb915cabb6135a6eaf6ab`

Result:

**PASS_READY_FOR_M3_1_C_FORMAL_CLOSE**

Validated locally:

- concrete deterministic `IWorldGenerator`;
- explicit world-generation version `1` support boundary;
- authoritative M2 topology reuse;
- domain-separated deterministic random streams;
- seed/version/Goldberg/domain participation in stream derivation;
- stream-order independence;
- fixed deterministic known vector;
- local Release suite `475/475`;
- zero compiler warnings/errors.

Risk effect:

`RISK-004 — Determinism failure` remains `WATCHING / HIGH`.

M3.1-C materially reduces this risk by making domain-separated generation randomness executable and testable, but its new production/test tree has not yet completed the post-push Ubuntu/Windows/macOS regression. M3.1-D must consume that evidence before any further maturity promotion.

`RISK-008 — Architecture underengineering` remains `OPEN / CRITICAL`.

The pipeline boundary is now exercised by a concrete generator and M2 topology reuse. Broader generated-world layer composition remains future M3 work.

`RISK-006 — Scope expansion` remains `MITIGATING / CRITICAL`.

M3.1-C remains inside the frozen M3.1-A scope and introduces no Economy, Civilization runtime, UI or networking responsibility.

`RISK-002 — Tactical resolution scalability` and `RISK-012 — Memory footprint` remain unchanged.

M3.1-C generates strategic topology and deterministic random streams but does not materialize new tactical physical fields.

Global Risk Level remains:

**HIGH**

Active CRITICAL risks remain:

**7**

Next risk review gate:

**M3.1-D accumulated deterministic and cross-platform validation**
---

# 21. M3.1-D accumulated deterministic validation and M3.1 close risk review

**Date:** 2026-09-29

Evidence:

GlobalArena-Evidence-M3.1-D-R1-ACCUMULATED-M31-VALIDATION-20260929-221715.zip

SHA-256:

33f1b9cb5ada5c4b7f23bf505ff69a07b97d49a6116ddd1db4da66965c4e5d27

Result:

**PASS_READY_FOR_M3_1_D_FORMAL_CLOSE**

Validated:

- accepted M3.1 source/test tree unchanged from HEAD 2e737933e2370676e7a838007b0fbafde17d7c8c;
- targeted M3.1 suite 40/40;
- full local Release suite 475/475;
- compiler warnings/errors 0/0;
- Ubuntu cross-platform suite 475/475;
- Windows cross-platform suite 475/475;
- macOS cross-platform suite 475/475;
- cross-platform artifacts uploaded on all three platforms;
- domain-separated deterministic random-stream contract preserved;
- fixed deterministic vector preserved;
- no production-code mutation in the accumulated close gate.

Risk effect:

RISK-004 — Determinism failure remains WATCHING / HIGH.

M3.1-D materially reduces the world-generation determinism risk: the executable generation identity/version boundary, deterministic stream partitioning and current regression vector now pass locally and on Ubuntu, Windows and macOS. The risk remains watching because later M3 physical layers must extend determinism to the complete generated-world semantics and M3.6 still owns the final generated-world signature and accumulated exit regression.

RISK-008 — Architecture underengineering remains OPEN / CRITICAL.

The M3.1 request/result/generator boundary is now validated and closed, but strategic physical fields, cross-scale refinement and later generated-world layer composition remain future architecture work.

RISK-006 — Scope expansion remains MITIGATING / CRITICAL.

M3.1 closes inside the frozen budget and responsibility envelope. No Economy runtime state, live Civilization state, UI, networking or unrelated product capability was introduced.

RISK-002 — Tactical resolution scalability remains OPEN / CRITICAL.

M3.1 does not materialize new tactical physical fields. Scale pressure moves to the later cross-scale refinement stages where tactical detail is introduced selectively.

RISK-012 — Memory footprint remains OPEN / CRITICAL.

The M3.1 contracts do not require global tactical materialization. Later M3 physical-field representation must continue to preserve the data-oriented and selective-materialization constraints.

Global Risk Level remains:

**HIGH**

Active CRITICAL risks remain:

**7**

Next risk review gate:

**M3.2 strategic geometry bridge, macro physical-field substrate and elevation/land-water foundation**
---

# 22. M3.2-A strategic geometry and physical-field design risk review

**Date:** 2026-09-29

Baseline:

86757b30ce67c35d8fc1283d0c2d94b4044423e7

Decision gate:

**M3.2-A — Strategic Geometry & Physical-Field Contract Freeze**

Key architectural finding:

M2 intentionally separates combinatorial topology from geometric/render embedding. StrategicTopology provides stable identity, adjacency and incidence, but coordinates are not topological truth.

Risk response:

### RISK-004 — Determinism failure

Status remains:

**WATCHING / HIGH**

M3.2-A reduces a new source of determinism risk by freezing the first macro physical-field substrate on canonical integer indexes and signed fixed-point raw values rather than platform-sensitive floating-point topology identity.

Later algorithms still require deterministic regression evidence, so the risk is not closed.

### RISK-008 — Architecture underengineering

Status remains:

**OPEN / CRITICAL**

The geometry bridge is explicitly a derived view over M2 topology rather than a second generator.

This avoids duplicate Goldberg ownership while creating a dedicated data-oriented boundary for physical fields.

The risk remains critical until the bridge and field substrate are executable and consumed by real generated-world stages.

### RISK-012 — Memory footprint

Status remains:

**OPEN / CRITICAL**

M3.2-A forbids mandatory global tactical physical materialization.

The first macro substrate is O(strategic cells) and uses compact canonical arrays/indexes.

Later tactical refinement remains stage-specific and must preserve this constraint.

### RISK-002 — Tactical resolution scalability

Status remains:

**OPEN / CRITICAL**

No new tactical density is frozen by M3.2-A.

The M2 Class I scale-6 hierarchy remains a validated semantic contract rather than a mandatory always-resident M3 field resolution.

### RISK-006 — Scope expansion

Status remains:

**MITIGATING / CRITICAL**

M3.2-A keeps climate, hydrology, biomes, resources, Economy runtime and Civilization runtime outside this checkpoint.

Global Risk Level remains:

**HIGH**

Active CRITICAL risks remain:

**7**

Next risk review gate:

**M3.2-B executable strategic surface graph and fixed-point scalar substrate**
---

# 23. M3.2-A formal close risk confirmation

**Date:** 2026-09-29

Accepted QA:

`GlobalArena-Evidence-M3.2-A-R4-GOVERNANCE-SANITIZATION-20260929-224521.zip`

Result:

**M3.2-A CLOSED**

Risk posture is unchanged by this design-only close:

- `RISK-004 — Determinism failure`: `WATCHING / HIGH`;
- `RISK-008 — Architecture underengineering`: `OPEN / CRITICAL`;
- `RISK-012 — Memory footprint`: `OPEN / CRITICAL`;
- `RISK-002 — Tactical resolution scalability`: `OPEN / CRITICAL`;
- `RISK-006 — Scope expansion`: `MITIGATING / CRITICAL`.

Global Risk Level remains:

**HIGH**

Active CRITICAL risks remain:

**7**

Next risk review gate:

**M3.2-B executable strategic surface graph and fixed-point scalar substrate**
---

# 24. M3.2-B executable physical-field substrate risk review

**Date:** 2026-09-29

Evidence:

`GlobalArena-Evidence-M3.2-B-R1-EXECUTABLE-SURFACE-GRAPH-SCALAR-SUBSTRATE-20260929-225811.zip`

Result:

**PASS_READY_FOR_M3_2_B_FORMAL_CLOSE**

Validated:

- canonical dense strategic surface indexing;
- canonical sorted strategic-neighbor indexes;
- immutable `Int64` fixed-point scalar substrate;
- denominator `1_000_000`;
- `WorldGenerationResult` integration;
- no global tactical materialization;
- targeted suite `32/32`;
- full local Release suite `507/507`;
- compiler warnings/errors `0/0`.

Risk effect:

`RISK-004 — Determinism failure` remains `WATCHING / HIGH`.

The first physical-field substrate now avoids ambient randomness and platform-sensitive floating-point storage in its authoritative baseline. M3.2-C must still prove deterministic elevation/relief/land-water semantics, and later accumulated/cross-platform gates remain required.

`RISK-008 — Architecture underengineering` remains `OPEN / CRITICAL`.

The previously frozen physical-field bridge is now executable and integrated into generation results, materially reducing ambiguity. The risk remains open because real physical-process consumers and later cross-scale refinement are not yet implemented.

`RISK-012 — Memory footprint` remains `OPEN / CRITICAL`.

The new macro substrate is O(strategic cells) and does not require global tactical residency. Later tactical physical fields must preserve selective materialization.

`RISK-002 — Tactical resolution scalability` remains `OPEN / CRITICAL`.

M3.2-B adds no new tactical density and therefore does not expand the accepted physical hierarchy envelope.

`RISK-006 — Scope expansion` remains `MITIGATING / CRITICAL`.

M3.2-B stays inside the frozen strategic geometry and macro physical-field substrate boundary and adds no climate, hydrology, biome, Economy, Civilization runtime, networking or presentation responsibility.

Global Risk Level remains:

**HIGH**

Active CRITICAL risks remain:

**7**

Next risk review gate:

**M3.2-C deterministic elevation, relief and land/water foundation**
---

# 25. M3.2-C deterministic elevation and land/water risk review

**Date:** 2026-09-30

Evidence:

`GlobalArena-Evidence-M3.2-C-R1-DETERMINISTIC-ELEVATION-RELIEF-LAND-WATER-20260930-084020.zip`

Result:

**PASS_READY_FOR_M3_2_C_FORMAL_CLOSE**

Validated:

- deterministic fixed-point strategic elevation;
- elevation randomness isolated to `WorldGenerationRandomDomain.Elevation`;
- fixed initial-version elevation regression vector;
- relief derived from canonical strategic neighbors;
- explicit deterministic sea-level contract;
- land/water derived from elevation without independent randomness;
- integrated strategic physical-field result;
- targeted suite `32/32`;
- full local Release suite `539/539`;
- compiler warnings/errors `0/0`.

Risk effect:

`RISK-004 — Determinism failure` remains `WATCHING / HIGH`.

The first terrain semantics are now executable with integer/fixed-point authoritative state and a deterministic regression vector. M3.2-D must consume the post-push Ubuntu/Windows/macOS regression before any accumulated maturity promotion.

`RISK-008 — Architecture underengineering` remains `OPEN / CRITICAL`.

The physical-field substrate now has a real terrain consumer and generation-result integration. The risk remains open because later climate, cross-scale refinement, hydrology and other world layers still depend on this architecture.

`RISK-012 — Memory footprint` remains `OPEN / CRITICAL`.

M3.2-C remains O(strategic cells) and introduces no global tactical field residency.

`RISK-002 — Tactical resolution scalability` remains `OPEN / CRITICAL`.

No tactical materialization or new tactical density is introduced by the elevation/relief/land-water foundation.

`RISK-006 — Scope expansion` remains `MITIGATING / CRITICAL`.

M3.2-C implements only elevation, derived relief and initial land/water. Climate, moisture, hydrology, biomes, resources, Civilization runtime, Economy runtime, networking and presentation remain outside this checkpoint.

Global Risk Level remains:

**HIGH**

Active CRITICAL risks remain:

**7**

Next risk review gate:

**M3.2-D accumulated M3.2 validation and close**
---

# 26. M3.2 accumulated validation and close risk review

**Date:** 2026-09-30

Accepted evidence:

`GlobalArena-Evidence-M3.2-D-R1-ACCUMULATED-M32-VALIDATION-20260930-085711.zip`

Cross-platform run:

`36710766790`

Result:

**M3.2 CLOSED**

Validated across the accepted M3.2 envelope:

- authoritative M2 topology reuse;
- canonical dense strategic indexing;
- canonical neighbor ordering;
- immutable `Int64` fixed-point scalar substrate;
- deterministic elevation domain ownership;
- fixed deterministic elevation vector;
- relief derived from canonical strategic neighbors;
- explicit deterministic sea level;
- land/water derived from elevation without independent randomness;
- integrated strategic physical fields in `WorldGenerationResult`;
- no global tactical physical-field materialization;
- local accumulated tests `64/64`;
- local full Release suite `539/539`;
- Windows/macOS/Ubuntu `539/539` each with zero compiler warnings/errors.

Risk effect:

`RISK-004 — Determinism failure` remains `WATCHING / HIGH`.

M3.2 physical fields now have local and cross-platform deterministic regression evidence. The risk remains active because climate, tactical refinement, hydrology, resources and later state/signature evolution can introduce new nondeterministic paths.

`RISK-008 — Architecture underengineering` remains `OPEN / CRITICAL`.

The strategic physical-field substrate has now survived executable consumption and cross-platform validation. The risk remains open because M3.3 introduces the first cross-scale boundary/refinement contracts and broader physical-system coupling.

`RISK-012 — Memory footprint` remains `OPEN / CRITICAL`.

M3.2 closed without global tactical field residency. M3.3 must preserve selective tactical materialization and strategic aggregation rather than expanding to full-planet tactical arrays.

`RISK-002 — Tactical resolution scalability` remains `OPEN / CRITICAL`.

M3.2 does not change tactical density. M3.3 cross-scale refinement will be the next stage where physical-field tactical materialization can materially affect this risk.

`RISK-006 — Scope expansion` remains `MITIGATING / CRITICAL`.

M3.2 closed inside its frozen 26 GPP envelope and did not absorb climate, hydrology, biomes, resources, Economy runtime, Civilization runtime, networking or presentation.

Global Risk Level remains:

**HIGH**

Active CRITICAL risks remain:

**7**

Next risk review gate:

**M3.3 entry/design audit — climate inputs and cross-scale physical refinement**
---

# 27. M3.3-A climate and cross-scale physical design risk review

**Date:** 2026-09-30

Baseline:

`9608fb2f514c1d6e4ab3541c76a1dbea78f2cba9`

Design result:

**M3.3-A candidate frozen for validation**

Risk effect:

`RISK-004 — Determinism failure` remains `WATCHING / HIGH`.

M3.3 explicitly assigns temperature, moisture and tactical-refinement randomness to separate existing domains. Tactical refinement additionally requires scope-keyed/order-independent deterministic derivation before stochastic local detail can be accepted.

`RISK-008 — Architecture underengineering` remains `OPEN / CRITICAL`.

M3.3 reuses the M2 physical hierarchy instead of creating a parallel tactical topology, and keeps strategic aggregates as the persistent macro boundary. The risk remains open until the executable cross-scale contracts prove this separation.

`RISK-012 — Memory footprint` remains `OPEN / CRITICAL`.

The design rejects persistent global tactical physical-field residency by default. M3.3-C must provide bounded materialization evidence and must not silently turn a globally materialized M2 reference map into a permanent generated-world storage requirement.

`RISK-002 — Tactical resolution scalability` remains `OPEN / CRITICAL`.

M3.3-C must demonstrate that physical refinement scope and aggregation do not make recurring strategic work proportional to global tactical resolution.

`RISK-006 — Scope expansion` remains `MITIGATING / CRITICAL`.

Hydrology, biomes, resources, Civilization runtime, Economy runtime, networking and presentation remain outside M3.3-A.

Global Risk Level remains:

**HIGH**

Active CRITICAL risks remain:

**7**

Next risk review gate:

**M3.3-B executable strategic temperature, moisture and water availability**
---

# 28. M3.3-A formal close risk confirmation

**Date:** 2026-09-30

Accepted QA:

`GlobalArena-Evidence-M3.3-A-R2-CLIMATE-CROSS-SCALE-CONTRACT-FREEZE-20260930-093817.zip`

Result:

**M3.3-A CLOSED**

Risk posture is unchanged by this design-only close:

- `RISK-004 — Determinism failure`: `WATCHING / HIGH`;
- `RISK-008 — Architecture underengineering`: `OPEN / CRITICAL`;
- `RISK-012 — Memory footprint`: `OPEN / CRITICAL`;
- `RISK-002 — Tactical resolution scalability`: `OPEN / CRITICAL`;
- `RISK-006 — Scope expansion`: `MITIGATING / CRITICAL`.

Global Risk Level remains:

**HIGH**

Active CRITICAL risks remain:

**7**

Next risk review gate:

**M3.3-B executable strategic temperature, moisture and water availability**
---

# 29. M3.3-B strategic climate fields risk review

**Date:** 2026-09-30

Accepted QA:

`GlobalArena-Evidence-M3.3-B-R1-STRATEGIC-CLIMATE-FIELDS-20260930-095956.zip`

Result:

**PASS_READY_FOR_M3_3_B_FORMAL_CLOSE**

Validated:

- deterministic strategic temperature;
- deterministic strategic moisture;
- derived strategic water availability;
- explicit domain separation for Temperature and Moisture;
- no independent water-availability RNG;
- fixed-point strategic climate storage;
- `WorldGenerationResult` integration;
- targeted suite `36/36`;
- full local Release suite `575/575`;
- compiler warnings/errors `0/0`.

Risk effect:

`RISK-004 — Determinism failure` remains `WATCHING / HIGH`.

Strategic climate inputs now have deterministic domain-separated executable contracts and stable regression vectors. Cross-platform validation remains required at later accumulated gates.

`RISK-008 — Architecture underengineering` remains `OPEN / CRITICAL`.

The strategic climate layer now uses the accepted M3.2 substrate. M3.3-C remains the key architectural gate for bounded physical refinement and strategic aggregation.

`RISK-012 — Memory footprint` remains `OPEN / CRITICAL`.

M3.3-B adds only O(strategic cells) persistent fields. Tactical physical-field residency remains deferred to bounded M3.3-C contracts.

`RISK-002 — Tactical resolution scalability` remains `OPEN / CRITICAL`.

M3.3-B adds no tactical materialization. M3.3-C must prove bounded physical scope and avoid global tactical work in recurring strategic paths.

`RISK-006 — Scope expansion` remains `MITIGATING / CRITICAL`.

Hydrology, biomes, resources, Civilization runtime, Economy runtime, networking and presentation remain outside M3.3-B.

Global Risk Level remains:

**HIGH**

Active CRITICAL risks remain:

**7**

Next risk review gate:

**M3.3-C bounded tactical refinement and strategic aggregation**
---

# 30. M3.3-C bounded tactical refinement and aggregation risk review

**Date:** 2026-09-30

Accepted QA:

`GlobalArena-Evidence-M3.3-C-R2-BOUNDED-TACTICAL-REFINEMENT-AGGREGATION-20260930-104047.zip`

Result:

**PASS_READY_FOR_M3_3_C_FORMAL_CLOSE**

Validated:

- bounded retained tactical scalar patches;
- M2 physical tactical identity reuse;
- explicit deterministic strategic boundary values;
- canonical physical ordering;
- explicit `6/3/2` aggregation weighting;
- deterministic strategic aggregation;
- targeted suite `34/34`;
- full local Release suite `609/609`;
- compiler warnings/errors `0/0`.

Risk effect:

`RISK-004 — Determinism failure` remains `WATCHING / HIGH`.

M3.3-C adds no tactical RNG and uses deterministic integer arithmetic and canonical identity ordering. M3.3-D still requires accumulated and cross-platform validation of the accepted M3.3 tree.

`RISK-008 — Architecture underengineering` remains `OPEN / CRITICAL`.

The M2 physical identity model is now consumed by an executable M3 bounded projection and strategic aggregation contract. The risk remains open until later stages prove broader physical-system composition.

`RISK-012 — Memory footprint` remains `OPEN / CRITICAL`.

The retained patch is bounded and does not retain the global physical incidence map. However, current materialization still creates the M2 full incidence map transiently before projection. This must not be mistaken for a fully bounded construction-memory implementation.

`RISK-002 — Tactical resolution scalability` remains `OPEN / CRITICAL`.

The current output scope is bounded, but construction cost still includes the global M2 incidence materialization. M3.3-D can validate current correctness and minimum performance evidence, but final V1 tactical density/scalability remains unresolved.

`RISK-006 — Scope expansion` remains `MITIGATING / CRITICAL`.

Hydrology, biomes, resources, Civilization runtime, Economy runtime, networking and presentation remain outside M3.3-C.

Global Risk Level remains:

**HIGH**

Active CRITICAL risks remain:

**7**

Next risk review gate:

**M3.3-D accumulated M3.3 validation and close**
---

# 31. M3.3 accumulated validation and stage-close risk review

**Date:** 2026-09-30

Accepted accumulated evidence:

`GlobalArena-Evidence-M3.3-D-R1-ACCUMULATED-M33-VALIDATION-20260930-110833.zip`

Result:

**M3.3 CLOSED — 23.80 / 28 GPP — 85.0%**

Validated:

- deterministic strategic temperature and moisture domain separation;
- derived water availability without an independent random domain;
- strategic climate integration into generated-world results;
- bounded retained tactical physical patches;
- M2 physical tactical identity reuse;
- deterministic incident-strategic boundary values;
- deterministic `6/3/2` weighted strategic aggregation;
- targeted M3.3 suite `70/70`;
- full local suite `609/609`;
- Windows/macOS/Ubuntu cross-platform regression `609/609` on all three platforms;
- bounded-path performance inside the existing blocking M2 physical budget.

Risk effect:

`RISK-004 — Determinism failure` remains `WATCHING / HIGH`.

M3.3 has accumulated local and cross-platform validation with explicit deterministic domains, integer arithmetic, fixed regression vectors and no tactical RNG in the accepted bounded baseline. Later hydrology, biomes, resources and full-world signatures can still introduce new determinism risk.

`RISK-008 — Architecture underengineering` remains `OPEN / CRITICAL`.

M3.3 proves the strategic climate layer and a first executable strategic↔tactical physical-field boundary. Hydrology and biome composition in M3.4 are the next architecture-coupling gate.

`RISK-012 — Memory footprint` remains `OPEN / CRITICAL`.

The retained M3.3 patch is bounded and does not retain the global physical incidence map. The construction path still materializes that M2 map transiently before projection, so final tactical construction-memory risk remains unresolved.

`RISK-002 — Tactical resolution scalability` remains `OPEN / CRITICAL`.

The bounded path passes the inherited blocking physical budget at `G(4,0) -> G(24,0)`, but construction cost remains tied to transient global incidence materialization. Final V1 tactical density and product-scale behavior remain later validation work.

`RISK-006 — Scope expansion` remains `MITIGATING / CRITICAL`.

M3.3 closed inside its frozen `28 GPP` envelope and did not absorb hydrology, biomes, resources, Civilization runtime, Economy runtime, networking or presentation.

Global Risk Level remains:

**HIGH**

Active CRITICAL risks remain:

**7**

Next risk review gate:

**M3.4 entry/design audit — hydrology and derived biome contracts**

# Global Arena — Risk Register

**Versão:** 0.1
**Milestone:** M2 — Planet Topology
**Status:** Ativo
**Última revisão formal:** 2026-09-19
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

Mitigação:

- data-oriented design;
- estruturas compactas;
- tiles como dados;
- carregamento e processamento seletivo;
- evitar GameObject por tile;
- benchmarks de memória;
- níveis de atividade.

Próxima ação:

benchmark estrutural após M2.

---

## RISK-003 — Goldberg hierarchy mapping

Status:

MITIGATING

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

Próxima ação:

iniciar M2.4.5 — Refinement Stage Validation & M2.4 Close com audit read-only do conjunto completo de evidências M2.4.1–M2.4.4, determinando o que pode ser formalmente declarado no fechamento do stage sem extrapolar o narrow lineage coverage atual.

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

Planetas gigantes com milhões de subtiles podem exceder budgets de memória se os dados forem representados de forma ingênua.

Mitigação:

- estruturas compactas;
- IDs numéricos;
- arrays contíguos;
- bit fields quando apropriado;
- dados derivados não persistidos quando barato recalcular;
- streaming;
- profiling.

---

# 5. Riscos globais atuais

Critical:

- Economy scalability
- Tactical resolution scalability
- Determinism
- Multiplayer synchronization
- Scope expansion
- Architecture underengineering
- UI complexity
- Memory footprint

High:

- Goldberg hierarchy
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

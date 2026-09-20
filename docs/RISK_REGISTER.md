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
- testes explícitos de pertencimento pai-filho;
- testes de continuidade entre regiões;
- validação de casos representativos das famílias Goldberg;
- permitir redução do conjunto de famílias suportadas caso a hipótese geral não se sustente.

Próxima ação:

implementar em M2.2.3 `StrategicTacticalRegionMaterializer`, consumindo `StrategicTopology` e produzindo uma coleção somente leitura com exatamente uma `TacticalRegion` por `StrategicCell`, em ordem canônica e com assinatura determinística; manter aggregate cross-region, shared border bands, pertencimento multi-região e conectividade cross-region fora do escopo até M2.3.

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

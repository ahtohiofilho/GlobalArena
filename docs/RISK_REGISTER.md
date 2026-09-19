# Global Arena — Risk Register

**Versão:** 0.1
**Milestone:** M1 — Deterministic Simulation Kernel
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

OPEN

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
- testes explícitos de pertencimento pai-filho;
- testes de continuidade entre regiões;
- validação de casos representativos das famílias Goldberg;
- permitir redução do conjunto de famílias suportadas caso a hipótese geral não se sustente.

Próxima ação:

spike geométrico em M2 para validar a hierarquia estratégico/tática e determinar quais famílias `G(m,n)` podem ser suportadas com os invariantes exigidos.

---

## RISK-004 — Determinism failure

Status:

MITIGATING

Probability:

3

Impact:

4

Score:

12 — CRITICAL

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
- hash de estado;
- avaliar uso de matemática determinística onde necessário.

Próxima ação:

ampliar a prova para múltiplos Commands simultâneos e, depois, validar replay através do EventLog antes do fechamento de M1.

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

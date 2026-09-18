# Global Arena — Roadmap

**Versão:** 0.1
**Status:** Baseline em construção
**Milestone atual:** M0 — Project Baseline

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

---

## M2 — Planet Topology

Objetivo:

estabelecer o planeta lógico.

Inclui:

- Goldberg estratégico;
- Goldberg tático;
- StrategicCell;
- StrategicEdge;
- StrategicVertex;
- TacticalCell;
- adjacências;
- tiles compartilhados;
- pertencimento;
- testes topológicos;
- validação de escalabilidade.

Gate:

o planeta forma uma topologia fechada e navegável sem depender da Unity.

---

## M3 — Procedural World

Objetivo:

transformar topologia em mundo.

Inclui:

- elevation;
- hydrology;
- climate;
- biomes;
- resources;
- habitability;
- civilization placement;
- generation versions;
- seed reproducibility.

Gate:

uma seed gera um planeta funcional e reproduzível.

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

**M0 — Project Baseline**

Current Stage:

**M0.7 — Minimum Kernel Baseline and Progress Calibration**

Current Subcheckpoint:

**M0.7.5 — Minimum Simulation Context**

Official Progress:

**Not yet consolidated**

Motivo:

o Kernel mínimo e a decomposição final do baseline M0 ainda precisam ser concluídos.

O primeiro percentual oficial será calculado no fechamento do baseline M0.

Last Baseline Review:

**2026-09-18**

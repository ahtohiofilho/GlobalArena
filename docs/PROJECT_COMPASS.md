# Global Arena — Project Compass

## 1. Identidade do projeto

**Nome:** Global Arena
**Plataforma inicial:** PC
**Modelo:** jogo de estratégia e tática em escala planetária
**Estado atual:** M0 — Project Baseline
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
Seed

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
**M0 — Project Baseline**

Etapa:
**M0.7 — Minimum Kernel Baseline and Progress Calibration**

Subetapa atual:
**M0.7.6 — Minimum World State**

Progresso oficial:
**ainda não estabelecido**

Motivo:
o baseline M0 ainda precisa do Kernel mínimo e da primeira calibração formal de GPP.

Scope Confidence:
**moderada**

Critical Path atual:
**estabelecimento do Simulation Kernel mínimo e fechamento do M0**

Última revisão de baseline:
**2026-09-18**

---

# 21. Política de atualização

Este documento deverá ser revisado quando:

- um milestone for iniciado ou concluído;
- ocorrer alteração relevante de escopo;
- uma decisão arquitetural mudar;
- surgir risco estrutural relevante;
- houver mudança significativa no Critical Path.

Mudanças históricas não devem ser apagadas do Progress Ledger.

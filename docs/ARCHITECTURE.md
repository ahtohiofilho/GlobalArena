# Global Arena — Architecture

**Versão:** 0.1
**Status:** Baseline inicial
**Milestone:** M0 — Project Baseline

---

# 1. Objetivo

Este documento descreve a arquitetura técnica inicial do Global Arena.

A arquitetura deve permitir:

- crescimento progressivo do projeto;
- subsistemas desacoplados;
- substituição de implementações;
- testes independentes;
- multiplayer;
- geração procedural;
- simulação em grande escala;
- controle do blast radius de mudanças;
- profiling e otimização localizada;
- evolução sem dependência da engine gráfica.

O objetivo não é prever todos os detalhes futuros.

O objetivo é estabelecer fronteiras suficientemente sólidas para que detalhes futuros possam evoluir sem exigir reconstrução sistêmica.

---

# 2. Princípio central

Global Arena será inicialmente um:

**monólito modular de simulação.**

Isso significa:

- os subsistemas podem executar no mesmo processo;
- cada subsistema possui responsabilidade própria;
- dependências entre subsistemas são explícitas;
- não haverá acesso arbitrário ao estado interno de outros módulos;
- não serão utilizados microserviços prematuramente.

Regra:

**simplicidade dentro dos módulos; contratos fortes entre os módulos.**

---

# 3. Separação fundamental

O projeto possui três grandes camadas conceituais:

## Simulation

Contém a verdade do jogo.

Responsável por:

- mundo;
- regras;
- economia;
- guerra;
- civilizações;
- diplomacia;
- IA;
- turnos;
- eventos;
- estado.

Não depende de Unity.

---

## Infrastructure

Fornece serviços externos à simulação.

Exemplos:

- networking;
- persistência;
- arquivos;
- banco de dados;
- logging;
- transporte de mensagens;
- integração de plataforma.

Pode depender da Simulation.

Simulation não deve depender das implementações concretas de Infrastructure.

---

## Presentation

Responsável pela experiência do jogador.

Exemplos:

- Unity;
- planeta 3D;
- câmera;
- UI;
- animação;
- áudio;
- input.

Presentation consome a Simulation.

Presentation nunca é fonte de verdade do estado do jogo.

---

# 4. Estrutura lógica prevista

A estrutura lógica de alto nível é:

SimulationKernel

World
WorldGeneration
Civilizations
Diplomacy
Economy
Warfare
ArtificialIntelligence

Persistence
Networking

Presentation

Nem todo módulo precisa corresponder imediatamente a um projeto C# separado.

A separação inicial será lógica e será promovida a separação física quando houver benefício claro.

---

# 5. Simulation Kernel

O Simulation Kernel é a fundação determinística.

Responsabilidades previstas:

- IDs estáveis;
- tempo lógico;
- número do turno;
- seeds;
- PRNG determinístico;
- Commands;
- Events;
- fila de resolução;
- políticas de turno;
- event log;
- versionamento de ruleset;
- invariantes globais da simulação.

O Kernel não deve conhecer:

- Unity;
- Steam;
- sockets;
- banco de dados;
- UI;
- shaders;
- áudio.

---

# 6. Modelo de execução

A simulação recebe três categorias fundamentais de entrada:

- `WorldState`: estado autoritativo do mundo antes da resolução;
- Commands: intenções submetidas à simulação;
- `SimulationContext`: metadados imutáveis necessários para executar aquela resolução de forma reproduzível.

Modelo conceitual:

WorldState
+
Commands
+
SimulationContext

→ Simulation →

NewWorldState
+
EventLog

Para uma mesma versão das regras, a combinação do mesmo:

- estado inicial;
- conjunto de comandos;
- turno;
- seed;

deve produzir o mesmo resultado.

Esse princípio constitui a base para:

- determinismo;
- replay;
- testes automatizados;
- diagnóstico de divergências;
- multiplayer autoritativo;
- simulações headless.

## 6.1 Minimum SimulationContext

`SimulationContext` representa exclusivamente o contexto determinístico de uma execução.

O baseline mínimo atual é:

- `TurnNumber Turn`;
- `SimulationSeed Seed`.

Conceitualmente:

SimulationContext
=
TurnNumber
+
SimulationSeed

O contexto é imutável e comparável por valor.

Ele não representa o estado do jogo e não é responsável por armazenar as entradas ou os resultados da resolução.

Por isso, não pertencem ao `SimulationContext`:

- `WorldState`;
- Commands;
- EventLog;
- estado mutável do PRNG.

O `WorldState` permanece como estado autoritativo da simulação.

Commands permanecem como intenções de entrada.

O `EventLog` permanece como produto da resolução.

O estado mutável do gerador pseudoaleatório pertence à execução da resolução e poderá ser criado a partir da `SimulationSeed`, sem ser persistido dentro do contexto.

Essa separação mantém distintas quatro responsabilidades:

1. estado do mundo;
2. intenções submetidas;
3. contexto determinístico da execução;
4. resultado produzido pela simulação.

Campos adicionais só deverão ser incorporados ao `SimulationContext` quando representarem metadados determinísticos realmente necessários à execução e possuírem contrato arquitetural explícito.

Versionamento de ruleset, por exemplo, permanece previsto pela arquitetura, mas não faz parte do baseline mínimo enquanto seu contrato próprio não estiver definido.

---

# 7. Commands e Events

Command representa intenção.

Exemplo:

MoveArmyCommand
AttackCommand
BuildCommand

Event representa uma ocorrência que será processada pela simulação.

Exemplo:

ArmyMoved
AttackResolved
UnitDestroyed
TerritoryControlChanged

Identidade determinística:

- CommandId identifica uma intenção submetida à simulação;
- EventId identifica uma ocorrência produzida a partir de um comando;
- EventId é composto por OriginCommandId + Sequence;
- Sequence é local à sequência de eventos produzidos pelo comando de origem;
- a identidade do evento não representa sua posição futura na fila de execução;
- ordenação, shuffle determinístico e posição de resolução permanecerão conceitos separados.

Essa separação preserva lineage e replay sem acoplar identidade à ordem de execução.

Commands e Events não são equivalentes.

Pipeline:

Command
→ validação inicial
→ geração de evento(s)
→ ordenação
→ execução
→ revalidação
→ alteração do estado
→ event log

---

# 8. Turn Resolution

O modelo inicial será:

Planning
→ Lock
→ Build Event Queue
→ Deterministic Shuffle
→ Sequential Resolution
→ Consolidation
→ Next Turn

Jogadores planejam simultaneamente.

Eventos são processados sequencialmente.

Cada evento deve ser revalidado no momento de sua execução.

A aleatoriedade da fila deve ser reproduzível.

## 8.1 Minimum Turn Resolution Contract

A resolução de turno possui um contrato explícito de entrada e saída antes da implementação do `TurnResolver`.

A entrada mínima é representada por `TurnResolutionInput`.

Ela contém:

- `WorldState WorldState`;
- `IReadOnlyList<ISimulationCommand> Commands`;
- `SimulationContext Context`.

Conceitualmente:

WorldState
+
Commands
+
SimulationContext

→ TurnResolutionInput

A coleção de Commands é capturada no momento da criação da entrada.

Alterações posteriores na coleção originalmente fornecida não modificam o conjunto de comandos da resolução.

Essa proteção estabiliza a membership e a ordem da coleção recebida pelo contrato, sem implicar cópia profunda dos Commands individuais.

O `WorldState` é referenciado como estado autoritativo de entrada e não é incorporado ao `SimulationContext`.

O resultado mínimo é representado por `TurnResolutionResult`.

Ele contém:

- `WorldState ResultingWorldState`;
- `IReadOnlyList<ISimulationEvent> Events`.

Conceitualmente:

TurnResolutionResult
=
ResultingWorldState
+
Events

A coleção de Events também é capturada no momento da criação do resultado, impedindo que alterações posteriores na coleção original modifiquem silenciosamente o resultado já produzido.

Nesse estágio, `Events` não constitui ainda o contrato definitivo de `EventLog`.

O EventLog permanece como capability própria e será definido quando existirem requisitos concretos de replay, persistência e diagnóstico.

O contrato atual não executa:

- validação de Commands;
- geração de Events;
- deterministic shuffle;
- resolução sequencial;
- revalidação de Events;
- mutação ou consolidação do `WorldState`;
- avanço de turno.

Essas responsabilidades pertencem às futuras etapas do `TurnResolver` e da pipeline de resolução.

O objetivo deste contrato é estabelecer a fronteira estável entre:

1. estado autoritativo de entrada;
2. intenções submetidas;
3. contexto determinístico;
4. estado resultante;
5. ocorrências produzidas.

---

# 9. Turn Policy

O modo temporal não deve fazer parte das regras fundamentais da simulação.

Uma TurnPolicy determina quando uma janela de ordens termina.

Exemplos previstos:

ManualReadyPolicy
TimedTurnPolicy
CorrespondencePolicy

Após o fechamento, todos utilizam o mesmo TurnResolver.

Isso permite que single-player e multiplayer compartilhem o mesmo núcleo.

---

# 10. World

World mantém a representação lógica do planeta.

A representação lógica é independente da mesh renderizada.

Principais conceitos previstos:

Planet
StrategicCell
StrategicEdge
TacticalCell
StrategicVertex
Terrain
Infrastructure

## 10.1 Minimum WorldState

`WorldState` representa a raiz do estado autoritativo do mundo utilizado pela simulação.

O baseline mínimo atual estabelece apenas a existência dessa raiz, sem antecipar estruturas de planeta, territórios, civilizações, economia ou guerra que ainda não possuem contratos concretos.

A criação inicial ocorre através de:

`WorldState.CreateInitial()`

Cada chamada produz uma nova instância independente.

O `WorldState` não contém o contexto determinístico da resolução.

Portanto, não pertencem ao estado do mundo:

- `SimulationContext`;
- `TurnNumber` utilizado como contexto da resolução;
- `SimulationSeed`;
- estado mutável do PRNG;
- Commands;
- EventLog.

Esses conceitos permanecem separados conforme o modelo de execução.

O `WorldState` também não utiliza igualdade estrutural por valor como contrato global. À medida que o mundo crescer, comparações determinísticas deverão utilizar mecanismos explícitos e adequados ao estado relevante, evitando transformar automaticamente toda a estrutura mundial em um grande value object.

Novos dados só deverão ser incorporados ao `WorldState` quando existir ownership de domínio e necessidade concreta na simulação.

Essa abordagem evita preencher prematuramente a raiz do mundo com conceitos ainda não definidos.

---

# 11. Topologia estratégica

O planeta estratégico utiliza topologia derivada de um poliedro de Goldberg.

Na lógica:

StrategicCell = nó
StrategicEdge = conexão
StrategicVertex = junção

A simulação estratégica trabalha prioritariamente sobre o grafo.

A renderização 3D é uma projeção dessa estrutura.

---

# 12. Camada tática

Cada região estratégica possui resolução tática muito superior.

A malha tática permite:

- posicionamento;
- terreno;
- combate;
- ocupação;
- movimento detalhado;
- fortificação;
- infraestrutura.

A resolução tática não deve determinar diretamente o custo dos sistemas estratégicos.

---

# 13. Arestas compartilhadas

StrategicEdges possuem uma faixa de TacticalCells compartilhada pelas regiões adjacentes.

Esses tiles constituem uma zona de transição real.

Usos previstos:

- fronteira;
- bloqueios;
- passagem;
- fortalezas;
- estradas;
- gargalos;
- controle militar;
- logística.

O estado estratégico de uma aresta pode ser derivado do estado tático dessa faixa.

---

# 14. World Generation

WorldGeneration será um pipeline substituível.

Pipeline inicial previsto:

Geometry
→ Elevation
→ Hydrology
→ Climate
→ Biomes
→ Resources
→ Habitability
→ Civilization Placement
→ Initial Economy

Cada etapa deve possuir contratos explícitos.

Uma etapa não deve precisar conhecer detalhes internos de etapas posteriores.

---

# 15. Seeds e geração reproduzível

Todo planeta procedural deverá possuir uma WorldSeed.

Também deverão ser versionados:

WorldGenerationVersion
RulesetVersion

Objetivo:

uma seed deve continuar semanticamente rastreável mesmo após evolução do gerador.

---

# 16. Civilizations

Civilizations será responsável por identidade e estado fundamental das civilizações.

Exemplos:

CivilizationId
Name
VisualIdentity
Territory
Relations reference
Economic ownership
Military ownership

A quantidade de civilizações não será limitada por uma paleta fixa.

---

# 17. Diplomacy

Modelo inicial:

Enemy
Neutral
Ally

Relações pertencem ao módulo Diplomacy.

Warfare não define relações diplomáticas.

Economy não define relações diplomáticas.

Esses módulos consultam Diplomacy através de contratos.

---

# 18. Economy

Economy será um bounded context próprio.

Responsabilidades previstas:

- recursos;
- mercadorias;
- produção;
- consumo;
- oferta;
- demanda;
- comércio;
- transporte;
- custos;
- capacidade;
- rotas;
- mercados.

Economy deve operar prioritariamente sobre o grafo estratégico.

Nunca deverá depender de pathfinding global sobre toda a malha tática para operações rotineiras.

---

# 19. Route System

Rotas deverão possuir dependências explícitas das arestas que utilizam.

Exemplo:

Route A
uses:
Edge 14
Edge 91
Edge 103
Edge 771

Se Edge 103 mudar:

apenas rotas dependentes de Edge 103 são invalidadas.

Objetivo:

evitar recomputação global desnecessária.

---

# 20. Warfare

Warfare será responsável por:

- unidades militares;
- ordens militares;
- movimentação;
- combate;
- dano;
- destruição;
- ocupação;
- controle;
- bloqueio.

Warfare não deve modificar diretamente estruturas internas de Economy.

Exemplo de interação:

Warfare
→ TerritoryControlChanged

World
→ StrategicEdgeStateChanged

Economy
→ invalida rotas afetadas

---

# 21. Artificial Intelligence

IA será consumidora das mesmas regras disponíveis a jogadores.

A IA não deverá possuir acesso privilegiado ao estado interno da simulação além do explicitamente permitido pelo design.

Arquiteturalmente deverão ser possíveis diferentes agentes:

EconomicAI
MilitaryAI
DiplomaticAI
StrategicAI

A implementação inicial poderá ser mais simples.

---

# 22. Networking

Networking não contém regras do jogo.

Responsabilidades:

- transporte de Commands;
- autenticação;
- sessões;
- sincronização;
- snapshots;
- mensagens;
- reconexão.

Modelo:

Client
→ Command
→ Authoritative Server
→ Simulation
→ Result
→ Clients

---

# 23. Autoridade

O servidor será autoridade em multiplayer.

Clientes enviam intenções.

Clientes não determinam o resultado final.

O servidor:

- valida;
- resolve;
- consolida;
- distribui o estado autorizado.

---

# 24. Persistência

Persistence será responsável por:

- saves;
- snapshots;
- carregamento;
- migração;
- versionamento;
- armazenamento do event log quando necessário.

Simulation não deve saber se o estado foi salvo:

- em arquivo;
- banco;
- memória;
- cloud.

---

# 25. Replay e diagnóstico

A arquitetura deverá permitir reconstrução suficiente para:

- reproduzir turnos;
- depurar desync;
- analisar bugs;
- gerar replay;
- comparar estados.

Não é obrigatório utilizar event sourcing completo.

Modelo inicial preferido:

snapshot periódico
+
command/event log

---

# 26. Presentation

Presentation terá sua implementação inicial em Unity.

Responsabilidades:

- visualizar;
- navegar;
- selecionar;
- apresentar;
- animar;
- receber input.

Unity não define as regras.

Unity solicita ações através de Commands.

---

# 27. Read Models

UI não deverá consultar estruturas complexas internas arbitrariamente.

Serão produzidas projeções apropriadas.

Exemplos:

PlanetOverview
RegionOverview
CivilizationOverview
EconomyOverview
TradeOverview
MilitaryOverview

Isso reduz acoplamento entre UI e domínio.

---

# 28. Performance

Performance será tratada arquiteturalmente.

Princípios:

- data-oriented design onde necessário;
- evitar um objeto pesado por tile;
- estruturas compactas;
- processamento em lote;
- cache consciente;
- atualização incremental;
- processamento paralelo onde seguro;
- diferentes cadências para diferentes sistemas;
- profiling antes de otimização profunda.

---

# 29. Cadências diferentes

Nem todo sistema precisa executar a cada tick.

Exemplo conceitual:

Combat
→ frequência alta

Strategic Movement
→ frequência média

Economy
→ frequência própria

Climate
→ frequência baixa

Diplomacy AI
→ frequência ainda diferente

O Simulation Kernel deverá permitir scheduling apropriado no futuro.

---

# 30. Estado derivado

Sempre que possível, diferenciar:

Source State

de

Derived State

Exemplo:

controle dos tactical tiles
→ source state

EdgeBlocked
→ derived state

Uma flag estratégica deve preferencialmente possuir uma origem rastreável.

---

# 31. Ownership de dados

Cada dado deve possuir um módulo dono.

Exemplo:

DiplomaticRelation
→ Diplomacy

TradeRoute
→ Economy

MilitaryUnit
→ Warfare

Terrain
→ World

Módulos externos não alteram diretamente esses dados.

---

# 32. Comunicação entre módulos

Prioridade inicial:

1. chamadas através de contratos explícitos;
2. eventos de domínio quando houver desacoplamento real;
3. evitar event bus global para tudo.

Um event bus universal precoce pode ocultar dependências.

Dependências relevantes devem permanecer observáveis.

---

# 33. Dependências

Regra geral:

Presentation
↓
Application / orchestration
↓
Simulation modules
↓
Kernel

Infrastructure implementa interfaces requeridas pelas camadas superiores.

Dependências circulares não serão permitidas.

---

# 34. Engine independence

Nenhum dos seguintes tipos poderá fazer parte dos contratos fundamentais da Simulation:

UnityEngine.GameObject
UnityEngine.Transform
UnityEngine.Vector3
MonoBehaviour

Tipos matemáticos próprios ou abstrações independentes deverão ser usados quando necessário.

---

# 35. Estrutura inicial da solução

Não criaremos todos os módulos físicos imediatamente.

Primeira estrutura pretendida:

GlobalArena.Console
GlobalArena.Kernel
GlobalArena.World
GlobalArena.Simulation
GlobalArena.Tests
GlobalArena.Benchmarks

Outros projetos serão adicionados quando os respectivos módulos iniciarem implementação real.

---

# 36. Crescimento futuro previsto

Possíveis projetos futuros:

GlobalArena.WorldGen
GlobalArena.Economy
GlobalArena.Warfare
GlobalArena.Civilizations
GlobalArena.Diplomacy
GlobalArena.AI
GlobalArena.Networking
GlobalArena.Persistence
GlobalArena.Unity

A criação antecipada desses projetos sem necessidade será evitada.

---

# 37. Testabilidade

Simulation deve poder ser executada sem gráficos.

Exemplo:

CreateWorld
SubmitCommands
ResolveTurn
InspectState

Isso permitirá:

- unit tests;
- property tests;
- fuzzing;
- benchmarks;
- simulações massivas;
- replay;
- debugging determinístico.

---

# 38. Observabilidade

Sistemas críticos deverão expor métricas.

Exemplos:

TurnResolutionTime
EconomyUpdateTime
RouteRecalculationCount
ActiveRoutes
WorldGenerationTime
MemoryBySubsystem

Isso será importante para a governança de performance.

---

# 39. Versionamento técnico

Artefatos persistentes deverão poder identificar:

SaveSchemaVersion
WorldGenerationVersion
RulesetVersion
ProtocolVersion

Essas versões poderão evoluir independentemente.

---

# 40. Regra de evolução arquitetural

Esta arquitetura não é imutável.

Mudanças estruturais relevantes deverão gerar um ADR.

A arquitetura deve mudar quando evidência técnica justificar a mudança.

Ela não deverá mudar silenciosamente.

---

# 41. Anti-patterns a evitar

Evitar:

- GameManager global contendo tudo;
- Singleton universal;
- acesso direto entre todos os sistemas;
- um GameObject por tile;
- economia dependente da renderização;
- networking contendo regras;
- UI alterando estado diretamente;
- random global não determinístico;
- IDs baseados em referência de memória;
- recalcular o planeta inteiro após toda mudança;
- criar abstrações sem necessidade real;
- otimização sem benchmark.

---

# 42. Critério arquitetural de sucesso

A arquitetura estará cumprindo sua função se for possível:

- substituir a economia sem reescrever Warfare;
- substituir a UI sem alterar a Simulation;
- executar o jogo sem Unity;
- executar o servidor sem renderização;
- alterar WorldGeneration sem quebrar o Kernel;
- testar turnos isoladamente;
- reproduzir resultados;
- evoluir multiplayer sem duplicar regras;
- aumentar resolução tática sem crescimento equivalente do custo estratégico.

---

# 43. Status

Versão atual:

0.1

Status:

Baseline inicial.

Este documento deverá ser refinado conforme os spikes técnicos produzirem evidência real.

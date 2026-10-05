# Global Arena — Architecture

**Versão:** 0.2
**Status:** M3 world-generation design baseline
**Milestone:** M3 — Procedural World

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
→ revalidação
→ execução
→ alteração do estado
→ event log

## 7.1 Minimum Command Processing Contract

`ISimulationCommandProcessor` estabelece a fronteira mínima entre uma intenção submetida e a geração de ocorrências da simulação.

Entrada:

WorldState
+
ISimulationCommand
+
SimulationContext

→ ISimulationCommandProcessor →

0..N ISimulationEvent

O processor recebe:

- o `WorldState` autoritativo disponível para consulta;
- um `ISimulationCommand`;
- o `SimulationContext` da resolução.

O resultado é uma sequência de zero ou mais `ISimulationEvent`.

Esse contrato preserva a distinção arquitetural entre intenção e ocorrência.

A identidade dos Events produzidos pode manter lineage com o Command de origem através de:

`EventId.OriginCommandId = CommandId`

O contrato não define ainda:

- qual processor trata cada tipo concreto de Command;
- registro ou descoberta de processors;
- dispatch;
- validação completa do Command;
- ordering entre Commands;
- ordering entre Events;
- deterministic shuffle;
- aplicação dos Events ao `WorldState`;
- EventLog.

O `WorldState` fornecido ao processor representa o estado autoritativo utilizado para avaliar a intenção.

A alteração efetiva do estado permanece responsabilidade da etapa de execução de Events, preservando o pipeline:

Command
→ validação
→ geração de Event(s)
→ ordering
→ revalidação
→ execução
→ novo estado.

Nenhuma abstração adicional de registry, dispatcher ou handler hierarchy é estabelecida neste checkpoint.

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

## 8.2 Minimum Turn Resolver Baseline (historical)

Esta seção preserva o primeiro baseline executável do `TurnResolver`.

As seções 8.3 a 8.18 documentam a evolução posterior e prevalecem sobre as limitações históricas descritas nesta seção.

`TurnResolver` materializa a primeira orquestração executável da pipeline de resolução.

O resolver depende explicitamente de um:

`ISimulationCommandProcessor`

Essa dependência representa a fronteira atualmente disponível entre um Command submetido e os Events produzidos a partir dessa intenção.

O comportamento atual possui três caminhos explícitos.

### Zero Commands

Entrada:

WorldState
+
zero Commands
+
SimulationContext

→ TurnResolver →

mesmo WorldState
+
zero Events

Quando a entrada não contém Commands:

- o `WorldState` de entrada é preservado;
- nenhum Event é produzido;
- o command processor não é invocado para produzir o resultado do turno vazio;
- nenhuma aleatoriedade é consumida;
- nenhuma regra concreta de domínio é executada.

A propriedade já estabelecida permanece válida:

**ausência de intenção produz ausência de alteração de estado e ausência de eventos.**

### Um Command

Entrada:

WorldState
+
um ISimulationCommand
+
SimulationContext

→ TurnResolver
→ ISimulationCommandProcessor →

mesmo WorldState
+
0..N ISimulationEvent

Quando existe exatamente um Command:

- o `TurnResolver` encaminha o `WorldState` autoritativo ao processor;
- encaminha o Command recebido;
- encaminha o `SimulationContext`;
- recebe zero ou mais Events;
- incorpora esses Events ao `TurnResolutionResult`;
- preserva o `WorldState` de entrada como `ResultingWorldState`.

Esse é o primeiro caminho não vazio executável da resolução:

Command
→ TurnResolver
→ ISimulationCommandProcessor
→ Event(s)

A produção de Events não significa ainda execução desses Events.

Neste estágio, nenhum Event modifica o `WorldState`.

A distinção permanece:

Command
→ intenção

Event
→ ocorrência

Event execution
→ futura alteração autoritativa do estado

### Mais de um Command

Quando existem dois ou mais Commands, o resolver rejeita explicitamente a resolução com `NotSupportedException`.

Essa restrição é deliberada.

O projeto ainda não definiu:

- dispatch entre processors;
- ordering de múltiplos Commands;
- deterministic shuffle;
- event queue;
- ordering entre Events;
- execução sequencial.

A rejeição explícita impede que ordering ou semântica de múltiplos Commands sejam introduzidos implicitamente.

### Dependência do processor

`TurnResolver` exige um `ISimulationCommandProcessor` válido em sua construção.

Processor nulo é rejeitado.

Nenhum registry, service locator, dispatcher ou handler hierarchy é introduzido neste estágio.

O `TurnResolver` ainda não implementa:

- validação completa de Commands;
- aplicação de Events ao `WorldState`;
- revalidação de Events;
- event queue;
- deterministic shuffle;
- resolução de múltiplos Commands;
- execução sequencial de Events;
- consolidação de mudanças;
- avanço de turno;
- EventLog.

O próximo limite arquitetural necessário é estabelecer como um Event válido pode ser aplicado ao estado autoritativo sem misturar geração de Events com sua execução.


## 8.3 Minimum Event Execution Contract

`ISimulationEventExecutor` estabelece a fronteira mínima entre uma ocorrência da simulação e o estado resultante de sua execução.

Conceitualmente:

WorldState
+
ISimulationEvent
+
SimulationContext

→ ISimulationEventExecutor →

WorldState

O executor recebe:

- o `WorldState` autoritativo atual;
- um `ISimulationEvent`;
- o `SimulationContext` da resolução.

O resultado é um `WorldState`.

Esse contrato mantém separadas as responsabilidades de:

Command
→ intenção

Command Processor
→ geração de Event

Event Executor
→ aplicação da ocorrência ao estado

O contrato não define ainda:

- dispatch entre tipos de Event;
- revalidação de Event;
- resultado explícito de Event rejeitado;
- execução de múltiplos Events;
- ordering;
- deterministic shuffle;
- EventLog;
- estratégia definitiva de mutabilidade ou cópia do `WorldState`.

A integração do executor ao `TurnResolver` é realizada pelo caminho descrito em 8.4.

## 8.4 Single-Event Execution Path

`TurnResolver` integra agora as duas fronteiras mínimas já estabelecidas:

- `ISimulationCommandProcessor`;
- `ISimulationEventExecutor`.

O caminho não vazio suportado passa a ser:

WorldState
+
um ISimulationCommand
+
SimulationContext

→ ISimulationCommandProcessor
→ zero ou um ISimulationEvent
→ ISimulationEventExecutor
→ ResultingWorldState

O comportamento atual é deliberadamente restrito.

### Zero Commands

O comportamento previamente estabelecido permanece:

- o `WorldState` é preservado;
- nenhum Event é produzido;
- o Event Executor não é invocado.

### Um Command que produz zero Events

O Command é processado normalmente.

Quando o processor produz zero Events:

- o `WorldState` de entrada é preservado;
- o resultado contém zero Events;
- o Event Executor não é invocado.

### Um Command que produz exatamente um Event

Quando o processor produz exatamente um Event:

- o `WorldState` autoritativo é encaminhado ao Event Executor;
- o Event produzido é encaminhado ao Event Executor;
- o mesmo `SimulationContext` é encaminhado ao Event Executor;
- o `WorldState` retornado pelo executor passa a ser o `ResultingWorldState`;
- o Event produzido permanece no `TurnResolutionResult`.

Esse é o primeiro caminho orquestrado completo:

Command
→ Event
→ Event Executor
→ ResultingWorldState

O caminho prova a ligação estrutural entre intenção, ocorrência e estado resultante, mas ainda não representa uma regra concreta de gameplay nem prova determinismo end-to-end.

### Mais de um Event

Quando um único Command produz dois ou mais Events, a resolução é rejeitada explicitamente com `NotSupportedException` antes da execução de qualquer Event.

Essa restrição evita introduzir implicitamente:

- ordering entre Events;
- resolução sequencial;
- revalidação entre execuções;
- event queue;
- deterministic shuffle.

### Limites ainda abertos

O `TurnResolver` ainda não implementa:

- validação concreta de Commands;
- resultado explícito estruturado para Event rejeitado;
- execução sequencial de múltiplos Events;
- ordering ou deterministic shuffle;
- EventLog;
- regras concretas de domínio que alterem propriedades observáveis do mundo.

O próximo limite arquitetural é integrar a revalidação mínima ao caminho de execução de um único Event.

## 8.5 Minimum Event Revalidation Contract

`ISimulationEventRevalidator` estabelece a fronteira mínima responsável por verificar se um Event ainda pode ser executado contra o `WorldState` autoritativo atual.

Conceitualmente:

WorldState
+
ISimulationEvent
+
SimulationContext

→ ISimulationEventRevalidator
→ bool CanExecute

O revalidator recebe:

- o `WorldState` autoritativo atual;
- um `ISimulationEvent`;
- o `SimulationContext` da resolução.

O resultado mínimo atual é booleano:

- `true` indica que o Event pode prosseguir para execução;
- `false` indica que o Event não deve ser executado contra aquele estado.

A revalidação não altera o `WorldState`.

Essa fronteira existe porque o estado pode ter mudado entre a geração de um Event e o momento em que esse Event for efetivamente executado.

O contrato preserva a separação entre:

Command Processor
→ geração de Event

Event Revalidator
→ decisão de elegibilidade no estado atual

Event Executor
→ aplicação da ocorrência ao estado

O contrato não define ainda:

- integração com o `TurnResolver`;
- semântica final do `TurnResolutionResult` quando a revalidação falha;
- motivo estruturado de rejeição;
- revalidação de múltiplos Events;
- execução sequencial;
- ordering;
- deterministic shuffle;
- EventLog.

A integração da revalidação ao `TurnResolver` é realizada pelo caminho descrito em 8.6.


## 8.6 Single-Event Revalidation Path

`TurnResolver` integra agora `ISimulationEventRevalidator` ao caminho de execução de um único Event.

A pipeline mínima suportada passa a ser:

WorldState
+
um ISimulationCommand
+
SimulationContext

→ ISimulationCommandProcessor
→ zero ou um ISimulationEvent
→ ISimulationEventRevalidator
→ ISimulationEventExecutor
→ ResultingWorldState

A revalidação ocorre imediatamente antes da execução.

### Event autorizado

Quando o processor produz exatamente um Event e o revalidator retorna `true`:

- o Event é revalidado contra o `WorldState` autoritativo atual;
- o mesmo `SimulationContext` é utilizado na revalidação;
- o Event é executado somente após a revalidação;
- o `WorldState` retornado pelo executor torna-se o `ResultingWorldState`;
- o Event produzido permanece em `TurnResolutionResult.Events`.

A ordem mínima é explicitamente:

revalidate
→ execute

### Event rejeitado

Quando o revalidator retorna `false`:

- o Event não é executado;
- o `WorldState` de entrada é preservado;
- o Event produzido permanece em `TurnResolutionResult.Events`;
- nenhuma transição parcial de estado ocorre.

Nesse estágio, `TurnResolutionResult.Events` continua representando Events produzidos pela resolução e não um `EventLog` definitivo de Events efetivamente aplicados.

Um resultado estruturado de rejeição permanece fora do contrato mínimo atual.

### Caminhos sem Event

Quando não existem Commands ou quando um Command produz zero Events:

- o revalidator não é invocado;
- o executor não é invocado;
- o `WorldState` é preservado.

### Mais de um Event

Dois ou mais Events continuam explicitamente não suportados.

A rejeição ocorre antes de qualquer revalidação ou execução, evitando semântica parcial enquanto ordering e execução sequencial ainda não estiverem definidos.

### Limites ainda abertos

O `TurnResolver` ainda não implementa:

- resultado estruturado para rejeições;
- execução sequencial de múltiplos Events;
- ordering ou deterministic shuffle;
- EventLog;
- regras concretas de domínio que alterem propriedades observáveis do mundo.

A fronteira mínima de validação de Command é estabelecida pelo contrato descrito em 8.7, e sua integração ao fluxo real é documentada em 8.8.


## 8.7 Minimum Command Validation Contract

`ISimulationCommandValidator` estabelece a fronteira mínima responsável por verificar se um Command pode ser processado contra o `WorldState` autoritativo atual.

Conceitualmente:

WorldState
+
ISimulationCommand
+
SimulationContext

→ ISimulationCommandValidator
→ bool IsValid

O validator recebe:

- o `WorldState` autoritativo atual;
- um `ISimulationCommand`;
- o `SimulationContext` da resolução.

O resultado mínimo atual é booleano:

- `true` indica que o Command pode prosseguir para processamento;
- `false` indica que o Command não deve gerar Events naquele estado.

A validação não altera o `WorldState` e não gera Events.

Essa fronteira preserva a separação entre:

Command Validator
→ decisão de validade da intenção

Command Processor
→ geração de Event(s)

Event Revalidator
→ decisão de elegibilidade do Event no estado atual

Event Executor
→ aplicação da ocorrência ao estado

O contrato não define ainda:

- integração com o `TurnResolver`;
- motivo estruturado de rejeição de Command;
- validação de múltiplos Commands;
- ordering entre Commands;
- deterministic shuffle;
- EventLog.

A integração da validação ao caminho de resolução de um único Command é realizada pelo fluxo descrito em 8.8.


## 8.8 Single-Command Validation Path

`TurnResolver` integra agora `ISimulationCommandValidator` ao caminho real de resolução de um único Command.

A pipeline mínima suportada passa a ser:

WorldState
+
um ISimulationCommand
+
SimulationContext

→ ISimulationCommandValidator
→ ISimulationCommandProcessor
→ zero ou um ISimulationEvent
→ ISimulationEventRevalidator
→ ISimulationEventExecutor
→ ResultingWorldState

A ordem do caminho não vazio é explicitamente:

validate
→ process
→ revalidate
→ execute

### Command aceito

Quando o validator retorna `true`:

- o mesmo `WorldState` autoritativo é encaminhado ao processor;
- o mesmo Command é encaminhado ao processor;
- o mesmo `SimulationContext` permanece em toda a resolução;
- o caminho anterior de geração, revalidação e execução continua válido.

### Command rejeitado

Quando o validator retorna `false`:

- o processor não é invocado;
- nenhum Event é produzido;
- o Event Revalidator não é invocado;
- o Event Executor não é invocado;
- o `WorldState` de entrada é preservado;
- `TurnResolutionResult.Events` é vazio.

Nenhum resultado estruturado de rejeição é introduzido neste checkpoint.

### Turno vazio

Quando existem zero Commands:

- o validator não é invocado;
- o processor não é invocado;
- o revalidator não é invocado;
- o executor não é invocado;
- o `WorldState` é preservado.

### Mais de um Command

Dois ou mais Commands continuam explicitamente não suportados.

A rejeição ocorre antes de qualquer validação individual, evitando semântica implícita de ordering entre Commands.

### Mais de um Event

Dois ou mais Events continuam explicitamente não suportados.

A rejeição ocorre após validação e processamento do Command, porém antes de qualquer revalidação ou execução de Event.

### Limites ainda abertos

O fluxo ainda não implementa:

- ordering explícito de múltiplos Events;
- deterministic shuffle;
- execução sequencial de múltiplos Events;
- múltiplos Commands;
- EventLog;
- resultado estruturado de rejeições;
- regras concretas de domínio que produzam uma transição observável e reproduzível de ponta a ponta.

A fronteira explícita de ordering determinístico para Events é estabelecida pelo contrato descrito em 8.9.


## 8.9 Deterministic Event Ordering Contract

`ISimulationEventOrderer` estabelece a fronteira mínima responsável por receber uma coleção de Events e produzir uma ordem explícita antes da futura resolução sequencial.

Conceitualmente:

IReadOnlyList<ISimulationEvent>
+
SimulationContext

→ ISimulationEventOrderer
→ IReadOnlyList<ISimulationEvent>

O orderer recebe:

- a coleção de `ISimulationEvent` produzida pela etapa anterior;
- o `SimulationContext` da resolução.

O contrato permite devolver uma nova ordem sem exigir mutação da coleção de entrada.

A identidade de cada Event continua independente de sua posição na ordem resultante.

Isso preserva a separação entre:

Event identity
→ lineage estável por `EventId`

Event ordering
→ posição de resolução

Event revalidation
→ elegibilidade no estado autoritativo corrente

Event execution
→ aplicação sequencial da ocorrência

O contrato não define ainda:

- algoritmo concreto de ordering;
- uso concreto do PRNG;
- integração com o `TurnResolver`;
- execução sequencial de múltiplos Events;
- EventLog.

A implementação concreta de ordering determinístico é realizada pelo componente descrito em 8.10.


## 8.10 Seeded Deterministic Event Ordering

`SeededSimulationEventOrderer` implementa `ISimulationEventOrderer` utilizando o `DeterministicRandom` já estabilizado no Kernel.

O comportamento concreto é:

IReadOnlyList<ISimulationEvent>
+
SimulationContext.Seed

→ cópia da coleção
→ deterministic Fisher-Yates
→ IReadOnlyList<ISimulationEvent> ordenada

### Fonte de aleatoriedade

O orderer cria uma nova instância de:

`DeterministicRandom(context.Seed)`

para cada chamada de `Order`.

O estado mutável do PRNG permanece local à operação e não é armazenado no `SimulationContext`.

Nesse estágio, `TurnNumber` continua fazendo parte do `SimulationContext`, mas não é misturado adicionalmente à seed pelo orderer.

A fonte explícita de aleatoriedade desta capability é `SimulationSeed`.

### Algoritmo

A permutação utiliza Fisher-Yates do último índice para o primeiro.

A seleção do índice de troca utiliza amostragem por rejeição sobre `NextUInt64()` para evitar viés de módulo quando o limite não divide uniformemente o espaço de `UInt64`.

Para a mesma:

- coleção de entrada na mesma ordem;
- `SimulationSeed`;
- implementação do PRNG;
- implementação do algoritmo;

a ordem resultante é reproduzível.

### Imutabilidade da entrada

A coleção fornecida ao orderer não é reorganizada in-place.

O componente cria uma cópia antes do shuffle e devolve uma coleção somente leitura.

A membership é preservada exatamente:

- nenhum Event é criado;
- nenhum Event é removido;
- nenhum Event é duplicado pelo algoritmo.

### Casos limite

- coleção vazia produz coleção vazia;
- coleção unitária preserva o único Event;
- coleção nula é rejeitada explicitamente.

### Compatibilidade determinística

O teste de referência com seed `0` congela uma permutação conhecida para detectar alterações futuras no comportamento.

Mudanças futuras no `DeterministicRandom`, no algoritmo Fisher-Yates ou na transformação de valores aleatórios em índices deverão ser tratadas como alteração de compatibilidade do comportamento determinístico.

### Limites ainda abertos

O orderer concreto ainda não está integrado ao `TurnResolver`.

O fluxo ainda não executa múltiplos Events sequencialmente e ainda não possui `EventLog`.

A integração de ordering ao caminho real e a resolução sequencial de múltiplos Events são realizadas pelo fluxo descrito em 8.11.


## 8.11 Multi-Event Sequential Resolution Path

`TurnResolver` integra agora `ISimulationEventOrderer` ao caminho real de resolução e permite que um único Command validado produza zero ou mais Events.

A pipeline estrutural suportada passa a ser:

WorldState
+
um ISimulationCommand
+
SimulationContext

→ validate
→ process
→ order
→ para cada Event ordenado:
  → revalidate contra o WorldState corrente
  → execute quando elegível
  → atualizar o WorldState corrente
→ TurnResolutionResult

### Ordering

A coleção produzida pelo `ISimulationCommandProcessor` é encaminhada ao `ISimulationEventOrderer` antes de qualquer revalidação ou execução.

O `TurnResolver` utiliza a ordem devolvida pelo orderer como ordem de resolução.

Isso remove a limitação anterior que rejeitava mais de um Event.

### Resolução sequencial

A resolução começa com:

`currentWorldState = input.WorldState`

Cada Event ordenado é revalidado contra o `currentWorldState` existente naquele instante.

Quando o Event é elegível:

- o executor recebe o estado corrente;
- o Event é executado;
- o estado retornado pelo executor passa a ser o novo `currentWorldState`;
- o próximo Event é revalidado contra esse estado atualizado.

Quando o Event não é elegível:

- o executor não é invocado para aquele Event;
- o `currentWorldState` é preservado;
- a sequência continua com o próximo Event.

Essa regra evita abortar silenciosamente Events posteriores apenas porque um Event intermediário foi rejeitado.

### Semântica de TurnResolutionResult.Events

`TurnResolutionResult.Events` passa a representar a sequência ordenada completa de Events produzidos para a resolução.

A coleção inclui também Events que foram rejeitados na etapa de revalidação.

Portanto:

`TurnResolutionResult.Events`

não significa:

- somente Events executados;
- somente Events aceitos;
- EventLog definitivo.

O futuro `EventLog` permanece uma capability separada e deverá registrar semântica suficiente para replay, persistência e diagnóstico.

### Caminhos sem resolução

Zero Commands:

- validator não é invocado;
- processor não é invocado;
- orderer não é invocado;
- revalidator não é invocado;
- executor não é invocado;
- o estado é preservado.

Command rejeitado:

- processor não é invocado;
- orderer não é invocado;
- nenhum Event é produzido;
- o estado é preservado.

Command aceito com zero Events:

- o processor é invocado;
- o orderer recebe a coleção vazia;
- não há revalidação;
- não há execução;
- o estado é preservado.

### Limites ainda abertos

Múltiplos Commands continuam explicitamente não suportados.

O fluxo ainda não possui:

- suporte a múltiplos Commands;
- EventLog;
- avanço de turno;
- resultado estruturado de rejeições.

A primeira transição observável e a prova end-to-end de reprodutibilidade são realizadas pelo fluxo descrito em 8.12.


## 8.12 Concrete Deterministic State Transition

O `WorldState` passa a possuir uma propriedade observável mínima chamada `Revision`.

O estado inicial possui:

`Revision = 0`

e `AdvanceRevision()` produz uma nova instância de `WorldState` com a revisão incrementada em uma unidade.

Essa propriedade existe para fornecer o menor estado autoritativo observável necessário à prova determinística do Kernel sem antecipar regras de gameplay pertencentes a milestones posteriores.

`Revision` não representa:

- `TurnNumber`;
- versão de save;
- versão de ruleset;
- versão de protocolo;
- uma regra concreta de Warfare, Economy ou World Generation.

### Prova end-to-end

O teste `IdenticalIndependentRunsProduceSameObservableStateAndEventOrder` executa duas resoluções independentes com:

- estados iniciais equivalentes;
- Commands equivalentes;
- o mesmo `SimulationContext`;
- a mesma seed;
- a mesma pipeline real de resolução.

A pipeline exercitada é:

validate
→ process
→ seeded deterministic ordering
→ revalidate
→ execute
→ novo WorldState

Cada execução gera três Events.

Com seed `0`, a ordem observada é congelada como:

3
→ 1
→ 2

Cada Event executado chama `AdvanceRevision()`.

As duas execuções independentes terminam com:

`Revision = 3`

e a mesma sequência de `EventId`.

Isso estabelece a primeira prova automatizada de:

**mesmo estado equivalente + mesmas ordens + mesmo contexto determinístico = mesmo resultado observável**

### Imutabilidade

O estado inicial permanece com `Revision = 0`.

Cada avanço produz nova instância.

A prova não depende de identidade de referência entre as duas execuções; compara propriedades observáveis e identidades determinísticas de Events.

### Limites ainda abertos

A prova atual utiliza um único Command que produz múltiplos Events.

Múltiplos Commands simultâneos continuam explicitamente não suportados pelo `TurnResolver`.

EventLog, replay formal e Turn Policies também permanecem capabilities abertas de M1.

A fronteira mínima para construir Events a partir de múltiplos Commands simultâneos é estabelecida pelo contrato descrito em 8.13.


## 8.13 Multi-Command Resolution Contract

`ISimulationCommandBatchProcessor` estabelece a fronteira mínima responsável por transformar o conjunto de Commands submetidos para a mesma resolução em uma coleção de Events antes do ordering determinístico.

Conceitualmente:

WorldState de planejamento
+
IReadOnlyList<ISimulationCommand>
+
SimulationContext

→ ISimulationCommandBatchProcessor
→ IReadOnlyList<ISimulationEvent>

O contrato recebe:

- o `WorldState` de planejamento;
- a coleção somente leitura de Commands submetidos;
- o `SimulationContext` da resolução.

A saída é uma coleção explícita de Events que poderá ser encaminhada posteriormente ao `ISimulationEventOrderer`.

### Snapshot de planejamento

O `WorldState` recebido representa o mesmo snapshot autoritativo de planejamento para o lote.

A intenção arquitetural é impedir que a execução de um Command altere o estado usado para validar ou processar outro Command pertencente ao mesmo lote simultâneo.

A resolução futura deverá separar:

planejamento simultâneo
→ construção agregada de Events
→ ordering determinístico
→ revalidação contra estado corrente
→ execução sequencial

### Commands e prioridade

A posição de um Command na coleção de entrada não constitui, por si só, prioridade de execução.

O contrato não transforma a ordem de chegada dos Commands em ordem autoritativa de resolução.

A coleção é recebida como contexto do lote e não deve ser mutada pelo processor.

### Event lineage

Cada Event produzido continua possuindo identidade independente através de `EventId`.

`EventId.OriginCommandId` preserva a origem do Event mesmo quando Events de vários Commands forem agregados na mesma coleção.

A identidade do Event permanece separada de sua futura posição de ordering.

### Limites do contrato

O contrato não define ainda:

- implementação concreta do batch processor;
- política concreta para Commands inválidos;
- integração ao `TurnResolver`;
- algoritmo de agregação;
- ordering entre Events;
- EventLog;
- Turn Policies.

A implementação concreta e a integração desse contrato ao caminho real são descritas em 8.14.


## 8.14 Multi-Command Resolution Path

`SimulationCommandBatchProcessor` implementa `ISimulationCommandBatchProcessor` utilizando os contratos já existentes de validação e processamento de Commands.

O fluxo concreto é:

IReadOnlyList<ISimulationCommand>
+
WorldState de planejamento
+
SimulationContext

→ canonicalização por CommandId
→ validação de cada Command contra o mesmo planning WorldState
→ processamento dos Commands aceitos contra o mesmo planning WorldState
→ agregação dos Events
→ ISimulationEventOrderer
→ revalidação sequencial
→ execução sequencial

### Canonicalização do lote

Antes da validação, o batch processor cria uma visão canônica dos Commands ordenando por:

1. `CommandId.Turn.Value`;
2. `CommandId.Sequence`.

A coleção de entrada não é mutada.

A canonicalização existe para que a ordem física de chegada ou enumeração do mesmo conjunto de Commands não altere a sequência agregada entregue ao ordering determinístico.

Essa etapa não constitui prioridade autoritativa de execução.

A ordem autoritativa dos Events continua pertencendo ao `ISimulationEventOrderer`.

### Identidade de Command

`CommandId` duplicado dentro do mesmo lote é rejeitado antes de qualquer validação ou processamento.

Isso impede ambiguidade de lineage para Events produzidos no mesmo ciclo de resolução.

### Validação e processamento

Todos os Commands são avaliados contra o mesmo `planningWorldState`.

Para cada Command na visão canônica:

- o validator recebe o snapshot de planejamento;
- Command inválido é ignorado para produção de Events;
- Command válido é processado contra o mesmo snapshot;
- os Events produzidos são anexados à coleção agregada.

Nenhuma execução de Event ocorre durante essa fase.

Portanto, um Command posterior do lote não observa mutações causadas por Events de um Command anterior.

### Integração com TurnResolver

`TurnResolver` deixa de rejeitar múltiplos Commands.

Para entradas não vazias, o resolver:

1. constrói os Events através do batch processor;
2. encaminha a coleção agregada ao orderer;
3. revalida cada Event ordenado contra o estado corrente;
4. executa Events aceitos sequencialmente;
5. utiliza o estado resultante de cada execução para a próxima revalidação.

Zero Commands continua preservando o estado e encerrando antes da fase de batch processing e ordering.

Quando todos os Commands são rejeitados, a coleção vazia produzida pelo batch processor ainda atravessa a fronteira do orderer e não há revalidação nem execução.

### Prova de independência da ordem de chegada

O teste `EquivalentMultiCommandBatchesIgnoreInputArrivalOrder` executa dois lotes equivalentes com a ordem física dos Commands invertida.

Mantidos:

- os mesmos CommandIds;
- o mesmo estado inicial equivalente;
- o mesmo `SimulationContext`;
- a mesma seed;

as duas resoluções produzem:

- a mesma sequência final de `EventId`;
- a mesma revisão observável do `WorldState`.

Isso demonstra que a ordem da coleção recebida não cria prioridade implícita.

### Limites ainda abertos

M1 ainda não possui:

- EventLog definitivo;
- replay formal;
- Turn Policies;
- hash de estado para diagnóstico;
- validação determinística entre plataformas.

O contrato mínimo do EventLog determinístico é estabelecido pela seção 8.15.


## 8.15 Event Log Contract

`SimulationEventLogEntry` e `SimulationEventLog` estabelecem a primeira representação concreta do EventLog da resolução.

O objetivo é registrar o resultado observável da etapa de resolução sem alterar a semântica já estabelecida de `TurnResolutionResult.Events`.

### Separação de responsabilidades

`TurnResolutionResult.Events` continua representando:

- a sequência completa de Events produzidos;
- já ordenada para resolução;
- incluindo Events posteriormente rejeitados na revalidação.

`SimulationEventLog` representa:

- a sequência de avaliação desses Events durante a resolução;
- a identidade estável de cada Event;
- a posição efetiva na sequência de resolução;
- se o Event estava elegível no estado corrente;
- se o Event foi efetivamente executado.

Assim, o EventLog não é substituto de `TurnResolutionResult.Events` e `TurnResolutionResult.Events` não é implicitamente promovido a EventLog.

### SimulationEventLogEntry

Cada entrada possui:

`ResolutionSequence`

`Event`

`EventId`

`WasEligible`

`WasExecuted`

`ResolutionSequence` é maior que zero e representa a posição da entrada na sequência de resolução.

`EventId` é derivado do próprio Event e preserva lineage através de `OriginCommandId`.

Um Event executado necessariamente deve ter sido elegível.

Portanto, a combinação:

`WasEligible = false`

`WasExecuted = true`

é inválida.

Um Event rejeitado pode ser representado explicitamente como:

`WasEligible = false`

`WasExecuted = false`

### SimulationEventLog

`SimulationEventLog` recebe uma coleção de entries e cria um snapshot somente leitura.

As entries devem possuir `ResolutionSequence` contínua começando em um:

1
→ 2
→ 3
→ ...

Isso torna a ordem da resolução parte explícita do contrato e evita depender apenas da ordem física de uma coleção externa mutável.

Um log vazio é válido.

### Persistência e replay

Este checkpoint não define:

- formato físico de persistência;
- serialização;
- armazenamento em arquivo ou banco;
- event sourcing completo;
- replay automático;
- reconstrução do `WorldState`;
- hash de estado.

O EventLog permanece uma estrutura de Simulation e a persistência física continuará responsabilidade de um módulo posterior.

### Limites ainda abertos

A integração do EventLog ao caminho real do `TurnResolver` é realizada pela seção 8.16.


## 8.16 Event Log Capture Path

`TurnResolver` integra `SimulationEventLog` ao caminho real de resolução.

O resultado da resolução passa a carregar três saídas distintas:

`ResultingWorldState`

`Events`

`EventLog`

As responsabilidades permanecem separadas.

### TurnResolutionResult.Events

`TurnResolutionResult.Events` continua representando a sequência completa e ordenada de Events produzidos para a resolução.

A coleção inclui Events que posteriormente foram rejeitados durante a revalidação.

Essa semântica não é alterada pela introdução do EventLog.

### TurnResolutionResult.EventLog

`TurnResolutionResult.EventLog` representa a avaliação efetiva da sequência ordenada durante a resolução.

Para cada Event ordenado, o resolver cria exatamente uma `SimulationEventLogEntry`.

A posição da entry é:

`ResolutionSequence = index + 1`

e segue a mesma ordem de `TurnResolutionResult.Events`.

### Event elegível

Quando o revalidator retorna `true`:

- o Event é executado;
- o `WorldState` corrente é atualizado;
- a entry recebe `WasEligible = true`;
- a entry recebe `WasExecuted = true`.

A entry é adicionada somente após o executor retornar com sucesso.

Se a execução lançar exceção, a resolução não produz um `TurnResolutionResult` parcial.

### Event rejeitado

Quando o revalidator retorna `false`:

- o executor não é invocado;
- o estado corrente é preservado;
- a entry recebe `WasEligible = false`;
- a entry recebe `WasExecuted = false`;
- a resolução continua com o próximo Event.

Events rejeitados permanecem representados simultaneamente em:

- `TurnResolutionResult.Events`;
- `TurnResolutionResult.EventLog`.

A diferença é que o EventLog registra explicitamente o resultado da elegibilidade.

### Turno vazio

Zero Commands continua encerrando antes de validation, processing, ordering, revalidation e execution.

O resultado contém:

- o mesmo `WorldState`;
- zero Events;
- `SimulationEventLog` vazio.

### Reprodutibilidade

O teste `IdenticalResolutionsProduceEquivalentEventLogs` executa duas resoluções independentes com entrada equivalente, mesmo contexto e mesma seed.

As duas execuções produzem EventLogs equivalentes quanto a:

- `ResolutionSequence`;
- `EventId`;
- `WasEligible`;
- `WasExecuted`.

Isso demonstra que a captura do EventLog participa do comportamento determinístico observável da pipeline.

### Limites ainda abertos

O EventLog agora está integrado, mas replay ainda não foi definido.

M1 ainda não possui:

- contrato de replay;
- aplicação de um EventLog pré-existente sobre um estado inicial;
- validação de resultado de replay;
- hash de estado;
- persistência física do log;
- Turn Policies;
- validação determinística entre plataformas.

A fronteira mínima de replay é estabelecida pela seção 8.17.


## 8.17 Replay Contract

`SimulationReplayInput` e `ISimulationEventLogReplayer` estabelecem a fronteira mínima para replay determinístico a partir de um EventLog já produzido.

O replay recebe:

`InitialWorldState`

`SimulationEventLog`

`SimulationContext`

e devolve:

`WorldState`

### SimulationReplayInput

`SimulationReplayInput` agrega os três elementos necessários para a futura reaplicação determinística:

- o `WorldState` inicial sobre o qual o replay começa;
- o `SimulationEventLog` capturado pela resolução original;
- o mesmo tipo de `SimulationContext` utilizado pelos executores da pipeline.

`InitialWorldState` e `EventLog` são obrigatórios e não podem ser nulos.

O contexto permanece explícito porque a execução de Events já possui `SimulationContext` como parte de seu contrato.

### ISimulationEventLogReplayer

`ISimulationEventLogReplayer` define:

`Replay(SimulationReplayInput) → WorldState`

O contrato separa replay da resolução normal de Commands.

Replay não recebe Commands e não refaz:

- validação de Commands;
- processamento de Commands;
- ordering de Events;
- revalidação de elegibilidade.

A fonte autoritativa do replay é o EventLog previamente capturado.

### Semântica esperada das entries

A futura implementação deverá percorrer as entries na ordem de `ResolutionSequence`.

Entries com:

`WasExecuted = false`

não deverão executar novamente seu Event.

Entries com:

`WasExecuted = true`

deverão reaplicar o Event correspondente na sequência registrada.

Como `SimulationEventLogEntry` já impede `WasExecuted = true` combinado com `WasEligible = false`, o replay não precisa reconstruir a decisão original de elegibilidade.

### Relação com determinismo

O objetivo do replay é demonstrar que:

estado inicial equivalente
+
mesmo EventLog
+
mesmo SimulationContext

→ mesmo estado resultante observável

A futura implementação deverá ser comparada com o `ResultingWorldState` obtido pela resolução original.

### Limites do contrato

Este checkpoint não implementa ainda:

- classe concreta de replay;
- execução de Events durante replay;
- comparação automática entre resultado original e resultado reproduzido;
- hash de estado;
- persistência física do EventLog;
- serialização;
- event sourcing completo;
- replay entre versões incompatíveis do ruleset.

A implementação concreta e a prova observável de replay são descritas pela seção 8.18.


## 8.18 Replay Path

`SimulationEventLogReplayer` implementa `ISimulationEventLogReplayer` utilizando o `ISimulationEventExecutor` já existente.

O replay começa em:

`SimulationReplayInput.InitialWorldState`

e percorre:

`SimulationReplayInput.EventLog.Entries`

na ordem já validada por `ResolutionSequence`.

### Entries não executadas originalmente

Quando:

`WasExecuted = false`

o replayer não invoca o executor.

A entry permanece apenas como registro histórico da resolução original e não produz transição de estado durante replay.

Isso inclui Events originalmente rejeitados pela revalidação.

### Entries executadas originalmente

Quando:

`WasExecuted = true`

o replayer chama:

`ISimulationEventExecutor.Execute`

com:

- o `WorldState` corrente do replay;
- o Event armazenado na entry;
- o `SimulationContext` do replay input.

O estado retornado passa a ser o estado corrente para a próxima entry executada.

### Separação entre replay e resolução

O replayer não refaz:

- validação de Commands;
- processamento de Commands;
- ordering de Events;
- revalidação de Events;
- decisões de elegibilidade.

A sequência e o outcome autoritativo já estão registrados no EventLog.

Replay reaplica somente as transições que a resolução original marcou como executadas.

### Log vazio

Quando o EventLog não possui entries:

- o executor não é invocado;
- o `InitialWorldState` é devolvido sem alteração.

### Prova de equivalência observável

O teste `ReplayMatchesOriginalResolutionObservableState` executa primeiro uma resolução real com:

- Command válido;
- três Events;
- ordering determinístico;
- uma rejeição intermediária na revalidação;
- duas execuções efetivas.

A resolução original produz um EventLog com outcomes:

`true`
→ `false`
→ `true`

Em seguida, o replay parte de um `WorldState` inicial equivalente e usa o EventLog capturado.

O replay executa somente as duas entries originalmente executadas.

Tanto resolução quanto replay terminam com:

`WorldState.Revision = 2`

Isso estabelece a primeira prova automatizada de:

**estado inicial equivalente + EventLog capturado + mesmo contexto = mesmo estado observável**

### Maturidade

A capability `EventLog / base de replay` passa a possuir:

- contrato de EventLog;
- captura integrada;
- contrato de replay;
- implementação concreta de replay;
- prova automatizada de equivalência observável.

Persistência física, hash de estado, compatibilidade entre versões e validação entre plataformas permanecem fora desta prova.

### Próximo limite arquitetural

O próximo trabalho do M1 é tornar explícita a política que encerra a janela de ordens sem acoplar o Kernel a um único modelo temporal.

A seção 9 estabelece a direção arquitetural de Turn Policies e passa a ser o próximo Critical Path.

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

## 9.1 Turn Policy Contract

`ITurnPolicy` estabelece a fronteira mínima responsável por decidir se a janela de ordens de um turno deve ser encerrada.

O contrato é:

`TurnPolicyInput`

→ `ITurnPolicy.ShouldClose`

→ `bool`

### TurnPolicyInput

O input contém somente fatos necessários para a decisão:

- `Turn`;
- `AllRequiredParticipantsReady`;
- `ExternalDeadlineReached`.

`Turn` identifica explicitamente o turno ao qual a decisão se refere.

`AllRequiredParticipantsReady` representa o fato de que todos os participantes exigidos para aquela política já estão prontos.

`ExternalDeadlineReached` representa um sinal temporal já calculado fora da Simulation.

### Relógio de parede

O contrato não consulta:

- `DateTime.Now`;
- `DateTime.UtcNow`;
- relógio do sistema;
- timers de UI;
- timers de networking.

Uma política temporizada ou de correspondência recebe somente o fato determinístico de que o prazo externo já foi atingido.

A responsabilidade por transformar tempo real em `ExternalDeadlineReached` pertence a uma camada externa posterior.

### Separação de responsabilidades

`ITurnPolicy` decide apenas:

**a janela de ordens deve fechar agora?**

A policy não:

- executa `TurnResolver`;
- valida Commands;
- processa Events;
- altera `WorldState`;
- acessa networking;
- acessa UI.

Isso mantém a decisão temporal separada da resolução determinística já validada.

### Extensibilidade

O mesmo contrato suporta políticas dirigidas por readiness e por deadline sem alterar o `TurnResolver`.

O checkpoint congela somente a fronteira.

Ainda não existem implementações concretas de:

- readiness/manual;
- deadline/timed;
- correspondence.

A primeira implementação concreta baseada exclusivamente em readiness é descrita pela seção 9.2.

## 9.2 Manual Ready Turn Policy

`ManualReadyTurnPolicy` implementa `ITurnPolicy` com a regra mínima para fechamento manual da janela de ordens.

A decisão é:

`AllRequiredParticipantsReady = false`

→ manter a janela aberta

`AllRequiredParticipantsReady = true`

→ fechar a janela

### Independência de deadline

`ExternalDeadlineReached` não participa da decisão desta policy.

Portanto, quando readiness é `false`, a janela permanece aberta mesmo que:

`ExternalDeadlineReached = true`

Da mesma forma, quando readiness é `true`, a janela fecha independentemente do valor do deadline externo.

Essa separação impede que uma policy manual adquira implicitamente comportamento temporizado.

### Determinismo

A policy depende apenas dos valores contidos em `TurnPolicyInput`.

Ela não consulta:

- relógio do sistema;
- timers;
- UI;
- networking;
- estado global.

Para a mesma entrada, a decisão é a mesma.

### Limites

`ManualReadyTurnPolicy` ainda não está integrada a um orquestrador que decida quando chamar `TurnResolver`.

A primeira implementação concreta baseada exclusivamente no sinal de deadline externo é descrita pela seção 9.3.

## 9.3 External Deadline Turn Policy

`ExternalDeadlineTurnPolicy` implementa `ITurnPolicy` com a regra mínima para fechamento de uma janela controlada por prazo externo.

A decisão é:

`ExternalDeadlineReached = false`

→ manter a janela aberta

`ExternalDeadlineReached = true`

→ fechar a janela

### Independência de readiness

`AllRequiredParticipantsReady` não participa da decisão desta policy.

Portanto, quando o deadline externo ainda não foi atingido, a janela permanece aberta mesmo que todos os participantes estejam prontos.

Da mesma forma, quando o deadline externo foi atingido, a janela fecha independentemente do readiness.

Essa separação mantém políticas temporais e políticas de readiness como comportamentos distintos sobre o mesmo contrato.

### Fonte temporal externa

A policy não calcula tempo.

Ela recebe somente:

`ExternalDeadlineReached`

como fato já determinado por uma camada externa.

A Simulation continua sem consultar:

- `DateTime.Now`;
- `DateTime.UtcNow`;
- timers;
- relógio do sistema;
- UI;
- networking.

### Determinismo

Para o mesmo `TurnPolicyInput`, a decisão é sempre a mesma.

O comportamento da policy depende apenas dos dados recebidos.

### Limites

As duas primeiras policies concretas agora existem em isolamento:

- `ManualReadyTurnPolicy`;
- `ExternalDeadlineTurnPolicy`.

Nenhuma delas ainda participa de um caminho que condicione a chamada ao `TurnResolver`.

A fronteira mínima desse gate é descrita pela seção 9.4.

## 9.4 Turn Policy Resolution Gate Contract

`ITurnPolicyResolutionGate` estabelece a fronteira entre a decisão de fechamento de uma `ITurnPolicy` e a execução do fluxo já existente de resolução.

O contrato é:

`TurnPolicyResolutionGateInput`

→ `ITurnPolicyResolutionGate.Resolve`

→ `TurnPolicyResolutionGateResult`

### Input do gate

`TurnPolicyResolutionGateInput` contém:

- `PolicyInput`;
- `ResolutionInput`.

O gate recebe explicitamente tanto os fatos necessários à policy quanto o `TurnResolutionInput` que será usado caso a janela seja encerrada.

### Invariante de turno

O contrato exige:

`PolicyInput.Turn == ResolutionInput.Context.Turn`

Inputs referentes a turnos diferentes são rejeitados.

Isso impede que uma decisão de fechamento tomada para um turno seja aplicada acidentalmente à resolução de outro.

### Resultado com turno aberto

Quando a janela permanece aberta, o resultado deve poder representar explicitamente:

`WasResolved = false`

e:

`ResolutionResult = null`

Esse estado não é um `TurnResolutionResult` vazio.

Ele significa que nenhuma resolução ocorreu.

### Resultado com turno resolvido

Quando a janela foi fechada e a resolução ocorreu:

`WasResolved = true`

e:

`ResolutionResult` contém o resultado real produzido pelo caminho de resolução.

A factory `Resolved` rejeita resultado nulo.

### Preservação dos contratos existentes

Este checkpoint não altera:

- `TurnResolutionInput`;
- `TurnResolutionResult`;
- `TurnResolver`;
- semântica de Commands;
- ordering de Events;
- revalidação;
- EventLog;
- replay.

O gate será uma camada anterior ao resolver.

### Limites

Este checkpoint define somente o contrato.

Ainda não existe implementação concreta que:

1. consulte uma `ITurnPolicy`;
2. devolva `Open()` quando a janela permanecer aberta;
3. invoque o `TurnResolver` quando a janela fechar;
4. envolva o resultado em `Resolved(...)`.

A implementação concreta desse caminho é descrita pela seção 9.5.

## 9.5 Turn Policy Resolution Gate Path

`TurnPolicyResolutionGate` implementa `ITurnPolicyResolutionGate` compondo:

- uma `ITurnPolicy`;
- o `TurnResolver` já existente.

O gate não reimplementa a resolução.

Ele decide somente se o resolver deve ou não ser chamado.

### Turno ainda aberto

O fluxo é:

`ITurnPolicy.ShouldClose(PolicyInput)`

quando retorna:

`false`

o gate devolve:

`TurnPolicyResolutionGateResult.Open()`

e não chama:

`TurnResolver.Resolve(...)`

Isso significa que nenhuma validação, transformação de Commands, ordering, revalidação ou execução de Events ocorre enquanto a policy mantiver a janela aberta.

### Turno fechado

Quando:

`ITurnPolicy.ShouldClose(PolicyInput) = true`

o gate chama:

`TurnResolver.Resolve(ResolutionInput)`

e envolve o resultado real em:

`TurnPolicyResolutionGateResult.Resolved(...)`

Toda a semântica interna continua pertencendo ao `TurnResolver`.

### Reuso do mesmo resolver

As implementações concretas:

- `ManualReadyTurnPolicy`;
- `ExternalDeadlineTurnPolicy`;

podem ser usadas por gates diferentes compartilhando a mesma instância de `TurnResolver`.

Isso prova a direção arquitetural definida para single-player e multiplayer: políticas diferentes de fechamento podem compartilhar o mesmo núcleo determinístico de resolução.

### Preservação do resolver

Este checkpoint não altera:

- `TurnResolver`;
- `TurnResolutionInput`;
- `TurnResolutionResult`;
- pipeline Command → Event;
- deterministic ordering;
- revalidação;
- EventLog;
- replay.

O gate é estritamente uma camada anterior à resolução.

### Maturidade

A capability `Turn policies` passa a estar integrada ao caminho real de resolução.

Ela ainda não é considerada `Validada`, porque falta uma prova específica de determinismo repetido do fluxo integrado com policies.

A validação determinística desse fluxo integrado é descrita pela seção 9.6.

## 9.6 Turn Policy Determinism Validation

A validação executa repetidamente o caminho:

`TurnPolicyResolutionGate`

→ `ITurnPolicy`

→ `TurnResolver`

com inputs equivalentes.

As duas policies concretas são cobertas:

- `ManualReadyTurnPolicy`;
- `ExternalDeadlineTurnPolicy`.

### Decisão aberta

Para o mesmo `TurnPolicyInput`, quando a policy mantém a janela aberta, execuções repetidas produzem:

`WasResolved = false`

e:

`ResolutionResult = null`

Nenhum resultado sintético de resolução é criado.

### Decisão fechada

Para o mesmo turno, mesmos Commands, mesma seed e mesmos fatos da policy, execuções repetidas produzem o mesmo resultado observável.

A prova compara:

- `WorldState.Revision`;
- quantidade e ordem dos Events;
- `EventId`;
- quantidade de entradas do EventLog;
- `ResolutionSequence`;
- `WasEligible`;
- `WasExecuted`.

### Ordering determinístico

A validação utiliza o `SeededSimulationEventOrderer` real com múltiplos Commands e a mesma `SimulationSeed`.

Assim, a prova cobre a policy e o caminho determinístico já existente de ordering e execução, em vez de validar somente um stub do resolver.

### Maturidade

A capability `Turn policies` passa de `Integrada` para `Validada`.

Com isso, todas as capabilities atualmente decompostas no orçamento de Foundation / Simulation Kernel alcançam pelo menos maturidade `Validada`.

Isso não encerra M1.

Antes do fechamento do milestone ainda é necessário ampliar os mecanismos de diagnóstico de determinismo e realizar validação entre plataformas conforme `RISK-004`.

O contrato mínimo de state hash/fingerprint determinístico é descrito a seguir.

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

O baseline mínimo atual mantém essa raiz deliberadamente pequena e acrescenta apenas uma `Revision` observável para permitir prova determinística de transição de estado, sem antecipar estruturas de planeta, territórios, civilizações, economia ou guerra que ainda não possuem contratos concretos.

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


### 10.1.1 Observable Revision

`WorldState.Revision` é um contador imutável mínimo utilizado para tornar uma transição de estado observável durante M1.

`WorldState.CreateInitial()` inicia em revisão zero.

`AdvanceRevision()`:

- devolve uma nova instância;
- preserva a instância anterior;
- incrementa a revisão em uma unidade;
- utiliza overflow verificado.

A propriedade não deve ser interpretada como turno lógico nem como versão de persistência.

Seu objetivo atual é fornecer uma observação simples e estável para testes end-to-end do Simulation Kernel.

---

# 11. Topologia estratégica

O planeta possui um único complexo topológico estratégico derivado de um Goldberg icosaédrico e um grafo de adjacência derivado desse complexo.

A parametrização geral pretendida é `G(m,n)`.

Na lógica:

StrategicCell = face Goldberg e região estratégica

StrategicEdge = fronteira Goldberg compartilhada por duas regiões

StrategicVertex = vértice Goldberg onde três regiões se encontram

O grafo utilizado por algoritmos estratégicos possui um nó por `StrategicCell` e uma conexão quando duas células compartilham um `StrategicEdge`.

A simulação estratégica trabalha prioritariamente sobre esse grafo.

Cada `StrategicCell` representa uma região capaz de possuir estado e propriedades estratégicas próprias.

A renderização 3D é uma projeção dessa estrutura lógica.

A topologia estratégica não depende da Unity.

## 11.1 Goldberg combinatorial baseline

Para a família icosaédrica de Goldberg, com `m` e `n` inteiros não negativos e não ambos zero:

`T = m² + mn + n²`

As contagens esperadas são:

- `StrategicCell count = 10T + 2`;
- `StrategicEdge count = 30T`;
- `StrategicVertex count = 20T`;
- pentágonos = `12`;
- hexágonos = `10(T - 1)`.

A identidade:

`StrategicVertex count - StrategicEdge count + StrategicCell count = 2`

deve ser preservada.

Cada célula pentagonal possui grau cinco.

Cada célula hexagonal possui grau seis.

Cada `StrategicEdge` possui exatamente duas células incidentes.

Cada `StrategicVertex` possui exatamente três células incidentes.

O grafo de células deve ser conexo.

`G(1,0)` é o caso mínimo de referência e corresponde ao dodecaedro:

- 12 células/faces;
- 30 arestas;
- 20 vértices.

## 11.2 Separação entre topologia e embedding

Identidade, adjacência e incidência são fatos combinatórios.

Coordenadas de ponto flutuante não são fonte de verdade topológica.

A realização visual poderá utilizar projeção esférica, embedding poliédrico ou triangulação, mas a topologia deverá permanecer idêntica.

Não é assumido que uma projeção que coloque todos os vértices exatamente sobre uma esfera preserve simultaneamente a planicidade exata de todas as faces Goldberg.

## 11.3 Determinismo topológico

Para os mesmos parâmetros e a mesma versão do gerador, a topologia deverá produzir:

- as mesmas entidades;
- as mesmas identidades;
- as mesmas adjacências;
- as mesmas incidências.

A identidade lógica não poderá depender da ordem de alocação, enumeração de coleções não ordenadas ou detalhes específicos de runtime.

## 11.4 Goldberg Parameter & Count Contract

`GoldbergParameters` representa o par ordenado `G(m,n)` usado como entrada combinatória da topologia Goldberg icosaédrica.

Contrato:

- `m` e `n` são inteiros não negativos;
- `(0,0)` é inválido;
- `(m,n)` permanece um par ordenado;
- `(m,n)` e `(n,m)` não são automaticamente canonicalizados como o mesmo value object;
- igualdade do value object considera os dois parâmetros originais;
- cálculos são executados com aritmética `checked`;
- combinações cujas contagens não cabem em `UInt64` são rejeitadas explicitamente;
- `IsValid` distingue instâncias construídas de `default(GoldbergParameters)`;
- o estado `default`, inevitável para um value type CLR, é uma sentinela inválida e deve ser rejeitado nas fronteiras que consomem parâmetros Goldberg.

O contrato expõe:

- `M`;
- `N`;
- `TriangulationNumber`;
- `StrategicCellCount`;
- `StrategicEdgeCount`;
- `StrategicVertexCount`;
- `PentagonCount`;
- `HexagonCount`.

As fórmulas permanecem:

`T = m² + mn + n²`

`StrategicCellCount = 10T + 2`

`StrategicEdgeCount = 30T`

`StrategicVertexCount = 20T`

`PentagonCount = 12`

`HexagonCount = 10(T - 1)`

Vetores de referência mínimos:

`G(1,0)`:

- `T = 1`;
- cells = 12;
- edges = 30;
- vertices = 20;
- pentagons = 12;
- hexagons = 0.

`G(1,1)`:

- `T = 3`;
- cells = 32;
- edges = 90;
- vertices = 60;
- pentagons = 12;
- hexagons = 20.

`G(2,1)`:

- `T = 7`;
- cells = 72;
- edges = 210;
- vertices = 140;
- pentagons = 12;
- hexagons = 60.

Este contrato não gera:

- faces;
- arestas;
- vértices;
- coordenadas;
- mesh;
- IDs topológicos;
- relações de adjacência.

Essas responsabilidades pertencem aos subcheckpoints posteriores de M2.1.

A distinção entre `(m,n)` e `(n,m)` é preservada porque orientação/chiralidade e equivalência geométrica ainda não possuem contrato de produção congelado.

## 11.5 Strategic Topology Identity Contract

A identidade topológica estratégica utiliza três tipos distintos:

- `StrategicCellId`;
- `StrategicEdgeId`;
- `StrategicVertexId`.

Cada ID:

- é um value object tipado;
- contém um valor lógico `UInt64`;
- exige `Value > 0` para uma identidade válida;
- utiliza igualdade por valor;
- expõe `IsValid`;
- não contém coordenadas;
- não contém estado mutável;
- não depende de Unity;
- não depende de ordem de alocação em memória.

Como todo value type do CLR possui um estado `default` zero-inicializado que contorna construtores explícitos, `default(StrategicCellId)`, `default(StrategicEdgeId)` e `default(StrategicVertexId)` são sentinelas inválidas.

Esse estado não poderá ser consumido silenciosamente como identidade:

- `IsValid` retorna `false`;
- acessar `Value` em uma sentinela `default` lança `InvalidOperationException`;
- construtores continuam rejeitando explicitamente `0`;
- futuros objetos de topologia e entidades deverão rejeitar IDs para os quais `IsValid == false` em suas fronteiras de entrada.

Os três tipos não são intercambiáveis.

O mesmo valor numérico pode existir simultaneamente nos três domínios sem colisão semântica porque o tipo faz parte do contrato de identidade.

Exemplo:

- `StrategicCellId(1)` identifica uma célula;
- `StrategicEdgeId(1)` identifica uma aresta;
- `StrategicVertexId(1)` identifica um vértice.

Essas identidades são locais a uma topologia estratégica.

Elas não pretendem ser identificadores globais entre planetas, partidas ou instâncias de topologia diferentes.

O futuro objeto de topologia fornecerá o escopo que associa:

- `GoldbergParameters`;
- coleções de entidades;
- seus IDs;
- relações de incidência e adjacência.

### Regra de ordinal canônico

`Value` representa um ordinal canônico one-based dentro de seu próprio tipo de entidade.

O gerador futuro deverá atribuir esse ordinal por uma regra determinística do algoritmo topológico.

É proibido derivar identidade de:

- endereço de memória;
- ordem incidental de criação;
- ordem de enumeração de coleção não determinística;
- coordenada de ponto flutuante;
- hash de runtime não estável.

Para os mesmos parâmetros Goldberg e a mesma versão do gerador, a mesma entidade topológica deverá receber o mesmo ID em Windows, Linux e macOS.

### Limite deste contrato

M2.1.3 não define ainda:

- geração de células;
- geração de arestas;
- geração de vértices;
- incidência;
- adjacência;
- coordenadas;
- orientação;
- chiralidade;
- ownership de border bands;
- mapeamento estratégico/tático.

O primeiro uso concreto dos IDs é materializado em M2.1.4 no caso mínimo `G(1,0)`.

## 11.6 Minimal G(1,0) Strategic Topology

M2.1.4 materializa a primeira topologia estratégica completa do projeto sem introduzir geometria de renderização.

O contrato de produção é exposto por:

`GoldbergStrategicTopologyGenerator.Generate(GoldbergParameters)`

Nesta etapa o gerador aceita apenas:

`G(1,0)`

Outros parâmetros válidos são rejeitados explicitamente com `NotSupportedException` até M2.1.5 generalizar a geração.

`default(GoldbergParameters)` é rejeitado como parâmetro inválido.

### Construção combinatória canônica

`G(1,0)` é o dodecaedro e é construído como dual combinatório de um icosaedro canônico.

A seed interna do icosaedro contém:

- 12 vértices canônicos;
- 30 arestas únicas;
- 20 faces triangulares.

O dual mapeia:

- vértice do icosaedro → `StrategicCell`;
- aresta do icosaedro → `StrategicEdge`;
- face triangular do icosaedro → `StrategicVertex`.

Nenhuma coordenada é usada para definir essas relações.

### Atribuição canônica de IDs

`StrategicCellId`:

- usa os ordinais `1..12` dos vértices da seed canônica.

`StrategicEdgeId`:

- normaliza cada par de células incidentes como `(menor, maior)`;
- remove duplicatas;
- ordena os pares lexicograficamente;
- atribui ordinais `1..30`.

`StrategicVertexId`:

- usa a ordem canônica congelada das 20 faces triangulares;
- atribui ordinais `1..20`.

Todas as coleções públicas de incidência e adjacência são expostas em ordem crescente de ID.

A ordem de enumeração de `Dictionary`, endereço de memória ou coordenada de ponto flutuante não participa da identidade.

### Entidades

`StrategicCell` contém:

- `Id`;
- `Kind`;
- células adjacentes;
- arestas incidentes;
- vértices incidentes.

`StrategicEdge` contém:

- `Id`;
- exatamente duas células incidentes;
- exatamente dois vértices incidentes.

`StrategicVertex` contém:

- `Id`;
- exatamente três células incidentes;
- exatamente três arestas incidentes.

`StrategicTopology` contém:

- `GoldbergParameters`;
- coleção canônica de cells;
- coleção canônica de edges;
- coleção canônica de vertices.

### Invariantes do caso G(1,0)

A implementação deve provar:

- 12 `StrategicCells`;
- 30 `StrategicEdges`;
- 20 `StrategicVertices`;
- 12 células pentagonais;
- grau 5 em toda célula;
- incidência 2 por edge;
- incidência 3 por vertex;
- reciprocidade entre relações;
- uma única edge por par de células adjacentes;
- ausência de self-loop;
- ausência de adjacência duplicada;
- conectividade global;
- Euler igual a 2;
- geração repetida produz a mesma topologia canônica.

M2.1.4 ainda não introduz:

- coordenadas;
- mesh;
- projeção esférica;
- hexágonos;
- geração Goldberg geral;
- refinamento tático;
- border bands.

---

# 12. Camada tática

Cada `StrategicCell` corresponde a uma região tática própria.

Conceitualmente, cada região estratégica funciona como um tabuleiro local de resolução muito superior.

Regiões táticas vizinhas permanecem conectadas através das relações existentes no grafo estratégico.

A camada tática permite:

- posicionamento;
- terreno;
- combate;
- ocupação;
- movimento detalhado;
- fortificação;
- infraestrutura;
- detalhe físico local.

Uma região tática poderá possuir grande quantidade de microtiles e, futuramente, múltiplos níveis hierárquicos de refinamento.

A resolução tática não deve determinar diretamente o custo dos sistemas estratégicos recorrentes.

A arquitetura assume inicialmente que refinamentos hierárquicos poderão ser construídos sobre topologias Goldberg `G(m,n)`.

Essa propriedade permanece hipótese até validação em M2.

Não é assumido que um `G(p,q)` de maior resolução seja automaticamente um refinamento pai-filho válido de qualquer `G(m,n)`.

Compatibilidade de submalha, orientação, pertencimento e fronteiras deverá ser demonstrada.

Caso a hipótese geral não se sustente, o suporte poderá ser reduzido para famílias ou combinações específicas sem alterar a separação estratégico/tática.

O requisito de fileiras compartilhadas de subtiles permanece parte do V1.

Uma entidade tática compartilhada não poderá existir como duas identidades lógicas independentes apenas porque é observada a partir de duas regiões vizinhas.

A forma de ownership dessas entidades permanece aberta para M2.

## 12.1 Contrato mínimo de TacticalRegion

M2.2 separa explicitamente topologia intra-região de conectividade entre regiões.

Cada `StrategicCell` identifica exatamente uma `TacticalRegion`; não existe `TacticalRegionId` separado nesta etapa.

Para células region-owned, `TacticalCellId` será formado conceitualmente por:

`ParentStrategicCellId + LocalOrdinal`

O ordinal local é one-based e a identidade não depende de coordenadas de ponto flutuante ou ordem incidental de criação.

Uma `TacticalRegion` materializada deverá preservar:

- pertencimento explícito ao `StrategicCell` pai;
- IDs canônicos;
- adjacência local sem self-loop ou duplicatas;
- reciprocidade;
- resolução de todas as referências locais;
- conectividade do grafo interno;
- geração determinística.

M2.2 não cria adjacências táticas cross-region.

Elementos compartilhados de fronteira permanecem fora deste contrato e serão definidos em M2.3. Eles não poderão ser representados pela duplicação silenciosa de duas células region-owned.

Coordenadas, mesh, terreno, renderização e conteúdo físico permanecem fora da fonte de verdade topológica deste checkpoint.

## 12.2 Grafo tático mínimo de referência

M2.2.2 materializa um primeiro grafo tático local apenas para provar geração determinística sobre o contrato de M2.2.1.

O audit de M2.2.2 não encontrou contrato existente que fixe:

- contagem final de células de uma região tática;
- geometria local definitiva;
- coordenadas 2D ou 3D;
- shape de pentágono ou hexágono para cada `TacticalCell`;
- layout de fronteira;
- ownership cross-region.

Por isso, o primeiro grafo é deliberadamente um **reference graph**, e não a topologia física final do planeta.

A referência mínima será um caminho canônico de três células:

`1 <-> 2 <-> 3`

onde os números representam `LocalOrdinal`.

Esse grafo é o menor conectado que também permite validar uma célula com múltiplas adjacências e sua ordenação canônica.

O gerador de referência deverá:

- receber um `StrategicCellId` válido;
- criar exatamente três `TacticalCellId` com ordinais one-based `1`, `2`, `3`;
- materializar adjacências recíprocas `1-2` e `2-3`;
- retornar uma `TacticalRegion` válida;
- produzir a mesma assinatura canônica em gerações repetidas;
- não usar ponto flutuante, coordenadas, hash de runtime ou ordem incidental como fonte de identidade.

Esse grafo não afirma que uma região tática real terá três células.

Também não afirma que a topologia física final seja uma cadeia.

A geometria hexagonal/pentagonal de alta resolução, refinamento de superfície, border bands e conectividade cross-region permanecem decisões posteriores.

## 12.3 Materialização estratégica → tática

M2.2.3 estabelece a primeira materialização do conjunto de regiões táticas a partir de uma topologia estratégica já válida.

A entrada autoritativa será um `StrategicTopology`.

Essa escolha preserva a topologia estratégica já canonicalizada como fonte da verdade para a cardinalidade e a identidade das regiões a materializar.

A operação conceitual será:

`StrategicTacticalRegionMaterializer.Materialize(StrategicTopology) -> IReadOnlyList<TacticalRegion>`

O materializador deverá:

- rejeitar `StrategicTopology` nulo;
- percorrer `StrategicTopology.Cells` na ordem canônica já validada;
- criar exatamente uma `TacticalRegion` para cada `StrategicCell`;
- usar `MinimalTacticalRegionGraphGenerator.Generate(cell.Id)` para cada região nesta tranche;
- preservar `TacticalRegion.StrategicCellId == StrategicCell.Id`;
- preservar a mesma ordem canônica da coleção estratégica;
- impedir omissões ou duplicação de parents no resultado;
- devolver snapshot somente leitura;
- produzir a mesma assinatura canônica em materializações repetidas da mesma topologia.

M2.2.3 não introduz um aggregate/container tático adicional.

A coleção somente leitura é suficiente para provar a correspondência um-para-um neste checkpoint.

Um aggregate mais rico poderá ser introduzido em M2.3 caso shared border bands, incidência multi-região ou outras relações cross-region exijam ownership e invariantes próprios.

O materializador não utilizará `StrategicEdge` ou `StrategicVertex` para criar adjacência tática cross-region em M2.2.3.

O contrato é genérico para qualquer `StrategicTopology` válido já produzido pelo gerador suportado, incluindo as famílias Class I, Class II e Class III. Casos representativos deverão ser usados nos testes sem transformar esses exemplos em restrição adicional do contrato.

A materialização continua sem congelar:

- geometria tática final;
- resolução de refinamento;
- coordenadas;
- mesh;
- shared border bands;
- ownership cross-region;
- adjacência tática cross-region.

## 12.4 Gate de validação acumulada de M2.2

M2.2.4 valida a topologia tática como stage integrado antes de avançar para contratos cross-region.

O audit acumulado confirmou cobertura direta já existente para:

- identidade `TacticalCellId`;
- rejeição de estado default inválido;
- ownership por parent estratégico;
- rejeição de self-loop, duplicatas, dangling adjacency e adjacência cross-region;
- reciprocidade e conectividade local;
- ordenação canônica de células e adjacências;
- geração local determinística;
- materialização um-para-um `StrategicCell -> TacticalRegion`;
- coleção materializada somente leitura;
- materialização representativa em Class I, Class II e Class III;
- regressão cross-platform.

M2.2.4 será deliberadamente validation-only.

Nenhum production type novo será criado nesta tranche, salvo se a validação revelar um defeito real que exija correção antes do fechamento.

O conjunto adicional de validação deverá provar:

- snapshot e read-only semantics de `TacticalCell.AdjacentCellIds`;
- snapshot e read-only semantics de `TacticalRegion.Cells`;
- materialização adicional de `G(0,2)`, `G(2,2)`, `G(1,2)`, `G(3,1)` e `G(3,2)`;
- cardinalidade de `G(3,2)` com 192 regiões e 576 células táticas no reference graph atual;
- unicidade global de `TacticalCellId` no conjunto materializado;
- preservação de parent-local adjacency em escala maior;
- assinatura canônica idêntica em materializações repetidas de `G(3,2)`.

`G(3,2)` funciona como smoke determinístico de escala para este stage.

M2.2.4 não introduzirá threshold de wall-clock.

Benchmark e baseline de performance quantitativa permanecem pertencentes às capabilities e gates específicos de escalabilidade, especialmente M2.5.

A validação adicional deverá ser implementada em um arquivo de testes dedicado:

`GlobalArena.Tests/TacticalRegionStageValidationTests.cs`

O gate final de M2.2 foi aprovado em 2026-09-20.

A tranche M2.2.4 permaneceu validation-only e adicionou somente `GlobalArena.Tests/TacticalRegionStageValidationTests.cs`.

Resultado acumulado:

- Release build com 0 warnings e 0 errors;
- 241/241 testes locais;
- snapshot/read-only semantics de `TacticalCell` e `TacticalRegion` diretamente validadas;
- variantes `G(0,2)`, `G(2,2)`, `G(1,2)`, `G(3,1)` e `G(3,2)` materializadas;
- `G(3,2)` com 192 regiões e 576 identidades táticas globalmente únicas;
- adjacency parent-local preservada;
- assinatura stage-level determinística em materializações repetidas;
- regressão cross-platform aprovada em Ubuntu, Windows e macOS.

Com esse gate, M2.2 — Tactical Region Topology é encerrado e `Tactical region topology` alcança maturidade `Validada — fator 0.85`.

Esse fechamento não constitui prova de:

- shared border bands;
- continuidade tática entre regiões;
- ownership multi-região;
- refinamento hierárquico Goldberg;
- geometria tática final.

Esses itens permanecem para M2.3 e etapas seguintes.

---

# 13. Conectividade entre regiões

Toda conexão entre `StrategicCells` deverá possuir correspondência determinística entre suas regiões táticas.

Essa correspondência deverá preservar:

- adjacência;
- pertencimento;
- continuidade espacial;
- capacidade de navegação;
- passagem de infraestrutura;
- passagem de sistemas físicos relevantes;
- transições militares e logísticas.

A representação concreta da fronteira ainda não está congelada.

Ela poderá envolver faixas compartilhadas, mapeamentos de borda ou outra estrutura que preserve os invariantes necessários.

Quando uma faixa ou subtile for compartilhado, deverá existir uma identidade canônica única e incidência explícita às regiões estratégicas relevantes.

A correspondência vista de lados opostos da mesma fronteira deverá ser determinística e recíproca.

M2.3.1 congela a direção arquitetural da fronteira compartilhada.

Cada `StrategicEdge` possuirá exatamente um `SharedBorderBand` lógico.

A identidade do band será o próprio `StrategicEdgeId`; não será criado um `SharedBorderBandId` redundante.

Elementos lógicos compartilhados da fronteira usarão uma categoria própria de identidade:

`SharedBorderElementId = StrategicEdgeId + LocalOrdinal`

Regras:

- `StrategicEdgeId` deve ser válido;
- `LocalOrdinal` é one-based;
- `LocalOrdinal > 0`;
- `default(SharedBorderElementId)` é inválido;
- a identidade não pertence a nenhuma `TacticalRegion`;
- o mesmo elemento compartilhado não poderá ser materializado como dois `TacticalCellId` region-owned.

A sequência canônica de uma faixa será orientada pelo próprio `StrategicEdge`.

A direção conceitual será:

`min(IncidentVertexIds) -> max(IncidentVertexIds)`

e `LocalOrdinal` crescerá nessa direção canônica.

Isso fornece orientação estável independente do lado da região que observa a fronteira e evita que cada região invente sua própria ordem.

`SharedBorderBand` deverá:

- carregar o `StrategicEdgeId` que o identifica;
- possuir coleção não vazia de `SharedBorderElement`;
- exigir que todos os elementos usem o mesmo `StrategicEdgeId`;
- exigir ordinais locais únicos, contíguos e one-based;
- expor os elementos em ordem canônica;
- preservar snapshot somente leitura.

Os dois `StrategicCellId` incidentes não serão uma segunda fonte de verdade armazenada independentemente no band.

Incidência regional deverá ser derivada autoritativamente de:

`StrategicTopology.Edges[StrategicEdgeId].IncidentCellIds`

Da mesma forma, a orientação canônica deverá ser derivada de `IncidentVertexIds`.

M2.3 não alterará o significado de `TacticalCellId`, que continua representando somente células region-owned.

Também não serão criadas adjacências diretas entre `TacticalCell` de regiões diferentes neste primeiro contrato de fronteira.

Quando os bands estiverem materializados, um aggregate cross-region passa a ser arquiteturalmente justificável porque haverá invariantes próprios entre:

- `StrategicTopology`;
- uma `TacticalRegion` por `StrategicCell`;
- um `SharedBorderBand` por `StrategicEdge`.

Esse aggregate deverá ser projetado em subcheckpoint posterior de M2.3 e possuir a responsabilidade de validar a incidência derivada e a cobertura completa de edges, em vez de duplicar relações dentro de `TacticalRegion`.

A quantidade final de elementos por band, a geometria física da fileira, o vínculo entre region-owned tactical cells e elementos compartilhados, coordenadas, mesh e refinamento Goldberg cross-region continuam não congelados.

M2.3.2 materializou o contrato local por meio de:

- `SharedBorderElementId`;
- `SharedBorderElement`;
- `SharedBorderBand`.

A implementação validada preserva:

- `SharedBorderElementId = StrategicEdgeId + LocalOrdinal`;
- ordinal one-based e estado `default` inválido;
- identidade do band diretamente pelo `StrategicEdgeId`;
- coleção de elementos obrigatoriamente não vazia;
- pertencimento de todos os elementos ao mesmo edge;
- IDs únicos;
- ordinais contíguos;
- canonicalização por `LocalOrdinal`;
- snapshot somente leitura;
- ausência de estado duplicado de `StrategicCellId` e `StrategicVertexId`;
- ausência de dependência de `TacticalCellId`.

O gate local fechou com 259/259 testes e regressão cross-platform aprovada em Ubuntu, Windows e macOS.

M2.3.2 não materializa ainda um band para cada edge existente no planeta. Essa responsabilidade pertence a M2.3.3.

M2.3.3 congelará a primeira materialização determinística de border bands a partir de uma `StrategicTopology` já válida.

A operação conceitual será:

`StrategicEdgeSharedBorderBandMaterializer.Materialize(StrategicTopology) -> IReadOnlyList<SharedBorderBand>`

A entrada autoritativa será somente `StrategicTopology`.

O materializador não receberá `GoldbergParameters` separados, não regenerará topologia e não dependerá de `TacticalRegion`.

Para esta tranche, cada `StrategicEdge` produzirá exatamente um `SharedBorderBand` com exatamente um `SharedBorderElement` de referência:

`SharedBorderElementId(edge.Id, 1)`

Esse único elemento é deliberadamente não geométrico.

Ele existe apenas para provar materialização, identidade, cobertura, ordering e determinismo sem congelar:

- comprimento físico da fronteira;
- quantidade final de subtiles;
- largura de border band;
- coordenadas;
- mesh;
- ligação com células táticas region-owned.

Invariantes de M2.3.3:

- topologia nula é rejeitada;
- exatamente um band é criado por `StrategicEdge`;
- a ordem dos bands é a ordem canônica de `StrategicTopology.Edges`;
- `band.StrategicEdgeId == edge.Id` na mesma posição;
- cada edge aparece exatamente uma vez;
- nenhum band referencia edge fora da topologia fornecida;
- cada band possui exatamente um elemento de referência nesta tranche;
- esse elemento usa ordinal local `1`;
- IDs dos elementos são globalmente únicos porque incluem `StrategicEdgeId`;
- a coleção retornada é snapshot somente leitura;
- materializações repetidas produzem a mesma assinatura canônica;
- nenhum `StrategicCellId`, `StrategicVertexId` ou `TacticalCellId` é duplicado no materializador.

Casos representativos de validação:

- Class I `G(2,0)` → 120 bands;
- Class II `G(2,2)` → 360 bands;
- Class III `G(3,2)` → 570 bands.

Esses casos validam cobertura do contrato genérico sobre `StrategicTopology`; não constituem prova de refinamento Goldberg universal.

M2.3.3 implementou `StrategicEdgeSharedBorderBandMaterializer`.

A implementação:

- recebe apenas uma `StrategicTopology` já válida;
- rejeita entrada nula;
- percorre `StrategicTopology.Edges` em ordem canônica;
- materializa exatamente um `SharedBorderBand` por edge;
- preserva identidade `band.StrategicEdgeId == edge.Id`;
- usa um único `SharedBorderElementId(edge.Id, 1)` de referência por band;
- retorna snapshot somente leitura;
- preserva identidade global única dos reference elements;
- produz assinatura determinística em materializações repetidas.

A validação representativa cobriu:

- Class I `G(2,0)` com 120 bands;
- Class II `G(2,2)` com 360 bands;
- Class III `G(3,2)` com 570 bands.

O fechamento local atingiu 271/271 testes, e a regressão cross-platform passou em Ubuntu, Windows e macOS.

O elemento único por band continua sendo apenas um artefato lógico de referência. Ele não congela a cardinalidade física final da fronteira.

M2.3.4 congela o aggregate lógico que une os contratos já materializados de estratégia, regiões táticas e shared borders.

Será introduzido:

`StrategicTacticalBorderAggregate`

com construção conceitual:

`new StrategicTacticalBorderAggregate(StrategicTopology, IEnumerable<TacticalRegion>, IEnumerable<SharedBorderBand>)`

A `StrategicTopology` fornecida permanece a fonte autoritativa da estrutura estratégica.

O aggregate não regenerará regiões nem border bands. Ele receberá as coleções já materializadas para poder validar explicitamente cobertura, uniqueness e consistência cross-region.

O contrato exigirá cobertura exata de regiões:

- exatamente uma `TacticalRegion` por `StrategicCell`;
- nenhum parent ausente;
- nenhum `StrategicCellId` duplicado;
- nenhuma região cujo parent não exista na topologia fornecida;
- exposição pública canonicalizada pela ordem de `StrategicTopology.Cells`, independentemente da ordem de entrada.

O contrato exigirá cobertura exata de border bands:

- exatamente um `SharedBorderBand` por `StrategicEdge`;
- nenhum edge ausente;
- nenhum `StrategicEdgeId` duplicado;
- nenhum band cujo edge não exista na topologia fornecida;
- exposição pública canonicalizada pela ordem de `StrategicTopology.Edges`, independentemente da ordem de entrada.

Também será introduzido:

`SharedBorderIncidence`

Cada incidence representará uma relação derivada e possuirá referências para:

- o `StrategicEdge` autoritativo;
- o `SharedBorderBand` daquele edge;
- exatamente duas `TacticalRegion` incidentes.

A identidade da incidence continuará sendo o próprio `StrategicEdge.Id`; não haverá um novo ID redundante.

As duas regiões serão resolvidas exclusivamente por:

`StrategicEdge.IncidentCellIds`

e expostas na mesma ordem canônica desses IDs.

Assim, a incidence não armazenará uma cópia independente de `StrategicCellId` como segunda fonte da verdade.

`StrategicTacticalBorderAggregate` exporá:

- `StrategicTopology`;
- `IReadOnlyList<TacticalRegion> TacticalRegions`;
- `IReadOnlyList<SharedBorderBand> SharedBorderBands`;
- `IReadOnlyList<SharedBorderIncidence> SharedBorderIncidences`.

Todas as coleções serão snapshots somente leitura.

M2.3.4 não introduzirá lookup APIs adicionais por ID; as coleções canônicas e a incidence derivada são suficientes para esta tranche.

A construção repetida do aggregate sobre inputs semanticamente equivalentes deverá produzir assinatura canônica idêntica.

Validação representativa será feita em:

- Class I `G(2,0)` → 42 regiões, 120 bands e 120 incidences;
- Class II `G(2,2)` → 122 regiões, 360 bands e 360 incidences;
- Class III `G(3,2)` → 192 regiões, 570 bands e 570 incidences.

M2.3.4 continua sem definir:

- mapping físico entre `TacticalCell` region-owned e border elements;
- adjacency direta entre `TacticalCell` de regiões diferentes;
- quantidade física final de elementos por band;
- geometria;
- coordenadas;
- mesh;
- pathfinding cross-region final;
- refinamento Goldberg universal.

M2.3.4 implementou `StrategicTacticalBorderAggregate` e `SharedBorderIncidence`.

A implementação validada:

- recebe uma `StrategicTopology` e coleções já materializadas de regions e bands;
- não rematerializa dependências internamente;
- exige exatamente uma `TacticalRegion` por `StrategicCell`;
- exige exatamente um `SharedBorderBand` por `StrategicEdge`;
- rejeita entries nulas, ausentes, duplicadas ou estrangeiras;
- canonicaliza regions por `StrategicTopology.Cells`;
- canonicaliza bands e incidences por `StrategicTopology.Edges`;
- deriva duas regiões incidentes por `StrategicEdge.IncidentCellIds`;
- preserva a ordem canônica das duas regiões incidentes;
- mantém referências aos domain objects fornecidos;
- expõe snapshots somente leitura;
- produz assinatura canônica determinística em construções repetidas.

A validação representativa cobriu:

- Class I `G(2,0)` com 42 regions, 120 bands e 120 incidences;
- Class II `G(2,2)` com 122 regions, 360 bands e 360 incidences;
- Class III `G(3,2)` com 192 regions, 570 bands e 570 incidences.

O gate local atingiu 291/291 testes, e a regressão cross-platform passou em Ubuntu, Windows e macOS.

Nenhum `SharedBorderIncidenceId`, mapping físico, geometry, mesh ou adjacency tática cross-region foi introduzido.

M2.3.5 será um gate de validação acumulada sem alteração de production code.

A validação será feita em um único arquivo:

`GlobalArena.Tests/SharedBorderStageValidationTests.cs`

Objetivo:

provar os invariantes integrados de M2.3 através dos contratos públicos já existentes, sem introduzir nova semântica antes do fechamento do stage.

O gate validará:

- para cada `StrategicCell`, o conjunto de `SharedBorderIncidence.StrategicEdge.Id` observado através das regiões incidentes coincide exatamente com `StrategicCell.IncidentEdgeIds`;
- regiões de células pentagonais participam de exatamente cinco incidences;
- regiões de células hexagonais participam de exatamente seis incidences;
- a soma dos graus regionais derivados é exatamente `2 * StrategicTopology.Edges.Count`;
- cada `SharedBorderIncidence` é observada por exatamente duas regiões e por nenhuma terceira;
- todos os `SharedBorderElementId` continuam globalmente únicos no aggregate;
- cada border element continua pertencendo ao `StrategicEdgeId` do próprio band;
- cada band contém o elemento lógico de referência com `LocalOrdinal == 1`, sem congelar a cardinalidade física final;
- toda adjacency de `TacticalCell` continua estritamente local ao mesmo `StrategicCellId`;
- duas pipelines independentes criadas a partir dos mesmos `GoldbergParameters` produzem a mesma assinatura canônica completa.

A cobertura Goldberg adicional de stage será:

- Class I invertida `G(0,2)` → 42 regions / 120 bands / 120 incidences;
- Class III `G(1,2)` → 72 regions / 210 bands / 210 incidences;
- Class III chiral correspondente `G(2,1)` → 72 regions / 210 bands / 210 incidences;
- Class III `G(3,1)` → 132 regions / 390 bands / 390 incidences.

M2.3.5 não repetirá testes de malformed input e snapshot mutation já provados diretamente em M2.3.4.

Matriz adicional:

- 6 Facts;
- 4 execuções de uma Theory;
- 10 casos executados adicionais;
- baseline: 291;
- esperado: 301.

Nenhum production file será alterado.

Se o gate local, a auditoria da implementação de testes e a regressão cross-platform passarem, o fechamento formal de M2.3 poderá promover:

`Shared subtile border bands`

de:

`Integrada ao sistema — fator 0.70`

para:

`Validada — fator 0.85`.

A promoção potencial é de `+2.40 GPP`.

`Strategic ↔ tactical hierarchy/refinement mapping` permanecerá em `0.00`, porque physical `TacticalCell`-to-border mapping e refinement continuam sem prova.

O fechamento de M2.3 não encerra M2.

O próximo stage após M2.3 é:

`M2.4 — Goldberg Family & Refinement Validation`

Esses limites permanecem para M2.4 e gates posteriores.

M2.3.5 foi concluído como validation-only gate, sem alteração de production code.

A validação acumulada de M2.3 provou:

- correspondência exata entre `StrategicCell.IncidentEdgeIds` e as incidences observadas por sua `TacticalRegion`;
- degree 5 para regions de parents pentagonais;
- degree 6 para regions de parents hexagonais;
- handshake global `sum(region incidence degree) == 2 * StrategicTopology.Edges.Count`;
- exatamente duas regions por `SharedBorderIncidence`;
- unicidade global de `SharedBorderElementId` no aggregate;
- edge-locality de todos os border elements;
- presença do reference element ordinal `1` em cada band sem congelar cardinalidade física;
- adjacency de `TacticalCell` estritamente parent-local;
- determinismo de duas pipelines completas e independentes iniciadas pelos mesmos `GoldbergParameters`.

A cobertura adicional incluiu:

- `G(0,2)` → 42 regions / 120 bands / 120 incidences;
- `G(1,2)` → 72 regions / 210 bands / 210 incidences;
- `G(2,1)` → 72 regions / 210 bands / 210 incidences;
- `G(3,1)` → 132 regions / 390 bands / 390 incidences.

O gate local atingiu 301/301 testes e a regressão cross-platform passou em Ubuntu, Windows e macOS.

M2.3 — Shared Border Bands & Strategic/Tactical Mapping está encerrado.

`Shared subtile border bands` atinge maturidade `Validada — fator 0.85`.

M2.3 não prova physical `TacticalCell`-to-border mapping, direct cross-region tactical adjacency, final border geometry, final physical border cardinality ou universal Goldberg refinement.

Esses problemas passam explicitamente para M2.4 — Goldberg Family & Refinement Validation.

## 12.4 M2.4 refinement compatibility baseline

O audit read-only de M2.4 confirmou que o repositório possui geração estratégica funcional para Class I, Class II e Class III, mas não possui nenhum production contract de coarse-to-fine refinement.

M2.4 separa explicitamente duas ideias que não podem ser confundidas:

- gerar duas topologias Goldberg válidas;
- provar que uma delas é um refinamento hierárquico suportado da outra.

Contagens maiores, `TriangulationNumber` maior ou igualdade de fórmulas não constituem prova de refinement.

O baseline V1 de compatibilidade será conservador.

Uma relação Goldberg coarse→fine será considerada suportada inicialmente somente quando existir um inteiro `scale >= 2` tal que:

`fine.M == coarse.M * scale`

e

`fine.N == coarse.N * scale`.

Essa regra preserva direção/orientação do vetor Goldberg e, para Class III, preserva a mesma quiralidade. Ela fornece um subconjunto verificável de relações candidatas a refinement sem declarar impossíveis outras relações matematicamente válidas.

A prova de compatibilidade por escala será representada por:

`GoldbergScaledRefinement`

com:

- `GoldbergParameters CoarseParameters`;
- `GoldbergParameters FineParameters`;
- `int Scale`.

O objeto:

- rejeita parâmetros `default`/inválidos;
- rejeita `scale == 1`;
- rejeita direção coarse→fine invertida;
- rejeita troca de eixo Class I;
- rejeita pares não colineares no espaço inteiro `(m,n)`;
- rejeita troca de quiralidade Class III;
- não gera `StrategicTopology`;
- não cria parent-child mapping;
- não usa coordenadas de ponto flutuante;
- não compara IDs locais de duas topologias como se fossem linhagem.

M2.4.1 valida apenas a compatibilidade matemática conservadora por escala.

O mapping real de entidades permanece para M2.4.2 e deverá ser derivado de provenance de construção inteira/combinatória, não de coincidência de ordinal de `StrategicCellId`.

O `MinimalTacticalRegionGraphGenerator` permanece um reference graph de M2.2 e não é reinterpretado como refinement físico.

`SharedBorderElement` continua sendo entidade de fronteira própria e não será reinterpretado como `TacticalCell`.

M2.4 fica decomposto em:

- M2.4.1 — Scaled Refinement Compatibility Contract;
- M2.4.2 — Canonical Construction Provenance & Reference Mapping;
- M2.4.3 — Shared Border Refinement Continuity;
- M2.4.4 — Class I/II/III Scaled Refinement Validation;
- M2.4.5 — Refinement Stage Validation & M2.4 Close.

M2.4.1 não promove GPP. `Strategic ↔ tactical hierarchy/refinement mapping` permanece em fator `0.00` até existir parent-child mapping executável.

M2.4.1 foi implementado e validado cross-platform.

A implementação introduziu somente:

- `GoldbergScaledRefinement`;
- `GoldbergScaledRefinementTests`.

O contrato:

- recebe `CoarseParameters` e `FineParameters`;
- deriva `Scale` por divisão inteira exata;
- exige `Scale >= 2`;
- exige `fine.M == coarse.M * Scale`;
- exige `fine.N == coarse.N * Scale`;
- rejeita parâmetros `default`;
- rejeita mesma resolução;
- rejeita direção coarse→fine invertida;
- rejeita pares não colineares;
- rejeita troca de eixo Class I;
- rejeita troca de quiralidade Class III;
- preserva a consequência `fine.T == coarse.T * Scale^2`;
- não materializa `StrategicTopology`;
- não cria lineage de IDs;
- não cria parent-child mapping;
- não altera `TacticalRegion`;
- não altera `SharedBorderBand`;
- não usa floating point.

A suíte local atingiu 313/313 testes.

A regressão `Cross-Platform Kernel Regression Validation`, run `35499086790`, passou em Ubuntu, Windows e macOS.

M2.4.1 está concluído.

O próximo subcheckpoint é M2.4.2 — Canonical Construction Provenance & Reference Mapping, que deverá investigar e congelar como provenance combinatória pode produzir um primeiro mapping executável entre coarse e fine sem usar coincidência de ordinais como lineage.

## 12.5 M2.4.2 canonical construction provenance and reference mapping

O audit read-only de M2.4.2 confirmou que os geradores já possuem provenance combinatória suficiente internamente, mas ela é descartada antes de se tornar parte de um contrato cross-resolution.

No caminho Class I / Class II:

- `SubdivisionLatticeVertexKey` representa uma posição canônica por seed vertices ponderados;
- os keys são ordenados antes da atribuição de `StrategicCellId`;
- `StrategicCellId` continua sendo identidade de saída local à topologia, não provenance.

No caminho Class III:

- `LocalPoint`, `LatticeIndex`, `SeedFace`, `FaceEdgeReference` e `DisjointSet` preservam informação de construção;
- a costura é canonicalizada por roots;
- os roots são detalhes internos e não podem se tornar identidade durável.

M2.4.2 não tentará resolver toda a provenance de Class I, II e III em um único salto.

O primeiro reference mapping executável será limitado ao par:

`G(1,0) -> G(2,0)`

com:

`scale = 2`.

Esse par é escolhido porque todo `StrategicCell` coarse de `G(1,0)` corresponde a um dos 12 vertices canônicos do icosaedro base.

### Icosahedron seed provenance

M2.4.2 introduzirá:

`IcosahedronSeedVertexId`

com domínio:

`1..12`.

O tipo:

- é uma identidade de provenance de construção;
- não é `StrategicCellId`;
- rejeita zero;
- rejeita valores acima de 12;
- trata `default` como inválido;
- não usa coordenadas floating-point.

### Reference relation

M2.4.2 introduzirá:

`GoldbergScaledCellReference`

com:

- `IcosahedronSeedVertexId SeedVertexId`;
- `StrategicCellId CoarseCellId`;
- `StrategicCellId FineCellId`.

A relação significa apenas:

o coarse cell e o fine cell foram materializados a partir do mesmo vertex canônico do seed icosaédrico.

Ela não significa:

- ownership de fine cells pelo coarse cell;
- parent-child completo;
- cobertura de todas as fine cells;
- edge refinement;
- vertex refinement;
- tactical refinement.

### Reference map

M2.4.2 introduzirá:

`GoldbergScaledRefinementReferenceMap`

e:

`GoldbergScaledRefinementReferenceMapper`.

O mapper:

- recebe um `GoldbergScaledRefinement`;
- nesta tranche aceita somente `G(1,0) -> G(2,0)`, scale 2;
- rejeita outros pares com `NotSupportedException`;
- obtém coarse/fine references da provenance de construção do gerador;
- não calcula lineage por igualdade ou aritmética de `StrategicCellId`;
- produz exatamente 12 references;
- ordena references por `SeedVertexId`;
- exige seed IDs únicos;
- exige coarse IDs únicos;
- exige fine IDs únicos;
- retorna coleção read-only.

O public behavior de `GoldbergStrategicTopologyGenerator.Generate` permanece inalterado.

A implementação poderá extrair um carrier interno de provenance do caminho triangular, mas esse carrier não será public API e não deverá duplicar o algoritmo de geração.

### Canonical reference vector

Para o baseline atual, o reference vector congelado de `G(1,0) -> G(2,0)` é:

- seed 1: coarse cell 1 -> fine cell 6;
- seed 2: coarse cell 2 -> fine cell 11;
- seed 3: coarse cell 3 -> fine cell 15;
- seed 4: coarse cell 4 -> fine cell 19;
- seed 5: coarse cell 5 -> fine cell 23;
- seed 6: coarse cell 6 -> fine cell 26;
- seed 7: coarse cell 7 -> fine cell 30;
- seed 8: coarse cell 8 -> fine cell 33;
- seed 9: coarse cell 9 -> fine cell 36;
- seed 10: coarse cell 10 -> fine cell 39;
- seed 11: coarse cell 11 -> fine cell 41;
- seed 12: coarse cell 12 -> fine cell 42.

Esse vetor é consequência do canonical construction ordering atual e fornece um oracle independente para o primeiro reference mapping.

### Scope boundary

M2.4.2 não:

- mapeia fine cells intermediárias para coarse parents;
- define ownership de cells em coarse boundaries;
- define coarse-edge para fine-edge-chain;
- define coarse-vertex para fine junction;
- altera `TacticalRegion`;
- altera `SharedBorderBand`;
- suporta Class II ou Class III reference mapping;
- prova universal Goldberg refinement.

Class II e Class III permanecem para M2.4.4 depois que M2.4.3 congelar continuidade de shared borders sobre o reference pair.

M2.4.2 não promove GPP no design. A capability `Strategic ↔ tactical hierarchy/refinement mapping` permanece em fator `0.00` enquanto só existir anchor/reference correspondence e não hierarchy coverage executável.

M2.4.2 foi implementado e validado cross-platform.

A implementação introduziu:

- `IcosahedronSeedVertexId`;
- `GoldbergScaledCellReference`;
- `GoldbergScaledRefinementReferenceMap`;
- `GoldbergScaledRefinementReferenceMapper`;
- provenance interna reutilizável no caminho triangular de `GoldbergStrategicTopologyGenerator`.

O reference mapper suporta exatamente:

`G(1,0) -> G(2,0)`

com:

`scale = 2`.

Foram materializadas 12 references em ordem canônica de seed vertex.

Coverage:

- coarse: 12/12 cells referenciadas;
- fine: 12/42 cells referenciadas;
- as 30 fine cells intermediárias permanecem sem owner neste checkpoint.

O canonical reference vector validado é:

- `1:1->6`;
- `2:2->11`;
- `3:3->15`;
- `4:4->19`;
- `5:5->23`;
- `6:6->26`;
- `7:7->30`;
- `8:8->33`;
- `9:9->36`;
- `10:10->39`;
- `11:11->41`;
- `12:12->42`.

O mapper reutiliza o mesmo construction path do gerador.

Ele não deriva lineage por igualdade de `StrategicCellId`.

O public contract de `GoldbergStrategicTopologyGenerator.Generate` permanece inalterado.

A suíte local atingiu 327/327 testes.

A regressão `Cross-Platform Kernel Regression Validation`, run `35500232516`, passou em Ubuntu, Windows e macOS.

M2.4.2 está concluído.

Ainda não existe full parent-child coverage, coarse-edge para fine-edge-chain mapping, Class II reference mapping ou Class III reference mapping.

O próximo subcheckpoint é M2.4.3 — Shared Border Refinement Continuity.

## 12.6 M2.4.3 shared border refinement continuity

O audit read-only de M2.4.3 executou um probe externo ao repositório sobre o reference pair:

`G(1,0) -> G(2,0)`

com:

`scale = 2`.

O probe confirmou:

- 30 coarse `StrategicEdge`;
- 120 fine `StrategicEdge`;
- 30 coarse `SharedBorderBand`;
- 120 fine `SharedBorderBand`;
- nenhum par de fine anchor cells é diretamente adjacente;
- cada par de fine anchors correspondente a um coarse edge possui exatamente um common neighbor;
- existem exatamente 30 unique two-edge chains;
- os 30 middle fine cells são distintos;
- os 30 middle fine cells são exatamente os 30 hexágonos de `G(2,0)`;
- as chains usam exatamente 60 fine edges distintos;
- os 60 fine edges representam metade dos 120 fine edges;
- os 60 fine bands correspondentes existem;
- todos os bands atuais continuam com um único reference `SharedBorderElement`.

Isso permite congelar um primeiro contrato de continuidade lógica de shared border sem criar geometry física.

### Cross-resolution shared border reference

M2.4.3 introduzirá:

`GoldbergScaledSharedBorderReference`

com:

- `StrategicEdgeId CoarseStrategicEdgeId`;
- `StrategicCellId MiddleFineCellId`;
- `IReadOnlyList<StrategicEdgeId> FineStrategicEdgeIds`.

Para o reference pair de M2.4.3:

- cada coarse edge possui exatamente dois fine edge IDs;
- os dois fine edges formam uma chain contínua;
- a chain começa no fine anchor correspondente ao primeiro `IncidentCellId` do coarse edge;
- passa pelo único middle fine cell;
- termina no fine anchor correspondente ao segundo `IncidentCellId` do coarse edge.

Essa ordering é uma ordering lógica de chain.

Ela não redefine a orientação interna conceitual de `SharedBorderBand` e não representa geometry física.

### Continuity map

M2.4.3 introduzirá:

`GoldbergScaledSharedBorderContinuityMap`

com:

- `GoldbergScaledRefinementReferenceMap CellReferenceMap`;
- `IReadOnlyList<GoldbergScaledSharedBorderReference> BorderReferences`.

Invariants para o reference pair:

- exatamente 30 border references;
- exatamente uma reference por coarse edge;
- exatamente dois fine edge IDs por reference;
- 30 middle fine cell IDs únicos;
- 60 fine edge IDs globalmente únicos;
- ordem canônica por `CoarseStrategicEdgeId`;
- snapshot read-only.

### Mapper

M2.4.3 introduzirá:

`GoldbergScaledSharedBorderContinuityMapper.Materialize(GoldbergScaledRefinementReferenceMap)`.

O mapper:

- exige o cell reference map validado de M2.4.2;
- gera coarse e fine topologies a partir dos parâmetros do refinement;
- usa `CoarseStrategicEdge.IncidentCellIds` para localizar os dois fine anchor cells via provenance;
- encontra o unique common fine neighbor;
- resolve os dois fine `StrategicEdge` que conectam anchor -> middle -> anchor;
- verifica que os respectivos coarse/fine `SharedBorderBand` existem;
- não compara coarse e fine `StrategicEdgeId` como lineage;
- não deriva mapping por arithmetic de IDs;
- não usa floating point;
- rejeita qualquer situação que não possua uma única two-edge chain.

### SharedBorder semantics

Uma border reference representa:

um coarse `SharedBorderBand`, identificado por seu `StrategicEdgeId`, correspondendo logicamente a dois fine `SharedBorderBand`, identificados pelos dois fine `StrategicEdgeId`.

Ela não transforma:

- `StrategicEdge` em `SharedBorderElement`;
- `SharedBorderElement` em `StrategicEdge`;
- `SharedBorderElement` em `TacticalCell`.

A cardinalidade atual de um reference element por band permanece inalterada.

M2.4.3 não define cardinalidade física final de border elements.

### Canonical reference vector

O oracle congelado usa:

`coarseEdge:middleFineCell:fineEdge1,fineEdge2`

e é:

- `1:1:3,6`;
- `2:2:8,11`;
- `3:3:13,16`;
- `4:4:18,21`;
- `5:5:22,25`;
- `6:7:27,29`;
- `7:8:31,33`;
- `8:9:35,38`;
- `9:10:39,42`;
- `10:12:44,46`;
- `11:13:48,50`;
- `12:14:51,54`;
- `13:16:56,58`;
- `14:17:60,62`;
- `15:18:63,66`;
- `16:20:68,70`;
- `17:21:72,74`;
- `18:22:75,78`;
- `19:24:80,82`;
- `20:25:83,85`;
- `21:27:87,89`;
- `22:28:91,93`;
- `23:29:94,97`;
- `24:31:99,101`;
- `25:32:102,104`;
- `26:34:106,108`;
- `27:35:109,111`;
- `28:37:113,115`;
- `29:38:116,118`;
- `30:40:119,120`.

Esse vector é um oracle de validação do canonical output atual.

O algoritmo de production não poderá usar esse vector hard-coded para construir o mapping.

### Scope boundary

M2.4.3 não:

- atribui ownership das 30 middle fine cells a um único coarse cell;
- mapeia coarse vertices para fine junctions;
- altera `SharedBorderElement`;
- altera a cardinalidade atual de `SharedBorderBand`;
- define physical tactical-border mapping;
- define final border geometry;
- suporta Class II ou Class III continuity;
- prova universal Goldberg refinement.

Class II e Class III permanecem para M2.4.4.

M2.4.3 não promove GPP no design.

`Strategic ↔ tactical hierarchy/refinement mapping` permanece em fator `0.00` até existir hierarchy coverage mais ampla do que seed anchors e logical edge chains.

M2.4.3 foi implementado e validado cross-platform.

A implementação introduziu:

- `GoldbergScaledSharedBorderReference`;
- `GoldbergScaledSharedBorderContinuityMap`;
- `GoldbergScaledSharedBorderContinuityMapper`;
- tests dedicados em `GoldbergScaledSharedBorderContinuityTests`.

Resultado executável para `G(1,0) -> G(2,0)`:

- 30/30 coarse strategic edges cobertos;
- exatamente uma border reference por coarse edge;
- exatamente dois fine strategic edges por chain;
- 30 middle fine cells únicos;
- os 30 middle fine cells são exatamente os 30 hexágonos de `G(2,0)`;
- 60 fine strategic edges distintos utilizados;
- 60/120 fine edges cobertos;
- 30/30 coarse shared-border bands resolvidos;
- 60/120 fine shared-border bands resolvidos;
- current single-element band semantics preservada;
- ordering lógica `first fine anchor -> middle -> second fine anchor`;
- repeated materialization determinística;
- collections read-only.

O mapper não usa igualdade ou arithmetic de `StrategicEdgeId` entre resoluções como lineage.

O mapper não usa o canonical oracle hard-coded para construir continuity.

`SharedBorderElement`, `SharedBorderBand`, `SharedBorderIncidence`, `StrategicTacticalBorderAggregate` e `StrategicEdgeSharedBorderBandMaterializer` permaneceram semanticamente inalterados.

A suíte local atingiu 340/340 testes.

A regressão `Cross-Platform Kernel Regression Validation`, run `35501841291`, passou em Ubuntu, Windows e macOS.

M2.4.3 está concluído.

Ainda não existe middle-cell ownership, coarse-vertex to fine-junction mapping, physical tactical-border mapping, final physical border geometry/cardinality, Class II continuity ou Class III continuity.

O próximo subcheckpoint é M2.4.4 — Class I/II/III Scaled Refinement Validation.

## 12.7 M2.4.4 multi-family scaled refinement validation

O audit read-only de M2.4.4 validou sete pares representativos em Class I, Class II e Class III.

Resultados públicos:

- exact scaled-parameter compatibility: validada;
- topology count scaling: validado;
- canonical determinism: validado;
- 12 pentágonos em coarse/fine: validado;
- Class I normal: validada;
- Class I inverted axis: validada;
- Class II: validada;
- Class III right chirality: validada;
- Class III left chirality: validada.

O audit também confirmou, apenas como evidência interna de construção:

- Class I/II possuem scale-homogeneous `SubdivisionLatticeVertexKey`;
- Class III possui scale-homogeneous local `(LocalPoint, LatticeIndex)`;
- raw `DisjointSet` root continua sendo implementation detail;
- Class III ainda não possui durable global provenance publicada.

### Design decision

M2.4.4 será **validation-only**.

Nenhum novo production type será introduzido neste checkpoint.

Motivo:

a evidência pública atual prova compatibilidade matemática, counts e determinismo multi-family, mas ainda não prova global durable lineage de Class III.

Transformar private local coordinates ou raw DSU roots em public identity agora criaria um contrato prematuro.

### Validation surface

Será adicionado somente:

`GlobalArena.Tests/GoldbergScaledRefinementFamilyValidationTests.cs`

com 14 Facts.

Representative pairs:

- Class I: `G(1,0) -> G(2,0)`, scale 2;
- Class I general: `G(2,0) -> G(4,0)`, scale 2;
- Class I inverted axis: `G(0,2) -> G(0,4)`, scale 2;
- Class I scale 3: `G(1,0) -> G(3,0)`;
- Class II: `G(1,1) -> G(2,2)`, scale 2;
- Class II general: `G(2,2) -> G(4,4)`, scale 2;
- Class II scale 3: `G(1,1) -> G(3,3)`;
- Class III right: `G(2,1) -> G(4,2)`, scale 2;
- Class III left: `G(1,2) -> G(2,4)`, scale 2;
- Class III right scale 3: `G(2,1) -> G(6,3)`;
- Class III left scale 3: `G(1,2) -> G(3,6)`.

Cada representative pair deverá validar somente public invariants:

- refinement `Scale`;
- exact parameter multiplication;
- generated topology counts equal parameter counts;
- `fine.StrategicCellCount = scale^2 * (coarse.StrategicCellCount - 2) + 2`;
- fine edges and vertices scale by `scale^2`;
- 12 pentágonos em coarse e fine;
- Euler;
- deterministic repeated topology signatures.

Aggregate validation deverá confirmar:

- todos os representative pairs permanecem deterministic;
- todos preservam 12 pentágonos e Euler;
- current `GoldbergScaledRefinementReferenceMapper` continua suportando somente `G(1,0) -> G(2,0)`;
- current `GoldbergScaledSharedBorderContinuityMapper` continua disponível somente através desse reference pair.

### Private construction evidence boundary

A evidência do audit sobre:

- `SubdivisionLatticeVertexKey`;
- `LocalPoint`;
- `LatticeIndex`;
- `DisjointSet`;

não será transformada em production API neste checkpoint.

Os testes de M2.4.4 não usarão reflection para congelar private implementation details.

Essa evidência permanece material de arquitetura para uma futura generalização de provenance, não contrato público atual.

### Scope boundary

M2.4.4 não:

- generaliza `GoldbergScaledRefinementReferenceMapper`;
- generaliza shared-border continuity;
- cria Class II cell lineage;
- cria Class III cell lineage;
- publica raw DSU roots;
- cria parent-child ownership;
- cria coarse-vertex junction mapping;
- cria physical tactical-border mapping;
- prova universal Goldberg refinement.

M2.4.4 não promove GPP no design.

Baseline de testes:

`340`.

Esperado após implementação:

`354`.

M2.4.4 foi implementado como validation-only e validado cross-platform.

Implementation delta:

- `GlobalArena.Tests/GoldbergScaledRefinementFamilyValidationTests.cs`;
- 14 Facts;
- 11 representative scaled pairs;
- nenhuma alteração em `GlobalArena.World`.

Coverage executável:

- Class I normal;
- Class I inverted axis;
- Class II;
- Class III right chirality;
- Class III left chirality;
- scale 2;
- scale 3.

A suíte valida por public contracts:

- `GoldbergScaledRefinement.Scale`;
- exact parameter multiplication;
- topology entity counts;
- scale-squared count relations;
- Euler;
- 12 pentágonos;
- deterministic canonical topology signatures.

O checkpoint também preserva explicitamente o narrow scope atual:

- `GoldbergScaledRefinementReferenceMapper` continua suportando apenas `G(1,0) -> G(2,0)`;
- `GoldbergScaledSharedBorderContinuityMapper` continua dependente desse mesmo reference pair;
- nenhum private generator detail foi promovido a public lineage.

A suíte local atingiu 354/354 testes.

A regressão `Cross-Platform Kernel Regression Validation`, run `35503057993`, passou em Ubuntu, Windows e macOS.

M2.4.4 está concluído.

M2.4.4 não promove GPP.

`Strategic ↔ tactical hierarchy/refinement mapping` permanece em fator `0.00`.

Permanecem abertos:

- multi-family cell lineage;
- multi-family edge-chain continuity;
- durable Class III global provenance;
- parent-child ownership;
- coarse-vertex junction mapping;
- physical tactical-border mapping;
- universal Goldberg refinement.

O próximo subcheckpoint é M2.4.5 — Refinement Stage Validation & M2.4 Close.

## 12.8 M2.4.5 refinement stage validation and M2.4 close

O audit read-only acumulado de M2.4.5 confirmou que M2.4.1–M2.4.4 formam um conjunto coerente de contratos e evidências, desde que o fechamento do stage preserve explicitamente seus limites.

O audit confirmou:

- os quatro formal-close commits anteriores pertencem à ancestry atual;
- build Release com 0 warnings e 0 errors;
- suíte atual em 354/354;
- 11 representative scaled pairs em Class I/II/III;
- scales 2 e 3;
- deterministic topology generation;
- Euler e 12 pentágonos em todos os pares;
- 12 canonical seed-cell references no reference pair `G(1,0) -> G(2,0)`;
- 30 shared-border continuity references;
- 30 unique middle fine cells;
- middle fine cells = todos os 30 fine hexagons;
- 60 unique mapped fine edges;
- 60/120 fine-edge coverage;
- mapped coarse/fine shared-border bands existentes;
- current single-element band semantics preservada;
- 10 non-reference representative pairs rejeitados pelo current reference mapper.

### Design decision

M2.4.5 será **validation-only**.

Nenhum production type será adicionado ou alterado.

Será criado somente:

`GlobalArena.Tests/GoldbergRefinementStageValidationTests.cs`

com 12 Facts cumulativos.

### Final stage validation matrix

1. all 11 representative scaled pairs preserve public scale/count invariants;
2. all representative topologies reproduce canonical signatures;
3. all representative pairs preserve Euler and 12 pentagons;
4. current reference mapper accepts only `G(1,0) -> G(2,0)` among the representative matrix;
5. reference map contains exactly 12 canonical seed references with unique coarse/fine anchors;
6. repeated reference-map materialization is deterministic;
7. continuity map contains exactly 30 references in coarse-edge order;
8. middle fine cells are exactly the 30 fine hexagons;
9. continuity uses exactly 60 globally unique fine edges;
10. every ordered two-edge chain connects the two expected fine anchors through its middle fine cell;
11. all mapped coarse/fine bands exist, mapped fine-edge coverage remains 60/120, and current single-element band semantics remain unchanged;
12. repeated continuity materialization is deterministic and exposed collections remain read-only.

Baseline:

`354`.

Expected after implementation:

`366`.

### Stage close semantics

Se a implementação de M2.4.5, a suíte completa e a regressão cross-platform passarem, M2.4 poderá ser fechado formalmente.

O fechamento de M2.4 poderá declarar:

- scaled refinement compatibility para os pares suportados pelo contract;
- representative multi-family topology regression coverage em Class I/II/III;
- canonical seed provenance e cell-reference mapping para o first reference pair;
- logical coarse-edge to fine-edge-chain continuity para o first reference pair.

O fechamento de M2.4 **não poderá** declarar:

- multi-family entity lineage;
- multi-family edge-chain continuity;
- durable global Class III provenance;
- parent-child ownership completo;
- coarse-vertex to fine-junction mapping;
- physical tactical-border mapping;
- final physical border geometry/cardinality;
- universal Goldberg refinement.

### Maturity and GPP

M2.4.5 não promove GPP no design.

`Strategic ↔ tactical hierarchy/refinement mapping` permanece:

`Inexistente — fator 0.00`.

O stage poderá fechar com essa capability ainda em `0.00` porque o escopo validado de M2.4 será registrado de forma estrita, enquanto a cobertura de hierarchy/physical mapping necessária ao M2 exit gate permanece para M2.5.

`RISK-003` permanece HIGH.

M2.4.5 foi implementado como validation-only no commit:

`01583a55f24119e398296c2c620cd76ab64277ca`.

Implementation delta:

- `GlobalArena.Tests/GoldbergRefinementStageValidationTests.cs`;
- 12 Facts cumulativos;
- nenhuma alteração em `GlobalArena.World`.

A suíte local atingiu 366/366 testes.

A regressão `Cross-Platform Kernel Regression Validation`, run `35504415048`, passou em Ubuntu, Windows e macOS.

O final gate confirmou em conjunto:

- 11 representative scaled pairs em Class I/II/III;
- scales 2 e 3;
- deterministic canonical topology signatures;
- Euler;
- 12 pentágonos;
- current reference mapper restrito ao first reference pair;
- 12 canonical seed references no first reference pair;
- 30 continuity references;
- 30 middle fine cells equivalentes aos 30 fine hexagons;
- 60 unique mapped fine edges;
- 60/120 fine-edge coverage;
- ordered two-edge chains válidos;
- mapped shared-border bands existentes;
- current single-element band semantics preservada;
- read-only exposed collections.

M2.4.5 está concluído.

M2.4 — Goldberg Family & Refinement Validation está formalmente concluído.

O fechamento do stage registra scaled refinement behavior validado e first-pair provenance/continuity contracts, mas não estabelece complete strategic-to-tactical hierarchy mapping.

`Strategic ↔ tactical hierarchy/refinement mapping` permanece:

`Inexistente — fator 0.00`.

M2.4 não promove GPP.

`RISK-003` permanece HIGH.

O próximo stage é:

`M2.5 — Scalability, Cross-Platform Regression & M2 Exit Gate`.

## 12.9 M2.5 entry audit and exit-gate contract

O entry audit read-only de M2.5 confirmou que o milestone M2 ainda não satisfaz seu exit gate.

Baseline auditada:

`e9e50c0ff957b286186e16cc7e9de86a01100256`

Evidência local:

- Release build: 0 warnings, 0 errors;
- suíte: 366/366;
- worktree limpo;
- no repository mutation;
- no commit;
- no push;
- 38/38 snapshot hashes recomputados sem divergência.

O audit confirmou que M2 exige conjuntamente:

- conectividade entre tabuleiros táticos vizinhos;
- mapeamento determinístico de fronteiras;
- pertencimento pai-filho;
- refinamento hierárquico;
- navegabilidade;
- comportamento explícito para as famílias Goldberg oficialmente suportadas;
- validação de escalabilidade;
- regressão cross-platform.

O estado atual ainda possui as seguintes lacunas:

- `GlobalArena.Benchmarks` continua como placeholder `Hello, World!`;
- o workflow de regressão atual não executa benchmark/scalability gate;
- `TacticalRegion` continua materializada pelo reference graph de três células;
- physical `TacticalCell`-to-shared-border attachment está ausente;
- cross-region tactical traversal/navigability está ausente;
- multi-family durable refinement lineage está ausente;
- `Strategic ↔ tactical hierarchy/refinement mapping` permanece em fator `0.00`.

Por isso, M2.5 não poderá ser um stage apenas de performance.

### M2.5 decomposition

M2.5 é decomposto em:

- M2.5.1 — Exit Gate Requirements & Performance Budget Contract;
- M2.5.2 — Strategic/Tactical Physical Boundary Attachment & Cross-Region Traversal;
- M2.5.3 — Hierarchy/Refinement Coverage Decision for Officially Supported Goldberg Families;
- M2.5.4 — Headless Scalability Benchmark Harness & Baseline;
- M2.5.5 — Cross-Platform Regression, Exit Audit & M2 Formal Close.

### M2.5.1 performance budget contract

M2.5.1 congela um budget de **M2 topology baseline**, não um budget final de V1.

Os canonical M2 load cases serão:

- Class I: `G(16,0)` — `T=256`, 2562 cells, 7680 edges, 5120 vertices;
- Class II: `G(10,10)` — `T=300`, 3002 cells, 9000 edges, 6000 vertices;
- Class III: `G(12,7)` — `T=277`, 2772 cells, 8310 edges, 5540 vertices.

O workload medido em M2.5.4 deverá construir do zero, em Release e headless, toda a produção exigida pelo M2 exit gate após M2.5.2/M2.5.3.

Isso inclui no mínimo:

- `StrategicTopology`;
- todas as `TacticalRegion`;
- todos os `SharedBorderBand`;
- `StrategicTacticalBorderAggregate`;
- qualquer estrutura adicional de boundary attachment/traversal tornada obrigatória por M2.5.2/M2.5.3.

A metodologia mínima será:

- 1 warmup por case;
- 5 measured samples por case;
- processo headless;
- .NET 10;
- registro de OS, arquitetura e runtime;
- tempo por `Stopwatch`;
- managed allocation report;
- validação estrutural antes de aceitar a amostra.

Hard budgets por canonical load case:

- median elapsed time: `<= 1000 ms`;
- maximum elapsed sample: `<= 2000 ms`;
- median managed allocation: `<= 192 MiB`;
- maximum managed allocation sample: `<= 256 MiB`.

Esses limites são intencionalmente amplos para o primeiro baseline de M2.

O entry audit observou, sem threshold e ainda sobre o workload atual de referência:

- `G(16,0)`: median 84.784 ms, max 116.604 ms, median allocation 63,876,136 bytes;
- `G(10,10)`: median 65.171 ms, max 70.425 ms, median allocation 75,541,888 bytes;
- `G(12,7)`: median 58.798 ms, max 82.909 ms, median allocation 62,493,848 bytes.

Esses números são evidência observacional, não acceptance evidence.

Se M2.5.2 ou M2.5.3 alterar o workload obrigatório do M2, qualquer benchmark anterior à mudança deixa de ser suficiente para fechamento.

Falha em budget bloqueia M2 close. O budget não poderá ser relaxado silenciosamente; mudança exige nova evidência e decisão arquitetural explícita.

### Scope boundary

O budget de M2.5.1:

- não define planet size final do V1;
- não fecha `RISK-002 — Tactical resolution scalability`;
- não fecha `RISK-012 — Memory footprint`;
- não prova performance de uma futura high-resolution tactical mesh;
- não substitui o requirement de physical boundary mapping;
- não substitui navigability;
- não prova universal Goldberg refinement.

Nenhuma promoção de GPP ocorre em M2.5.1.

O próximo gate é:

`M2.5.1 DESIGN AUDIT`.


## 13.x — Malha tática física global contínua (M2.5.2)

O audit vertex-aware de M2.5.2-A confirmou que os contratos de M2.2 e M2.3 são insuficientes, isoladamente, para representar a identidade física global.

M2.5.2 passa a separar explicitamente:

- contratos lógicos/reference de região e border band;
- malha tática física global.

A malha física será representada por uma `StrategicTopology` Goldberg fina dentro de um contexto coarse→fine aceito.

As células da topologia fina são os tiles táticos físicos canônicos e a adjacency da topologia fina é a adjacency física canônica.

A identidade física proposta é:

`PhysicalTacticalTileId = FineGoldbergParameters + FineStrategicCellId`

A relação com a partição estratégica grossa é representada por incidência coarse→fine explícita:

- 1 coarse `StrategicCell` → tile interior;
- 2 coarse `StrategicCell` → tile sobre `StrategicEdge`;
- 3 coarse `StrategicCell` → tile sobre `StrategicVertex`.

Os anchors de edge/vertex são derivados da `StrategicTopology` grossa e não duplicados como segunda fonte de verdade.

O caso de `StrategicVertex` é obrigatoriamente uma única célula fina compartilhada pelas três regiões estratégicas incidentes. Não poderá ser convertido em três `SharedBorderElement`.

`TacticalCellId` continua sendo identidade region-owned do reference graph de M2.2.

`SharedBorderElementId` continua sendo identidade lógica edge-scoped de M2.3.

Nenhum dos dois será usado como identidade física universal.

A navegação cross-region deverá usar a adjacency da topologia fina contínua, evitando costura por adjacências artificiais entre boards regionais independentes.

O audit de provenance do M2.5.2-C corrigiu o primeiro target de materialização. `G(1,0) -> G(3,0)`, scale 3, é vertex-aware mas não edge-aware: `72 interior + 0 edge-shared + 20 vertex-shared = 92`. O primeiro scale testado que contém simultaneamente incidência física 2-way e 3-way é `G(1,0) -> G(6,0)`, scale 6, com target de materialização `312 interior + 30 edge-shared + 20 vertex-shared = 362`. Essa decomposição é agora sustentada pelo audit combinatório de provenance, mas ainda precisa ser materializada e validada em production code pelo M2.5.2-C.

Detalhes e limites estão congelados em `ADR-021`.
### M2.5.2-B implementation evidence

The minimum physical identity/incidence vocabulary is now implemented:

- `PhysicalTacticalTileId` is scoped by `FineGoldbergParameters + FineStrategicCellId`;
- fine cell identity is rejected when it falls outside the selected fine Goldberg topology;
- `PhysicalTacticalTileIncidence` requires an accepted scaled coarse→fine refinement relation;
- incidence cardinality is restricted to exactly 1, 2 or 3 coarse strategic cells;
- two-cell incidence must correspond to one authoritative coarse `StrategicEdge`;
- three-cell incidence must correspond to one authoritative coarse `StrategicVertex`;
- incident coarse cells are exposed as a canonical read-only snapshot;
- the contract has no dependency on `TacticalCellId` or `SharedBorderElementId`.

Implementation evidence:

`GlobalArena-Evidence-M2.5.2-B-IMPLEMENTATION-R2-20260928-083834.zip`

Evidence SHA-256:

`9d1e552a6f15c0e390bf645fa86b6fb24d03bb41c4c98844c0dcbaaa2a2ca4fc`

Validation baseline:

`716976f2bda735527323746ae6d623a2686292ad`

Validation result:

- Release build: 0 warnings, 0 errors;
- tests: 389/389;
- new tests in this tranche: 23;
- exact production/test changeset: 4 new files;
- no coarse→fine physical incidence materializer yet.

M2.5.2-B freezes the production vocabulary. M2.5.2-C now materializes the accepted `G(1,0) -> G(6,0)` reference mapping completely; family/scale generalization remains a later gate.
### M2.5.2-C materializer implementation evidence

M2.5.2-C now exposes deterministic dominant triangular-seed provenance and materializes the accepted physical incidence reference target:

`G(1,0) -> G(6,0)`, scale `6`.

Production artifacts:

- `PhysicalTacticalIncidenceMap`;
- `PhysicalTacticalIncidenceMapper`;
- dominant-seed provenance exposed by `GoldbergStrategicTopologyGenerator`.

Validated reference invariants:

- every fine strategic cell becomes exactly one canonical `PhysicalTacticalTileId`;
- all `362/362` fine tiles are covered exactly once;
- `312` tiles have one coarse-cell incidence;
- `30` tiles have two-cell incidence and cover the `30` authoritative coarse `StrategicEdge` values exactly once;
- `20` tiles have three-cell incidence and cover the `20` authoritative coarse `StrategicVertex` values exactly once;
- the 12 fine pentagons remain interior and cover the 12 coarse strategic cells;
- repeated materialization is deterministic;
- public incidence collections are read-only;
- no `TacticalCellId` or `SharedBorderElementId` is used as physical identity.

Evidence:

`GlobalArena-Evidence-M2.5.2-C-MATERIALIZER-IMPLEMENTATION-R1-20260928-093748.zip`

Evidence SHA-256:

`bd3ff882fc0cf9c411f351dabd64a0c245a230a8c075bc028e79193592b38f98`

Quality gate:

- Release build: PASS;
- compiler warnings: 0;
- compiler errors: 0;
- tests: 402/402;
- new tests in M2.5.2-C materializer tranche: 13.

M2.5.2-C closes the first functional physical coarse-to-fine incidence materialization. M2.5.2-D now closes deterministic fine-topology cross-region traversal for the accepted reference target.

### M2.5.2-D traversal implementation evidence

Physical traversal now uses only the authoritative fine topology:

`PhysicalTacticalIncidenceMap.FineTopology.Cells[*].AdjacentCellIds`

Production artifacts:

- `PhysicalTacticalPath`;
- `PhysicalTacticalPathfinder`.

Validated invariants:

- deterministic BFS shortest paths;
- canonical `PhysicalTacticalTileId` endpoints and path steps;
- no repeated physical identity in a returned path;
- read-only path snapshots;
- edge-shared cross-coarse traversal;
- vertex-shared cross-coarse traversal;
- distant traversal across distinct coarse ownership;
- shortest-path distance independently cross-checked;
- no synthetic cross-region adjacency;
- no `TacticalCellId` or `SharedBorderElementId` dependency;
- accumulated tests: `417/417`.

Evidence:

`GlobalArena-Evidence-M2.5.2-D-TRAVERSAL-IMPLEMENTATION-R2-20260928-101812.zip`

Evidence SHA-256:

`99f1d20c8f252f3d2bdc84dea5c5f53c1158a1f4faed025d9057c7ef381ae8b9`

M2.5.2-D is closed. M2.5.2-E subsequently completed accumulated validation and formally closes the M2.5.2 physical tactical mesh reference contract.

### M2.5.2-E accumulated validation and close

M2.5.2-E performs no production mutation. It validates the complete accepted `G(1,0) -> G(6,0)` physical tactical contract as one integrated system.

Accumulated proof:

- `362/362` canonical physical tiles covered;
- incidence signature `312 interior / 30 edge-shared / 20 vertex-shared`;
- all `30/30` coarse `StrategicEdge` values represented exactly once;
- all `20/20` coarse `StrategicVertex` values represented exactly once;
- `1080` authoritative fine edges with reciprocal adjacency;
- edge-shared neighborhood pattern `3+3`;
- vertex-shared neighborhood pattern `2+2+2`;
- all `65.703/65.703` unordered-with-self physical tile pairs validated for shortest path;
- each path distance cross-checked against an independent BFS oracle;
- maximum shortest distance observed in the reference target: `18`;
- `61.441` endpoint pairs with distinct coarse-incidence ownership validated;
- deterministic representative traversal;
- no synthetic cross-region adjacency;
- tracked repository hash drift: `0`.

Evidence:

`GlobalArena-Evidence-M2.5.2-E-R2-READONLY-ACCUMULATED-VALIDATION-20260928-120824.zip`

Evidence SHA-256:

`2d26ea5a00c0457d733d0e0886b858238b13ef2f2161e675950b3f62278542e1`

M2.5.2 is closed. Official family/refinement support beyond the reference target remains M2.5.3.

### M2.5.3 physical hierarchy coverage contract

M2.5.3 separates strategic topology family support from physical strategic-to-tactical hierarchy support.

The M2.5.3-A coverage audit proves:

- strategic topology generation supports Class I, II and III;
- the public scaled-refinement compatibility contract accepts representative pairs from all three families;
- executable physical incidence currently supports only `G(1,0) -> G(6,0)`;
- the current 12-seed primitive is complete only for the Class I unit coarse topology;
- non-unit Class I requires durable lineage covering every coarse cell;
- Class II requires lineage beyond the current 12-seed map;
- Class III currently exposes no compatible dominant-seed provenance.

The M2.5.3-B frozen design contract is therefore:

`official M2 physical hierarchy = Class I, scale 6`

with accepted forms:

`G(k,0) -> G(6k,0)`

and:

`G(0,k) -> G(0,6k)`.

This does not remove strategic Class II/III generation.

It does not claim universal Goldberg physical refinement.

The scale-6 mapping must preserve exact single canonical physical identities and derive 1/2/3 coarse incidence from durable integer/combinatorial lineage.

For coarse triangulation number `Tc`, the implementation target is:

- fine cells: `360 * Tc + 2`;
- edge-shared physical tiles: `30 * Tc`;
- vertex-shared physical tiles: `20 * Tc`;
- interior physical tiles: `310 * Tc + 2`.

The existing `G(1,0) -> G(6,0)` behavior remains the compatibility oracle.

Final supported world-size limits are not frozen here; M2.5.4 must measure the complete accepted workload.

Evidence:

`GlobalArena-Evidence-M2.5.3-A-R2-READONLY-HIERARCHY-COVERAGE-AUDIT-20260928-131956.zip`

O estado estratégico de uma conexão poderá futuramente ser derivado do estado tático correspondente.

Exemplos futuros incluem:

- bloqueios;
- estradas;
- gargalos;
- fortificações;
- controle militar;
- capacidade logística.

---

# 14. World Generation

`WorldGeneration` será um pipeline reproduzível e substituível.

A geração deverá ser multiescala.

O grafo estratégico será utilizado para coordenar propriedades macroscópicas, relações globais e condições entre regiões.

A resolução tática poderá ser utilizada durante a geração para produzir detalhe físico local muito superior ao utilizado rotineiramente pelos sistemas estratégicos.

Campos físicos contínuos são preferidos como fonte da verdade.

Exemplos previstos incluem:

- altitude;
- latitude e/ou insolação;
- temperatura;
- umidade;
- disponibilidade de água;
- características do relevo.

Classificações qualitativas como biomas deverão, sempre que apropriado, ser derivadas desses campos em vez de constituírem a causa primária da geração.

Assim, uma região estrategicamente classificada como predominantemente desértica ainda poderá conter variações locais emergentes produzidas por seus parâmetros físicos.

A geração poderá utilizar o grafo estratégico para fornecer condições de contorno às regiões táticas e posteriormente condensar o detalhe produzido em propriedades estratégicas.

Conceitualmente:

WorldSeed
→ Strategic Geometry
→ Macro Physical Fields
→ Strategic Constraints / Boundary Conditions
→ Tactical Refinement
→ Physical Processes
→ Derived Classifications
→ Strategic Aggregation
→ World Ready

Essa sequência representa direção arquitetural e não congela a ordem exata dos algoritmos.

## 14.1 M3.1 frozen world-generation boundary

M3.1 freezes the architectural boundary before production world-generation algorithms are introduced.

Identity and versioning:

- `WorldSeed` is a world-generation identity value and is distinct from `SimulationSeed`;
- the initial executable representation will preserve a 64-bit unsigned seed payload, including zero;
- no implicit conversion or semantic alias between `WorldSeed` and `SimulationSeed` is allowed;
- `WorldGenerationVersion` is a positive version identifier for generator semantics;
- if the same generation request and seed would intentionally produce a materially different world because generator semantics changed, `WorldGenerationVersion` must change;
- deterministic sub-streams may be derived from `WorldSeed`, but they must use explicit domain separation and must not depend on mutable global random state, wall-clock time, process state or unordered enumeration.

Initial request boundary:

`WorldGenerationRequest`

will minimally identify:

- `WorldSeed`;
- `WorldGenerationVersion`;
- `GoldbergParameters` for the strategic topology requested.

Additional generation options may be introduced later, but once they influence output they become part of the deterministic request contract.

Topology ownership:

- M3 consumes the authoritative M2 topology implementation;
- strategic topology creation remains owned by `GoldbergStrategicTopologyGenerator`;
- M3 must not duplicate Goldberg topology algorithms;
- tactical physical materialization uses the M2 physical hierarchy contracts only where an M3 stage requires tactical detail;
- the M2 official Class I scale-6 hierarchy is a validated topology contract, not a declaration of final V1 tactical density.

Result boundary:

`WorldGenerationResult`

will be an immutable generation-domain result rather than an in-place mutation of `WorldState`.

The result may progressively contain:

- canonical strategic topology;
- macro physical fields;
- tactical physical fields when materialized;
- derived classifications;
- strategic aggregates;
- resource potential;
- habitability;
- deterministic civilization-start-site candidates.

`WorldState` integration is deferred until generated-world contracts are sufficiently stable. M3 must not use the minimal `WorldState.Revision` state as an ad hoc storage bag for partially generated layers.

Cross-scale rule:

- macro fields may be computed at strategic scale;
- tactical materialization is stage-specific and must not be assumed globally resident;
- strategic constraints or boundary conditions must be explicit before local tactical refinement;
- tactical physical detail may be aggregated back into strategic values;
- recurring strategic systems must consume strategic aggregates rather than scan the full tactical resolution.

Classification rule:

- physical scalar fields are primary where practical;
- hydrology follows elevation / relief and water availability;
- biomes are derived from physical fields rather than becoming the primary cause of those fields;
- resources are generated as world properties or potentials, not as Economy runtime inventories;
- civilization placement in M3 means deterministic start-site suitability/candidates, not live Civilization or Economy runtime state.

Module boundary:

M3 begins as a logical `WorldGeneration` module inside `GlobalArena.World`.

No separate `GlobalArena.WorldGeneration` project is created by default. Physical project separation requires concrete evidence that compile-time dependency control, ownership or test isolation benefits from it, consistent with ADR-005.

Determinism contract:

the same:

`WorldGenerationRequest + WorldGenerationVersion + WorldSeed`

must produce the same semantic generated-world result on supported platforms.

Cross-platform deterministic signatures and performance budgets remain M3 exit concerns and will be made executable before M3 closes.

A hidrologia deverá ser consequência do relevo e da disponibilidade de água.


Rios, lagos e estruturas hidrográficas não deverão ser tratados apenas como decoração aplicada posteriormente.

O relevo tático básico deverá existir antes da resolução hidrográfica local, mas o algoritmo exato, número de passagens, erosão e estratégia de otimização permanecem decisões futuras.

A geração inicial poderá gastar mais processamento do que os sistemas executados repetidamente durante a partida.

Entretanto:

**aumentar a resolução tática pode aumentar o custo de geração do mundo, mas não deve provocar crescimento proporcional no custo recorrente dos sistemas estratégicos.**

Detalhes táticos extremamente finos poderão futuramente ser materializados integralmente ou produzidos sob demanda.

Essa decisão permanece aberta.

---

## 14.2 M3.2 strategic geometry bridge and macro physical-field substrate

M3.2 begins by separating **authoritative topology** from the **data-oriented physical-field domain** that consumes it.

The M2 `StrategicTopology` remains the source of truth for:

- `StrategicCellId`;
- `StrategicEdgeId`;
- `StrategicVertexId`;
- adjacency;
- incidence;
- Goldberg family and parameters.

M3.2 does not introduce a second Goldberg generator and does not reinterpret a rendering embedding as topology.

### Strategic surface graph

The first executable bridge will be named:

`StrategicSurfaceGraph`

It is an immutable, derived view over one authoritative `StrategicTopology`.

Its purpose is to make strategic physical-field algorithms operate over compact canonical indexes without repeatedly traversing object graphs or depending on unstable collection enumeration.

Contract:

- exactly one surface node exists for every `StrategicCell`;
- node index is canonical and zero-based;
- for the current contiguous one-based cell identity contract, `index = checked((int)StrategicCellId.Value - 1)`;
- reverse lookup from canonical index to `StrategicCellId` is explicit;
- neighbor indexes are derived only from `StrategicCell.AdjacentCellIds`;
- neighbor indexes are stored in ascending canonical order;
- pentagon degree remains `5`;
- hexagon degree remains `6`;
- the surface graph does not own or mutate topology;
- the surface graph does not create tactical cells;
- the surface graph does not contain gameplay state.

This bridge is intentionally topological/data-oriented rather than a rendering embedding.

The architectural rule from section 11.2 remains binding:

**floating-point coordinates are not a source of truth for topology.**

A later presentation embedding may use spherical coordinates or mesh geometry without changing the M3.2 physical-field identity contract.

### Strategic scalar substrate

The first macro physical-field storage contract will be named:

`StrategicScalarField`

It is immutable and aligned one-to-one with a `StrategicSurfaceGraph`.

Initial authoritative scalar representation:

- signed `Int64` raw values;
- fixed denominator `1_000_000`;
- no culture-dependent parsing or formatting participates in generation;
- no `float` or `double` value is required to reproduce authoritative M3.2 scalar output;
- one raw value exists for every strategic surface node;
- lookup by `StrategicCellId` is deterministic through the canonical graph index;
- array/list order is canonical and must not depend on dictionary enumeration.

The fixed-point substrate is a deterministic storage and calculation boundary, not a declaration that every later M3 algorithm must avoid floating-point intermediates forever. Any later authoritative use of floating point requires its own deterministic contract and validation.

### M3.2 field ownership

M3.2 owns two capabilities:

1. `Strategic geometry bridge & macro physical-field substrate` — `12 GPP`;
2. `Elevation, relief & land/water foundation` — `14 GPP`.

The first capability provides the reusable strategic surface/index and scalar-field substrate.

The second capability will build deterministic elevation on that substrate.

`Elevation` randomness must come only from the existing `WorldGenerationRandomDomain.Elevation` stream.

Land/water is derived from elevation plus an explicit deterministic sea-level contract.

Land/water must not consume an independent random stream merely to classify a cell.

Relief is derived from elevation relationships between canonical neighboring strategic cells; it is not an unrelated random label.

### M3.2 scope boundary

M3.2 does not yet:

- materialize the global tactical physical mesh;
- define final render coordinates;
- implement temperature or moisture;
- implement hydrology;
- derive biomes;
- generate resources;
- create civilization runtime state;
- mutate `WorldState`;
- create Economy inventories;
- introduce networking or presentation dependencies.

Tactical physical refinement remains stage-specific and is deferred until a later M3 stage requires it.

### Determinism and iteration rules

Authoritative M3.2 field generation must use:

- canonical strategic node order;
- canonical neighbor order;
- explicit world-generation random domains;
- checked arithmetic where overflow would otherwise become silent;
- immutable output boundaries.

It must not depend on:

- wall-clock time;
- `Random.Shared`;
- `System.Random`;
- unordered dictionary/hash-set enumeration;
- UI or presentation state;
- network state;
- mutable process-global RNG state.

### M3.2 checkpoint decomposition

M3.2 is decomposed into:

- **M3.2-A — Strategic Geometry & Physical-Field Contract Freeze**;
- **M3.2-B — Executable Strategic Surface Graph & Scalar Field Substrate**;
- **M3.2-C — Deterministic Elevation, Relief & Land/Water Foundation**;
- **M3.2-D — Accumulated M3.2 Validation & Close**.

M3.2-A is a design/governance freeze and awards no GPP.

Implementation maturity is earned only by executable later checkpoints.


## 14.3 M3.3 climate inputs and cross-scale physical refinement

M3.3 extends the validated M3.2 strategic physical-field substrate without replacing its topology, fixed-point representation or deterministic world-generation boundary.

M3.3 owns two capabilities:

1. `Temperature, climate, moisture & water availability` — `14 GPP`;
2. `Cross-scale boundary conditions, tactical refinement & strategic aggregation` — `14 GPP`.

### Strategic climate-field direction

Strategic-scale climate inputs remain aligned one-to-one with `StrategicSurfaceGraph` and use the existing immutable `StrategicScalarField` fixed-point substrate unless a later ADR explicitly justifies a different authoritative representation.

The initial executable M3.3 strategic climate aggregate will be represented by:

`StrategicClimateFieldSet`

with strategic fields for:

- temperature;
- moisture;
- water availability.

Random-domain ownership is frozen as follows:

- strategic temperature consumes only `WorldGenerationRandomDomain.Temperature`;
- strategic moisture consumes only `WorldGenerationRandomDomain.Moisture`;
- water availability has no independent random domain and is derived from accepted physical inputs;
- hydrology remains outside M3.3 and must not be smuggled into water-availability generation.

M3.3 does not freeze a qualitative climate-class enum.

Physical scalar fields remain primary. Later derived classifications must not become the cause of temperature, moisture or water availability.

No latitude or insolation value may become authoritative merely because a rendering embedding can provide floating-point coordinates. If latitude/insolation becomes required by a later executable climate algorithm, it first requires an explicit deterministic physical-coordinate or insolation contract.

### Cross-scale physical identity

M3.3 must reuse the physical hierarchy already owned by M2.

The M2 contracts remain authoritative for:

- `GoldbergScaledRefinement`;
- `PhysicalTacticalTileId`;
- `PhysicalTacticalTileIncidence`;
- `PhysicalTacticalIncidenceMap`;
- supported coarse-to-fine physical hierarchy families and scales.

`TacticalRegion` and `TacticalCellId` remain the logical/reference-region contracts established by M2.2. They must not be reinterpreted as the authoritative physical high-resolution field identity.

M3.3 must not create a third tactical topology or derive cross-region physical identity from local ordinals.

### Bounded tactical field materialization

The persistent generated-world baseline remains strategic.

Tactical physical fields are materialized only where a generation stage requires local detail.

M3.3 does not require a full-planet tactical scalar array to remain resident.

A future tactical physical-field patch must:

- be bounded to an explicitly requested physical refinement scope;
- use canonical M2 physical identities;
- expose deterministic canonical ordering;
- preserve shared edge/vertex physical identity rather than duplicating boundary state;
- avoid dependence on request order, thread scheduling or global mutable caches.

If the current M2 public API can only provide a global physical incidence map for a required operation, M3.3 must not silently make that global residency a permanent generated-world contract. A bounded adapter, bounded projection or later explicit memory decision is required before persistent tactical fields are accepted.

### Boundary conditions

Strategic physical fields provide explicit boundary conditions to any tactical refinement.

Boundary input must be derived from:

- the target strategic cell;
- canonical adjacent strategic cells where required;
- already accepted strategic physical fields;
- the requested physical refinement contract.

Boundary-condition ordering must be canonical.

A tactical patch must not infer boundary values from presentation geometry or neighboring patch creation order.

### Tactical refinement randomness

Any stochastic local variation introduced only by tactical refinement belongs to:

`WorldGenerationRandomDomain.TacticalRefinement`.

Patch generation order must not alter authoritative output.

Therefore, an executable tactical-refinement random contract must be keyed or deterministically derived from stable generation identity plus stable physical scope identity before stochastic patch refinement is accepted.

A single ambient sequential stream whose result changes when patches are requested in another order is not acceptable.

### Strategic aggregation

Tactical detail may be aggregated back into strategic values.

Strategic aggregation must:

- consume canonical physical identities in canonical order;
- use deterministic arithmetic;
- make weighting explicit;
- reject silent dependence on hash/dictionary enumeration;
- return strategic-scale outputs without requiring recurring strategic systems to scan the tactical mesh.

Aggregation does not mutate M2 topology.

### M3.3 scope boundary

M3.3 does not yet:

- implement hydrology;
- derive biomes;
- generate resources;
- create Civilization runtime state;
- create Economy runtime inventories;
- mutate `WorldState`;
- introduce networking;
- introduce Unity or presentation dependencies;
- freeze final V1 tactical density;
- declare universal Goldberg physical refinement beyond M2 accepted contracts.

### M3.3 checkpoint decomposition

M3.3 is decomposed into:

- **M3.3-A — Climate & Cross-Scale Physical Contract Freeze**;
- **M3.3-B — Executable Strategic Temperature, Moisture & Water Availability**;
- **M3.3-C — Bounded Tactical Refinement & Strategic Aggregation**;
- **M3.3-D — Accumulated M3.3 Validation & Close**.

M3.3-A is a design/governance freeze and awards no GPP.

Implementation maturity is earned only by executable later checkpoints.

## 14.4 M3.4 hydrology and derived biomes

M3.4 consumes the validated M3.2/M3.3 physical-field substrate and adds strategic hydrology plus derived biome classification.

M3.4 owns two capabilities:

1. `Hydrology` — `12 GPP`;
2. `Derived biomes` — `8 GPP`.

### Strategic hydrology direction

Hydrology remains downstream of accepted physical fields.

The baseline hydrology inputs are:

- `StrategicSurfaceGraph`;
- strategic elevation;
- strategic relief where required;
- strategic land/water;
- strategic water availability.

The baseline does not consume `WorldGenerationRandomDomain.Hydrology`.

The enum domain remains reserved for a later explicitly approved stochastic residual.

### Deterministic drainage graph

For each land node, downstream flow may target only a canonical adjacent node whose elevation is strictly lower.

Selection order is:

1. lowest adjacent elevation;
2. lowest canonical node index as a tie-breaker.

Water nodes are terminal water outlets.

Land nodes with no strictly lower adjacent node are explicit inland sink/basin terminals.

Every directed drainage edge strictly decreases elevation.

Therefore the baseline drainage graph is acyclic by construction and does not require a later cycle-breaking pass.

### Flow accumulation

The initial executable hydrology aggregate will be represented by:

`StrategicHydrologyFieldSet`.

It will expose deterministic strategic drainage structure and flow accumulation aligned one-to-one with `StrategicSurfaceGraph`.

Land-node local runoff is derived from accepted strategic water availability.

Water nodes add no local runoff but may receive upstream accumulation.

Accumulation is processed in descending elevation order with canonical node-index tie-breaking.

Authoritative accumulation uses integer/fixed-point semantics, checked `Int64` storage and wider deterministic intermediate arithmetic where required.

### Rivers and basins

Strategic flow accumulation is the physical substrate for later river/stream significance.

Visual river geometry is not authoritative hydrology.

Land nodes that cannot descend are explicit basin terminals.

M3.4 does not silently convert every sink into an ocean-connected path by arbitrary carving.

Detailed depression fill/spill, erosion, sediment and tactical shoreline algorithms remain later decisions.

### Derived biome direction

Biomes remain derived classifications.

The initial executable biome output will be:

`StrategicBiomeMap`.

It remains aligned one-to-one with `StrategicSurfaceGraph`.

A biome classifier may consume only accepted generated-world fields such as:

- land/water;
- temperature;
- moisture;
- water availability;
- elevation/relief where explicitly required;
- strategic hydrology outputs where explicitly required.

Water classification has precedence over terrestrial biome classification.

Terrestrial biome rules use explicit fixed-point thresholds and deterministic precedence.

The baseline does not consume `WorldGenerationRandomDomain.Biomes`.

Exact initial terrestrial biome names and threshold values become versioned/tested generator semantics when implemented by M3.4-C.

### Strategic-first residency

M3.4 does not require globally resident tactical hydrology or tactical biome arrays.

Any future tactical hydrology/detail must use the bounded physical-field rules already established by M3.3 rather than creating a second global tactical residency assumption.

Recurring strategic systems consume strategic hydrology/biome outputs.

### M3.4 scope boundary

M3.4 does not implement:

- erosion;
- sediment transport;
- dynamic weather;
- seasons;
- ocean circulation;
- tectonics;
- globally resident tactical hydrology;
- visual river/lake presentation;
- resources;
- habitability;
- civilization placement;
- Civilization runtime state;
- Economy runtime state;
- `WorldState` mutation;
- networking;
- presentation.

### M3.4 checkpoint decomposition

M3.4 is decomposed into:

- **M3.4-A — Hydrology & Derived Biome Contract Freeze**;
- **M3.4-B — Executable Strategic Drainage & Flow Accumulation**;
- **M3.4-C — Executable Derived Strategic Biome Classification**;
- **M3.4-D — Accumulated M3.4 Validation & Close**.

M3.4-A is a design/governance freeze and awards no GPP.

Implementation maturity is earned only by executable later checkpoints.
---
## 14.5 M3.5 resources, habitability and civilization-placement inputs

M3.5 converts the validated physical world into strategic inputs that later Economy and Civilization systems may consume.

M3.5 owns:

1. `Resources` — `7 GPP`;
2. `Habitability & civilization-placement suitability` — `5 GPP`.

### Resource-potential direction

Generated resources represent strategic world potential/distribution, not Economy runtime inventory.

Resource potential remains aligned with the canonical strategic graph and may consume accepted physical, climate, hydrology and biome inputs.

The final resource catalogue, abundance curves, rarity coefficients, tactical deposit geometry and extraction rules remain open.

If stochastic resource variation is introduced, it uses only `WorldGenerationRandomDomain.Resources`.

### Habitability direction

Habitability is a derived environmental suitability signal.

It remains separate from:

- resource richness;
- civilization-start selection;
- Civilization runtime state.

The exact formula and coefficients remain calibratable.

If stochastic habitability variation is introduced, it uses only `WorldGenerationRandomDomain.Habitability`.

### Civilization-placement direction

Placement suitability consumes strategic generated-world inputs and remains a separate decision layer.

The policy must be capable of balancing:

- local suitability;
- spatial separation among selected starts;
- deterministic seeded variation where approved.

High suitability is a tendency rather than an absolute rule requiring selection of the highest-value cells.

The exact placement algorithm is intentionally not frozen by M3.5-A.

Open calibration includes:

- quality-versus-distance weights;
- graph-distance or weighted-traversal metric;
- first-reference policy;
- minimum separation;
- water eligibility/weight;
- land-water and water-land transition costs;
- coast/island treatment;
- candidate-pool size.

Historical prototype behavior may inform later calibration but is not an implementation contract.

### Calibration boundary

Weights and thresholds expected to change during visual/gameplay tuning must be centralized in a coherent versioned policy/configuration boundary rather than scattered as unrelated constants.

A material change in generated-world semantics remains governed by `WorldGenerationVersion`.

M3.5 does not require a tuning UI.

### Strategic-first residency

Resources, habitability and placement operate on strategic generated-world data.

M3.5 does not require globally resident tactical resource/habitability arrays or global tactical pathfinding for civilization placement.

### Scope boundary

M3.5 does not create:

- Civilization runtime entities;
- names/colors/visual identity;
- ownership;
- diplomacy;
- Economy runtime;
- production/extraction;
- tactical settlement simulation;
- AI strategy;
- networking;
- presentation.

### M3.5 checkpoint decomposition

M3.5 is decomposed into:

- **M3.5-A — Resources, Habitability & Placement Policy Freeze**;
- **M3.5-B — Executable Strategic Resource Potential**;
- **M3.5-C — Executable Habitability & Civilization Placement Suitability**;
- **M3.5-D — Accumulated M3.5 Validation & Close**.

M3.5-A is a design/governance freeze and awards no GPP.

Implementation maturity is earned only by executable later checkpoints.
---
## 14.6 M3.6 deterministic world-generation exit contract

M3.6 adds no new gameplay layer.

Its responsibility is to prove the accumulated generated world through:

- a canonical SHA-256 generated-world signature;
- cross-platform known regression vectors;
- explicit generation-time and managed-memory budgets;
- accumulated M3 exit validation.

### Canonical generated-world signature

The signature is an explicit binary contract over `WorldGenerationResult`.

It binds:

- request identity;
- canonical strategic topology identity;
- strategic physical fields;
- climate fields;
- hydrology;
- derived biomes;
- resource potential;
- habitability;
- civilization-placement suitability;
- active version markers that materially define generated-world semantics.

Serialization must be canonical and platform-independent:

- big-endian integers;
- explicit framing/counts;
- canonical ordering;
- no JSON/reflection serializer;
- no culture-sensitive text;
- no unordered enumeration.

The signature format has its own version independent from `WorldGenerationVersion`.

### M3 regression vectors

M3.6-B freezes known digests for:

- Class I — seed `0`, version `1`, `G(2,0)`;
- Class II — seed `42`, version `1`, `G(2,2)`;
- Class III — seed `ulong.MaxValue`, version `1`, `G(2,1)`.

All supported CI platforms must produce the same digests.

### Performance and memory

M3.6-C measures complete strategic generation through `IWorldGenerator.Generate(request)`.

Measurement protocol:

- Release only;
- invariant culture;
- 1 warmup;
- 5 measured runs;
- `Stopwatch` elapsed time;
- `GC.GetAllocatedBytesForCurrentThread`;
- GC generation counts;
- median and maximum time/allocation;
- blocking Class I/II/III workloads;
- optional larger non-blocking stress workload.

Numeric budgets are calibrated from a separate ReadOnly observational run before acceptance thresholds are frozen.

Hosted CI timing is not a blocking performance metric. CI remains authoritative for correctness and digest equivalence.

### M3.6 decomposition

- **M3.6-A — World Generation Exit, Signature & Performance Contract Freeze**;
- **M3.6-B — Canonical Generated-World Signature & Regression Vectors**;
- **M3.6-C — World Generation Performance & Memory Acceptance**;
- **M3.6-D — Accumulated Cross-Platform M3 Exit Validation & Close**.

Planned maturity:

- A: no GPP;
- B: factor `0.50`;
- C: factor `0.70`;
- D: factor `0.85`.

M3.6 closes at `3.40 / 4 GPP — 85.0%`; `0.60 GPP` remains reserved for V1 Definition of Done.

No M3.6 checkpoint creates Economy or Civilization runtime state.
---
# 15. Seeds e geração reproduzível


Todo planeta procedural deverá possuir uma WorldSeed.

Também deverão ser versionados:

WorldGenerationVersion
RulesetVersion

Objetivo:

uma seed deve continuar semanticamente rastreável mesmo após evolução do gerador.

M3.1 freezes the following distinction:

- `WorldSeed` owns procedural world-generation randomness;
- `SimulationSeed` owns deterministic simulation-resolution randomness;
- equal numeric payloads do not make the two seed domains semantically equivalent;
- `WorldGenerationVersion` versions generator semantics independently from future `RulesetVersion`;
- generation algorithms must not read real time or ambient process randomness.

The initial executable M3.1 contract will use `WorldGenerationVersion = 1` as the first valid generator version and reject non-positive/default version values.

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

Cada `StrategicCell` poderá expor propriedades econômicas agregadas derivadas de sua região tática.

A economia global deverá consumir prioritariamente esses agregados em vez de percorrer rotineiramente milhões ou bilhões de microtiles.

Alterações táticas relevantes poderão atualizar os agregados estratégicos correspondentes através de contratos ainda a definir.

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

## 25.1 State Hash Contract

`WorldStateHash` representa um fingerprint determinístico versionado para diagnóstico e comparação de estado.

O contrato congela:

- digest com 32 bytes, representado por 64 caracteres hexadecimais;
- `FormatVersion` maior que zero;
- digest normalizado para hexadecimal em maiúsculas;
- igualdade baseada em `FormatVersion` e digest;
- rejeição de digest nulo, com tamanho inválido ou com caracteres não hexadecimais.

`IWorldStateHasher` define:

`WorldStateHash Compute(WorldState worldState)`

### Responsabilidade

O hash é um mecanismo de diagnóstico.

Ele não:

- substitui o `WorldState`;
- passa a ser fonte de verdade;
- altera a semântica da simulação;
- define persistência física;
- define formato de save;
- define protocolo de rede.

### Versionamento

`FormatVersion` identifica a definição canônica usada para produzir o fingerprint.

Mudanças futuras na composição ou codificação do estado podem exigir nova versão, evitando comparar fingerprints produzidos por definições incompatíveis.

### Limites deste checkpoint

Este checkpoint congela somente o contrato.

A implementação concreta para o `WorldState` mínimo atual é descrita pela seção 25.2.

A validação cross-platform continua pendente.

## 25.2 Canonical WorldState Hash Path

`CanonicalWorldStateHasher` implementa `IWorldStateHasher` para o `WorldState` mínimo atual.

A definição canônica usa:

- `FormatVersion = 1`;
- algoritmo `SHA-256`;
- payload binário fixo de 16 bytes;
- assinatura ASCII `GAWS` nos primeiros 4 bytes;
- `FormatVersion` codificado como `UInt32` big-endian;
- `WorldState.Revision` codificado como `UInt64` big-endian.

O payload é:

`GAWS | UInt32BE(FormatVersion) | UInt64BE(WorldState.Revision)`

Nenhuma serialização textual, cultura corrente, reflection, ordem de propriedades ou representação específica de runtime participa do cálculo.

### Vetores canônicos

Para `Revision = 0`:

`47415753000000010000000000000000`

produz:

`E52764CDAC5F546D1BD7AF34E0B03141E27EAAC1E25C40580350E2C9A72FDC9C`

Para `Revision = 1`:

`47415753000000010000000000000001`

produz:

`5F99DEE3022BD8F617BA44730B089FE08405E711F8578145938FF732C56E1C11`

Esses vetores passam a ser referência objetiva para comparação entre runtimes e plataformas.

### Escopo

A implementação cobre somente o estado observável existente hoje:

- `WorldState.Revision`.

Quando `WorldState` ganhar novos campos autoritativos, a definição canônica deverá ser revisada explicitamente e, se incompatível, deverá avançar o `FormatVersion`.

O hash continua sendo diagnóstico, não estado autoritativo.

### Próximo passo

A validação cross-platform do state hash é descrita pela seção 25.3.

## 25.3 Cross-Platform State Hash Validation

A definição canônica de `WorldStateHash` foi executada em GitHub Actions sobre três famílias de sistema operacional usando o mesmo commit e a mesma suíte focada de testes.

Execução de referência:

- workflow: `Cross-Platform State Hash Validation`;
- run ID: `35443826986`;
- commit: `6e218d83f80075a4e6e981e2d84e52eb72b7642c`;
- .NET SDK: `10.0.401`.

Ambientes validados:

- Ubuntu `24.04.5 LTS`, image `ubuntu-24.04`, arquitetura x64;
- Microsoft Windows Server `2025`, build `10.0.26100`, image `windows-2025-vs2026`, arquitetura x64;
- macOS `26.6.2`, image `macos-26-arm64`, arquitetura arm64.

Em cada ambiente:

- checkout concluído;
- .NET 10 configurado;
- solução restaurada;
- build Release concluído;
- `CanonicalWorldStateHasherTests` executado;
- 5 testes aprovados;
- 0 falhas;
- artefato de evidência publicado.

Como os testes focados incluem os vetores canônicos de `Revision = 0` e `Revision = 1`, a mesma definição binária e os mesmos digests foram validados nas três plataformas.

Isso estabelece evidência de portabilidade do caminho de state hash entre Windows, Linux e macOS e também entre x64 e arm64 dentro do conjunto exercitado.

### Invariante arquitetural

O núcleo de domínio e simulação deverá permanecer cross-platform por design.

Dependências específicas de sistema operacional devem permanecer nas bordas de infraestrutura, apresentação ou integração e não devem alterar as regras autoritativas da simulação.

A validação cross-platform deverá continuar como mecanismo de regressão à medida que o estado autoritativo evoluir.

### Limite da evidência

Este checkpoint valida especificamente o caminho canônico de state hash.

A validação cross-platform da suíte completa do Simulation Kernel é registrada pela seção 25.4.

## 25.4 Cross-Platform Kernel Regression Exit Gate

O fechamento técnico do M1 utiliza uma regressão cross-platform da suíte completa do `GlobalArena.Tests` sobre o mesmo commit.

Execução de referência:

- workflow: `Cross-Platform Kernel Regression Validation`;
- run ID: `35444833011`;
- commit: `ae8f91f0fe9317895ea316eb05b27d4f566dacb6`;
- .NET SDK: `10.0.401`.

Ambientes validados:

- Ubuntu `24.04` / x64;
- Microsoft Windows Server `2025` / x64;
- macOS `26` / arm64.

Resultado em cada ambiente:

- restore aprovado;
- build Release aprovado;
- 0 warnings;
- 0 erros;
- suíte completa: 126/126 testes aprovados;
- 0 falhas;
- 0 testes ignorados;
- artefato TRX publicado.

Total de execuções de teste do gate:

**378 aprovadas / 0 falhas**

A suíte inclui provas com resultados de referência fixos para o caminho determinístico, incluindo:

- resolução end-to-end com `Revision = 3` e ordem de Events `3, 1, 2` para seed zero;
- ordem de referência do `SeededSimulationEventOrderer` `3, 4, 2, 5, 1`;
- vetores canônicos de `WorldStateHash`;
- replay;
- EventLog;
- Turn Policies;
- contratos e invariantes do pipeline.

Consequentemente, o gate do M1:

**mesmo estado + mesmas ordens + mesma seed = mesmo resultado**

fica atendido para o Simulation Kernel mínimo definido neste milestone, de forma headless e com evidência automatizada em Windows, Linux e macOS.

### Invariante de continuidade

O fechamento do M1 não transforma determinismo ou portabilidade em assunto encerrado.

Novos campos autoritativos, rulesets, algoritmos, estruturas de coleção, concorrência ou dependências externas deverão preservar esse gate e ampliar a regressão quando necessário.

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


## 42.1 M4 systemic vertical-slice integration contract

M4 begins the runtime gameplay layer over the validated M0-M3 foundation.

Authoritative runtime mutation remains:

`Command -> Event -> WorldState`

`WorldGenerationResult` remains immutable procedural source data and is not reused as mutable gameplay state.

`TurnResolver` remains an orchestration boundary. Domain rules for Civilizations, Economy and Warfare remain owned by their respective logical modules.

The minimum systemic dependency direction is:

`World Generation`
-> runtime civilization instantiation
-> ownership/control
-> Economy and Warfare

and, for cross-system effects:

`Warfare`
-> territorial/strategic-edge effect
-> Economy route dependency invalidation
-> affected economic recomputation.

Warfare does not mutate Economy internals directly.

Economy does not mutate Warfare internals directly.

Routine economy and movement remain strategic-first. M4 does not introduce routine global tactical pathfinding.

The accepted M4 entry contract and GPP ownership decomposition are recorded in ADR-029.


## 42.2 Authoritative runtime-state module

M4.1-B establishes `GlobalArena.Runtime` as the aggregation boundary for mutable authoritative gameplay state.

Dependency direction:

`GlobalArena.Kernel`
and
`GlobalArena.World`
-> `GlobalArena.Runtime`
-> `GlobalArena.Simulation`

`GlobalArena.World` does not depend on Runtime.

`WorldState` is owned by `GlobalArena.Runtime`, not by generated-world code.

The initial runtime aggregate contains:

- canonical civilization roster;
- canonical strategic stocks;
- canonical military-unit identities, owners and strategic locations.

The aggregate remains immutable-by-replacement.

The canonical WorldState hash is format version `2` and covers the authoritative M4 runtime snapshots using explicit big-endian encoding and canonical ordering.

Turn-resolution orchestration remains unchanged.

The accepted decision is recorded in ADR-030.


## 42.3 Runtime world binding and cross-domain invariants

M4.1-C binds authoritative runtime state to generated-world identity without copying generated topology.

`RuntimeWorldBinding` stores:

- generated-world signature format version;
- canonical generated-world SHA-256 digest;
- strategic cell count.

The binding derives from `WorldGenerationCanonicalSignature`.

Authoritative `WorldState` invariants now require:

- Economy stock owners to exist in the Civilization roster;
- Warfare unit owners to exist in the Civilization roster;
- referenced owners to remain present while Economy/Warfare still reference them;
- bound military-unit strategic locations to belong to the generated world.

`WorldState` may remain unbound for generic kernel/simulation tests, but an authoritative game state may bind only once. Rebinding to another generated world is rejected.

The canonical `WorldState` hash is format version `3` and includes generated-world binding identity.

The accepted decision is recorded in ADR-031.


## 42.4 M4.2 runtime civilizations, territory and baseline diplomacy contract

M4.2 turns the existing civilization identity roster into runtime presence on the generated world.

The accepted direction is strategic-first:

`WorldGenerationResult`
-> deterministic start selection
-> runtime civilization records
-> sparse strategic territorial control
-> sparse symmetric baseline diplomacy.

Runtime civilization count remains an explicit materialization input. The final product-level formula for recommending or choosing that count remains open.

The current baseline requires civilization starts on strategic land.

Civilization identities are assigned canonically as `1..N` in deterministic selected-start order.

Initial territorial ownership is sparse:

- each civilization owns its unique start cell;
- all other strategic cells begin unowned;
- one cell has at most one owner.

Baseline diplomacy uses `Enemy`, `Neutral`, and `Ally`.

Neutral is implicit/default. Only non-neutral relations need persistence, and pair identity is canonical/symmetric.

Civilization, territory and diplomacy state remain authoritative Runtime state and must participate in canonical WorldState hashing when implemented.

The accepted decision is recorded in ADR-032.


## 42.5 Deterministic runtime civilization materialization

M4.2-B makes runtime civilizations executable without introducing territory or diplomacy early.

`RuntimeCivilizationMaterializer` receives a generated world plus an explicit positive civilization count.

The accepted M3.5 placement selector remains authoritative.

Runtime materialization preserves selector order and adds only the M4.2 land-start eligibility rule:

- deterministic selector prefixes are requested;
- water candidates are filtered while preserving selector order;
- the prefix expands deterministically until enough land candidates exist or the candidate domain is exhausted;
- the first `N` eligible land candidates become runtime starts.

Materialized civilizations receive canonical identities `1..N`.

`CivilizationRuntimeRecord` carries civilization identity and optional strategic start cell.

Identity-only records remain supported for generic simulation states.

Materialized start cells must be unique.

Bound `WorldState` rejects a materialized civilization start outside the bound generated world.

`StartCellId` is civilization origin only. Strategic territorial ownership remains owned by M4.2-C.

The canonical `WorldState` hash is format version `4` and covers civilization start-cell presence and identity.

The accepted decision is recorded in ADR-033.


## 42.6 Strategic territory control

M4.2-C introduces explicit authoritative strategic territorial ownership.

Territory is represented by `StrategicTerritoryRuntimeState` as a sparse set of `StrategicTerritoryControlEntry` values.

Each entry maps one `StrategicCellId` to one controlling `CivilizationId`.

Absence means unowned.

Entries are canonicalized by strategic-cell identity, and duplicate controlled cells are rejected.

`RuntimeStrategicTerritoryMaterializer` creates initial territory from materialized civilization starts:

- every civilization must have a `StartCellId`;
- each civilization controls its own start cell;
- no additional cells are assigned by the baseline materializer.

`WorldState` owns territory as immutable-by-replacement authoritative state.

Cross-domain validation requires territorial controllers to exist in the civilization roster and, for bound states, controlled cells to belong to the generated world.

A civilization identity cannot be removed while territory still references it.

The canonical `WorldState` hash is format version `5` and covers strategic-cell/controller pairs in canonical order.

M4.2-C does not introduce conquest, control-transfer behavior, tactical ownership, diplomacy effects, Economy effects or Warfare mechanics.

The accepted decision is recorded in ADR-034.


## 42.7 Baseline diplomacy state

M4.2-D introduces executable authoritative baseline diplomacy over runtime civilization identities.

`BaselineDiplomacyRelationKind` defines `Neutral`, `Enemy`, and `Ally`.

`BaselineDiplomacyRuntimeState` is sparse:

- Neutral is implicit and is not persisted;
- Enemy and Ally are persisted as pair overrides;
- civilization pairs are canonicalized by ascending identity;
- pair lookup is symmetric;
- duplicate canonical pairs are rejected;
- self-relations are never persisted.

`WithRelation` returns an immutable replacement snapshot. Setting Neutral removes the persisted override.

`WorldState` owns diplomacy as authoritative Runtime state and validates that every persisted participant exists in the civilization roster.

A civilization identity cannot be removed while diplomacy references it.

The canonical `WorldState` hash is format version `6` and covers canonical diplomacy pairs after territory and before Economy.

M4.2-D does not introduce treaties, scoring, reputation, diplomacy AI, alliance obligations or automatic Economy/Warfare effects.

The accepted decision is recorded in ADR-035.


## 42.8 M4.2 ownership-link integration and stage close

M4.2-E closes the runtime civilization stage without adding production code.

The accumulated integration scenario binds:

`Generated World`
-> deterministic runtime civilizations
-> initial strategic territory
-> baseline diplomacy
-> Economy ownership references
-> Warfare ownership/location references
-> canonical `WorldState` hash.

`WorldState` remains the common invariant boundary.

All Territory, Diplomacy, Economy and Warfare civilization references must resolve to the authoritative runtime civilization roster.

No subsystem receives a second ownership authority and no direct Territory/Diplomacy/Economy/Warfare mutation coupling is introduced.

M4.2-E keeps canonical WorldState hash format `6` because no new authoritative payload field is added.

The stage closes at `Integrada ao sistema — factor 0.70`, with broader validation maturity reserved for later M4 accumulated evidence.

Production, consumption, trade, routing, military orders, movement, combat and conquest remain outside M4.2.

The accepted decision is recorded in ADR-036.


## 42.9 M4.3 economic points, workforce, production and demand contract

M4.3 begins the executable Economy layer by refining the temporary inventory-shaped runtime substrate established in M4.1.

The V1 Economy direction is flow-based rather than physical-inventory-first.

Production for one economic cycle is an abstract supply flow. Demand is an abstract consumption flow. Market allocation and financial settlement convert those flows into economic return during the cycle.

Goods are not required to persist as physical stored inventory between cycles.

The persistent Economy model must instead be able to represent economically meaningful places and the capabilities that generate or process flow.

### Economic points

The fundamental spatial abstraction is an `EconomicPoint`.

M4.3 does not require cities as first-class entities.

An economic point represents a location where population/workforce and economic activity may exist.

It requires at minimum:

- stable runtime identity;
- owning civilization identity;
- location with a strategic routing anchor;
- localized workforce/population quantity or future-compatible workforce reference;
- economic activity configuration.

The final physical realization remains open.

An economic point may later correspond to:

- a strategic cell;
- a tactical site projected to a strategic parent;
- a settlement-like location;
- another compact representation justified by later architecture/UI evidence.

### Three economic activities

The baseline conceptual activities are:

1. Agriculture;
2. Mining;
3. Trade / Logistics.

Agriculture uses diffuse productive potential.

Mining depends on concentrated resource deposits.

Trade / Logistics consumes workforce and infrastructure to increase transfer capacity and reduce the marginal cost of moving large flows.

A point may host one or more activities.

A location may therefore become economically important through trade/logistics even when it has little or no direct agricultural/mineral production.

### Workforce and endogenous concentration

Workforce is localized.

The exact population-growth model is not frozen by M4.3.

The architecture must allow future population growth to be decoupled from immediate economic growth and to place new workforce only among already populated/economically inhabited locations through deterministic simulation rules.

The player may influence economic geography indirectly by creating available productive/logistics opportunities rather than selecting every population placement.

Agriculture and mining must support diminishing marginal productivity from additional workforce as a future rule.

The exact function is not frozen.

This preserves the strategic trade-off between:

- concentrating workforce and keeping transport/internal-market costs low;
- expanding to new productive points to avoid diminishing returns.

### Production and demand are flows

M4.3 does not model production as durable stored goods by default.

For each economic cycle:

`productive capability + workforce + local conditions`
-> supply flow

and:

`population / economic activity`
-> demand flow.

The market system later allocates supply to reachable demand and produces financial return.

Unsold or unconsumed flow is not automatically carried into the next cycle as physical inventory.

A future deeper physical-inventory model remains possible, but it is not required by the V1 M4 contract.

### Production-potential derivation remains replaceable

Economy must not depend permanently on one world-classification method.

Agricultural potential may derive from:

- explicit biome;
- continuous physical/resource fields;
- a hybrid.

Mining potential is based on concentrated deposits/resources.

Economy should consume a stable economic-potential/profile boundary so World Generation may evolve without rewriting Economy.

The exact commodity catalogue is not frozen.

Historical examples such as six grains and six minerals are non-binding design references.

### Trade/logistics capacity and emergent hubs

`Hub` is not frozen as a separate mandatory entity.

A point becomes hub-like by specializing in Trade / Logistics and acquiring infrastructure and workforce.

Even a point with zero dedicated trade workforce or specialized infrastructure retains a small baseline transfer ability. Small flows may therefore move without a formal port/customs/market structure.

As volume rises, specialized infrastructure and trade/logistics workforce become economically necessary.

The future transfer-cost model must support increasing marginal cost as flow approaches or exceeds practical local transfer capacity.

The exact formula is not frozen.

Infrastructure alone does not imply unlimited throughput, and workforce alone does not create modern logistics capacity. Their interaction may later be complementary.

This allows port-like or customs-like centers to emerge endogenously from economic incentives rather than from a mandatory city/hub classification.

### M4.4 forward boundary

M4.4 owns global markets and inter-point flow allocation.

It must be able to support:

- multiple producers and consumers of the same commodity;
- transport-sensitive destination attractiveness;
- local/internal destinations with zero or low transfer cost;
- one shared strategic routing graph rather than permanent graph copies per producer;
- route-edge dependency tracking;
- incremental invalidation when strategic access changes;
- capacity-sensitive transfer costs;
- deterministic simultaneous allocation.

Water-filling is the leading allocation hypothesis.

Literal packet simulation such as fixed `0.001` increments is not frozen. A deterministic analytical, batched or event-driven equivalent is preferred if it preserves the intended marginal-allocation behavior more efficiently.

An inverse-square scarcity curve such as `price = 1 / (supply / demand)^2` remains a calibratable pricing hypothesis, not a frozen law.

Market allocation ultimately settles into financial return. Physical commodity stock carry-over is not required.

### Operational decomposition

- M4.3-A — Economic Points, Workforce, Production & Demand Contract Freeze;
- M4.3-B — Economic Point & Localized Workforce/Activity Substrate;
- M4.3-C — Deterministic Production & Demand Flow Resolution;
- M4.3-D — Accumulated Validation & M4.3 Close.

M4.4 is refined as:

**M4.4 — Markets, Trade, Routes & Flow Allocation**

The accepted decision is recorded in ADR-037.


## 42.10 M4.3-B economic point runtime substrate and hash v7

M4.3-B replaces the temporary inventory-shaped Economy payload with the first executable localized economic substrate.

Authoritative resident Economy state now consists of canonical EconomicPoints.

Each point carries stable identity, civilization owner, strategic routing anchor, localized workforce and sparse canonical allocation across Agriculture, Mining and Trade / Logistics.

`StrategicStockEntry` is no longer active V1 authoritative state.

`CommodityId` remains available for production/demand flows and later market allocation.

`WorldState` preserves the existing Economy aggregate boundary while validating point ownership against the civilization roster and point anchors against `RuntimeWorldBinding` when bound.

`CanonicalWorldStateHasher` format advances from `6` to `7` because the authoritative Economy payload semantics changed.

M4.3-B deliberately does not implement production, demand, population growth, diminishing returns, infrastructure, routes, markets, water filling, pricing or financial settlement.

The existing `6.00 GPP` Economy credit is now confirmed as functional-isolated maturity of the refined `Commodity identity, economic points/locality & flow substrate` capability. M4.3-B adds `0.00 GPP`.

The accepted implementation decision is recorded in ADR-038.

## 42.11 M4.3-C deterministic production and demand flow resolution

M4.3-C adds a pure transient resolver over the localized Economy substrate.

Production opportunities are explicit deterministic fixed-point inputs keyed by EconomicPoint, CommodityId and Agriculture/Mining activity.

Supply uses the workforce allocated to the selected producing activity.

Demand profiles are explicit deterministic fixed-point inputs keyed by EconomicPoint and CommodityId, and demand uses total localized workforce so productive, logistics and unallocated workers can all consume.

Resolved supply and demand are transient per-cycle flow values. They are not persisted into WorldState and do not change canonical WorldState hash format `7`.

The implementation uses UInt128 intermediate multiplication, UInt64 raw fixed-point quantities and fail-fast overflow behavior. No floating-point simulation arithmetic is introduced.

M4.3-C does not freeze the final commodity catalogue, agriculture/worldgen mapping, commodity-specific deposits, diminishing returns, population growth, routes, market allocation, water filling, pricing or financial settlement.

The `Workforce, production & demand` capability advances to functional-isolated factor `0.50`, earning `9.00 GPP`.

The accepted implementation decision is recorded in ADR-039.

# 43. Status

Versão atual:

0.1

Status:

Baseline inicial.

Este documento deverá ser refinado conforme os spikes técnicos produzirem evidência real.

# Global Arena — Progress Ledger

**Documento:** histórico de evolução
**Política:** append-only

---

# 1. Objetivo

Este documento registra a trajetória do Global Arena.

O objetivo é impedir perda de referência histórica.

O estado atual pode mudar.

O histórico não deve ser reescrito.

Entradas anteriores só devem ser corrigidas em caso de erro factual claramente identificado.

---

# 2. Métricas registradas

Cada checkpoint poderá registrar:

- data;
- milestone;
- estágio;
- GPP conquistados;
- progresso global;
- Scope Confidence;
- Risk Level;
- Critical Path;
- entregas concluídas;
- mudanças de escopo;
- riscos descobertos;
- próxima meta.

---

# 3. Formato

## YYYY-MM-DD — Checkpoint

Milestone:

M?

Progress:

XX.X%

GPP:

XXX / 1000

Scope Confidence:

XX%

Risk:

LOW / MODERATE / HIGH / CRITICAL

Critical Path:

...

### Concluído

- ...

### Decisões relevantes

- ...

### Novos riscos

- ...

### Scope Change

Nenhum.

### Próximo checkpoint

...

---

# 4. Histórico

## 2026-09-14 — Fundação documental iniciada

Milestone:

**M0 — Project Baseline**

Progress:

**Não consolidado**

GPP:

**Não consolidado / 1000**

Scope Confidence:

**Baixa / moderada**

Risk:

**HIGH**

Motivo do risco:

o projeto possui alta ambição sistêmica e diversos subsistemas ainda não validados por protótipos ou benchmarks.

Critical Path:

**definição e validação da fundação arquitetural**

### Concluído

- nome do projeto definido: Global Arena;
- plataforma inicial definida: PC;
- Visual Studio Community configurado;
- C# e .NET 10 LTS selecionados;
- Unity definida como camada de apresentação prevista;
- PROJECT_COMPASS criado;
- V1_DEFINITION_OF_DONE criado;
- ARCHITECTURE criado;
- ROADMAP criado;
- modelo de acompanhamento por 1000 GPP definido;
- separação estratégica/tática conceitualmente definida;
- Goldberg definido como base da topologia planetária;
- conceito de tiles compartilhados nas arestas definido;
- resolução de ordens simultâneas por eventos sequenciais definida;
- determinismo identificado como requisito;
- multiplayer identificado como requisito arquitetural;
- economia identificada como bounded context crítico;
- simulação independente da engine gráfica definida como princípio.

### Decisões relevantes

- simulação será headless;
- arquitetura inicial será monólito modular;
- Unity não será fonte da verdade;
- servidor será autoridade no multiplayer;
- diferentes políticas de duração do turno deverão compartilhar o mesmo resolver;
- o V1 será medido em uma escala fixa de 1000 GPP;
- descoberta de tarefas não aumentará automaticamente o escopo.

### Novos riscos

Serão formalizados no RISK_REGISTER.

### Scope Change

Nenhum.

O baseline V1 ainda está em construção.

### Próximo checkpoint

Criar Risk Register e ADRs fundamentais.

## 2026-09-14 — M0.5 Solution Foundation concluído

Milestone:

**M0 — Project Baseline**

Stage:

**M0.5 — Solution Foundation**

Progress:

**Ainda não consolidado oficialmente**

GPP:

**Ainda não consolidado / 1000**

Scope Confidence:

**Moderada**

Risk:

**HIGH**

Critical Path:

**conclusão do Project Baseline e início do Simulation Kernel**

### Concluído

- repositório Git local inicializado;
- branch principal definida como `main`;
- solução criada em formato `GlobalArena.slnx`;
- .NET 10 LTS adotado;
- seis projetos estruturais criados:
  - GlobalArena.Kernel;
  - GlobalArena.World;
  - GlobalArena.Simulation;
  - GlobalArena.Console;
  - GlobalArena.Tests;
  - GlobalArena.Benchmarks;
- dependências direcionais iniciais estabelecidas;
- Kernel mantido na base da árvore de dependências;
- arquivos descartáveis dos templates removidos;
- build completo da solução validado;
- infraestrutura xUnit validada;
- `.gitignore` validado para artefatos de build;
- `.editorconfig` criado;
- `Directory.Build.props` criado;
- nullable habilitado globalmente;
- compilação determinística habilitada;
- warnings configurados como erros;
- documentação arquitetural e ADRs fundamentais criados.

### Evidências

- solution build: aprovado;
- projetos compilados: 6/6;
- testes automatizados: 1 aprovado, 0 falhas;
- `bin/`, `obj/` e `.vs/` ignorados pelo Git.

### Decisões relevantes

- projetos físicos serão adicionados progressivamente;
- não serão criados todos os bounded contexts antecipadamente;
- políticas compartilhadas de compilação serão aplicadas através de `Directory.Build.props`;
- configuração de estilo será centralizada em `.editorconfig`.

### Novos riscos

Nenhum risco estrutural novo identificado neste checkpoint.

### Scope Change

Nenhum.

### Próximo checkpoint

**M0.6 — Repository Baseline and Source Control**

Objetivos:

- inspecionar conteúdo do primeiro stage;
- estabelecer repositório GitHub canônico;
- criar primeiro commit;
- sincronizar branch `main`;
- registrar baseline remoto.

---

## 2026-09-14 — M0.6 Repository Baseline and Source Control concluído

Milestone:

**M0 — Project Baseline**

Stage:

**M0.6 — Repository Baseline and Source Control**

Progress:

**Ainda não consolidado oficialmente**

GPP:

**Ainda não consolidado / 1000**

Scope Confidence:

**Moderada**

Risk:

**HIGH**

Critical Path:

**estabelecer o mínimo Simulation Kernel e concluir o baseline M0**

### Concluído

- primeiro stage Git auditado;
- 24 arquivos incluídos no baseline inaugural;
- verificação de whitespace aprovada;
- build completo da solução aprovado;
- testes automatizados aprovados;
- commit inaugural criado;
- repositório GitHub canônico criado;
- repositório configurado como privado;
- remote `origin` configurado;
- branch `main` publicada e configurada para rastrear `origin/main`;
- integridade entre commit local e remoto confirmada;
- política de finais de linha padronizada com `.gitattributes`;
- arquivos de texto normalizados para LF no repositório.

### Evidências

- repositório canônico: `ahtohiofilho/GlobalArena`;
- branch: `main`;
- commit baseline: `67f0aead77ccc63fd21b23508bc9a6f5792b8f71`;
- mensagem: `chore: establish Global Arena project baseline`;
- projetos compilados: 6/6;
- testes: 1 aprovado, 0 falhas;
- working tree limpa após o push;
- `git diff --cached --check`: aprovado;
- política Git de texto: `* text=auto eol=lf`.

### Scope Change

Nenhum.

### Novos riscos

Nenhum risco estrutural novo identificado neste checkpoint.

### Próximo checkpoint

**M0.7 — Minimum Kernel Baseline and Progress Calibration**

Objetivos:

- substituir o código de template por um Kernel mínimo real;
- estabelecer os primeiros contratos fundamentais da simulação;
- adicionar testes reais de domínio;
- validar determinismo mínimo;
- decompor o restante do M0;
- calcular o primeiro baseline oficial de GPP e percentual global.

---

## 2026-09-14 — M0.7.1 Deterministic Simulation Primitives concluído

Milestone:

**M0 — Project Baseline**

Stage:

**M0.7 — Minimum Kernel Baseline and Progress Calibration**

Subcheckpoint:

**M0.7.1 — Deterministic Simulation Primitives**

Progress:

**Ainda não consolidado oficialmente**

GPP:

**Ainda não consolidado / 1000**

Scope Confidence:

**Moderada**

Risk:

**HIGH**

Critical Path:

**estabelecimento do Simulation Kernel mínimo e fechamento do M0**

### Concluído

- primeiro tipo de domínio real criado no Kernel;
- `SimulationSeed` criado como value type imutável;
- gerador pseudoaleatório determinístico criado;
- algoritmo SplitMix64 implementado explicitamente;
- dependência de `System.Random` evitada para o núcleo determinístico;
- teste de igualdade de sequência para seeds idênticas criado;
- vetor de referência fixo criado para detectar quebra futura de determinismo;
- teste de template removido;
- arquivo de testes renomeado para refletir sua responsabilidade real;
- build completo da solução aprovado;
- testes automatizados aprovados.

### Evidências

- projetos compilados: 6/6;
- testes: 2 aprovados, 0 falhas;
- `SameSeedProducesSameSequence`: aprovado;
- `SeedZeroProducesStableReferenceSequence`: aprovado.

### Decisões relevantes

- aleatoriedade da simulação será controlada por seed explícita;
- mudanças futuras no algoritmo determinístico deverão ser tratadas como alteração de compatibilidade;
- testes de referência protegerão replay e reprodutibilidade.

### Scope Change

Nenhum.

### Novos riscos

Nenhum risco estrutural novo identificado neste subcheckpoint.

### Próximo subcheckpoint

**M0.7.2 — Turn Identity Primitive**

Objetivos:

- criar representação fortemente tipada para número de turno;
- impedir estados inválidos básicos;
- adicionar testes de domínio correspondentes.

---

## 2026-09-14 — M0.7.2 Turn Identity Primitive concluído

Milestone:

**M0 — Project Baseline**

Stage:

**M0.7 — Minimum Kernel Baseline and Progress Calibration**

Subcheckpoint:

**M0.7.2 — Turn Identity Primitive**

Progress:

**Ainda não consolidado oficialmente**

GPP:

**Ainda não consolidado / 1000**

Scope Confidence:

**Moderada**

Risk:

**HIGH**

Critical Path:

**estabelecimento do Simulation Kernel mínimo e fechamento do M0**

### Concluído

- `TurnNumber` criado como tipo fortemente tipado para identidade de turno;
- turno zero definido como estado inválido;
- avanço de turno encapsulado em `Next()`;
- proteção contra overflow adicionada;
- testes de criação, validação, avanço e limite máximo implementados;
- build completo da solução aprovado;
- testes automatizados aprovados.

### Evidências

- projetos compilados: 6/6;
- testes totais: 6;
- testes aprovados: 6;
- falhas: 0;
- `PositiveValueCreatesTurnNumber`: aprovado;
- `ZeroIsRejected`: aprovado;
- `NextAdvancesExactlyOneTurn`: aprovado;
- `MaximumValueCannotAdvance`: aprovado.

### Decisões relevantes

- números de turno não serão representados por inteiros genéricos nas interfaces fundamentais;
- invariantes básicos de turno permanecerão encapsulados no próprio tipo;
- estados numericamente possíveis, mas inválidos no domínio, serão rejeitados na criação.

### Scope Change

Nenhum.

### Novos riscos

Nenhum risco estrutural novo identificado neste subcheckpoint.

### Próximo subcheckpoint

**M0.7.3 — Simulation Command Identity**

Objetivos:

- estabelecer identidade determinística para comandos;
- distinguir explicitamente comando de evento;
- preparar a fundação para ordenação e resolução determinística de turnos;
- adicionar testes de domínio correspondentes.

---

## 2026-09-14 — M0.7.3 Simulation Command Identity concluído

Milestone:

**M0 — Project Baseline**

Stage:

**M0.7 — Minimum Kernel Baseline and Progress Calibration**

Subcheckpoint:

**M0.7.3 — Simulation Command Identity**

Progress:

**Ainda não consolidado oficialmente**

GPP:

**Ainda não consolidado / 1000**

Scope Confidence:

**Moderada**

Risk:

**HIGH**

Critical Path:

**estabelecimento do Simulation Kernel mínimo e fechamento do M0**

### Concluído

- `CommandId` criado como identidade fortemente tipada de comandos;
- identidade composta por turno e sequência;
- sequência zero definida como inválida;
- igualdade determinística de identificadores validada;
- contrato `ISimulationCommand` criado;
- contrato `ISimulationEvent` criado;
- distinção entre intenção e ocorrência da simulação materializada no código;
- build completo da solução aprovado;
- testes automatizados aprovados.

### Evidências

- projetos compilados: 6/6;
- testes totais: 10;
- testes aprovados: 10;
- falhas: 0;
- `ValidTurnAndSequenceCreateCommandId`: aprovado;
- `ZeroSequenceIsRejected`: aprovado;
- `SameTurnAndSequenceProduceEqualIds`: aprovado;
- `DifferentSequencesProduceDifferentIds`: aprovado.

### Decisões relevantes

- `Command` representa intenção submetida à simulação;
- `Event` representa ocorrência produzida ou resolvida pela simulação;
- Command e Event permanecerão conceitos distintos;
- `CommandId.Sequence` será uma sequência autoritativa global dentro do turno;
- a sequência será atribuída pela autoridade da simulação, evitando colisões entre jogadores;
- eventual identidade da requisição original do cliente será um conceito separado.

### Scope Change

Nenhum.

### Novos riscos

Nenhum risco estrutural novo identificado neste subcheckpoint.

### Próximo subcheckpoint

**M0.7.4 — Simulation Event Identity**

Objetivos:

- estabelecer identidade determinística para eventos;
- permitir rastrear a origem de um evento;
- preparar a futura sequência determinística de resolução;
- adicionar testes de domínio correspondentes.

---

## 2026-09-18 — M0.7.4 Simulation Event Identity concluído

Milestone:

**M0 — Project Baseline**

Stage:

**M0.7 — Minimum Kernel Baseline and Progress Calibration**

Subcheckpoint:

**M0.7.4 — Simulation Event Identity**

Progress:

**Ainda não consolidado oficialmente**

GPP:

**Ainda não consolidado / 1000**

Scope Confidence:

**Moderada**

Risk:

**HIGH**

Critical Path:

**estabelecimento do Simulation Kernel mínimo e fechamento do M0**

### Concluído

- `EventId` criado como identidade fortemente tipada de eventos;
- identidade de evento vinculada explicitamente ao `CommandId` de origem;
- sequência zero definida como inválida;
- múltiplos eventos do mesmo comando podem ser distinguidos deterministicamente;
- eventos de comandos diferentes permanecem distintos mesmo com a mesma sequência;
- `ISimulationEvent` passou a exigir `EventId`;
- lineage entre Command e Event materializado no contrato do Kernel;
- identidade do evento mantida separada da futura ordem de execução;
- build completo da solução aprovado;
- testes automatizados aprovados.

### Evidências

- projetos compilados: 6/6;
- testes totais: 15;
- testes aprovados: 15;
- falhas: 0;
- `ValidOriginCommandAndSequenceCreateEventId`: aprovado;
- `ZeroSequenceIsRejected`: aprovado;
- `SameOriginAndSequenceProduceEqualIds`: aprovado;
- `DifferentSequencesProduceDifferentIds`: aprovado;
- `DifferentOriginsProduceDifferentIds`: aprovado;
- `git diff --check`: aprovado.

### Decisões relevantes

- `EventId` será composto por `OriginCommandId` e sequência;
- a sequência do evento diferencia eventos produzidos pelo mesmo comando;
- identidade e ordem de execução permanecerão conceitos distintos;
- shuffle ou posição na fila de resolução não serão codificados dentro do `EventId`;
- a origem do evento permanecerá rastreável para replay, diagnóstico e auditoria determinística.

### Scope Change

Nenhum.

### Novos riscos

Nenhum risco estrutural novo identificado neste subcheckpoint.

### Próximo subcheckpoint

**M0.7.5 — Minimum Simulation Context**

Objetivos:

- estabelecer o contexto mínimo necessário para uma execução determinística;
- definir explicitamente quais dados acompanham uma resolução de simulação;
- preservar separação entre contexto, estado do mundo e comandos;
- adicionar testes de domínio correspondentes.

---

## 2026-09-18 — M0.7.5 Minimum Simulation Context concluído

Milestone:

**M0 — Project Baseline**

Stage:

**M0.7 — Minimum Kernel Baseline and Progress Calibration**

Subcheckpoint:

**M0.7.5 — Minimum Simulation Context**

Progress:

**Ainda não consolidado oficialmente**

GPP:

**Ainda não consolidado / 1000**

Scope Confidence:

**Moderada**

Risk:

**HIGH**

Critical Path:

**estabelecimento do Simulation Kernel mínimo e fechamento do M0**

### Concluído

- `SimulationContext` criado como value type imutável;
- contexto mínimo composto por `TurnNumber` e `SimulationSeed`;
- contexto de execução mantido separado de `WorldState`;
- contexto de execução mantido separado de Commands;
- estado mutável do PRNG não foi incorporado ao contexto;
- igualdade determinística por valor estabelecida;
- build completo da solução aprovado;
- testes automatizados aprovados.

### Evidências

- projetos compilados: 6/6;
- testes totais: 19;
- testes aprovados: 19;
- falhas: 0;
- `TurnAndSeedCreateSimulationContext`: aprovado;
- `SameTurnAndSeedProduceEqualContexts`: aprovado;
- `DifferentTurnsProduceDifferentContexts`: aprovado;
- `DifferentSeedsProduceDifferentContexts`: aprovado;
- `git diff --check`: aprovado.

### Decisões relevantes

- `SimulationContext` mínimo será composto por `TurnNumber` e `SimulationSeed`;
- `WorldState` não fará parte do contexto;
- Commands não farão parte do contexto;
- o estado mutável do PRNG não fará parte do contexto;
- o PRNG poderá ser derivado da seed pela camada de resolução;
- campos adicionais só serão adicionados quando houver contrato arquitetural explícito.

### Scope Change

Nenhum.

### Novos riscos

Nenhum risco estrutural novo identificado neste subcheckpoint.

### Próximo subcheckpoint

**M0.7.6 — Minimum World State**

Objetivos:

- estabelecer uma representação mínima e explícita do estado autoritativo do mundo;
- preservar separação entre estado persistente e contexto de execução;
- definir o mínimo necessário para permitir futura resolução de turno;
- adicionar testes de domínio correspondentes.


---

## 2026-09-18 — M0.7.6 Minimum World State concluído

Milestone:

**M0 — Project Baseline**

Stage:

**M0.7 — Minimum Kernel Baseline and Progress Calibration**

Subcheckpoint:

**M0.7.6 — Minimum World State**

Progress:

**Ainda não consolidado oficialmente**

GPP:

**Ainda não consolidado / 1000**

Scope Confidence:

**Moderada**

Risk:

**HIGH**

Critical Path:

**estabelecimento do Simulation Kernel mínimo e fechamento do M0**

### Concluído

- `WorldState` criado como raiz explícita do estado autoritativo do mundo;
- criação inicial encapsulada por `WorldState.CreateInitial()`;
- construtor direto mantido privado;
- cada criação inicial produz uma instância independente;
- `WorldState` mantido separado de `SimulationContext`;
- nenhum estado fictício de planeta, território, civilização, economia ou guerra foi introduzido prematuramente;
- igualdade estrutural global não foi imposta ao estado do mundo;
- build completo da solução aprovado;
- testes automatizados aprovados.

### Evidências

- projetos compilados: 6/6;
- testes totais: 21;
- testes aprovados: 21;
- falhas: 0;
- `CreateInitialReturnsWorldState`: aprovado;
- `SeparateInitialStatesAreSeparateInstances`: aprovado;
- `git diff --check`: aprovado.

### Decisões relevantes

- `WorldState` será a raiz do estado autoritativo do mundo;
- o baseline mínimo poderá existir antes da definição das capabilities concretas do mundo;
- `SimulationContext` não fará parte do `WorldState`;
- seed, PRNG, Commands e EventLog permanecerão fora do estado mundial;
- não será utilizado singleton para representar estado inicial;
- igualdade estrutural de todo o mundo não será assumida como contrato padrão;
- novos dados só entrarão no `WorldState` quando houver ownership e necessidade concreta.

### Scope Change

Nenhum.

### Novos riscos

Nenhum risco estrutural novo identificado neste subcheckpoint.

### Próximo subcheckpoint

**M0.7.7 — Minimum Turn Resolution Contract**

Objetivos:

- estabelecer o contrato mínimo de entrada e saída para resolução de turno;
- conectar conceitualmente `WorldState`, Commands e `SimulationContext`;
- preservar separação entre estado, contexto, orquestração e resultado;
- preparar a futura implementação de event queue, deterministic shuffle e resolução sequencial;
- adicionar testes de domínio correspondentes.

---

## 2026-09-18 — M0.7.7 Minimum Turn Resolution Contract concluído

Milestone:

**M0 — Project Baseline**

Stage:

**M0.7 — Minimum Kernel Baseline and Progress Calibration**

Subcheckpoint:

**M0.7.7 — Minimum Turn Resolution Contract**

Progress:

**Ainda não consolidado oficialmente**

GPP:

**Ainda não consolidado / 1000**

Scope Confidence:

**Moderada**

Risk:

**HIGH**

Critical Path:

**estabelecimento do Simulation Kernel mínimo e fechamento do M0**

### Concluído

- `TurnResolutionInput` criado como contrato explícito de entrada da resolução;
- entrada composta por `WorldState`, Commands e `SimulationContext`;
- coleção de Commands capturada no momento da criação;
- alterações posteriores na coleção original não modificam a entrada já criada;
- `TurnResolutionResult` criado como contrato explícito de saída da resolução;
- resultado composto por `ResultingWorldState` e Events;
- coleção de Events capturada no momento da criação;
- alterações posteriores na coleção original não modificam o resultado já criado;
- estado, contexto, comandos e resultado permaneceram conceitos separados;
- nenhum comportamento de resolução foi introduzido prematuramente;
- build completo da solução aprovado;
- testes automatizados aprovados.

### Evidências

- projetos compilados: 6/6;
- testes totais: 25;
- testes aprovados: 25;
- falhas: 0;
- `InputCarriesWorldStateCommandsAndContext`: aprovado;
- `InputSnapshotsCommandCollection`: aprovado;
- `ResultCarriesWorldStateAndEvents`: aprovado;
- `ResultSnapshotsEventCollection`: aprovado;
- `git diff --check`: aprovado.

### Decisões relevantes

- o contrato de resolução pertence ao módulo `GlobalArena.Simulation`;
- `TurnResolutionInput` conectará `WorldState`, Commands e `SimulationContext` sem transferir ownership entre eles;
- as coleções de Commands e Events terão membership e ordem estabilizadas na criação do contrato;
- os objetos individuais não serão clonados pelo contrato;
- `TurnResolutionResult.Events` ainda não constitui o contrato definitivo de `EventLog`;
- validação, geração de eventos, shuffle, execução e consolidação permanecerão responsabilidades futuras do `TurnResolver`;
- nenhuma regra de resolução será incorporada aos tipos de entrada e saída.

### Scope Change

Nenhum.

### Novos riscos

Nenhum risco estrutural novo identificado neste subcheckpoint.

### Próximo subcheckpoint

**M0.7.8 — Minimum Turn Resolver**

Objetivos:

- materializar o primeiro componente responsável por resolver um turno;
- consumir `TurnResolutionInput` e produzir `TurnResolutionResult`;
- preservar determinismo e separação de responsabilidades;
- evitar introduzir shuffle, EventLog ou regras de domínio antes da existência de comportamento concreto;
- preparar a expansão incremental da pipeline de resolução;
- adicionar testes correspondentes.

---

## 2026-09-18 — M0.7.8 Minimum Turn Resolver concluído

Milestone:

**M0 — Project Baseline**

Stage:

**M0.7 — Minimum Kernel Baseline and Progress Calibration**

Subcheckpoint:

**M0.7.8 — Minimum Turn Resolver**

Progress:

**Ainda não consolidado oficialmente**

GPP:

**Ainda não consolidado / 1000**

Scope Confidence:

**Moderada**

Risk:

**HIGH**

Critical Path:

**estabelecimento do Simulation Kernel mínimo e fechamento do M0**

### Concluído

- `TurnResolver` criado como primeiro componente executável de resolução de turno;
- resolver passou a consumir `TurnResolutionInput`;
- resolver passou a produzir `TurnResolutionResult`;
- turno vazio preserva o mesmo `WorldState`;
- turno vazio produz zero Events;
- ausência de Commands não consome aleatoriedade nem altera estado;
- Commands não suportados são rejeitados explicitamente;
- Commands não são ignorados silenciosamente;
- nenhuma infraestrutura prematura de handlers, dispatcher, shuffle ou EventLog foi criada;
- input nulo é rejeitado explicitamente;
- build completo da solução aprovado;
- testes automatizados aprovados.

### Evidências

- projetos compilados: 6/6;
- testes totais: 29;
- testes aprovados: 29;
- falhas: 0;
- `EmptyTurnPreservesWorldState`: aprovado;
- `EmptyTurnProducesNoEvents`: aprovado;
- `CommandsAreRejectedUntilCommandResolutionExists`: aprovado;
- `NullInputIsRejected`: aprovado;
- `git diff --check`: aprovado.

### Decisões relevantes

- o primeiro comportamento do `TurnResolver` será a resolução determinística de um turno vazio;
- ausência de Commands implica ausência de alteração de estado e ausência de Events;
- o mesmo `WorldState` poderá ser retornado enquanto nenhuma mudança tiver ocorrido;
- Commands permanecerão explicitamente não suportados até existir infraestrutura concreta para processá-los;
- o resolver não deverá descartar Commands silenciosamente;
- `ITurnResolver` não será criado enquanto não existir necessidade real de múltiplas implementações;
- handlers, dispatcher, event queue, shuffle e EventLog continuarão fora deste baseline mínimo.

### Scope Change

Nenhum.

### Novos riscos

Nenhum risco estrutural novo identificado neste subcheckpoint.

### Próximo subcheckpoint

**M0.7.9 — Baseline Calibration and M0 Closure**

Objetivos:

- verificar integralmente a Definition of Done do M0;
- decompor e calibrar o primeiro baseline oficial de GPP;
- calcular o primeiro percentual oficial do V1;
- revisar Scope Confidence, Technical Risk e Critical Path;
- verificar se documentação, arquitetura, riscos e ADRs estão coerentes com o estado implementado;
- congelar o baseline inicial do V1;
- fechar formalmente o M0;
- preparar a transição para M1 — Deterministic Simulation Kernel.

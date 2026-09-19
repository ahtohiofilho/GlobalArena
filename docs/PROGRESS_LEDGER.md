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

---

## 2026-09-18 — M0.7.9 Baseline Calibration and M0 Closure concluído

Milestone encerrado:

**M0 — Project Baseline**

Stage encerrado:

**M0.7 — Minimum Kernel Baseline and Progress Calibration**

Subcheckpoint:

**M0.7.9 — Baseline Calibration and M0 Closure**

### Resultado do milestone

**M0 concluído**

Definition of Done do M0:

**11 / 11 critérios atendidos**

Critérios verificados:

1. `PROJECT_COMPASS.md` criado e atualizado;
2. `V1_DEFINITION_OF_DONE.md` criado e baseline 0.1 congelado;
3. `ARCHITECTURE.md` criado e coerente com o Kernel implementado;
4. `ROADMAP.md` criado e primeiro baseline de GPP calibrado;
5. `PROGRESS_LEDGER.md` criado e histórico preservado;
6. `RISK_REGISTER.md` criado e revisado formalmente;
7. ADRs fundamentais registrados;
8. estrutura inicial da solução criada;
9. infraestrutura de testes criada;
10. Kernel mínimo compilando;
11. primeiro baseline oficial de progresso calculado.

### ADRs auditados

Permanecem válidos e `Accepted`:

- ADR-001 — Headless Deterministic Simulation;
- ADR-002 — Strategic / Tactical World Hierarchy;
- ADR-003 — Simultaneous Orders with Sequential Resolution;
- ADR-004 — Authoritative Multiplayer;
- ADR-005 — Modular Monolith First.

Nenhum ADR novo foi necessário para o fechamento do M0.

### Baseline V1

O orçamento global permanece congelado em:

**1000 GPP = 100% do V1**

Foundation / Simulation Kernel permanece com:

**70 GPP = 7% do V1**

A decomposição inicial dos 70 GPP foi formalizada no Roadmap.

### Calibração de Foundation / Simulation Kernel

| Capability | Budget | Maturidade no fechamento do M0 | Fator | GPP ganhos |
|---|---:|---|---:|---:|
| Fundação modular, solução e infraestrutura de testes | 10 | Validada | 0.85 | 8.50 |
| Tempo lógico, seed e PRNG determinístico | 8 | Validada | 0.85 | 6.80 |
| Identidade e contratos de Command/Event | 8 | Validada | 0.85 | 6.80 |
| SimulationContext | 5 | Validada | 0.85 | 4.25 |
| WorldState mínimo | 5 | Validada | 0.85 | 4.25 |
| Contratos de entrada/saída da resolução | 5 | Validada | 0.85 | 4.25 |
| Orquestração mínima do TurnResolver | 4 | Validada | 0.85 | 3.40 |
| Command → Event, validação e execução sequencial | 8 | Especificada | 0.20 | 1.60 |
| Deterministic shuffle / ordering | 5 | Especificada | 0.20 | 1.00 |
| EventLog / base de replay | 5 | Especificada | 0.20 | 1.00 |
| Turn policies | 3 | Especificada | 0.20 | 0.60 |
| Determinismo end-to-end / simulação headless automatizada | 4 | Especificada | 0.20 | 0.80 |
| **TOTAL** | **70** |  |  | **43.25** |

### Primeiro progresso oficial

GPP conquistados:

**43.25 / 1000**

Global Progress exato:

**4.325%**

Global Progress exibido:

**4.3%**

Foundation / Simulation Kernel:

**43.25 / 70 GPP = 61.8%**

Os outros 930 GPP permanecem em 0 GPP conquistado neste baseline.

Descrições conceituais de domínios futuros não foram contabilizadas como progresso sem contratos ou capabilities suficientemente definidos.

### Scope Confidence

**50%**

Interpretação:

a arquitetura e os principais sistemas do V1 estão identificados, mas ainda existem grandes áreas conceituais, riscos técnicos e detalhes de implementação por resolver.

Scope Confidence não representa percentual de implementação.

### Technical Risk

**HIGH**

Riscos individuais ativos:

- 8 CRITICAL;
- 4 HIGH.

O Risk Level global permanece HIGH porque ainda não existe ameaça imediata demonstrada à viabilidade do projeto, apesar da existência de riscos individuais de impacto crítico.

Risco mais diretamente associado ao próximo Critical Path:

**RISK-004 — Determinism failure**

### Evidência técnica do fechamento

Estado técnico imediatamente anterior à calibração:

- projetos compilados: 6/6;
- testes automatizados: 29;
- testes aprovados: 29;
- falhas: 0;
- `git diff --check`: aprovado;
- `WorldState` mínimo: implementado;
- `SimulationContext`: implementado;
- `CommandId`: implementado;
- `EventId`: implementado;
- `ISimulationCommand`: implementado;
- `ISimulationEvent`: implementado;
- PRNG determinístico: implementado;
- contratos de resolução: implementados;
- `TurnResolver` mínimo: implementado;
- resolução de turno vazio: validada.

O Kernel ainda não prova determinismo end-to-end para resolução não vazia.

Essa responsabilidade passa para M1.

### Scope Change

Nenhum.

O total de 1000 GPP não foi alterado.

A decomposição dos 70 GPP de Foundation / Simulation Kernel constitui calibração interna do orçamento existente, não expansão de escopo.

### Revisão de governança

Foram revisados no fechamento do M0:

- `PROJECT_COMPASS.md`;
- `ROADMAP.md`;
- `V1_DEFINITION_OF_DONE.md`;
- `RISK_REGISTER.md`;
- `PROGRESS_LEDGER.md`;
- `ARCHITECTURE.md`;
- ADR-001;
- ADR-002;
- ADR-003;
- ADR-004;
- ADR-005.

`ARCHITECTURE.md` e os ADRs não exigiram alteração adicional durante a calibração final.

### Baseline congelado

Baseline V1 inicial:

**0.1**

Data:

**2026-09-18**

O histórico deste baseline não deverá ser apagado.

Mudanças materiais futuras deverão utilizar os mecanismos de Scope Change e atualização de governança definidos pelo projeto.

### Próximo milestone

**M1 — Deterministic Simulation Kernel**

Stage inicial:

**M1.1 — Non-empty Deterministic Turn Resolution**

Primeiro subcheckpoint:

**M1.1.1 — Command Processing Contract**

Critical Path:

**primeira resolução não vazia reproduzível end-to-end**

Objetivos imediatos:

- estabelecer o contrato mínimo de processamento de Commands;
- permitir que um Command válido produza ocorrência(s) determinísticas;
- preservar lineage entre `CommandId` e `EventId`;
- manter ordering explícito e reproduzível;
- preparar a futura fila de Events;
- avançar incrementalmente até provar o gate do M1:

**mesmo estado + mesmas ordens + mesma seed = mesmo resultado.**

---

## 2026-09-18 — M1.1.1 Command Processing Contract concluído

Milestone:

**M1 — Deterministic Simulation Kernel**

Stage:

**M1.1 — Non-empty Deterministic Turn Resolution**

Subcheckpoint:

**M1.1.1 — Command Processing Contract**

### Progresso oficial

GPP conquistados:

**43.25 / 1000**

Global Progress:

**4.3%**

Foundation / Simulation Kernel:

**43.25 / 70 GPP — 61.8%**

Nenhum GPP adicional foi conquistado neste subcheckpoint.

A capability `Command → Event, validação e execução sequencial` permanece classificada como:

**Especificada — fator 0.20**

O contrato tornou a fronteira mais concreta, mas ainda não existe resolução funcional não vazia suficiente para elevar a capability para `Funcional isoladamente`.

### Concluído

- `ISimulationCommandProcessor` criado no módulo `GlobalArena.Simulation`;
- contrato explícito entre `WorldState`, Command, `SimulationContext` e Events estabelecido;
- processor pode produzir zero ou mais `ISimulationEvent`;
- lineage entre `CommandId` e `EventId.OriginCommandId` validado em teste;
- Command continua representando intenção;
- Event continua representando ocorrência;
- nenhuma lógica de dispatch foi introduzida;
- nenhum registry de processors foi criado;
- nenhum handler hierarchy foi criado;
- `TurnResolver` ainda não foi alterado;
- nenhuma regra concreta de gameplay foi introduzida.

### Evidências

- projetos compilados: 6/6;
- testes totais: 30;
- testes aprovados: 30;
- falhas: 0;
- `ProcessorCanReceiveWorldCommandAndContextAndProduceEvents`: aprovado;
- `git diff --check`: aprovado.

### Incidente ambiental durante QA

Uma primeira execução dos testes apresentou 11 falhas devido ao Windows Smart App Control bloquear o carregamento de `GlobalArena.World.dll`.

Evidência observada:

- `SmartAppControlState: On`;
- eventos Code Integrity 3077 e 3033;
- bloqueio por política de assinatura;
- testes previamente aprovados também falharam pelo mesmo motivo.

Após desativação do Smart App Control para o ambiente de desenvolvimento, limpeza dos artefatos e recompilação:

- build: 6/6;
- testes: 30/30;
- falhas: 0.

O incidente foi classificado como falha ambiental externa ao código do Global Arena.

Nenhuma alteração de código foi necessária para corrigi-lo.

### Decisões relevantes

- o contrato de processamento de Commands pertence ao módulo `GlobalArena.Simulation`;
- `ISimulationCommand` permanece mínimo e não recebe comportamento de processamento;
- o processor recebe explicitamente `WorldState` e `SimulationContext`;
- geração de Events permanece separada da futura execução desses Events;
- dispatch e descoberta de processors serão introduzidos somente quando houver necessidade concreta;
- nenhuma abstração adicional será criada apenas para antecipar tipos futuros de Commands.

### Scope Change

Nenhum.

### Novos riscos

Nenhum risco estrutural novo identificado.

O incidente do Smart App Control é tratado como configuração do ambiente local de desenvolvimento e não altera o Risk Register do produto neste momento.

### Próximo subcheckpoint

**M1.1.2 — Single-Command Resolution Path**

Objetivos:

- integrar o contrato de processamento ao primeiro caminho de resolução não vazia;
- permitir que exatamente um Command seja processado de forma explícita;
- produzir Events preservando lineage;
- manter o `WorldState` inalterado enquanto execução de Events ainda não existir;
- evitar introduzir ordering de múltiplos Commands prematuramente;
- preparar o primeiro fluxo executável Command → Event através do `TurnResolver`.

---

## 2026-09-18 — M1.1.2 Single-Command Resolution Path concluído

Milestone:

**M1 — Deterministic Simulation Kernel**

Stage:

**M1.1 — Non-empty Deterministic Turn Resolution**

Subcheckpoint:

**M1.1.2 — Single-Command Resolution Path**

### Progresso oficial

GPP conquistados:

**43.25 / 1000**

Global Progress:

**4.3%**

Foundation / Simulation Kernel:

**43.25 / 70 GPP — 61.8%**

Nenhum GPP adicional foi conquistado neste subcheckpoint.

A capability `Command → Event, validação e execução sequencial` permanece classificada como:

**Especificada — fator 0.20**

O caminho `Command → Event` passou a existir através do `TurnResolver`, mas a capability completa ainda não possui validação real de Commands, execução de Events sobre o `WorldState` nem resolução sequencial suficiente para qualificá-la como `Funcional isoladamente`.

### Concluído

- `TurnResolver` passou a depender explicitamente de `ISimulationCommandProcessor`;
- turno com zero Commands continua preservando o `WorldState` e produzindo zero Events;
- exatamente um Command pode atravessar o `TurnResolver`;
- o processor recebe o estado, o Command e o `SimulationContext`;
- Events produzidos pelo processor são retornados pelo `TurnResolutionResult`;
- lineage `CommandId → EventId.OriginCommandId` permanece preservado;
- o `WorldState` permanece inalterado porque Event execution ainda não existe;
- dois ou mais Commands continuam explicitamente não suportados;
- processor nulo é rejeitado;
- nenhuma infraestrutura de dispatch foi introduzida;
- nenhum ordering de múltiplos Commands foi definido;
- nenhum deterministic shuffle foi introduzido;
- nenhuma regra concreta de gameplay foi adicionada.

### Evidências

- projetos compilados: 6/6;
- testes totais: 32;
- testes aprovados: 32;
- falhas: 0;
- `SingleCommandIsProcessedAndProducesEvents`: aprovado;
- `MultipleCommandsAreRejected`: aprovado;
- `NullCommandProcessorIsRejected`: aprovado;
- regressões anteriores do `TurnResolver`: aprovadas;
- `git diff --check`: sem erros de whitespace.

Foi emitido apenas o aviso esperado de normalização CRLF → LF no working copy de `TurnResolverTests.cs`, consistente com a política de line endings do repositório.

### Decisões relevantes

- o `TurnResolver` passa a orquestrar o processor diretamente;
- não será criado dispatcher antes de existir mais de um tipo concreto de processamento que realmente exija dispatch;
- o limite de um Command é explícito para evitar semântica implícita de ordering;
- geração de Events permanece separada de Event execution;
- o `ResultingWorldState` continua sendo o estado de entrada enquanto execução de Events não existir;
- nenhuma abstração adicional foi criada antecipadamente.

### Scope Change

Nenhum.

### Novos riscos

Nenhum risco estrutural novo identificado.

`RISK-004 — Determinism failure` permanece relevante.

O checkpoint reduz incerteza sobre o caminho `Command → Event`, mas ainda não prova determinismo end-to-end nem execução reproduzível sobre o estado.

### Próximo subcheckpoint

**M1.1.3 — Single-Event Execution Contract**

Objetivos:

- estabelecer a fronteira mínima responsável por aplicar um Event ao `WorldState`;
- manter geração de Events separada de sua execução;
- permitir a futura revalidação do Event no momento da execução;
- evitar event queue, shuffle e múltiplos Events antes da existência do contrato mínimo;
- preparar o primeiro caminho `Command → Event → state transition`.

---

## 2026-09-19 — Refinamento arquitetural da hierarquia mundial e geração multiescala

Foi realizada revisão conceitual da futura geometria planetária e da geração procedural.

Nenhuma implementação foi iniciada e nenhum GPP adicional foi conquistado.

### Decisões consolidadas

- haverá um único grafo estratégico global;
- cada `StrategicCell` corresponderá a uma região ou tabuleiro tático local;
- regiões táticas vizinhas permanecerão conectadas através da topologia estratégica;
- sistemas globais deverão operar prioritariamente sobre propriedades estratégicas agregadas;
- a resolução tática poderá ser muito superior sem se tornar unidade obrigatória dos cálculos econômicos e estratégicos;
- geração do mundo poderá utilizar detalhe tático e posteriormente condensá-lo em propriedades estratégicas;
- campos físicos contínuos serão preferidos como fonte da verdade;
- biomas serão prioritariamente classificações derivadas;
- hidrologia deverá emergir do relevo e da disponibilidade de água, e não ser apenas decoração.

### Hipótese a validar

A relação hierárquica estratégico → tático será inicialmente concebida para Goldberg `G(m,n)` de forma geral.

M2 deverá determinar se essa hipótese é válida para todas as famílias relevantes.

Caso existam limitações, o conjunto de famílias suportadas poderá ser reduzido sem alterar a arquitetura fundamental.

### Scope Change

Nenhum.

As decisões refinam capacidades já previstas em Planet Topology e Procedural World.

### Riscos

`RISK-003 — Goldberg hierarchy mapping` foi refinado para registrar explicitamente a hipótese de universalidade do refinamento `G(m,n)`.

Nenhum novo risco foi criado.

### Estado atual

O desenvolvimento permanece em:

**M1.1.3 — Single-Event Execution Contract**

Official Progress permanece:

**43.25 / 1000 GPP — 4.3%**

---

## 2026-09-19 — M1.1.3 Single-Event Execution Contract concluído

Milestone:

**M1 — Deterministic Simulation Kernel**

Stage:

**M1.1 — Non-empty Deterministic Turn Resolution**

Subcheckpoint:

**M1.1.3 — Single-Event Execution Contract**

### Progresso oficial

GPP conquistados:

**43.25 / 1000**

Global Progress:

**4.3%**

Foundation / Simulation Kernel:

**43.25 / 70 GPP — 61.8%**

Nenhum GPP adicional foi conquistado neste subcheckpoint.

A capability `Command → Event, validação e execução sequencial` permanece classificada como:

**Especificada — fator 0.20**

O contrato de execução de Event foi estabelecido, mas ainda não está integrado ao fluxo do `TurnResolver` e não existe execução sequencial ou revalidação suficiente para elevar a maturidade da capability.

### Concluído

- `ISimulationEventExecutor` criado em `GlobalArena.Simulation`;
- contrato explícito `WorldState + Event + SimulationContext → WorldState`;
- geração de Events permanece separada de sua execução;
- o contrato permite produzir um estado resultante distinto do estado recebido;
- nenhuma regra concreta de gameplay foi introduzida;
- nenhum dispatch de Events foi criado;
- nenhuma event queue foi criada;
- nenhuma política de mutabilidade do `WorldState` foi congelada.

### Evidências

- projetos compilados: 6/6;
- testes totais: 33;
- testes aprovados: 33;
- falhas: 0;
- `ExecutorCanReceiveWorldEventAndContextAndProduceWorldState`: aprovado;
- SHA-256 dos dois arquivos novos verificado;
- `git diff --check`: aprovado.

### Scope Change

Nenhum.

### Novos riscos

Nenhum risco estrutural novo identificado.

`RISK-004 — Determinism failure` permanece relevante.

### Próximo subcheckpoint

**M1.1.4 — Single-Event Execution Path**

Objetivos:

- integrar `ISimulationEventExecutor` ao `TurnResolver`;
- permitir execução de exatamente um Event;
- produzir um `ResultingWorldState` derivado da execução;
- manter zero Events como caminho explícito;
- rejeitar múltiplos Events enquanto execução sequencial ainda não existir;
- completar o primeiro caminho `Command → Event → state transition`.

---

## 2026-09-19 — M1.1.4 Single-Event Execution Path concluído

Milestone:

**M1 — Deterministic Simulation Kernel**

Stage:

**M1.1 — Non-empty Deterministic Turn Resolution**

Subcheckpoint:

**M1.1.4 — Single-Event Execution Path**

### Progresso oficial

GPP conquistados:

**43.25 / 1000**

Global Progress:

**4.3%**

Foundation / Simulation Kernel:

**43.25 / 70 GPP — 61.8%**

Nenhum GPP adicional foi conquistado neste subcheckpoint.

A capability `Command → Event, validação e execução sequencial` permanece classificada como:

**Especificada — fator 0.20**

O fluxo `Command → Event → ResultingWorldState` agora existe através do `TurnResolver`, mas a capability completa ainda não possui revalidação real de Events, validação concreta de Commands nem execução sequencial de múltiplos Events.

### Concluído

- `TurnResolver` passou a depender explicitamente de `ISimulationEventExecutor`;
- zero Commands preservam o `WorldState` e não invocam o executor;
- um Command que produz zero Events preserva o `WorldState` e não invoca o executor;
- um Command que produz exatamente um Event executa esse Event;
- o `WorldState` retornado pelo executor torna-se o `ResultingWorldState`;
- o Event produzido permanece registrado no `TurnResolutionResult`;
- dois ou mais Events são rejeitados antes de qualquer execução;
- dois ou mais Commands continuam explicitamente não suportados;
- executor nulo é rejeitado;
- nenhuma event queue foi introduzida;
- nenhum ordering ou deterministic shuffle foi introduzido;
- nenhuma regra concreta de gameplay foi adicionada.

### Evidências

- projetos compilados: 6/6;
- warnings de build: 0;
- erros de build: 0;
- testes totais: 36;
- testes aprovados: 36;
- falhas: 0;
- `SingleCommandSingleEventIsExecutedAndProducesResultingState`: aprovado;
- `SingleCommandWithNoEventsPreservesWorldState`: aprovado;
- `MultipleEventsAreRejected`: aprovado;
- `NullEventExecutorIsRejected`: aprovado;
- regressões anteriores do `TurnResolver`: aprovadas;
- `git diff --check`: aprovado;
- SHA-256 dos dois arquivos alterados verificado no pacote de evidências.

### Decisões relevantes

- o executor é uma dependência explícita do `TurnResolver`;
- zero Events possuem semântica explícita de ausência de transição de estado;
- múltiplos Events são rejeitados até existir semântica de execução sequencial;
- nenhum Event é executado parcialmente quando a quantidade produzida é maior que a suportada;
- geração e execução de Events continuam separadas;
- revalidação de Event permanece uma responsabilidade distinta a ser estabelecida antes da execução sequencial.

### Scope Change

Nenhum.

### Novos riscos

Nenhum risco estrutural novo identificado.

`RISK-004 — Determinism failure` permanece relevante.

O checkpoint reduz a incerteza do caminho não vazio, mas ainda não demonstra uma transição concreta de domínio reproduzível de ponta a ponta.

### Próximo subcheckpoint

**M1.1.5 — Event Revalidation Contract**

Objetivos:

- estabelecer a fronteira mínima de revalidação de um Event contra o `WorldState` atual;
- executar a revalidação imediatamente antes da futura aplicação do Event;
- manter validação separada da mutação do estado;
- evitar ainda execução sequencial de múltiplos Events;
- preparar o caminho para revalidação + execução determinística.

---

## 2026-09-19 — M1.1.5 Event Revalidation Contract concluído

Milestone:

**M1 — Deterministic Simulation Kernel**

Stage:

**M1.1 — Non-empty Deterministic Turn Resolution**

Subcheckpoint:

**M1.1.5 — Event Revalidation Contract**

### Progresso oficial

GPP conquistados:

**43.25 / 1000**

Global Progress:

**4.3%**

Foundation / Simulation Kernel:

**43.25 / 70 GPP — 61.8%**

Nenhum GPP adicional foi conquistado neste subcheckpoint.

A capability `Command → Event, validação e execução sequencial` permanece classificada como:

**Especificada — fator 0.20**

A fronteira de revalidação de Event passou a existir, mas ainda não está integrada ao `TurnResolver` e não existe execução sequencial de múltiplos Events.

### Concluído

- `ISimulationEventRevalidator` criado em `GlobalArena.Simulation`;
- contrato explícito `WorldState + Event + SimulationContext → bool CanExecute`;
- o revalidator recebe o estado autoritativo atual;
- o revalidator recebe o Event candidato à execução;
- o revalidator recebe o mesmo `SimulationContext` da resolução;
- o contrato permite autorizar ou rejeitar a execução;
- a revalidação permanece separada da alteração de estado;
- nenhuma alteração foi feita no `TurnResolver`;
- nenhuma event queue foi introduzida;
- nenhum ordering ou deterministic shuffle foi introduzido;
- nenhuma regra concreta de gameplay foi adicionada.

### Evidências

- projetos compilados: 6/6;
- warnings de build: 0;
- erros de build: 0;
- testes totais: 38;
- testes aprovados: 38;
- falhas: 0;
- `RevalidatorCanReceiveWorldEventAndContextAndAllowExecution`: aprovado;
- `RevalidatorCanRejectExecution`: aprovado;
- `git diff --check`: aprovado;
- SHA-256 dos dois arquivos novos verificado no pacote de evidências.

### Decisões relevantes

- a revalidação pertence ao módulo `GlobalArena.Simulation`;
- a decisão mínima de revalidação é representada por `bool CanExecute`;
- o contrato não modifica o `WorldState`;
- revalidação e execução permanecem responsabilidades separadas;
- motivo estruturado de rejeição não será introduzido antes de existir necessidade concreta;
- a semântica do resultado de turno para Event rejeitado será definida durante a integração, sem antecipar EventLog.

### Scope Change

Nenhum.

### Novos riscos

Nenhum risco estrutural novo identificado.

`RISK-004 — Determinism failure` permanece relevante.

O checkpoint reduz a incerteza sobre a fronteira de revalidação, mas ainda não prova que a decisão ocorre imediatamente antes da execução no fluxo real.

### Próximo subcheckpoint

**M1.1.6 — Single-Event Revalidation Path**

Objetivos:

- integrar `ISimulationEventRevalidator` ao `TurnResolver`;
- revalidar exatamente um Event imediatamente antes de sua execução;
- garantir que Event rejeitado não seja executado;
- definir explicitamente a semântica mínima do resultado quando a revalidação falha;
- manter múltiplos Events ainda não suportados;
- preservar separação entre geração, revalidação e execução.

---

## 2026-09-19 — M1.1.6 Single-Event Revalidation Path concluído

Milestone:

**M1 — Deterministic Simulation Kernel**

Stage:

**M1.1 — Non-empty Deterministic Turn Resolution**

Subcheckpoint:

**M1.1.6 — Single-Event Revalidation Path**

### Progresso oficial

GPP conquistados:

**43.25 / 1000**

Global Progress:

**4.3%**

Foundation / Simulation Kernel:

**43.25 / 70 GPP — 61.8%**

Nenhum GPP adicional foi conquistado neste subcheckpoint.

A capability `Command → Event, validação e execução sequencial` permanece classificada como:

**Especificada — fator 0.20**

A revalidação de um único Event agora ocorre no fluxo real imediatamente antes da execução, mas a capability completa ainda não possui validação explícita de Commands nem execução sequencial de múltiplos Events.

### Concluído

- `TurnResolver` passou a depender explicitamente de `ISimulationEventRevalidator`;
- exatamente um Event é revalidado antes de sua execução;
- Event autorizado segue a ordem `revalidate → execute`;
- Event rejeitado não é executado;
- Event rejeitado preserva o `WorldState` atual;
- Event rejeitado permanece em `TurnResolutionResult.Events` como Event produzido;
- zero Commands não invocam revalidator nem executor;
- zero Events não invocam revalidator nem executor;
- dois ou mais Events continuam rejeitados antes de qualquer revalidação ou execução;
- revalidator nulo é rejeitado;
- nenhuma event queue foi introduzida;
- nenhum ordering ou deterministic shuffle foi introduzido;
- nenhuma regra concreta de gameplay foi adicionada.

### Evidências

- projetos compilados: 6/6;
- warnings de build: 0;
- erros de build: 0;
- testes totais: 40;
- testes aprovados: 40;
- falhas: 0;
- `SingleCommandSingleEventIsRevalidatedBeforeExecutionAndProducesResultingState`: aprovado;
- `RejectedSingleEventPreservesWorldStateAndIsNotExecuted`: aprovado;
- `SingleCommandWithNoEventsPreservesWorldState`: aprovado;
- `MultipleEventsAreRejected`: aprovado;
- `NullEventRevalidatorIsRejected`: aprovado;
- regressões anteriores do `TurnResolver`: aprovadas;
- `git diff --check`: aprovado;
- SHA-256 dos dois arquivos alterados verificado no pacote de evidências.

### Decisões relevantes

- a revalidação ocorre imediatamente antes da execução do Event;
- Event rejeitado não produz alteração de estado;
- `TurnResolutionResult.Events` continua representando Events produzidos, não Events necessariamente executados;
- resultado estruturado de rejeição permanece adiado até existir necessidade concreta;
- múltiplos Events continuam não suportados para evitar ordering implícito;
- geração, revalidação e execução permanecem responsabilidades separadas.

### Scope Change

Nenhum.

### Novos riscos

Nenhum risco estrutural novo identificado.

`RISK-004 — Determinism failure` permanece relevante.

O checkpoint reduz a incerteza sobre o fluxo de execução, mas ainda não demonstra validação completa de Commands, execução sequencial de múltiplos Events ou uma transição concreta de domínio reproduzível de ponta a ponta.

### Próximo subcheckpoint

**M1.1.7 — Command Validation Contract**

Objetivos:

- estabelecer a fronteira mínima responsável por validar um Command contra o `WorldState` autoritativo;
- manter validação de Command separada da geração de Events;
- permitir rejeição explícita antes de `ISimulationCommandProcessor`;
- evitar ainda múltiplos Commands e múltiplos Events;
- preparar a integração `Command validation → Event generation → Event revalidation → execution`.

---

## 2026-09-19 — M1.1.7 Command Validation Contract concluído

Milestone:

**M1 — Deterministic Simulation Kernel**

Stage:

**M1.1 — Non-empty Deterministic Turn Resolution**

Subcheckpoint:

**M1.1.7 — Command Validation Contract**

### Progresso oficial

GPP conquistados:

**43.25 / 1000**

Global Progress:

**4.3%**

Foundation / Simulation Kernel:

**43.25 / 70 GPP — 61.8%**

Nenhum GPP adicional foi conquistado neste subcheckpoint.

A capability `Command → Event, validação e execução sequencial` permanece classificada como:

**Especificada — fator 0.20**

A fronteira de validação de Command passou a existir, mas ainda não está integrada ao `TurnResolver` e não existe execução sequencial de múltiplos Events.

### Concluído

- `ISimulationCommandValidator` criado em `GlobalArena.Simulation`;
- contrato explícito `WorldState + Command + SimulationContext → bool IsValid`;
- o validator recebe o estado autoritativo atual;
- o validator recebe o Command candidato ao processamento;
- o validator recebe o mesmo `SimulationContext` da resolução;
- o contrato permite aceitar ou rejeitar o Command;
- validação permanece separada da geração de Events;
- nenhuma alteração foi feita no `TurnResolver`;
- nenhuma lógica de múltiplos Commands foi introduzida;
- nenhum ordering ou deterministic shuffle foi introduzido;
- nenhuma regra concreta de gameplay foi adicionada.

### Evidências

- projetos compilados: 6/6;
- warnings de build: 0;
- erros de build: 0;
- testes totais: 42;
- testes aprovados: 42;
- falhas: 0;
- `ValidatorCanReceiveWorldCommandAndContextAndAcceptCommand`: aprovado;
- `ValidatorCanRejectCommand`: aprovado;
- `git diff --check`: aprovado;
- SHA-256 dos dois arquivos novos verificado no pacote de evidências.

### Decisões relevantes

- a validação de Command pertence ao módulo `GlobalArena.Simulation`;
- a decisão mínima de validação é representada por `bool IsValid`;
- o validator não modifica o `WorldState`;
- o validator não gera Events;
- validação e processamento permanecem responsabilidades separadas;
- motivo estruturado de rejeição não será introduzido antes de existir necessidade concreta;
- a integração com o `TurnResolver` permanece para o próximo subcheckpoint.

### Scope Change

Nenhum.

### Novos riscos

Nenhum risco estrutural novo identificado.

`RISK-004 — Determinism failure` permanece relevante.

O checkpoint reduz a incerteza sobre a fronteira de validação de Commands, mas ainda não prova que Commands inválidos são bloqueados antes da geração de Events no fluxo real.

### Próximo subcheckpoint

**M1.1.8 — Single-Command Validation Path**

Objetivos:

- integrar `ISimulationCommandValidator` ao `TurnResolver`;
- validar exatamente um Command antes de `ISimulationCommandProcessor`;
- garantir que Command rejeitado não produza Events;
- preservar o `WorldState` quando o Command for rejeitado;
- manter múltiplos Commands ainda não suportados;
- completar o caminho `Command validation → Event generation → Event revalidation → execution`.

---

## 2026-09-19 — M1.1.8 Single-Command Validation Path concluído

Milestone:

**M1 — Deterministic Simulation Kernel**

Stage:

**M1.1 — Non-empty Deterministic Turn Resolution**

Subcheckpoint:

**M1.1.8 — Single-Command Validation Path**

### Progresso oficial

GPP conquistados:

**43.25 / 1000**

Global Progress:

**4.3%**

Foundation / Simulation Kernel:

**43.25 / 70 GPP — 61.8%**

Nenhum GPP adicional foi conquistado neste subcheckpoint.

A capability `Command → Event, validação e execução sequencial` permanece classificada como:

**Especificada — fator 0.20**

O caminho de um único Command agora integra validação, geração de Event, revalidação e execução, mas a capability completa ainda não possui ordering explícito nem execução sequencial de múltiplos Events.

### Concluído

- `TurnResolver` passou a depender explicitamente de `ISimulationCommandValidator`;
- zero Commands não invocam validator, processor, revalidator nem executor;
- exatamente um Command é validado antes do processor;
- Command rejeitado preserva o `WorldState`;
- Command rejeitado produz zero Events;
- Command rejeitado não invoca processor, revalidator ou executor;
- Command aceito segue o caminho `validate → process → revalidate → execute`;
- um Command que produz zero Events preserva o estado e encerra após `process`;
- Event rejeitado continua preservando o estado e não é executado;
- dois ou mais Events continuam rejeitados antes de revalidação ou execução;
- dois ou mais Commands continuam rejeitados antes de validação individual;
- validator nulo é rejeitado;
- nenhuma event queue foi introduzida;
- nenhum ordering ou deterministic shuffle foi introduzido;
- nenhuma regra concreta de gameplay foi adicionada.

### Evidências

- projetos compilados: 6/6;
- warnings de build: 0;
- erros de build: 0;
- testes totais: 44;
- testes aprovados: 44;
- falhas: 0;
- `SingleCommandSingleEventFollowsValidationRevalidationAndExecutionPipeline`: aprovado;
- `RejectedCommandPreservesWorldStateAndProducesNoEvents`: aprovado;
- `RejectedSingleEventPreservesWorldStateAndIsNotExecuted`: aprovado;
- `SingleCommandWithNoEventsPreservesWorldState`: aprovado;
- `MultipleEventsAreRejected`: aprovado;
- `MultipleCommandsAreRejected`: aprovado;
- `NullCommandValidatorIsRejected`: aprovado;
- regressões anteriores do `TurnResolver`: aprovadas;
- `git diff --check`: aprovado;
- SHA-256 dos dois arquivos alterados verificado no pacote de evidências.

### Decisões relevantes

- validação de Command ocorre antes de geração de Events;
- Command rejeitado não produz Event;
- ausência de Event encerra a pipeline antes da revalidação;
- revalidação continua imediatamente antes da execução;
- múltiplos Commands continuam sem semântica de ordering;
- múltiplos Events continuam sem semântica de ordering ou execução sequencial;
- resultado estruturado de rejeição permanece adiado;
- validação, processamento, revalidação e execução permanecem responsabilidades separadas.

### Scope Change

Nenhum.

### Novos riscos

Nenhum risco estrutural novo identificado.

`RISK-004 — Determinism failure` permanece relevante.

O checkpoint completa estruturalmente o caminho de um Command e um Event, mas ainda não demonstra ordering determinístico de múltiplos Events nem uma transição concreta de domínio reproduzível de ponta a ponta.

### Próximo subcheckpoint

**M1.1.9 — Deterministic Event Ordering Contract**

Objetivos:

- estabelecer a fronteira mínima de ordering para uma coleção de Events;
- manter ordering separado de revalidação e execução;
- permitir que múltiplos Events recebam uma ordem explícita antes da futura execução sequencial;
- utilizar `SimulationContext` como parte do contrato de determinismo;
- evitar ainda execução sequencial de múltiplos Events;
- preparar o caminho `generation → deterministic ordering → revalidation → sequential execution`.

---

## 2026-09-19 — M1.1.9 Deterministic Event Ordering Contract concluído

Milestone:

**M1 — Deterministic Simulation Kernel**

Stage:

**M1.1 — Non-empty Deterministic Turn Resolution**

Subcheckpoint:

**M1.1.9 — Deterministic Event Ordering Contract**

### Progresso oficial

GPP conquistados:

**43.25 / 1000**

Global Progress:

**4.3%**

Foundation / Simulation Kernel:

**43.25 / 70 GPP — 61.8%**

Nenhum GPP adicional foi conquistado neste subcheckpoint.

A capability `Deterministic shuffle / ordering` permanece classificada como:

**Especificada — fator 0.20**

A fronteira de ordering passou a existir, mas ainda não há algoritmo concreto nem integração ao `TurnResolver`.

### Concluído

- `ISimulationEventOrderer` criado em `GlobalArena.Simulation`;
- contrato explícito `Events + SimulationContext → ordered Events`;
- ordering permanece separado de identidade de Event;
- ordering permanece separado de revalidação e execução;
- o contrato permite devolver ordem explícita sem mutar a coleção de entrada;
- `SimulationContext` faz parte da fronteira determinística;
- nenhuma alteração foi feita no `TurnResolver`;
- nenhum algoritmo concreto de shuffle foi introduzido;
- nenhuma execução sequencial de múltiplos Events foi introduzida;
- nenhum EventLog foi introduzido.

### Evidências

- projetos compilados: 6/6;
- warnings de build: 0;
- erros de build: 0;
- testes totais: 46;
- testes aprovados: 46;
- falhas: 0;
- `OrdererCanReceiveEventsAndContextAndReturnExplicitOrder`: aprovado;
- `OrdererCanReturnNewOrderWithoutMutatingInput`: aprovado;
- `git diff --check`: aprovado;
- SHA-256 dos dois arquivos novos verificado no pacote de evidências.

### Decisões relevantes

- ordering recebe `SimulationContext` explicitamente;
- EventId não representa posição na fila;
- o contrato não obriga mutação in-place;
- algoritmo concreto de ordering permanece fora deste checkpoint;
- integração com `TurnResolver` permanece adiada;
- revalidação continuará ocorrendo após ordering e imediatamente antes da execução.

### Scope Change

Nenhum.

### Novos riscos

Nenhum risco estrutural novo identificado.

`RISK-004 — Determinism failure` permanece relevante.

O checkpoint reduz a incerteza sobre a fronteira de ordering, mas ainda não prova que uma mesma entrada e contexto produzam a mesma permutação.

### Próximo subcheckpoint

**M1.1.10 — Seeded Deterministic Event Ordering**

Objetivos:

- implementar um orderer concreto baseado no `DeterministicRandom` existente;
- produzir a mesma ordem para a mesma coleção de Events e o mesmo contexto determinístico;
- preservar a coleção de entrada;
- preservar exatamente a membership dos Events;
- tratar coleções vazias e unitárias sem efeitos colaterais;
- manter a implementação ainda isolada do `TurnResolver`;
- preparar a integração futura de ordering com execução sequencial.

---

## 2026-09-19 — M1.1.10 Seeded Deterministic Event Ordering concluído

Milestone:

**M1 — Deterministic Simulation Kernel**

Stage:

**M1.1 — Non-empty Deterministic Turn Resolution**

Subcheckpoint:

**M1.1.10 — Seeded Deterministic Event Ordering**

### Progresso oficial

GPP conquistados:

**44.75 / 1000**

Global Progress:

**4.5%**

Foundation / Simulation Kernel:

**44.75 / 70 GPP — 63.9%**

GPP adicionais conquistados neste subcheckpoint:

**+1.50 GPP**

A capability `Deterministic shuffle / ordering`, com orçamento de 5 GPP, passa de:

**Especificada — fator 0.20 — 1.00 GPP**

para:

**Funcional isoladamente — fator 0.50 — 2.50 GPP**

Incremento:

**+1.50 GPP**

### Justificativa de maturidade

A capability agora possui implementação concreta isolada e testes automatizados que demonstram comportamento determinístico reproduzível, preservação da coleção de entrada, preservação de membership e tratamento dos casos limite mínimos.

Ela ainda não é classificada como `Integrada`, pois o orderer não participa do fluxo real do `TurnResolver`.

### Concluído

- `SeededSimulationEventOrderer` criado em `GlobalArena.Simulation`;
- implementação concreta de `ISimulationEventOrderer`;
- utilização explícita de `DeterministicRandom`;
- seed obtida de `SimulationContext.Seed`;
- Fisher-Yates determinístico implementado;
- seleção limitada por amostragem de rejeição para evitar viés de módulo;
- mesma entrada e mesmo contexto produzem a mesma ordem;
- vetor de referência estável definido para seed `0`;
- membership dos Events é preservada;
- coleção de entrada não é mutada;
- coleção vazia é suportada;
- coleção unitária é suportada;
- entrada nula é rejeitada;
- nenhuma alteração foi feita no `TurnResolver`;
- nenhuma execução sequencial de múltiplos Events foi introduzida;
- nenhum EventLog foi introduzido.

### Evidências

- projetos compilados: 6/6;
- warnings de build: 0;
- erros de build: 0;
- testes totais: 52;
- testes aprovados: 52;
- falhas: 0;
- `SameInputAndContextProducesSameOrderAcrossCalls`: aprovado;
- `SeedZeroProducesStableReferenceOrder`: aprovado;
- `OrderingPreservesMembershipAndDoesNotMutateInput`: aprovado;
- `EmptyCollectionProducesEmptyOrder`: aprovado;
- `SingleEventRemainsSingle`: aprovado;
- `NullEventsAreRejected`: aprovado;
- `git diff --check`: aprovado;
- SHA-256 dos dois arquivos novos verificado no pacote de evidências.

### Decisões relevantes

- o algoritmo concreto de ordering é Fisher-Yates;
- a aleatoriedade é derivada diretamente de `SimulationContext.Seed`;
- `TurnNumber` não é misturado adicionalmente à seed neste estágio;
- uma nova instância de PRNG é criada por operação de ordering;
- o input é copiado antes da permutação;
- o comportamento de referência passa a fazer parte da compatibilidade determinística;
- integração com `TurnResolver` permanece para o próximo subcheckpoint.

### Scope Change

Nenhum.

### Novos riscos

Nenhum risco estrutural novo identificado.

`RISK-004 — Determinism failure` permanece relevante, mas a incerteza específica sobre o algoritmo isolado de ordering foi reduzida.

A integração do ordering ao fluxo real ainda precisa provar que múltiplos Events são processados sequencialmente contra o estado autoritativo atualizado.

### Próximo subcheckpoint

**M1.1.11 — Multi-Event Sequential Resolution Path**

Objetivos:

- integrar `ISimulationEventOrderer` ao `TurnResolver`;
- permitir que um único Command validado produza múltiplos Events;
- ordenar os Events antes da resolução;
- revalidar cada Event imediatamente antes de sua execução;
- revalidar cada Event contra o `WorldState` resultante das execuções anteriores;
- executar sequencialmente Events aceitos;
- preservar o estado corrente quando um Event for rejeitado e continuar a sequência;
- manter múltiplos Commands ainda não suportados;
- definir explicitamente a semântica de `TurnResolutionResult.Events` para a sequência ordenada.

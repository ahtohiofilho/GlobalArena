# GA — G3-J3a: fechamento de turno ecológico com um único dono do save

**Status:** implementação opt-in e restrita a turnos econômico-ambientais sem comandos de jogadores. Empilhada sobre G3-J2 (#70). **Não** substitui o `AuthoritativeTurnCommitGate` para turnos gerais nem prova autorização de ordens humanas. Sem merge.

## Problema e decisão

O G3-J2 integrou a simulação ecológica ao `PhasedTurnResolutionEngine` por um evento **projetado**, sem publicar reservas. O G3-J1/G3-I6 mantêm save `GAFULL09`, journal e um único head durável separado do `CanonicalGameSession`. Invocar `CommitNext` de dentro de `ITurnResolutionPhase.Resolve` criaria risco de **dupla publicação**: disco poderia avançar antes de a política e o kernel aceitarem o turno.

O G3-J3a cria `EconomicClosedEcologicalDurableTurnGate`: um coordenador dedicado à **única classe de turno que já pode ser aceita de modo seguro pelo head existente** — turno ecológico sem comandos, sem qualquer alteração de `WorldState` ou outra sessão em memória. Ele não chama `CanonicalGameSession.AcceptSnapshot` nem cria segundo contador persistente.

## 1. Caminho de execução

`ResolveAndCommit(closureInput, seed, expectedCheckpointDigest, workerDemand, originHolds)`:

1. sob lock de coordenador, verifica que o digest esperado é o atualmente aceito no `EconomicCompleteV9DurableSaveHost` e que o turno é `LastAppliedTurn + 1`;
2. copia demandas e retenções **uma só vez**, impedindo re-enumeração mutável;
3. verifica política de fechamento. Turno aberto retorna `WasCommitted=false` sem eventos, bundle ou journal novo;
4. obtém do **save completo v9 já aceito** o `WorldState` com instalações físicas e os livros de capital natural/reservas; valida integralmente o bundle;
5. constrói a fase `EconomicEcologicalTurnProjectionPhase`, e executa o `TurnPolicyResolutionGate` e `PhasedTurnResolutionEngine` existentes com **lote de comandos vazio**;
6. exige política ainda fechada, evento **único** de origem System, ID estável, turno/identidade coerentes, log elegível e executado, mundo exatamente imutável e compromisso do mundo igual ao checkpoint projetado;
7. envia o compromisso **proposto pelo evento** para `EconomicCompleteV9DurableSaveHost.CommitProjected`, que usa a comparação CAS do digest anterior e **recalcula a transição antes da gravação**, rejeitando divergências de resultado;
8. somente depois grava `GAFULL09` completo e registro `GAJRN001`, publica head único v2; em memória o host passa a referenciar o checkpoint aceito, e o resultado da API informa `WasCommitted=true` e o evento de projeção auditado.

Assim, o evento do motor está realmente vinculado ao resultado do arquivo confirmado, sem publicar outro `WorldState` e sem duplicar o journal. A verificação do digest proposto acontece **antes** de arquivo e head, não após a confirmação.

## 2. O que fica protegido

- Rejeita CAS desatualizado, turno pulado/repetido e mundo gerado incompatível.
- Rejeita reserva acima da produção material efetiva e não credita aquisição no destino sem entrega comprovada.
- Não cria segunda fonte de consumo: demandas continuam originadas nos trabalhadores; censo legado apenas compatibilidade.
- Exige que `ITurnResolutionPhase` produza apenas o evento de avaliação, sem modificar `WorldState`.
- Turnos abertos e falhas de resolução/política não escrevem journal nem fazem commit.
- O host único já exige arquivo completo antes de publicar head, replay da cadeia de journal na restauração e digest atual **confiável externamente** para evitar rollback.

## 3. Limitações decisivas

**Não é ainda transação única entre sessão geral e disco.** O coordenador trabalha só com o `WorldState` imutável aceito no host. Não pode processar mudanças de território, instalação, unidade, frete ou outros comandos. `AuthoritativeTurnCommitGate` continua sendo o dono dos turnos da sessão geral e **não deve ser executado em paralelo como outro publicador para a mesma partida**. O host G3-J3a é apenas uma alternativa limitada para a trilha de turnos econômicos de referência.

- Não existe identificação, assinatura ou autorização de `ISimulationCommand` do jogador. A API **não recebe esses comandos**. A política de fechamento é fornecida pelo chamador, não evidencia consentimento dos participantes.
- O evento foi efetivamente resolvido no motor de fases, mas o journal `GAJRN001` ainda guarda entradas ecológicas; não serializa um log geral de comandos/eventos do kernel.
- Não altera `CanonicalWorldStateHasher.FormatVersion=9` nem o modelo de instalações do PR #50.
- Não implementa pagamentos, market clearing ou atestação de entrega física.
- Se houver alteração do `WorldState` num turno futuro, esta fronteira deve ser substituída por um mecanismo que grave **estado, livros, comandos e eventos da mesma sessão** em um único commit. O G3-J3a recusa essa composição em vez de tolerar divergência.
- `Flush(true)`/rename e digest local não substituem fsync de diretório garantido em todo hardware, assinatura do journal ou transação com a âncora confiável externa.

## 4. Testes

`G3J3ClosedEcologicalDurableTurnGateTests.cs` contém sete cenários:

1. turno fechado produz evento de sistema auditado e exatamente um commit durável, mantendo instalações, demanda e reservas;
2. turno aberto não faz projeção, gravação nem alteração de head;
3. turnos pulados/repetidos e digest stale recusados;
4. retenção impossível e política de fechamento instável recusadas sem publicação;
5. digest proposto incorreto em `CommitProjected` é recusado **antes** de criar journal ou novo bundle;
6. reinício e continuação de dois turnos pelo mesmo caminho de fechamento com replay da cadeia;
7. mundo gerado divergente não consegue projetar o checkpoint aceito.

```sh
dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~G3J3ClosedEcologicalDurableTurnGateTests
```

**Aceite:** todas as matrizes Windows/Ubuntu/macOS de kernel, hash e benchmark no SHA final.

## 5. Próximo gate G3-J3b / G3-J4

Para turnos autoritativos verdadeiros, tornar **o armazenamento durável o único dono da sessão**, formalizar preflight de `ISimulationCommand` autenticado, evento-log versionado com hash e implementação de `WorldState` staged, e só então acoplar estado de gameplay, capital natural, reservas e journal no mesmo commit. Um mecanismo de recuperação precisa coordenar persistência do head com confiança externa e o estado em memória; não há garantia de atomicidade entre processos aqui.

**Nenhum merge automático.** PRs #49/#50, #70 e `main` preservados.

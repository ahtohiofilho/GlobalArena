# GA — G3-J3b: autorização explícita e transcript reproduzível de movimentos

**Status:** avanço incremental verificável sobre G3-J3a (#71). **Ainda não publica comandos militares em GAFULL09** e não altera o scheduler autoritativo da sessão. Sem merge.

## 1. Fronteira de confiança

O kernel já contém `MilitaryMoveCommand`, `MilitaryMoveCommandValidator`, `MilitaryMoveCommandProcessor`, `MilitaryMoveEvent`, revalidador e executor de movimentos. O fato de um comando declarar `IssuingCivilizationId` e a unidade pertencer a essa civilização **não autentica** a identidade externa de quem o enviou.

`IMilitaryMoveOrderAuthority` é um contrato **obrigatório e fornecido pelo host**: `IsAuthorized(command, acceptedWorld, context)`. Deve verificar credenciais, permissões e vínculo com uma civilização num serviço confiável, fora desta biblioteca. G3-J3b não inventa uma assinatura ou sessão autenticada. Um host que escolha um autorizador permissivo continua responsável pelo risco.

`AuthorizedMilitaryMoveTurnAudit.Project` exige checkpoint GAFULL09 aceito e digest confiável externo, vínculo do mundo gerado exato, hash `WorldState` v9, lote não vazio e exclusivamente de `MilitaryMoveCommand`, IDs únicos e turno correto. Todo comando deve ser **explicitamente autorizado** e passar `MilitaryMoveCommandValidator` no estado aceito. Se qualquer um falhar, **todo o lote é recusado**: nenhum comando inválido é simplesmente omitido como se tivesse sido executado.

Após o preflight, `TurnResolver` original executa os eventos ordenados por `SeededSimulationEventOrderer`, revalida contra o estado staged e registra `WasEligible`/`WasExecuted`. Comandos que eram válidos no planejamento mas conflitam na resolução são explicitamente registrados como **não executados**.

## 2. Transcript canônico GAMVJ001

O codec binário de testemunho de execução contém:

- magic `GAMVJ001`, versão 1, número do turno e `SimulationSeed`;
- compromisso ecológico aceito, versão 9 e SHA-256 do `WorldState` **anterior**;
- versão 9 e SHA-256 do `WorldState` **resultante**, ainda não publicado;
- lote de comandos em ordem de `CommandId.Sequence`, com emissor, unidade e destino;
- eventos **na ordem realmente resolvida**, contendo sequências, origem do comando, emissor, unidade, origem, destino, aresta, elegibilidade e execução;
- checksum SHA-256 final de todo o payload.

Os campos numéricos usam big-endian e formatos de largura fixa. Contagens declaradas são verificadas contra os bytes efetivamente disponíveis antes de alocar arrays. Nenhum registro depende da ordem de um dicionário ou de `GetHashCode`.

`VerifyReplay` recebe o checkpoint aceito e seu digest confiável, **o turno e a semente esperados de uma fonte confiável externa**, verifica a integridade do arquivo, reconstrói as ordens, **reconsulta o autorizador externo**, executa novamente o mesmo `TurnResolver`, compara hash do estado resultante e exige bytes de transcript **idênticos**. Uma mudança de comando, semente, bandeira `WasExecuted`, origem de evento ou resultado é rejeitada mesmo que alguém recalcule apenas o checksum externo.

**Limitação:** como a autorização é reconsultada no replay, uma permissão revogada pode impedir verificações históricas. Para replay histórico auditável após revogação, precisamos registrar **atestação de autorização versionada e assinada** no momento do fechamento, com política de identidade e validade específica. Isso é próximo gate; não fingimos que este codec já faz isso.

## 3. Arquivamento sem publicação

`AuthorizedMilitaryMoveAuditArchive` armazena `<SHA256 do testemunho>.gamove` como blob imutável usando arquivo temporário, `Flush(true)`, renomeação sem sobrescrita e recusa de colisão com conteúdo divergente. Ao carregar, exige digests confiáveis do **checkpoint v9** e do **testemunho**, **além de turno e semente confiáveis externos**, e reautoriza e reexecutar o lote.

**Importante:** arquivar o transcript não publica a mudança. Um `*.gamove` é evidência de pré-execução/projeção. O componente não chama `CanonicalGameSession.AcceptSnapshot`, `AuthoritativeTurnCommitGate`, `EconomicCompleteV9DurableSaveHost.CommitNext`, nem escreve uma nova head `full-v9-head.gahead`. Ele não cria segunda trilha paralela de aceitação.

## 4. Testes G3-J3b

`GlobalArena.Tests/G3J3bAuthorizedMilitaryMoveAuditTests.cs` cobre sete cenários:

1. comando autorizado executado no resolvedor existente, alterando somente o estado staged; replay a partir do GAFULL09 carregado e íntegro;
2. ordem canônica e conflitos na mesma unidade: um evento executado e um não executado, ambos registrados;
3. autorização negada, tipo de comando desconhecido, turno errado, IDs duplicados e lote vazio recusados;
4. autorização posteriormente revogada impede um replay que exigiria nova aprovação;
5. manipulação de flags ou seed com checksum externo recalculado, truncamento, digest confiável errado ou outro mundo recusados;
6. arquivo imutável, idempotente e verificado pelo digest do conteúdo, com rejeição de corrupção;
7. transcript em disco **não** modifica o head completo nem publica reservas/ecologia.

```sh
dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~G3J3bAuthorizedMilitaryMoveAuditTests
```

## 5. Gate seguinte

**G3-J3c:** um envelope **assinado** de credenciais/autorização por comando, versionamento de origem e tempo do consentimento, com verificação histórica sem reautorizar por permissões atuais; integração da resolução militar e da evolução ecológica/estoques em um único commit de `WorldState` + `GAFULL09` + journal de ações, com recuperação de crash. Isso exige também contrato único de identidade e scheduler e proteção da âncora externa. Não anunciar transação geral até implementá-la.

O protótipo atual de movimentação é **estratégico**, não garante transporte material por tile fino, compra/venda, pagamento ou entrega física. As instalações do PR #50 continuam no hash v9 e não são recriadas ou modificadas por movimentos.

**Nenhum merge realizado.** PRs #49/#50, #71 e `main` intactos.

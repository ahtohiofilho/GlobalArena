# GA — G3-I6: confirmação única e durável do save completo v9

**Status:** candidato com escrita em disco local e retomada verificável, empilhado sobre G3-I5 (#67). Sem merge, sem integração autoritativa ao scheduler de jogo.

## Objetivo e fronteira

O save GAFULL09 (G3-I4) já contém o `WorldState` v9 completo, inclusive instalações de transporte com identidade, tile físico fino e capacidade dirigida, além dos livros ecológicos e reservas. G3-I5 autentica e migra GAFULL08 para GAFULL09. G3-I6 acrescenta **uma única head em disco** que autoriza localmente a versão aceita do arquivo completo, em vez de manter heads separadas para `WorldState` e ecologia.

## 1. Protocolo de commit local

`EconomicCompleteV9DurableSaveHost` exige diretório absoluto, lock exclusivo no arquivo `.full-v9-writer.lock` (`FileShare.None`), processo único e digest esperado (CAS).

`Create` exige turno ecológico inicial zero e nenhum cursor de retenção já executado; impede reinicializar como genesis uma partida avançada.

`CommitNext(expected, turn, demand, holds)`:

1. valida digest esperado e compara o head persistido com o head que o host efetivamente aceitou;
2. calcula exatamente um turno ecológico/econômico G3 por meio do resolvedor existente;
3. grava o **GAFULL09 completo**, codificado e verificado: mundo, população/trabalhadores, instalações, capital natural, reservas e cursores;
4. utiliza `EconomicCompleteV9SaveBundleArchive.Store`, com arquivo temporário, escrita, `Flush(true)` e renomeação sem sobrescrever blob anterior;
5. escreve head temporário com `Flush(true)` e substitui o **único** `full-v9-head.gahead`, no mesmo diretório;
6. **somente após a publicação do head**, altera o checkpoint aceito em memória.

Uma interrupção após 3/4 e antes de 5 deixa um blob órfão: ele não representa turno aceito. Uma interrupção após 5 permite restaurar o novo blob. A head contém magic e versão, `WorldStateHash` v9, digest ecológico aceito, digest predecessor imediato, último turno, sequência de commits, indicação de migração e âncoras históricas, protegidos por SHA-256 contra corrupção casual.

`Restore` exige o **digest atualmente aceito de fonte independente e confiável**. O blob é decodificado como GAFULL09 completo, todos os hashes, checksum e identidades são validados, e os campos de mundo/turno devem coincidir com a head. Após o primeiro commit, exige também que o blob predecessor exista e pertença ao mesmo mundo e ao turno imediatamente anterior. Isso prova continuidade dos snapshots mas **não reexecuta comandos**: um journal confiável continua necessário.

## 2. Origem de migração v8 → v9

`CreateFromLegacy` exige o GAFULL08 original e duas **âncoras aceitas externamente**: `WorldStateHash` v8 e compromisso ecológico v8. Invoca o migrador do G3-I5, que verifica o arquivo antigo, conserva capacidades, madeira, pedra, solo, reservas, mapeamento, políticas, cursores e força de trabalho, e produz um GAFULL09. O primeiro head v9 guarda os dois compromissos de origem e a marca de migração (sem instalações inventadas).

`Restore` de uma partida migrada exige **também** os dois compromissos históricos confiáveis, além do digest v9 atualmente aceito. A marca e os dados de origem persistem em commits posteriores do mesmo ramo.

**Importante:** a validação não faz escrita conjunta no armazenamento externo de âncoras confiáveis. Um administrador de disco malicioso com acesso a tudo pode fabricar/checksumar head e bundle; o digest externo confiável permanece indispensável para detectar rollback. Em caso de crash entre o head local e a atualização da âncora externa, a política de recuperação precisa de mecanismo explícito e auditado; o G3-I6 não assume que ambas foram confirmadas.

## 3. Modelagem e determinismo preservados

- Mantém `CanonicalWorldStateHasher.FormatVersion = 9` e a identidade de instalações de PR #50.
- Mantém `EconomicEcologicalCheckpoint.CommitmentVersion = 1`, com compromisso exterior ao `WorldState` canônico.
- Trabalhadores são origem de produção e demanda; o censo legado é somente compatibilidade de save.
- Sem criação automática de materiais não vendidos, compra/entrega não atestadas, instalações gratuitas ou regeneração de pedra superficial.
- Migração não cria instalações ausentes no formato v8, não altera recursos naturais nem gera bens.
- Não simula transação de mercado autoritativa; o host registra somente o **turno ecológico projetado**.

## 4. Testes e limites

`GlobalArena.Tests/G3I6DurableCompleteV9SaveTests.cs` cobre:

1. save completo único com instalações, trabalhadores e materiais, retomada e continuação de turnos após reinício;
2. blob completo ainda não publicado não avança a head;
3. escritor concorrente e CAS desatualizado recusados;
4. transição inválida/arquivo candidato corrompido não confirmam novo turno;
5. rollback de head válida anterior recusado mediante digest confiável atualizado;
6. head corrompido, blob aceito ou predecessor ausente rejeitados;
7. origem de migração v8 autenticada e obrigatória na restauração;
8. genesis posterior a turno zero e migrador com hash falso recusados.

```sh
dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~G3I6DurableCompleteV9SaveTests
```

CI de kernel, hash e benchmark em Windows, Ubuntu, macOS para o commit exato é condição de aceite.

### Limites que impedem chamar o sistema de save autoritativo final

- Não vinculado ao `PhasedTurnResolutionEngine`, nem ao journal de **comandos executados/autorizados**; um SHA-256 não autentica quem executou o turno.
- Não há transação distribuída com o armazenamento externo do digest confiável, nem garantia uniforme de fsync de diretório em todas as plataformas/hardwares.
- A verificação de predecessor não substitui replay do histórico de comandos.
- Não importa instalações históricas externas de forma inventada nem resolve o acoplamento físico dos tiles finos.
- O host mantém o mesmo `WorldState` entre turnos ecológicos; mudança de mundo, guerra, transporte ou instalação exigirá um novo gateway autoritativo que confirme todo o estado na mesma transação.
- PRs #49/#50 e pilha G3 ainda não foram mesclados. Sem merges automáticos.

**Próximo gate:** journal autenticado de comandos/eventos, integração ao scheduler e aceitação única para transações com mudança efetiva de `WorldState`.

# GA — G3-J1: journal encadeado de entradas econômicas e replay do save v9

**Status:** implementação candidata verificável, baseada em G3-I6 (#68). Não implementa execução autoritativa do `PhasedTurnResolutionEngine` nem um journal assinado de comandos dos jogadores. Sem merge.

## 1. Por que é necessário

G3-I6 publicava um **único head durável** sobre o save completo `GAFULL09`, mas verificava somente que os snapshots anteriores existiam. Essa condição sozinha não prova que a sequência de ações podia reproduzir os saldos.

G3-J1 armazena as **entradas exatas de cada transição econômico-ambiental** e, ao restaurar, as executa novamente. O mecanismo verifica não só os checkpoints como também a integridade dos próprios registros de entrada.

Um detalhe decisivo: uma alteração de **perfil de demanda** pode não modificar o estado ecológico/reservas do turno; portanto uma verificação que compare **somente hashes de checkpoints** deixaria essa alteração passar despercebida. Para evitar isso, o hash do journal mais recente é vinculado ao head e cada registro contém o hash de seu predecessor.

## 2. Arquivos e formatos

### `EconomicEcologicalTransitionJournalRecord` / `EconomicEcologicalTransitionJournalCodec`

O registro representa um turno ecológico puro, contendo:
- número ordinal do turno, compromisso anterior e compromisso posterior;
- hash do **journal anterior** (zeros no primeiro registro);
- sequência exata dos perfis de demanda por trabalhador (ponto, mercadoria e taxa);
- sequência exata das retenções voluntárias de origem (ponto, mercadoria, atividade, quantidade).

O codec `GAJRN001` (formato 1) grava inteiros big-endian, contagens explícitas e SHA-256 sobre o payload. A leitura exige re-encode byte a byte idêntico, rejeita checksum inválido, versão desconhecida, truncamento, contagens fisicamente impossíveis e dados extras. Não depende de `GetHashCode`, ordem de dicionário, reflexão ou JSON.

`EconomicEcologicalTransitionJournalArchive` usa nome `<compromisso do checkpoint resultante>.gajournal`, grava um arquivo temporário no mesmo diretório, `Flush(true)`, `File.Move` sem sobrescrita e recusa qualquer colisão com bytes divergentes. Um arquivo não referenciado pelo head é órfão e **não** constitui commit.

### `EconomicCompleteV9DurableSaveHost` — head v2

A única head `full-v9-head.gahead` passa do **formato 1** (G3-I6) para o **formato 2**: adiciona os 32 bytes do hash do journal recém confirmado, preservando campos de mundo v9, digest do checkpoint, predecessor, sequência e a proveniência de migração v8.

**Compatibilidade:** heads locais v1 de G3-I6 são recusadas explicitamente pelo leitor v2, sem conversão silenciosa. O formato do bundle completo `GAFULL09`, o hash de `WorldState` v9 e o compromisso ecológico v1 **não** mudam. Em produção, a migração de heads v1 exigirá um importador próprio, lastreado em registros confiáveis; não inferir comandos antigos que nunca foram registrados.

No `CommitNext`:
1. exige o digest esperado, confere head persistido/lock exclusivo e valida o próximo turno;
2. copia as entradas recebidas **uma única vez** para evitar que enumeradores mutáveis gerem journal diferente das entradas efetivamente processadas;
3. calcula produção, extração e reservas usando os resolvedores G3 já existentes;
4. constrói registro `GAJRN001`, encadeado ao hash do registro anterior, e calcula seu digest;
5. grava o **bundle GAFULL09 completo** e o registro imutável **antes** de publicar a nova head;
6. publica a head v2 que inclui o digest do journal, e só então muda o estado aceito em memória.

Uma interrupção entre os passos 4–5 pode deixar blobs de save/journal órfãos, que não avançam o turno confirmado.

## 3. Verificador de replay

`EconomicCompleteV9JournalReplayVerifier.VerifyChain(generated, bundles, journal, acceptedDigest, commitSequence, expectedLatestJournalDigest)`:

- parte do **checkpoint atual cujo digest foi fornecido como confiável** pelo chamador;
- carrega o journal da ponta da cadeia pelo nome do resultado;
- compara o **hash completo dos bytes do journal** com o hash enraizado na head ou no registro subsequente;
- carrega o GAFULL09 predecessor, verifica mundo, turno e compromisso;
- reexecuta a transição ecológica com perfis e retenções gravados;
- exige o compromisso resultante e bytes completos do bundle reproduzidos exatamente;
- repete até a sequência zero e exige que a cadeia de journals termine no digest de zeros.

Isso também funciona para saves originados de migração v8→v9: o **snapshot migrado** é a base (sequência zero) e suas duas âncoras históricas continuam obrigatórias na restauração.

**Importante:** o registro atesta **reprodutibilidade e consistência**, não legitimidade de quem enviou a ordem. Um administrador capaz de alterar simultaneamente journal + head pode produzir outra cadeia auto-consistente que a checagem local não autentica. A fonte externa confiável do compromisso atual e, para certificação das **entradas históricas**, uma âncora independente do digest do journal/assinatura continuam requisitos antes de execução hostil em produção. O SHA do checkpoint sozinho não autentica perfis de demanda que não alteram os saldos.

## 4. Fronteira com o scheduler

O `PhasedTurnResolutionEngine` e o `AuthoritativeTurnCommitGate` existentes **não são modificados**. G3-J1 registra e reproduz a **trilha econômico-ambiental de referência**, que ainda mantém o mesmo `WorldState` entre turnos ecológicos. Ele **não** grava comandos do kernel, não registra eventos do `SimulationEventLog`, não calcula pagamentos/liquidação, nem comprova entrega física de mercadorias. Não existe dupla publicação de turnos no scheduler nesta etapa.

A integração futura G3-J2 deve projetar as entradas de G3 num contrato de fase com eventos/IDs do kernel e adicionar **um único ponto de aceitação**, no qual tanto o save persistido quanto a sessão canônica mudem em conjunto. Deve ser explícita a recuperação quando a head local for publicada mas a âncora confiável externa não for.

## 5. Testes

Arquivo: `GlobalArena.Tests/G3J1DurableEcologicalReplayJournalTests.cs`, com seis testes:

- ida/volta do codec e três turnos seguidos com restauração;
- falsificação de demanda com checksum válido e **mesmo resultado material** rejeitada pela head encadeada;
- journal ausente/corrompido impede restauração de turno já confirmado;
- journal e bundle gerados mas sem head são órfãos e não avançam a partida;
- journal já existente e divergente impede publicação do próximo head;
- formato desconhecido, truncamento e nome de resultado incorreto rejeitados.

Toda a regressão G3-I6 também deve permanecer verde no commit exato.

```sh
dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~G3J1DurableEcologicalReplayJournalTests
```

**Aceite somente após CI kernel/hash/benchmark em Windows, Ubuntu e macOS no SHA final.**

## 6. Próximos gates

- **G3-J2:** integração de fase econômica ao scheduler com eventos auditáveis e identificadores próprios; sem dupla demanda entre população legada e trabalhadores.
- **G3-J3:** journal de comandos realmente fechados/autorizados, incluindo qualquer modificação a território, instalações, guerra e logística, além de market clearing/entregas comprovadas.
- **G3-J4:** armazenamento de confiança independente (hash atual e journal root), protocolo transacional/recovery e plano de importação de head v1 para v2, sem falsificar história ausente.

**Nenhum merge automático. PR #49/#50, main e pilha anterior intactos.**

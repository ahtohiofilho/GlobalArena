# GA — G3-I5: migração íntegra de GAFULL08 + ecologia para GAFULL09

**Status:** candidato opt-in, baseado em G3-I4 (#66), ainda não ligado ao scheduler nem a uma head de save completo. **Sem merge.**

## Problema resolvido

G3-I3 migrava somente o WorldState v8 para v9. O sidecar ecológico v8, porém, incorporava explicitamente o **hash do mundo v8**. Simplesmente anexá-lo a um mundo v9 falharia corretamente — ou criaria um risco grave se a verificação fosse omitida. O G3-I5 importa **o arquivo completo GAFULL08 com o sidecar original**, verifica-o com o algoritmo histórico e só então reconstrói um GAFULL09 sem alterar saldos.

## Implementação

- `EconomicLegacyV8EcologicalState` preserva o compromisso histórico `GAECO-CHECKPOINT` versão 1 calculado sobre o hash v8 real (`LegacyWorldStateV8Hasher`), mais capital natural, parâmetros, mapeamento de mercadorias, reservas e cursores.
- `EconomicLegacyV8EcologicalSidecarCodec`: leitor/codificador estrito do formato original `GAECO1\r\n`; valida checksum, formato e digest de mundo **8**, versão de derivação, identidade de células e pontos, integridade do compromisso ecológico e re-encode byte a byte canônico.
- `EconomicLegacyV8CompleteSaveCodec`: leitor histórico `GAFULL08`, reconstrói o WorldState pelo codec v8 original e usa o leitor de sidecar **v8**, rejeitando documentos v9 rotulados incorretamente. A escrita histórica existe apenas para compatibilidade/testes; não autoriza novos saves gameplay v8.
- `EconomicV8ToV9EcologicalSaveMigrator.Migrate(generated, oldCompleteV8Bundle, independentlyAcceptedV8WorldHash, independentlyAcceptedV8EcologicalDigest)`:
  1. exige **duas âncoras de confiança independentes do arquivo**: hash do WorldState v8 e compromisso ecológico v8 já aceitos;
  2. verifica todo o GAFULL08 (envelope, hash histórico do mundo, sidecar, livros e compromisso);
  3. executa o migrador v8→v9 do G3-I3, sem inventar instalações e sem alterar trabalhadores, território, censo legado ou revisão;
  4. reutiliza os **mesmos livros imutáveis** de recursos naturais e materiais, políticas, mapeamento e cursores, sem reinicializá-los;
  5. cria novo checkpoint com hash do WorldState **v9**; o SHA-256 ecológico muda porque a versão/hash do mundo mudou, mesmo com estoques idênticos;
  6. serializa `GAFULL09`, decodifica novamente e compara o hash e o compromisso do novo arquivo.
- `EconomicV8ToV9EcologicalMigrationResult` informa digest antigo aceito, hash antigo, novo checkpoint e bytes completos para arquivamento pela camada de save.

## Garantias e limites

**Conservação:** madeira em pé, pedra superficial, vitalidade do solo, capacidades, reservas por ponto/mercadoria, cursores por civilização, último turno, taxa de regeneração, parâmetros e IDs são preservados exatamente. Nenhum saldo comercial é gerado a partir de vegetação que ainda está no terreno e nenhum item comprado é creditado sem entrega.

**Infraestrutura:** o arquivo v8 não carregava a seção 35, então a migração pode acrescentar somente `TransportFacilitiesRuntimeState.Empty`. Isso NÃO atesta que não existiam instalações em algum sistema externo. Para importar instalações históricas de outro sistema é exigido fluxo separado e atestado; nunca preencher a lista por suposição.

**Trabalhadores:** a população econômica continua sendo a dos trabalhadores, que produzem e geram demanda. O census legado é copiado por compatibilidade, não como segunda fonte de consumo.

**Versões:** preserva `CanonicalWorldStateHasher.FormatVersion = 9` e `EconomicEcologicalCheckpoint.CommitmentVersion = 1`. O SHA-256 do mundo v8 e o SHA-256 ecológico v8 são **verificados antes** de qualquer compromisso v9. Não substitui silenciosamente o digest antigo.

**Autoridade:** checksum e comprometimento não são assinatura digital. Se o chamador fornecer um hash antigo que ele mesmo inventou, o migrador não prova legitimidade. Um host confiável deve guardar as âncoras aprovadas externamente, decidir expressamente pela migração e registrar a relação irreversível entre digest antigo e novo. A camada atual ainda não executa essa transação durável.

## Testes G3-I5

`G3I5V8ToV9EcologicalMigrationTests.cs`:
- round-trip autêntico de `GAFULL08` e migração `GAFULL09` sem mudar nenhum campo ecológico nem reservas ou cursores;
- reproduzibilidade byte a byte ao repetir migração;
- hashes confiáveis de mundo/ecologia incorretos, versão errada e mundo de outra semente recusados;
- manipulação de payload ecológico com recomputação somente do checksum externo recusada;
- recusa cruzada entre leitores v8/v9 e proibição de esconder instalações em v8;
- continuidade de produção no turno seguinte à migração com estoque preservado e compromisso v9.

```sh
dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~G3I5V8ToV9EcologicalMigrationTests
```

**Aceite depende da CI do commit exato (kernel, hash, benchmark; Linux, Windows e macOS).**

## Próximo gate G3-I6

Um único head durável do **arquivo completo GAFULL09**, com vínculo imutável do predecessor v8, transação de aceitação/recovery de falhas, digest confiável e journal externo autenticado. Não fazer duas heads concorrentes (uma do mundo e outra da ecologia), nem declarar replay autoritativo antes de confirmar pagamento e entrega física das mercadorias.

**Nenhum merge de #49/#50/#64/#65/#66 ou main.**

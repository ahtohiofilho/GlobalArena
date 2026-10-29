# GA — G3-J3d: commit durável único de turno militar + ecológico v9

**Status:** integração candidata, sobre o PR #73, sem merge. O host `EconomicCompleteV9DurableSaveHost` continua sendo o único publicador do save completo `GAFULL09`; `CanonicalGameSession` não é publicado separadamente.

## Contrato do turno misto

`CommitMixed(expectedDigest, trustedContext, sealedMilitaryAuthorization, workerDemand, originHolds, historicalVerifier)` aplica sob o lock de escritor já existente:

1. compara o compromisso aceito do último `GAFULL09` (CAS) e lê a head real em disco; exige turno ecológico `LastAppliedTurn+1`;
2. materializa uma vez as entradas econômicas e copia defensivamente o certificado;
3. verifica `GAHMAC01` com a chave histórica confiável do host, turno e seed confiáveis, digest original e a execução determinística dos movimentos pelo motor do kernel;
4. constrói `WorldState` v9 alterado **somente** no domínio militar e combina-o com os livros anteriores de solo, madeira, pedra e reservas;
5. calcula a produção/demanda dos trabalhadores e as retenções físicas no **mesmo** turno; produz um único arquivo `GAFULL09` completo e verifica seu round-trip;
6. cria `GJMX0001`, journal imutável **misto**, encadeado ao SHA-256 do journal aceito anteriormente; rejeita um resultado cujo caminho já tenha journal ecológico;
7. grava/flush o bundle completo e journal misto no diretório local antes de substituir a **mesma head única** `full-v9-head.gahead` com digest de checkpoint, hash v9 do mundo alterado, predecessor, turno, sequência e hash do novo journal;
8. só então atualiza o checkpoint aceito na memória do host.

Se o processo cair depois de 6/7, antes da troca da head, os blobs são órfãos e **não alteram o último commit aceito**. Se a troca for publicada, a próxima restauração deve validar e reexecutar todos os turnos.

## Journal `GJMX0001`

`EconomicMixedV9TransitionRecord` contém turno, seed, compromisso do `GAFULL09` anterior e resultante, hash do journal anterior, bytes do **certificado HMAC militar histórico**, perfis de demanda por trabalhador e ordens de retenção voluntária. O codec grava big-endian e SHA-256 ao final, valida limites de contagem/tamanho e re-encode byte a byte. O archive `<checkpoint>.gamixed` é imutável e endereçado pelo digest do checkpoint; a head mantém o **SHA do registro** (que também cobre entradas que poderiam não mudar o estado).

Um `*.gamixed` e um `*.gajournal` para o mesmo checkpoint são ambíguos e causam rejeição no restore. Um mesmo encadeamento pode intercalar turnos ecológicos simples e turnos mistos. No verificador de replay:

- journal ecológico `GAJRN001`: mesmo hash v9 do mundo anterior/posterior; reexecuta resolvedor ecológico;
- journal misto `GJMX0001`: permite mudança do hash v9 do mundo **somente mediante certificado histórico válido**, reexecuta o comando do kernel e a evolução ecológica do turno, conferindo digest e **bytes completos** do bundle resultante.

A cadeia deve terminar na raiz zero do genesis ou checkpoint inicial oriundo de migração v8→v9. O `WorldState` anterior não precisa coincidir com o posterior em um turno misto; os demais domínios continuam protegidos pelo resolvedor militar e checksums v9.

## Confiança externa e restauração

Para restaurar um histórico que contenha `*.gamixed`, a chamada `Restore(..., historicalVerifier: seloComChaveHistórica, independentlyTrustedJournalRoot: raizAceitaForaDoDiretório)` deve receber:

- digest do `GAFULL09` atualmente aceito de fonte confiável independente;
- certificado/verificador com chave histórica do host obtida **fora do save**;
- raiz do journal aceita em fonte confiável independente.

Além disso, saves originados da migração v8 exigem as âncoras confiáveis antigas de `WorldState` v8 e compromisso ecológico v8. **Não basta consultar os próprios arquivos de disco para estabelecer essa confiança.**

O campo `AcceptedJournalSha256Hex` expõe a raiz para a camada externa armazenar de maneira protegida. Não é gravação transacional da âncora externa: em caso de crash entre publicar a head local e a âncora externa, é necessária reconciliação/recuperação segura com protocolo de *write-ahead* ou aprovação da origem externa. Não promover automaticamente arquivos locais adiantados a estado autorizado.

O formato físico da head permanece **v2** (PR #69); o conteúdo do journal é discriminado pela extensão e magic e ancorado no mesmo campo SHA-256 da head. Isso mantém os saves ecológicos existentes recuperáveis pelo mesmo leitor, mas os leitores anteriores a J3d **não entendem journals mistos** e devem falhar, não ignorá-los.

## Limites

- **Commit local e único**, não transação distribuída entre armazenamento, serviço de confiança e `CanonicalGameSession`. Não chamar um segundo `AuthoritativeTurnCommitGate` para a mesma sessão.
- A autorização militar é HMAC do **host**: não comprova identidade pública ou não repúdio do jogador. Credenciais são verificadas fora do simulador antes do selo; chaves antigas precisam continuar disponíveis sob proteção.
- Demandas e retenções econômicas do journal são vinculadas à raiz histórica e ao head, mas não recebem certificado individual de jogador. O host/administrador deve validar política de admissibilidade dessas entradas.
- É suportado exclusivamente o domínio de **movimentação militar estratégica** já implementado e o ciclo econômico-ambiental de referência. Não confirma conquista territorial, instalações novas, logística real, preço, pagamento ou entrega física por simples intenção.
- Um checkpoint aceito conserva uma única classe de trabalhadores produtores/consumidores. Censo legado é mantido por compatibilidade e não cria consumo adicional.
- Sem fsync de diretório garantido por todos os sistemas/hardwares, sem assinatura pública ou consenso multi-servidor. Recuperação de crash continua fail-closed e requer digest e journal root confiáveis.

## Testes

`GlobalArena.Tests/G3J3dMixedDurableV9CommitTests.cs` contém testes herdados de selos/autorização e cinco novos:

1. um turno misto confirmado e outro ecológico subsequente, com replay dos dois após reinício;
2. restauração sem chave histórica, sem raiz externa, com raiz divergente ou chave incorreta recusada;
3. journal misto ausente ou corrompido impede recuperar um turno misto confirmado;
4. CAS stale, salto de turno e repetição recusados sem publicar nova head;
5. bundle e journal mistos órfãos antes da troca de head não avançam a partida.

```sh
dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~G3J3dMixedDurableV9CommitTests
```

**Aceite** somente após kernel, hash e benchmark aprovados em Ubuntu, Windows e macOS no commit final.

**Nenhum merge autorizado.** `main`, PR #49, #50 e pilha G3 permanecem intactos.

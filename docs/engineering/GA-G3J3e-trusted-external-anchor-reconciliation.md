# GA — G3-J3e: reconciliação fail-closed de âncora externa v9

**Status:** gate candidato de recuperação, baseado no PR #74; **DRAFT e sem merge**.

## Problema

O commit local único de `GAFULL09` publica o bundle e o journal antes da head v2.
A confirmação em uma **origem confiável externa** não é atômica com essa publicação.
Após uma interrupção, é possível encontrar:

1. head local igual à âncora externa confirmada;
2. intenção de próximo commit já preparada externamente, mas a head ainda antiga;
3. head local avançada até o sucessor **previamente preparado**, ainda sem confirmação externa;
4. head local não autorizada pela âncora, arquivo adulterado ou rollback.

Nenhum arquivo local, nome de diretório, hash autodeclarado ou head passa a ser
autoridade só por existir.

## Contratos públicos

- `EconomicV9ExternalAnchor`: digest do checkpoint, raiz do journal, sequência e turno.
- `EconomicV9PreparedExternalAdvance`: vínculo estrito entre âncora confirmada e **um** próximo turno.
- `EconomicV9ExternalAnchorSnapshot`: leitura atômica fornecida por fonte externa confiável.
- `EconomicCompleteV9DurableSaveHost.InspectExternalReconciliation`: verifica integralmente o save e a cadeia de replay, sem modificar head, journal, bundle ou a origem externa.
- `EconomicCompleteV9DurableSaveHost.RestoreExternallyConfirmed`: permite reabrir apenas se **não houver prepare pendente** e head/âncora externa estiverem de acordo.

A inspeção retorna `Confirmed`, `PreparedWithoutLocalPublication` ou
`LocalPublicationAwaitingExternalConfirmation`. Os dois últimos estados
**não habilitam a retomada da simulação**.

## Regras de segurança e verificação

A fonte externa deve oferecer leitura consistente e CAS durável da âncora;
os seus dados **não podem** ser derivados da pasta de saves. O gate exige:

- digest, raiz do journal, sequência e turno da head iguais à âncora selecionada;
- se houver prepare, ele deve referir-se à âncora efetivamente confirmada, com
  sequência/turno incrementados em exatamente um;
- head local adiantada só é elegível se for o **mesmo sucessor** previamente
  autorizado na fonte externa;
- nesse caso, a head deve apontar diretamente para o checkpoint confirmado e
  o journal mais recente (`GAJRN001` ou `GJMX0001`) deve referir-se ao
  checkpoint e à **raiz do journal confirmada**, sem ambiguidade de tipos;
- `Restore` verifica bundle, cabeça, mundo, herança v8 quando aplicável,
  reexecução histórica e hashes de todos os journals até o genesis;
- turnos mistos continuam exigindo verificador HMAC histórico e raiz externa;
- mismatch, corrupção, salto de turno, prepare de outra base ou avanço local
  sem prepare causam rejeição.

A aquisição do lock de escritor ocorre na verificação completa. A head lida
antes do lock é comparada à head protegida pelo lock, rejeitando troca durante
a inspeção. O resultado é **diagnóstico**, não autorização autoexecutável.

## Playbook de recuperação para a aplicação/host

1. Obter snapshot de âncora e prepare **diretamente da fonte externa**.
2. Executar `InspectExternalReconciliation`, com chave HMAC histórica e
   âncoras v8 se aplicáveis.
3. `Confirmed`: reabrir via `RestoreExternallyConfirmed`.
4. `PreparedWithoutLocalPublication`: manter bloqueado, sem avançar head;
   o serviço externo deverá decidir explicitamente se cancela ou tenta
   novamente o prepare, sob CAS, depois de avaliar os arquivos.
5. `LocalPublicationAwaitingExternalConfirmation`: manter bloqueado;
   após verificação completa e aprovação do serviço de confiança, esse
   serviço poderá **finalizar por CAS** o prepare externo. Em seguida,
   obter snapshot externo novo, sem prepare, e reabrir pelo gate.
6. Falha em CAS, leitura não confiável ou estados divergentes: **fail-closed**,
   sem aceitar arquivos locais adiantados nem sobrescrever âncoras.

A finalização **não** é executada pelo gate. Em particular, um commit local
válido não é suficiente para forçar alteração da âncora externa.

## Limites e próximo gate

Esta entrega implementa **inspeção e admissão após confirmação externa**,
não o adaptador específico de armazenamento externo com CAS nem a criação
transacional de prepares antes de `CommitMixed`. A aplicação que usa o
`CommitMixed` legado continua responsável pelo protocolo externo; ele
não deve ser usado como se fosse transação distribuída. O próximo incremento
deve implementar um coordenador com *external prepare -> local publish ->
external finalize*, política de timeout, recuperação e testes de falha
injetada entre essas fases.

Não altera o formato head v2, `GAFULL09`, `GAJRN001` nem `GJMX0001`.
Não cria segundo publicador, não atualiza `CanonicalGameSession`, não fornece
assinatura pública nem afirma identidade de jogador. Entradas econômicas
exigem aprovação pela camada de aplicação.

## Testes

Os cenários G3-J3e foram adicionados a
`GlobalArena.Tests/G3J3dMixedDurableV9CommitTests.cs`:

- prepare misto sem head publicada;
- head mista adiantada com prepare externo e reabertura só após confirmação;
- avanço local sem prepare e tentativa de raiz externa divergente;
- corrupção do journal do sucessor preparado;
- mesmo princípio aplicado ao turno ecológico simples.

```sh
dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~G3J3dMixedDurableV9CommitTests
```

**Gate de aceite:** regressão de kernel, hash cross-platform e benchmark
aprovados em Ubuntu, Windows e macOS para o SHA final. **NÃO FAZER MERGE**
sem autorização expressa.

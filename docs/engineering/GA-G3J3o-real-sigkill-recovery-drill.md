# GA — G3-J3o: encerramento forçado real, inspeção forense e bloqueio de readmissão

**Incremento sobre o PR #84 (G3-J3n). DRAFT: sem merge e sem implantação.**

## Problema resolvido

G3-J3n injeta exceções, que executam blocos `finally`. **SIGKILL não
executa `finally`**, portanto pode deixar diretórios de quarentena com
ZIP original e arquivos parcialmente extraídos; depois de um rename,
o destino já existe mas a aplicação nunca retornou autorização para
o gameplay. Antes do G3-J3o, o método público
`EconomicCompleteV9DurableSaveHost.Restore` também não recusava
o marcador de destino publicado `.ga-v9-not-admitted`.

## Proteções implementadas

- `EconomicCompleteV9DurableSaveHost.Restore` passa a recusar
  `.ga-v9-not-admitted`, mesmo quando checkpoint/journal estão iguais
  à autoridade. A API pública de inspeção e
  `RestoreExternallyConfirmed` igualmente recusam o marcador pela
  mesma rotina central de replay. O método anterior de admissão para
  recuperação **sem** marcador permanece compatível.
- O `EconomicV9FailoverAdmissionGate` mantém uma continuação
  **privada** de publicação que pode reproduzir um save marcado
  durante a mesma operação estrita. Isso só ocorre após rechecagem
  de primário e testemunha, e a API pública não recebe um boolean
  que permita ignorar o marcador.
- A continuação readquire a lease de writer, valida de novo a cadeia
  GAFULL09 e as autoridades, e remove o marcador apenas após as
  verificações finais. Uma falha anterior deixa o marcador para
  impedir readmissão automática em outro processo.
- `EconomicV9VersionedRecoveryCrashInspector.Inspect` é
  **somente leitura**: reporta `Absent`, `Unadmitted`,
  `PresentWithoutMarker` e `UnsafeReparsePoint`, além de contar
  quarentenas interrompidas e identificar o arquivo de lease por
  destino. Um diretório sem marcador **não é certificado** como
  admitido: o operador continua obrigado a validar primário, epoch,
  witness, replay e fencing.

## Teste de interrupção real em Linux

`tools/ci/ga-v9-real-process-crash-drill.sh` é um supervisor Bash que:

1. Compila `GlobalArena.Tests` com .NET 10.
2. Para cada ponto crítico prepara um save ecológico GAFULL09 de
   um turno, com ZIP versionado e âncora persistidos.
3. Inicia `dotnet test` para `J3oCrashWorker` em um grupo de
   processos independente via `setsid`.
4. O testhost escreve `worker-ready` e faz `Flush(true)` **quando
   atinge o ponto de falha**; em vez de simular exceção, bloqueia.
5. O supervisor envia **`SIGKILL` ao grupo inteiro** e exige código
   de saída 137, demonstrando encerramento forçado do testhost.
6. Um **novo processo dotnet test** executa
   `J3oNewProcessInspectsCrashWithoutAdmittingWriter`, verifica
   estados de diretórios, origem preservada e rejeição de todas as
   APIs públicas de restore quando o destino está marcado.

São seis cutpoints: `ArchiveVerified`,
`InventoryExtracted`, `ReplayVerified`,
`BeforePublish`, `AfterPublish`, `BeforeAdmission`.

Antes do rename, o destino inexiste e há **uma quarentena órfã**.
Depois do rename, o destino é `Unadmitted` com o marcador, e
resta igualmente uma quarentena órfã (contendo o ZIP baixado).
`finally` do processo morto não é executado. Essas sobras
**não** são removidas automaticamente pelo inspetor e não
autorizam um novo host.

A validação de ZIP com assinatura ECDSA/retenção do PR #82
continua sendo coberta em workflow separado; no teste de processo
morto, a fonte de arquivo é injetada, para isolar a semântica da
interrupção do sistema operacional. Não afirmar que o drill valida
Object Lock real.

## Recuperação operacional depois do kill

1. **Não** usar o diretório publicado marcado, nem remover o
   marcador manualmente.
2. Consultar `EconomicV9VersionedRecoveryCrashInspector` e
   guardar cópia forense dos diretórios de quarentena órfãos.
3. Confimar isolamento/fencing do escritor anterior, lock
   de origem e consistência de primário + testemunha independente,
   usando os gates G3-J3k/l.
4. Selecionar um **novo nome de destino**, e realizar novamente
   o download da versão exata assinada e replay integral.
   Não recuperar de uma quarentena parcial deixada por SIGKILL
   sem novo comprovante externo de autenticidade.
5. Após sucesso, proceder à limpeza forense manual conforme política
   de retenção e auditoria. Não apagar os backups imutáveis.

## Limites claros

- `SIGKILL` prova o comportamento da aplicação diante de encerramento
  abrupto do processo em Linux e a inspeção após nova execução; não
  simula corte de energia, perda de cache do storage, falha do FS,
  persistência de `rename` após reboot, nem confirma `fsync`
  da entrada de diretório.
- A lease local só serializa aplicações cooperantes. Diretório pai e
  ancestrais devem ser protegidos por ACL/isolamento de volumes; esta
  entrega não resolve um adversário com write/rename no diretório pai.
- `Internal` + `InternalsVisibleTo` são para o gate de testes:
  operadores de produção não devem distribuir assemblies de teste.
- Um marcador removido maliciosamente por quem controla o filesystem
  não pode ser autenticado pelo host; a autoridade externa e o replay
  continuam obrigatórios. O sistema ainda não implementa um
  daemon de recuperação com aprovação humana nem Object Lock real.
- O teste de SIGKILL é exclusivo do Linux; gates Windows e macOS
  continuam cobrindo kernel/hash/benchmark, não interrupção forçada.

**Estado:** CI é requisito para considerar G3-J3o validado. PR em
rascunho, `main` intacta.

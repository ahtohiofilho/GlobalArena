# Global Arena — G3-J3p: custódia supervisionada de quarentenas órfãs

**Base:** PR #85 (G3-J3o). **Sem merge, `main` não modificada.**

## Objetivo e limite de autoridade

Uma recuperação interrompida por SIGKILL deixa diretórios
`.ga-j3m-quarantine-<hex32>` no parent de recuperação, contendo o
ZIP original, arquivos extraídos e eventualmente cabeçalhos locais.
O G3-J3o só os inventariava e proibia readmissão implícita.

O G3-J3p acrescenta `EconomicV9SupervisedOrphanCustody`, um caminho
**supervisionado, explicitamente autorizado e somente de ARQUIVAMENTO
FORENSE**. Ele não retoma partidas, não seleciona save, não remove o
marcador `.ga-v9-not-admitted`, não recicla ZIP temporário como backup
verificado e **não apaga** diretórios de jogo. Para voltar a jogar é
obrigatório fazer nova recuperação completa por G3-J3m/n/o e verificar
primário, witness, epoch, fencing, recibo ECDSA, inventário e replay.

## Protocolo de autorização

O operador de resposta a incidentes escolhe um `CaseId` canônico
`case-<32 hex minúsculo>`, uma identidade de partida, um epoch de
escritor anterior e uma quarentena específica. `Plan(...)` calcula
recursivamente um digest SHA-256 do inventário dos arquivos: caminhos
relativos, tamanho e SHA de cada arquivo (ordenados), além de contagem
e tamanho total. Os limites são 1024 arquivos, 512 MiB totais, 256 MiB
por arquivo e no máximo três níveis de subpastas. Reparse points/
symlinks são recusados.

O plano de custódia contém:

- `CaseId`, `GameId`, `FormerWriterEpoch`;
- `QuarantineName` exato (prefixo e GUID de origem);
- `VaultName`, **obrigatoriamente igual ao CaseId**;
- `TreeSha256Hex`, `FileCount`, `TotalBytes`.

A autorização `EconomicV9OrphanCustodyApproval` acrescenta
`OperatorId`, `IssuedAtUnixSeconds`, `ExpiresAtUnixSeconds` e
`SignatureBase64`. O operador ou serviço independente assina
`EconomicV9SupervisedOrphanCustody.EncodeApproval` com ECDSA P-256
SHA-256 (assinatura DER). O payload binário usa `BinaryWriter`, domínio
`GA-V9-ORPHAN-ARCHIVE-1` e os campos na ordem especificada pela API,
**sem incluir SignatureBase64**. A aplicação recebe somente a chave
pública do operador **pré-aprovada**. A chave privada não fica junto ao
host de gameplay ou aos bancos PostgreSQL.

As janelas de autorização são no máximo **10 minutos**. Uma assinatura
para outra partida, caso, epoch, arquivo, versão do conteúdo ou operador
não pode ser reaproveitada para uma ação diferente. A autorização permite
exclusivamente **mover a quarentena para o cofre forense**: nunca
admitir, reconstruir, sobrescrever, limpar ou reverter save.

## Sequência fail-closed

1. Provisionar um diretório pai de recuperação protegido por ACL e,
   nele, um cofre forense separado preexistente (por exemplo,
   `.ga-j3p-vault`). Ambos devem ser diretórios, não symlinks.
   O cofre é subdiretório do mesmo parent, de modo que o movimento seja
   um rename no mesmo filesystem.
2. Inspecionar com `EconomicV9VersionedRecoveryCrashInspector` e
   confirmar que não se trata de save ativo. O operador pausa workers,
   decide o incidente e verifica a prova de isolamento pelo contrato
   `IEconomicV9FormerWriterIsolationVerifier` conectado ao
   orquestrador confiável de fencing (ver G3-J3l).
3. Calcular `Plan`, obter a aprovação assinada fora da aplicação e
   chamar `ArchiveOnly(approval)`.
4. A rotina revalida assinatura, identidade, prazo, digest, limites,
   fencing independente e lease exclusiva por `CaseId`.
5. O sink `IEconomicV9OrphanCustodyAuditSink` registra `Intent`
   **sincronamente antes** do rename. Se o sink falhar, **não mover**.
6. Checar novamente isolamento e hash, mover o diretório para
   `<parent>/.ga-j3p-vault/<CaseId>` via `Directory.Move` (nunca
   cópia/remoção do conteúdo), revalidar seu digest e registrar
   `Completed`. Somente então retornar `Archived`.

Uma interrupção após `Intent` deixa a origem presente: repetir
`ArchiveOnly` **com a mesma aprovação ainda válida** revalida o
arquivo e pode concluir. Uma interrupção após o rename deixa o destino
no cofre com origem ausente: repetir com a mesma aprovação resulta em
`ReconciledAfterInterruption` e evento `Reconciled`, sem segundo
movimento. Qualquer presença simultânea de origem e destino, ambos
ausentes, alteração de digest, falta de fencing, prazo expirado ou
falha no auditor gera recusa. Não há rollback ou limpeza automática.

## Rastreabilidade

A interface `IEconomicV9OrphanCustodyAuditSink` recebe entradas
`Intent`, `Completed` e `Reconciled`, incluindo caso, partida,
quarentena, destino, digest, operador e timestamp. O sink de referência
`EconomicV9LocalOrphanCustodyAuditSink` anexa linhas JSON e chama
`FileStream.Flush(flushToDisk: true)` antes de confirmar cada evento.

**Este arquivo local não é WORM, tamper-proof nem autoridade
independente.** Para operação real, substituir o sink por coletor
independente com ACK durável, identidade autenticada, auditoria
imutável e privilégios separados, além de retenção do próprio cofre.
Se houver crash entre rename e confirmação do evento `Completed`,
o estado físico e o `Intent` permitem reconciliação manual.

## Exercício com SIGKILL real em CI

O workflow `ga-supervised-orphan-custody.yml` executa
`tools/ci/ga-v9-supervised-custody-sigkill.sh` em Ubuntu com .NET 10.
Para cada ponto:

- `IntentPersisted`: o processo de teste chega após o `Intent`
  fsynced e antes do `Directory.Move`;
- `DirectoryMoved`: o teste chega após o rename e antes do evento
  `Completed`.

O testhost escreve um marcador próprio via `Flush(true)`,
o supervisor manda `SIGKILL` ao grupo de processos `setsid`,
exige exit code 137 e reinicia **outro** processo .NET para verificar
log/arquivos e reconciliar usando a assinatura original. Também testa
negação por assinatura falsa/expirada, conteúdo alterado, ausência
de fencing, destino já ocupado, symlink e falha do sink antes/depois
do rename. Kernel/hash/benchmark cross-platform e todos os gates
PostgreSQL/HA/backup continuam habilitados.

**A prova é encerramento real de processo + persistência observável
no mesmo filesystem entre processos. Não simula queda de energia,
reboot do kernel, perda de cache, persistência de metadados de rename
após power loss ou Object Lock real.** Não confundir `Flush(true)`
com transação entre ledger e filesystem ou com garantia física de
discos/controladoras. Nenhum segredo nem chave privada de produção
deve aparecer nos artefatos da CI.

## Operação após custódia

Guardar a aprovação, snapshot do inventário, eventos de auditoria,
estado do witness e evidência de fencing no incidente. Não reabrir o
cofre como save. Para tentar recuperação, provisionar **um novo nome**
de destino, buscar novamente o backup independentemente assinado e
executar o replay completo; somente após sucesso liberar a partida.
A exclusão futura do material forense exige política de retenção e
autorização separadas, **não fornecidas por este PR**.

**PR rascunho, sem merge e sem alteração em `main`.**

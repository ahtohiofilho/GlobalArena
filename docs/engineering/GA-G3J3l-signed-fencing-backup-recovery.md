# GA — G3-J3l: evidência operacional assinada, verificação de backup e admissão restrita

**Status:** candidato ao PR G3-J3l, sobre o PR #81. **Sem merge.**

## Finalidade

O G3-J3k deixou propositalmente o isolamento físico como contrato.
Este incremento disponibiliza **adaptadores HTTPS com verificação ECDSA P-256**
para dois domínios operacionais independentes: o **orquestrador de fencing**
e o **serviço de backups versionados com retenção imutável**.

**Nenhum endpoint externo, equipamento de fencing, armazenamento WORM,
Object Lock ou credencial de produção é criado ou configurado por este PR.**
As integrações são executáveis com serviços externos que respeitem os contratos
abaixo. As respostas de testes usam provedores HTTP controlados, não uma
infraestrutura de produção.

## Fencing assinado por desafio

`EconomicV9HttpSignedFencingVerifier` implementa
`IEconomicV9FormerWriterIsolationVerifier` do G3-J3k. Usa HTTPS,
chave pública **fixada fora do primário e do witness**, `HttpClient`
fornecido pelo host e `TimeProvider`. Cada consulta é um GET ao endpoint
imutável configurado, com parâmetros `gameId`, `formerEpoch` e
`challenge` (32 bytes aleatórios / 64 caracteres hexadecimais).

Exemplo do contrato de resposta JSON:

```json
{
  "ClusterId": "cluster-production",
  "GameId": "game-123",
  "FormerWriterEpoch": 8,
  "ChallengeHex": "64-HEXADECIMAL-CHARACTERS",
  "IsolationEventId": "fence-event-581",
  "FormerPrimaryFenced": true,
  "FormerGameWritersFenced": true,
  "IssuedAtUnixSeconds": 1793300000,
  "ExpiresAtUnixSeconds": 1793300030,
  "SignatureBase64": "DER-ECDSA-P256-SIGNATURE-BASE64"
}
```

O orquestrador autenticado deve **verificar de verdade** que o primário
anterior não pode receber escrita e que as sessões escritoras antigas
foram desativadas por mecanismo independente (por exemplo, desligamento
e isolamento confirmado de volume/rede, fencing externo a cluster).
Só então assina a declaração. Um mero `ping` negativo, um boolean
fornecido pelo cliente ou um aviso de desligamento sem evidência
operacional não é suficiente. A chave privada não pode estar no game host
nem no PostgreSQL principal.

O verificador exige identidade exata da partida, cluster e epoch,
ambas as alegações de isolamento como verdadeiras, nonce **único por
requisição**, janela de validade curta (até 60 s), relógio coerente e
assinatura DER ECDSA P-256 SHA-256. Recusa HTTP, troca de origem,
certificados TLS inválidos, assinaturas adulteradas, indisponibilidade
e qualquer resultado inconclusivo. Exige novas respostas a cada
checagem em `Rotate(N)`, não recicla recibos.

O payload binário assinado é gerado por
`EconomicV9OperationalProofCodec.EncodeFencing`: um `BinaryWriter`
.NET (strings UTF-8 com prefixo de comprimento) contendo separador
`GA-V9-FENCING-1` e todos os campos **exceto SignatureBase64** na
ordem do record. O orquestrador deve usar a mesma canonicalização ou
um SDK versionado dessa especificação. A assinatura não certifica a
segurança do orquestrador; ela só autentica a declaração emitida.

## Comprovante de backup versionado

`EconomicV9SignedImmutableBackupGate` consulta dois endpoints HTTPS,
mesma origem autenticada:

- GET `manifest?gameId=...&epoch=...&checkpoint=...` retorna
  `EconomicV9ImmutableBackupReceipt`, assinado por **chave pública
  de backup independente**.
- GET `archive?gameId=...&backupId=...&version=...` retorna os bytes
  da versão **exatamente identificada** no recibo.

O recibo contém `GameId`, `WriterEpoch`, `Anchor` completa,
`BackupId`, `ObjectVersionId`, `ArchiveSha256Hex`,
`ArchiveLength`, `RetentionLocked`, `RetentionUntilUnixSeconds`,
`IssuedAtUnixSeconds` e `SignatureBase64`.

A verificação exige igualdade com o epoch e a âncora da testemunha,
assinatura ECDSA P-256 DER válida sobre
`EconomicV9OperationalProofCodec.EncodeBackup` com domínio
`GA-V9-BACKUP-1`, comprovante recente (até 24 h), afirmação de
retenção imutável por pelo menos **mais 24 h**, identificador da versão
e SHA-256 dos bytes reais do arquivo obtido via stream. O limite é
256 MiB por objeto. Qualquer corrupção, tamanho diferente, retenção
insuficiente, versão incorreta, perda de serviço ou erro de assinatura
**nega a operação**.

**Atenção:** `RetentionLocked` é uma declaração assinada do
administrador de backup, **não é prova autônoma de Object Lock real**.
O emissor do recibo deve consultar metadata do provedor, validar
Object Lock/Legal Hold, governança, retenção e versão específica e
restringir o direito de liberar retenção. O arquivo é **verificado por
hash mas não extraído nem adotado automaticamente** como save. O
replay do `GAFULL09` local continua obrigatório.

## Recuperação estrita após promoção

`EconomicV9FailoverAdmissionGate.RestoreAfterPromotionWithVerifiedBackup`
é a nova entrada de produção que:

1. Exige `IEconomicV9AcknowledgmentWitness` com `WriterEpoch`
   independente, não só uma leitura de checkpoint sem geração.
2. Confere o primário promovido contra a âncora reconhecida e recusa
   qualquer prepare pendente.
3. Exige verificação criptográfica e de bytes do backup da **mesma
   partida, epoch e checkpoint** antes de restaurar qualquer save.
4. Relê primário e witness após a validação; divergência bloqueia.
5. Executa o `RestoreAfterPromotion` existente, que verifica toda
   a cadeia local, replay e lease exclusiva do writer.
6. Relê ambos os serviços novamente após o replay; se qualquer um
   mudou, descarta o host e libera a lease.

`RestoreAfterPromotion` permanece como API legada por compatibilidade.
**É responsabilidade da composição de produção restringir exclusivamente
o fluxo de entrada a `RestoreAfterPromotionWithVerifiedBackup`**.
O código não fornece imposição global de política nem um gateway de
infraestrutura. A existência do método novo não impede um consumidor
de chamar o método antigo.

## Exemplo de composição

```csharp
using var fencing = new EconomicV9HttpSignedFencingVerifier(
    operatorHttpClient, new Uri("https://operator.example/v1/fencing/evidence"),
    trustedOperatorPublicKeyPem, trustedClusterId);

var rotation = new EconomicV9PostgresWriterEpochRotation(
    primaryAdminDataSource, witnessAdminDataSource, fencing, gameId);
// rotation.Rotate(oldEpoch); SOMENTE após isolamento e parada do gameplay.

using var immutableGate = new EconomicV9SignedImmutableBackupGate(
    backupHttpClient,
    new Uri("https://backup-control.example/v1/manifest"),
    new Uri("https://backup-control.example/v1/archive"),
    trustedBackupPublicKeyPem);

using var recovered = failoverAdmission.RestoreAfterPromotionWithVerifiedBackup(
    localSaveDirectory, generatedWorld, immutableGate,
    independentlyTrustedLegacyWorldHash,
    independentlyTrustedLegacyEcologicalDigest, historicalVerifier);
```

As duas chaves públicas devem estar **pré-aprovadas/pinadas**, com
mudança somente por política de segurança, não baixadas do próprio
endpoint sob verificação. Configurar `HttpClient` com TLS validado,
sem bypass de certificado, sem redirecionamento a origem desconhecida,
e contas de aplicação separadas. Evitar dados confidenciais em URLs
e não registrar conteúdo das credenciais de acesso.

## Gate CI / falhas negativas

`G3J3lSignedOperationalEvidenceTests`: atestação fresca e válida,
replay de nonce rejeitado, epoch/game distintos, relógio expirado,
assinatura inválida, alegação de writer não isolado e falha de rede.
Também valida hash de arquivo versionado, corrupção de bytes,
retenção falsa ou curta, assinatura de backup inválida e âncora
de partida antiga.

`G3J3lBackupGatedRecoveryTests`: verifica que o gate de backup bloqueia
o replay quando a prova é recusada, que uma mudança de geração no witness
interrompe o processo e que a entrada estrita aceita uma cadeia local
válida sem perder a lease.

Demais gates permanecem: duas instâncias PostgreSQL independentes,
failover físico com replicação síncrona, kernel/hash/benchmark nos
três sistemas operacionais.

## Limites de produção / próximos passos

- O PR **não** opera um servidor de fencing: um integrador deve
  implementar o endpoint, a política física e o assinador.
- O PR **não** opera armazenamento imutável: um integrador deve
  implementar o serviço HTTPS de metadata, a emissão de recibos e
  Object Lock real.
- A assinatura não elimina risco de **assinador comprometido**,
  downgrade na composição, manipulação da identidade de cluster,
  comprometimento do controle de backups ou rotação indevida de
  chaves públicas.
- A janela de verificação é temporal, e **não constitui lease ou
  quórum contínuo** após a retomada. O ambiente precisa manter
  fencing efetivo enquanto novos commits acontecerem.
- O objeto backup é conferido integralmente mas sua estrutura interna
  não é automaticamente restaurada. Para recuperação a partir de
  arquivos baixados será necessária etapa posterior de extração segura,
  inventário de arquivos e replay sobre pasta isolada.

**DRAFT, SEM MERGE. A `main` permanece intacta.**

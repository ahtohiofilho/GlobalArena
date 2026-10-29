# Global Arena — G3-J3q: livro de auditoria independente com comprovantes ECDSA

**Milestone empilhado sobre o PR #86 (G3-J3p). Em draft, sem merge.**

## Objetivo

O sink local de G3-J3p usa JSONL e `Flush(true)`, porém não é
independente do operador da máquina; não garante retenção WORM nem
integridade contra administrador do filesystem. G3-J3q disponibiliza
`EconomicV9HttpSignedCustodyAuditSink`, um adapter de rede para um
**livro de auditoria operacional mantido fora da infraestrutura do jogo**.

O adapter implementa a interface J3p
`IEconomicV9OrphanCustodyAuditSink.Append`; assim, a própria ordem
da operação de custódia continua:

1. Receber autorização ECDSA do operador e verificar fencing externo.
2. Enviar `Intent` e **exigir comprovante remoto válido** antes de
   mover a quarentena.
3. Verificar novamente isolamento e hash; renomear a origem para o
   cofre forense no mesmo filesystem.
4. Enviar `Completed` e exigir novo comprovante remoto antes de
   reportar `Archived`.
5. Se o processo morreu após o rename ou o comprovante `Completed`
   falhou, tentar `Reconciled` apenas com a autorização original ainda
   válida, o mesmo digest e uma nova confirmação de fencing.

Se `Intent` falhar, o diretório **não é movido**.
Se `Completed` falhar, o destino **permanece no cofre forense**,
sem apagar o conteúdo, sem limpar marcadores de saves e sem
reportar custódia concluída. Não se tenta rollback do movimento.

## API HTTPS (serviço externo a implementar)

Provisionar URI fixa HTTPS, sem query, fragmento ou credenciais
embutidas. Host e chave pública de assinatura são pré-aprovados
por configuração separada do host de gameplay. O mesmo processo
não deve custodiar a chave privada do emissor.

`POST /v1/custody/append`, `Content-Type: application/json`,
com JSON `EconomicV9CustodyAuditAppendRequest`:

```json
{
  "LedgerId": "ga-independent-ledger",
  "Entry": {
    "CaseId": "case-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
    "Event": "Intent",
    "GameId": "j3p-game",
    "QuarantineName": ".ga-j3m-quarantine-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
    "VaultName": "case-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
    "TreeSha256Hex": "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
    "OperatorId": "operator-incident-17",
    "RecordedAtUnixSeconds": 1790000000
  },
  "ChallengeHex": "64 UPPERCASE HEX CHARACTERS"
}
```

O serviço deve autenticar o cliente, validar autorização de escrita e
identidade da partida/caso, registrar o evento **antes** de assinar,
confirmar persistência em append-only audit storage independente e
verificar seu próprio mecanismo de retenção. O controller não pode
emitir `DurablyCommitted: true` ou `RetentionLocked: true` sem
ter verificado esses fatos no provedor real.

Resposta `200 OK`, JSON `EconomicV9CustodyAuditReceipt`:

```json
{
  "LedgerId": "ga-independent-ledger",
  "EntrySha256Hex": "SHA256 DO EVENTO CANONICO, 64 HEX",
  "ChallengeHex": "MESMO DESAFIO DO POST",
  "AppendId": "receipt-000293",
  "Sequence": 293,
  "DurablyCommitted": true,
  "RetentionLocked": true,
  "IssuedAtUnixSeconds": 1790000001,
  "RetentionUntilUnixSeconds": 1790600000,
  "SignatureBase64": "DER ECDSA P-256 BASE64"
}
```

O cliente valida identidade do ledger, SHA-256 exato do evento enviado,
nonce aleatório **novo por evento**, `AppendId` não vazio,
sequência positiva, alegações de escrita durável e retenção positiva,
timestamp assinado recente (até 60 s para trás, até 5 s adiante),
retenção restante superior a 24 h, assinatura DER ECDSA P-256/
SHA-256 e resposta vinda da URI HTTPS esperada. Falha de DNS/TLS,
indisponibilidade, timeouts, downgrade/redirect, assinaturas inválidas,
prova expirada ou event hash alterado bloqueiam `Append`.
Timeout é 8 s por chamada. A chave pública precisa ser fixada fora
do serviço que está sendo consultado.

### Canonicalização e domínios

`EconomicV9CustodyAuditProofCodec.EncodeEvent` usa `BinaryWriter`
com domain separator `GA-V9-CUSTODY-AUDIT-EVENT-1`, seguido de
`CaseId, Event, GameId, QuarantineName, VaultName, TreeSha256Hex,
OperatorId, RecordedAtUnixSeconds`. O `EntrySha256Hex` é SHA-256
hex maiúsculo desses bytes.

`EncodeReceipt` usa domain separator
`GA-V9-CUSTODY-AUDIT-RECEIPT-1`, seguido de
`LedgerId, EntrySha256Hex, ChallengeHex, AppendId, Sequence,
DurablyCommitted, RetentionLocked, IssuedAtUnixSeconds,
RetentionUntilUnixSeconds`. A assinatura é sobre esses bytes,
sem `SignatureBase64`. Formato `BinaryWriter` .NET: strings
UTF-8 com comprimento em 7-bit, inteiros little-endian.

Essa assinatura **atesta a declaração assinada**, e não constitui,
sozinha, garantia física de WORM ou prova de que o serviço nunca
escreveu outros eventos falsos. Um integrador deve implementar
ledger/WORM real com chave privada guardada externamente e controles
de acesso de produção (por exemplo, mTLS ou outro mecanismo de
autenticação configurado no `HttpClient`), controle de versões,
auditoria de operações administrativas, retenção imutável e
recuperação de recibos por `AppendId` independente da aplicação.

### Ambiguidade e idempotência

Se o servidor fizer commit, mas o recibo não chegar (timeout),
o cliente **não pode presumir sucesso**. Requisições futuras usam
um novo nonce e exigem um novo recibo assinado. O serviço
de produção deve implementar uma chave lógica de idempotência
por caso, evento e conteúdo e registrar eventual repetição;
uma resposta antiga não pode ser reciclada, pois seu nonce será
rejeitado. O adapter por si só **não verifica monotonicidade global
nem prova completude da sequência**: é preciso auditar periodicamente
a sequência do ledger e comparar com os eventos dos incidentes
fora do host de jogo.

## Composição recomendada

```csharp
using var remoteAudit = new EconomicV9HttpSignedCustodyAuditSink(
    ledgerHttpClientWithAuthenticatedTls,
    new Uri("https://independent-ledger.example/v1/custody/append"),
    "ga-independent-ledger",
    independentlyPinnedLedgerP256PublicKeyPem);

using var custody = new EconomicV9SupervisedOrphanCustody(
    trustedRecoveryParent, ".ga-j3p-vault",
    pinnedIndependentOperatorApprovalKeyPem,
    independentFencingVerifier,
    remoteAudit);

// Apenas com aprovação assinada e vigente, jamais automaticamente:
var result = custody.ArchiveOnly(operatorSignedApproval);
```

`EconomicV9LocalOrphanCustodyAuditSink` permanece no código para
laboratório e sites legados. **Não usar somente o sink local para
atestar independência ou proteção contra adulteração em produção.**
Não é possível forçar a substituição global da composição do host
somente ao disponibilizar um novo adapter.

## Validação

Os testes `J3q` utilizam `HttpMessageHandler` isolado e chaves
ECDSA de testes para simular um ledger externo:

- `Intent/Completed/Reconciled` com nonce distinto e recibos válidos;
- assinatura com chave errada, assinatura adulterada e SHA de evento
  não correspondente;
- ledger incorreto, desafio reutilizado/trocado, data expirada;
- `DurablyCommitted=false`, `RetentionLocked=false` ou retenção
  insuficiente;
- HTTP 503, indisponibilidade do transporte e URI alterada;
- sink remoto negado **antes** do rename, impedindo o movimento;
- falha do recibo `Completed` **após** o rename, exigindo
  reconciliação e comprovante novo.

O novo gate
`.github/workflows/ga-independent-custody-audit.yml` exige esses
testes no Ubuntu .NET 10. Permanecem todos os gates G3-J3g/p e
regressões cross-platform.

**Não há serviço WORM em produção implantado neste PR**, nem
credenciais reais, certificado cliente, gestor físico de fencing ou
prova de retenção do provedor. As respostas dos testes são
assinadas pelo serviço simulado, não pela infraestrutura real.
Não realizar merge ou ativação de produção sem essas integrações.

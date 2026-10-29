# Global Arena — G3-J3m: recuperação de GAFULL09 por backup versionado

**Entrega proposta:** PR empilhado sobre o G3-J3l (#82), aberto como *draft*, sem merge.

## Lacuna fechada

O G3-J3l validava assinatura, retenção declarada e SHA-256 do arquivo
versionado, mas descartava seus bytes: um caminho separado de importação
poderia buscar outro objeto ou extrair um arquivo sem segurança. O J3m
fornece o caminho integrado **um único download autenticado → quarentena
→ inventário e digest de cada arquivo → replay → publicação por rename →
admissão**, sem sobrescrever nenhum save existente.

## API e fluxo

- `IEconomicV9VerifiedBackupArchiveSource.TryDownloadVerifiedArchive(..., Stream)`
  retorna verdadeiro **somente após** a assinatura ECDSA P-256 do recibo,
  versão exata, comprovante de retenção e SHA-256 do ZIP integral terem
  sido validados. O adapter existente
  `EconomicV9SignedImmutableBackupGate` implementa essa interface.
  A operação escreve bytes inicialmente **não confiáveis** numa quarentena;
  um retorno falso/erro exige descartar tudo. A API legada
  `VerifyBackup` continua disponível e usa o mesmo validador com
  `Stream.Null`.
- `EconomicV9VersionedSaveArchive.WriteArchive` cria um ZIP com
  `ga-v9-inventory.json` canônico e arquivos do save GAFULL09.
  Somente arquivos regulares e nomes ASCII **planos** são aceitos;
  `.full-v9-writer.lock` é excluído. O inventário descreve o nome,
  comprimento e SHA-256 de **cada arquivo**, ordenados de forma
  determinística.
- `ExtractVerifiedArchive` recusa links simbólicos, diretórios,
  ZIP-slip/`../`, barras, caminhos absolutos, nomes duplicados mesmo
  com caixa diferente, itens fora do inventário, arquivo de head ausente,
  JSON não canônico e digests ou comprimentos divergentes. Limites:
  ZIP de até **256 MiB**, até **512 arquivos**, até **256 MiB por
  arquivo** e **512 MiB descomprimidos**. A extração ocorre numa
  pasta privada e vazia, nunca no diretório ativo.
- `EconomicV9FailoverAdmissionGate.RestoreFromVerifiedVersionedBackup`
  verifica `WriterEpoch` e a âncora do witness independente e
  a ausência de prepare no primário promovido **antes** do download.
  Exige recuperação em **novo caminho** dentro de diretório pai já
  provisionado e protegido por ACL; jamais sobrescreve ou apaga o
  save ativo. Faz download uma só vez, extrai e reexecuta integralmente
  o GAFULL09 numa pasta de quarentena (incluindo validação HMAC de
  comandos mistos quando aplicável).
- Antes da publicação, relê witness e primário e exige igualdade
  exata da geração, sequência, turno, SHA do checkpoint e raiz do
  journal. Libera a lease temporária, publica a pasta de replay
  por `Directory.Move` no mesmo sistema de arquivos, relê os
  compromissos e adquire novamente a lease exclusiva do diretório
  promovido. Se algo divergir, **não libera o host para gameplay**.
  Em falha após a publicação, a nova pasta permanece como artefato
  não admitido para inspeção, sem substituir o save anterior.

### Exemplo

```csharp
using var backupGate = new EconomicV9SignedImmutableBackupGate(
    backupHttpClient,
    new Uri("https://backup-control.example/v1/manifest"),
    new Uri("https://backup-control.example/v1/archive"),
    pinnedBackupSigningPublicKeyPem);

using var recovered =
    failoverGate.RestoreFromVerifiedVersionedBackup(
        "/trusted/recovery/game-123-new", generatedWorld, backupGate,
        independentlyTrustedLegacyWorldHash,
        independentlyTrustedLegacyEcologicalDigest,
        historicalMilitaryVerifier);
```

O workflow de publicação deve primeiro encerrar instâncias escritoras,
atestar fencing físico e rotação de epoch pelos fluxos G3-J3k/l.
**O código de recuperação não efetua fencing de infraestrutura sozinho**.

## Formato do arquivo

`ZipArchive` com `ga-v9-inventory.json` na raiz:

```json
{
  "FormatVersion": 1,
  "Files": [
    {
      "Name": "full-v9-head.gahead",
      "Length": 264,
      "Sha256Hex": "64-CHAR-UPPERCASE-SHA256"
    }
  ]
}
```

O exemplo ilustra apenas a estrutura, não um inventário executável.
Cada outro arquivo de bundle e journal da cadeia deve estar incluído
no mesmo ZIP e inventário. O serviço externo de backups deve
calcular e assinar o digest **do ZIP completo**, sua versão
imutável e sua retenção real em armazenamento com Object Lock.
Uma alteração posterior invalida o recibo, ainda que o inventário
interno tenha sido manipulado.

## Gates e limites

Os testes `J3m` cobrem restauração de journal ecológico de um turno,
recusa de destino existente, quarentena no erro, ZIP-slip, arquivo extra
não inventariado, download HTTP com recibo real ECDSA e corrupção
dos bytes do ZIP. Permanecem kernel/hash/benchmark cross-platform,
PostgreSQL + witness, laboratório de failover físico e testes G3-J3l.

**Não inclui** provisionamento de Object Lock, serviço externo de
assinatura, backup de produção real, certificado de integridade de
filesystem do diretório pai ou STONITH de cluster. O inventário
não substitui o replay GAFULL09: um ZIP assinado pode conter um
journal semanticamente inválido e será rejeitado pelo replay.

O formato é intencionalmente **flat** nesta versão; arquivos que
exijam diretórios aninhados precisam de nova versão de formato
validada. Não há recuperação sobre diretório ativo, e falhas
remotas provocam indisponibilidade de recuperação (*fail-closed*),
não regressão automática de sequência.

**DRAFT — manter PRs anteriores abertos e não fazer merge na main.**

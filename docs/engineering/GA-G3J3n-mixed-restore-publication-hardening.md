# GA — G3-J3n: recuperação mista, falhas de publicação e isolamento do destino

**Incremento do PR #83 (G3-J3m), sem merge.**

## Motivação

O G3-J3m recupera um GAFULL09 do ZIP versionado, inventaria e reproduz a cadeia de journals antes de publicar uma pasta nova. O J3n cobre duas classes de falha adicionais: **turnos mistos** com comandos militares historicamente autenticados e **interrupções/concorrência** nos limites de publicação.

## Reforços de execução

- `RestoreFromVerifiedVersionedBackup` utiliza uma **lease de arquivo com FileShare.None por nome de destino**, calculada sobre SHA-256 do nome final e mantida durante download, replay, publicação e readmissão. Isso serializa ferramentas de recuperação cooperantes no mesmo filesystem. O nome da lease fica no diretório pai.
- A recuperação rejeita **diretório-pai com atributo ReparsePoint** (symlink/junction). O diretório pai e todos seus ancestrais devem ser provisionados por operador e protegidos de escrita não confiável. **Um processo com acesso arbitrário ao filesystem ainda pode substituir caminhos fora da lease**, pois ela não é STONITH nem solução contra usuários com privilégio de escrita no pai.
- Depois do replay bem-sucedido, **antes** de publicar, o fluxo cria `.ga-v9-not-admitted` dentro do diretório em quarentena. Esse marcador acompanha o rename atômico. Somente depois de readquirir a lease de writer, repetir o replay no caminho publicado, conferir epoch/witness/primário e verificar ausência de divergência o marcador é apagado.
- Uma falha **antes** de `Directory.Move` mantém o destino ausente e descarta a quarentena. Se ocorre **depois** da publicação, o destino fica como artefato forense marcado **não admitido**, não é substituído nem entregue como host válido. O operador deve inspecionar manualmente; a API estrita recusa reutilizar diretório existente.
- A aplicação de produção deve conferir esse marcador na composição operacional e **nunca abrir o save por caminhos legados**. O marcador não é uma trava criptográfica: um administrador de filesystem malicioso pode apagá-lo, e a API legada do host ainda existe. A defesa de autoridade definitiva continua sendo o witness externo e replay.

## Testes com checkpoints determinísticos

Apenas em assembly de testes (`InternalsVisibleTo("GlobalArena.Tests")`), a implementação fornece um `IEconomicV9VersionedRecoveryPhaseProbe` com seis marcos:

1. `ArchiveVerified`: ZIP integral reconhecido e fechado no disco de quarentena.
2. `InventoryExtracted`: arquivos extraídos e SHA por arquivo validado.
3. `ReplayVerified`: replay GAFULL09 executado e witness relido.
4. `BeforePublish`: destino ainda ausente, pouco antes do rename.
5. `AfterPublish`: diretório já visível, ainda não readmitido.
6. `BeforeAdmission`: lease local no diretório novo readquirida, falta última checagem.

Em cada marco, o teste injeta uma exceção e confirma que **nenhum writer é retornado**. Os casos depois do rename verificam a existência do marcador forense; os casos anteriores verificam que não sobrou destino/quarentena.

Outros testes simulam duas recuperações do mesmo destino, arquivo sentinela de outro escritor criado no instante anterior ao rename, e alteração do `writerEpoch` no witness imediatamente antes ou após a publicação.

## Saves mistos

Uma partida de teste executa um turno **militar + ecológico misto** certificado historicamente e, em seguida, um turno ecológico. O backup exporta `*.gamixed`, `*.gajournal`, bundles e head; o restore de quarentena exige:

- `historicalVerifier` com a chave e identificação corretas;
- journal root exatamente igual à âncora independente;
- sequência e turno finais iguais aos reconhecidos;
- replay da cadeia completa, e não apenas verificação da existência do ZIP.

A ausência do verificador, uma chave histórica incorreta ou a adulteração de `*.gamixed` **mesmo quando o manifesto interno corresponde ao arquivo adulterado** deve impedir a publicação.

## Limites de crash-consistency

- A pasta é publicada via rename no mesmo filesystem, não por transação distribuída. Regras de fsync de diretórios, interrupção de energia, snapshots de filesystem e integridade do volume requerem avaliação conforme o SO/FS de produção.
- O marcador e a lease não substituem fencing físico de nós, permissões de diretório/volumes ou validação externa. A existência de um save no destino não significa que ele foi admitido.
- Falhas injetadas são **exceções controladas**, não `SIGKILL`, queda de energia ou falha real de volume. O próximo gate pode adicionar testes de processo separado e recovery drill de storage.
- Não há conexão com Object Lock real ou com um orquestrador HA real. Os recibos ECDSA já validados pelo J3-J3l/m continuam a ser exigidos no adaptador estrito de backup; testes injetam fontes controladas onde apropriado.

## Validação

Workflow `.github/workflows/ga-mixed-backup-publication.yml` executa os testes `J3n` no Ubuntu .NET 10. Outros workflows já existentes mantêm kernel/hash/benchmark em três plataformas, PostgreSQL/witness real, failover de PostgreSQL, assinaturas operacionais e recuperação versionada.

**DRAFT; não alterar a main nem fazer merge sem autorização.**

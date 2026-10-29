# GA — G3-J3g: PostgreSQL como autoridade externa para saves GAFULL09

**Status:** integração candidata sobre o PR #76. **DRAFT; sem merge.**

## Resultado

Implementa `EconomicV9PostgresTrustedAnchorStore : IEconomicV9TrustedAnchorStore` com
**PostgreSQL primário independente do diretório de saves**. A aplicação usa
`Npgsql` 10.0.3 e uma chave de `game_id` estável por partida.

A leitura é executada por `public.ga_v9_anchor_read(game_id)`; o prepare e
o finalize usam `public.ga_v9_anchor_cas(game_id, old_snapshot, new_snapshot)`.
O backend realiza um `UPDATE ... WHERE snapshot = old_snapshot` atômico
para um registro PK por partida. A atualização e o registro de auditoria
permanecem na mesma transação. O servidor também valida o formato básico
e as transições de prepare/finalize. A aplicação verifica a serialização
canônica estrita em cada leitura.

Uma transação com `SET LOCAL synchronous_commit = on` exige confirmação
do WAL local antes de reconhecer CAS. Para redundância de datacenter é
necessária política adicional de replicação síncrona e failover.

## Implantação e fronteira de confiança

1. Administrar um **PostgreSQL em infraestrutura distinta dos arquivos
   `GAFULL09`**. Habilitar TLS de servidor verificado
   (`Ssl Mode=VerifyFull` e CA apropriada), credenciais segregadas,
   proteção de backups/WAL e auditoria externa.
2. Executar o script como proprietário confiável do schema:
   `docs/engineering/sql/GA-G3J3g-trusted-anchor-postgres.sql`.
   O script cria uma tabela por `game_id`, uma tabela de auditoria
   e as funções `SECURITY DEFINER` com `search_path` restrito.
3. Criar um role de aplicação **sem** acesso direto a
   `ga_trusted_v9_anchor` ou `ga_trusted_v9_anchor_audit`.
   Conceder apenas USAGE no schema e EXECUTE nas duas funções;
   não conceder INSERT/UPDATE/DELETE, propriedade do schema ou superuser.
   As funções são explicitamente revogadas do PUBLIC no script.
4. Efetuar o bootstrap inicial da partida em **processo administrativo
   autenticado**, após validar o checkpoint v9 inicial em fonte independente.
   Inserir uma única linha com `game_id` e o JSON canônico gerado por
   `EconomicV9PostgresTrustedAnchorStore.SerializeSnapshot(new
   EconomicV9ExternalAnchorSnapshot(genesisAnchor))`.
   A conta de jogo não pode criar, sobrescrever ou excluir esse registro.
   `ReadSnapshot` falha fechado se o jogo não estiver provisionado.
5. Configurar o jogo para passar todo turno por
   `EconomicV9ExternalCasTurnCoordinator`, com este store e
   `EconomicCompleteV9DurableSaveHost` como o único publicador.
   Nunca expor o método de commit legado ao fluxo de jogo normal.

Exemplo de composição em C# (aplicação/host, não biblioteca):

```csharp
using var dataSource = Npgsql.NpgsqlDataSource.Create(
    trustedServerConnectionString);
var anchors = new EconomicV9PostgresTrustedAnchorStore(
    dataSource, trustedGameId);
var coordinator = new EconomicV9ExternalCasTurnCoordinator(
    generatedWorld, anchors);
// coordinator.CommitMixed(...) ou coordinator.CommitEcological(...)
```

A connection string e os segredos devem vir de um cofre seguro, não de
código ou do diretório de saves. Esse adapter **não provisiona** o banco,
não recebe segredos do jogador, nem configura a infraestrutura de produção.

## Concorrência, integridade e recuperação

- **Isolamento:** a chave primária `game_id` separa âncoras; CAS de
  partida A não altera B.
- **Linearização:** o predicado de igualdade do snapshot completo
  impede dois prepares concorrentes de terem sucesso; transações
  PostgreSQL serializam o `UPDATE` sobre a mesma linha.
- **Admissão:** a camada G3-J3f produz uma intenção canônica
  `Before -> After`, prepara primeiro, publica localmente uma vez e
  só então finaliza por CAS.
- **Crash/timeout:** uma exceção após envio de CAS pode representar
  sucesso remoto sem reconhecimento. O coordenador conserva bloqueio;
  a recuperação deve reler a autoridade, inspecionar/reexecutar os
  journals (G3-J3e) e só confirmar `After` se previamente preparado.
- **Evidência:** uma linha na tabela de auditoria é escrita com a
  transição confirmada dentro da mesma transação. Logs/WAL enviados
  para uma origem realmente independente são recomendados.

O adapter PostgreSQL não concede proteção criptográfica contra
**administrador do banco comprometido**, replay de backup antigo,
acesso com permissões excessivas ou replicação assíncrona que perca
commit reconhecido. Para anti-rollback forte, manter checkpoints
periodicamente em âncora independente/imutável. Não reivindicar
transação distribuída ACID com save local.

## Gate de verificação

`GlobalArena.Tests/G3J3gPostgresTrustedAnchorIntegrationTests.cs`
testa o banco PostgreSQL real: persistência entre conexões, quatro CAS
de prepare/finalize, stale writers, concorrência simultânea com apenas
um vencedor, isolamento por jogo, auditoria e falhas de formato/provisão.
Usa `GA_TEST_PG_CONNECTION`; não há fallback a memória.

O workflow `.github/workflows/ga-trusted-anchor-postgres.yml`
executa integração em PostgreSQL 17 efêmero (TLS desabilitado **apenas
na CI isolada**). Kernel, hash e benchmark cross-platform continuam
no pipeline existente.

**Próximos riscos para o gate posterior:** provisionamento do serviço
gerenciado e política de administração/HA, testes reais de failover
com WAL durável, fencing de escritores por sessão, rotação de credenciais,
cancelamento/retentativa segura de prepares não publicados e políticas
de recuperação operacional. Nada disso é declarado pronto neste PR.

**PR #76 e anteriores permanecem draft. Sem alteração de `main` e
sem merge autorizado.**

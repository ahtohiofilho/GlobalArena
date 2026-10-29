# GA — G3-J3h: isolamento de credenciais, autorização por partida e plano operacional PostgreSQL

**Estado:** integração candidata baseada no PR #77, sem merge. Este gate reforça o controle de acesso do banco real; **não implanta infraestrutura de produção**, não executa failover real e não garante anti-rollback contra administrador do banco.

## Falha de isolamento corrigida

O PR #77 isolava os registros por `game_id` e revogava DML sobre as tabelas, mas um login com EXECUTE nas funções `SECURITY DEFINER` poderia solicitar **qualquer** `game_id`. O G3-J3h adiciona **autorização de acesso por login autenticado e por partida no servidor**:

- Nova tabela protegida `public.ga_trusted_v9_game_principal(game_id, login_role)`.
- As funções `public.ga_v9_anchor_read` e `public.ga_v9_anchor_cas` verificam a associação do `session_user` PostgreSQL ao `game_id` antes de ler, preparar ou finalizar; sem associação, retornam erro de permissão `42501`.
- `session_user` identifica o LOGIN que abriu a conexão (não o proprietário efetivo da função com `SECURITY DEFINER`); trocar o role ativo via `SET ROLE` não muda essa identidade.
- Nenhum login de jogo deve ter privilégios de leitura/escrita direta nas três tabelas, nem ser membro de papéis administrativos.
- A verificação ocorre **dentro das funções existentes**: mesmo um role com GRANT EXECUTE legado precisa estar vinculado explicitamente a cada jogo. Nenhum cliente pode contornar usando nomes antigos.

**Aplicação:** reexecutar o script atualizado do G3-J3g como proprietário do schema, em janela de manutenção controlada, antes de dar acesso aos serviços de jogo. A migração não insere automaticamente permissões; o bloqueio inicial é deliberadamente *fail-closed*. Faça backup consistente e teste a restauração antes da migração. O administrador provisiona e revoga mapeamentos **fora** do processo de jogo.

## Fluxo de provisionamento seguro (administrador)

1. Criar a instância externa com TLS certificado/verificado, proteção de credenciais, WAL e backups isolados da pasta `GAFULL09`. Usar roles de **login por domínio de confiança**; se um login opera vários jogos, cada vínculo deve ser aprovado individualmente.
2. Aplicar `docs/engineering/sql/GA-G3J3g-trusted-anchor-postgres.sql` como owner confiável; **não** usar credencial de aplicação para migrar schema.
3. Criar a partida por um workflow administrativo que verifique o genesis v9 independente e insira `snapshot` canônico em `ga_trusted_v9_anchor`. A conta de jogo **não** recebe INSERT.
4. Criar/associar o LOGIN de aplicação após aprovação:
   ```sql
   -- Executado pelo DBA, com game_id e role previamente aprovados.
   GRANT USAGE ON SCHEMA public TO ga_v9_game_service;
   GRANT EXECUTE ON FUNCTION public.ga_v9_anchor_read(text)
       TO ga_v9_game_service;
   GRANT EXECUTE ON FUNCTION public.ga_v9_anchor_cas(text, text, text)
       TO ga_v9_game_service;
   INSERT INTO public.ga_trusted_v9_game_principal(game_id, login_role)
   VALUES ('game-approved-id', 'ga_v9_game_service');
   ```
   No provisionamento real, parâmetros administrativos devem ser validados e submetidos por canais autenticados; não confiar em nomes ou IDs fornecidos pelo jogador.
5. Confirmar que a conta tem apenas USAGE no schema e EXECUTE nessas funções; nenhum SELECT/UPDATE/DELETE/INSERT nas tabelas de âncora, auditoria ou ACL. Validar acesso à própria partida e **negação** de partida diferente.
6. A revogação é administrativa: `DELETE FROM public.ga_trusted_v9_game_principal WHERE game_id = ... AND login_role = ...`. Ela bloqueia imediatamente novas chamadas de função, inclusive de conexões já existentes. Antes de alterar permissões, assegurar que prepares legítimos tenham runbook de recuperação sob um novo login autorizado.

A tabela de ACL deve ficar sob dono administrativo confiável. O LOGIN de serviço não deve ter `CREATEROLE`, `SUPERUSER`, ser membro do owner, nem contar com `BYPASSRLS` para outras tabelas. A aplicação deve bloquear qualquer commit legado que contorne o coordenador G3-J3f.

## Incidentes, timeout e failover

- Falha de rede **antes** de qualquer prepare confirmado: não presumir ausência de gravação. Bloquear novo turno, reconectar ao **primário confiável** e reler `ReadSnapshot()` com a credencial do jogo.
- Prepare observado com head antiga: a operação não pode ser finalizada como publicada. Manter a partida parada e executar um plano administrativo explícito de retry ou cancelamento; não promover blob órfão.
- Head local igual ao sucessor preparado: descartar qualquer presunção de sucesso anterior, executar `InspectExternalReconciliation` / replay com HMAC histórico e anchors v8 quando aplicável. Só então `FinalizeRecoveredPublished` pode usar CAS e voltar a ler o snapshot externo.
- Resultado de `TryFinalize` ambíguo: recuperar pela **leitura confiável externa**, nunca pelo estado local isolado. Se o snapshot estiver confirmado, restaurar usando `RestoreExternallyConfirmed`.
- Após failover, validar se o primário promovido possui todas as confirmações anteriormente reconhecidas **antes** de liberar turnos. `synchronous_commit=on` é durabilidade do WAL local, **não equivale** a consenso entre regiões nem garante que um secundário assíncrono possua o commit.
- Em caso de snapshot/revision atrasado, WAL ausente, divergência do journal, papel comprometido ou incerteza da linhagem, manter **fail-closed** e requerer intervenção operacional; não restaurar backup antigo como estado autoritativo sem uma âncora independente.

**Importante:** esta entrega não executa testes de promoção entre nós PostgreSQL. Uma etapa posterior deve criar infraestrutura HA de teste, `synchronous_standby_names` / `synchronous_commit=remote_apply` conforme SLA, e testes de queda do primário com commit reconhecido vs. WAL replicado. Não há token persistente de fencing local nem âncora imutável terceira independente neste PR.

## Evidências do gate

O novo teste `GlobalArena.Tests/G3J3hPostgresLeastPrivilegeIntegrationTests.cs` conecta ao PostgreSQL 17 como **dois logins reais com senhas temporárias**, não como administrador com `SET ROLE`. Ele verifica:

- LOGIN A pode ler e confirmar exclusivamente a partida A;
- LOGIN A não pode ler nem preparar a partida B (erro `42501`);
- operações diretas contra tabelas de save confiável, auditoria e ACL são negadas;
- tentativa de `SET ROLE` para LOGIN B é recusada;
- CAS próprio continua linearizável e não altera a partida B;
- remoção do vínculo ACL revoga imediatamente a leitura e a finalização;
- contas e jogos usados nos testes são efêmeros; a CI não armazena essas credenciais em arquivos.

A integração G3-J3g continua cobrindo CAS concorrente, journal raiz, auditoria e reconexão. O workflow de PostgreSQL é atualizado para incluir os cenários G3-J3h. Kernel, hash e benchmark cross-platform continuam obrigatórios.

```sh
dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release \
  --filter 'FullyQualifiedName~G3J3gPostgresTrustedAnchorIntegrationTests|FullyQualifiedName~G3J3hPostgresLeastPrivilegeIntegrationTests'
```

O teste de banco requer `GA_TEST_PG_CONNECTION` administrativo e PostgreSQL real, fornecidos pela CI. Sem essa variável, os testes se omitem; **os gates de CI com banco não podem ser substituídos por um resultado de regressão sem banco**.

**Sem merge. Sem alteração de `main`. O próximo gate será o laboratório de failover/fencing com autorização externa de cancelamento de prepare e backup anti-rollback independente.**

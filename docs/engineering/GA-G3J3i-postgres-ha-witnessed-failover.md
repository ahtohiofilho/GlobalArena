# GA — G3-J3i: gate de retomada após failover PostgreSQL com testemunha independente

**Status:** incremento candidato, baseado no PR #78. **DRAFT — sem merge.**

## Problema

Um PostgreSQL secundário em streaming pode parecer consistente, mas estar
atrasado em relação a um CAS que o primário já reconheceu. Promovê-lo sem
validar os compromissos aceitos permitiria rollback silencioso de partidas.
Uma segunda ameaça é o *split brain*: o primário antigo continuar aceitando
transações depois de o secundário ser promovido.

## Entrega do gate

- `EconomicV9PostgresTrustedAnchorStore.ReadSnapshot` só retorna o snapshot
  se o servidor **não** está em `pg_is_in_recovery()`. A réplica em leitura
  não pode ser admitida como autoridade de commits.
- `IEconomicV9IndependentFailoverWitness.ReadLastAcknowledged(gameId)`
  é a fronteira com uma fonte externa **separada de ambos os membros do
  cluster PostgreSQL e do diretório de saves**. Seu conteúdo deve ser
  uma âncora reconhecida como duravelmente confirmada e com história monotônica.
- `EconomicV9FailoverAdmissionGate.RestoreAfterPromotion` verifica igualdade
  estrita da âncora do primário promovido com a última âncora reconhecida pela
  testemunha (digest do checkpoint, raiz do journal, sequência e turno).
  Qualquer prepare pendente resulta em bloqueio. A restauração reexecuta
  toda a cadeia local, incluindo assinatura HMAC histórica em turnos mistos;
  a testemunha e a base são relidas antes de liberar o writer host.
- Se o CAS foi reconhecido mas não existe no secundário promovido, o gate
  falha fechado. Não há promoção automática de dados locais.
- Testes `J3i*` verificam admissão com testemunha coincidente, rollback
  reconhecido perdido, prepare pendente e mudança da testemunha no meio do
  replay. Se a verificação final falhar, a lease local é liberada.

## Laboratório REAL de duas instâncias PostgreSQL

O workflow `.github/workflows/ga-postgres-ha-failover.yml` invoca
`tools/ci/ga-postgres-ha-failover.sh` em ambiente Ubuntu efêmero:

1. Sobe PostgreSQL 17 primário e um **segundo processo PostgreSQL** com
   `pg_basebackup -R` e replicação física por WAL.
2. Aguarda `pg_stat_replication` indicar streaming e configura
   `synchronous_standby_names = 'FIRST 1 (*)'`.
3. Executa a migração SQL real, provisiona uma partida de teste e publica
   **prepare + finalize** usando a função SQL existente. Ambos usam
   `SET LOCAL synchronous_commit='remote_apply'`.
4. Conecta ao secundário ativo: confirma que está em recuperação e que
   `ReadSnapshot()` rejeita a réplica como autoridade.
5. **Para o primário antigo antes de promover** o secundário no laboratório,
   verifica o processo interrompido e executa `pg_ctl promote`.
6. Lê o **mesmo jogo, sem genesis novo**, por meio do adapter real na
   instância promovida; confere checkpoint SHA, journal SHA, sequência,
   turno, revision 2 e as duas entradas da tabela de auditoria.

Isso testa a presença de commit reconhecido após promoção **com replicação
síncrona**, não uma simulação que copia JSON de uma instância para outra.

## Fronteiras que NÃO estão implementadas

- **Não foi instalado PostgreSQL de produção**, nem topologia HA permanente.
- O laboratório efetua fencing via `docker stop` do primário antigo,
  mas não implementa fencing de infraestrutura de produção (*STONITH*,
  revogação de volume, roteamento ou quorum externo). O gate só pode
  ser acionado **após** o operador/orquestrador ter bloqueado fisicamente
  o escritor antigo. Sem isso, mesmo o snapshot verificado pode sofrer
  *split brain* imediatamente depois da admissão.
- A interface de testemunha independente é um **contrato**, sem
  serviço externo implantado. Seu valor não pode ser calculado a partir
  do novo primário, de arquivos locais ou de um espelho assíncrono
  do mesmo banco; o integrador deve atualizar a testemunha
  **após cada CAS confirmado** com garantia monotônica adequada.
  Uma testemunha atrasada bloqueia a partida; não autoriza inferir
  avanço só porque o primário possui mais estado.
- Não implementa token de fencing persistido em cada journal nem
  prova criptográfica contra administrador comprometido.
- `synchronous_commit=on` no adapter de produção anterior só exige
  WAL local. O laboratório força `remote_apply` por transação; o
  deployment HA precisará configurar durabilidade e ack policy reais.
- Um teste isolado de failover com `remote_apply` **não** prova
  segurança de todo cenário de falha, perda regional, PITR, nem
  correctness de um gerenciador HA real.

## Playbook operacional de retomada

1. Interromper agendamento de turnos e impedir novos commits por qualquer
   caminho legado.
2. Isolar o primário antigo e exigir confirmação auditável de fencing.
3. Confirmar política de réplica síncrona e os compromissos reconhecidos
   através de uma testemunha confiável **fora** do cluster.
4. Promover réplica; verificar `pg_is_in_recovery() = false`, saúde do WAL
   e ausência de escritores concorrentes.
5. Obter snapshot do primário promovido usando credencial da partida e
   testar `EconomicV9FailoverAdmissionGate.RestoreAfterPromotion`.
6. Se testemunha e base divergirem, existir prepare, ou replay falhar:
   **fail-closed** e intervenção humana. Sem rebaselining automático,
   sem sobrescrever save nem testemunha.
7. Somente após admissão completa reativar o coordenador único G3-J3f.

**Próximo gate:** implementar um serviço externo de testemunha durável,
rotacionar tokens de fencing de escritores e testar failover assíncrono
com WAL intencionalmente ausente para comprovar rejeição de rollback.

**Não fazer merge sem aprovação expressa.**

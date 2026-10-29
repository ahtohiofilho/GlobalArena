# GA — G3-J3k: rotação de liderança *fail-closed* e diagnóstico de divergência

**Branch empilhada sobre o PR #80. Sem merge e sem implantação de infraestrutura.**

## Mudança de segurança

O G3-J3j introduziu o `writer_epoch` em dois bancos, mas sua rotação
ainda dependia de dois comandos DBA independentes, sem exigir que ambos
comparecessem a **âncora exata** nem oferecer recuperação controlada
após interrupção. O G3-J3k introduz:

- `EconomicV9PostgresWriterEpochRotation.Rotate(oldEpoch)`;
- `IEconomicV9FormerWriterIsolationVerifier` — fonte confiável que deve
  atestar que o primário antigo e suas sessões escritoras estão
  efetivamente **isolados de aceitar commits**;
- CAS DBA `ga_v9_anchor_rotate_verified(game, oldEpoch, exactSnapshot)`;
- CAS DBA `ga_v9_witness_rotate_verified(game, oldEpoch, exactAck)`;
- `Observe()` — diagnóstico somente leitura de epoch, prepare e divergência.

Os dois CAS executam transações duráveis, com igualdade estrita de
identidade da partida, epoch antigo e checkpoint reconhecido.
O primeiro CAS recusa qualquer prepare pendente; o segundo recusa
qualquer confirmação nova entre o *preflight* e a rotação.
A função do witness registra o evento de rotação na auditoria.

## Sequência do operador

1. **Pausar todos os agendadores** e fechar hosts de save do jogo.
   Impedir qualquer caminho de commit legado.
2. Garantir **fencing físico pelo gerenciador externo** do primário antigo,
   de seu volume de dados e dos escritores anteriores. O implementador de
   `IEconomicV9FormerWriterIsolationVerifier` deve consultar evidência
   autenticada do orquestrador (por exemplo, API de STONITH/fencing).
   **Apenas um ping de rede falhando ou um boolean do cliente não bastam.**
3. A partir de dois **datasources DBA independentes**, conectar ao primário
   eleito e ao banco-testemunha. Passar a fonte de isolamento confiável
   para o coordenador administrativo e chamar `Rotate(N)`.
4. Exigir ausência de prepare e igualdade de checkpoint SHA, journal SHA,
   sequência e turno nos dois bancos. O CAS do primário avança de
   N para N+1 **antes** do CAS no witness; confirma novamente o isolamento
   entre os dois passos.
5. O witness avança de N para N+1 somente se seu `acknowledged`
   continuar exatamente igual ao valor observado. O método relê ambos,
   verifica o mesmo epoch e a mesma âncora e só então retorna
   `EconomicV9WriterRotationReceipt`.
6. Instanciar o novo adapter `EconomicV9PostgresTrustedAnchorStore`
   com `writerEpoch: N+1`, o witness da mesma geração e passar pelo
   gate de admissão/replay completo do G3-J3i antes de reativar turnos.
   **O recibo de rotação isolado não libera gameplay.**

O administrador da aplicação normal não deve possuir privilégios de
execução nas novas funções; o DBA realiza a migração e libera EXECUTE
das funções `*_rotate_verified` somente a papéis administrativos
auditados. As duas contas DBA usadas pelo orquestrador administrativo
não devem ser credenciais do host de jogo.

## Falha entre commits dos dois bancos

Uma interrupção entre os CAS deixa intencionalmente:

- primário com epoch N+1,
- testemunha com epoch N,
- checkpoint/root iguais, sem prepare.

O observador retorna `PrimaryRotatedWitnessPending`. Nenhum writer
de N deve confirmar no primário atualizado; o writer de N+1 ainda não
tem testemunha correspondente e, portanto, também deve ser bloqueado.

O operador **não** desfaz o epoch do primário. Após verificar novamente
o isolamento físico e a âncora por ambas as conexões, chama
`Rotate(N)` novamente. Esse comando reconhece a transição parcial e
tenta somente o CAS pendente do witness, respeitando seu estado exato.
Com o mesmo procedimento, um comando repetido após sucesso retorna
o mesmo recibo sem criar epoch N+2.

Caso o witness esteja **à frente** do primário, haja prepare,
checkpoint/root distintos, a autoridade externa esteja indisponível,
o verificador de isolamento falhe ou o CAS remoto tenha resultado
incerto, **interromper**. A aplicação não pode executar rollback,
reinicializar a âncora nem substituir a testemunha para fazê-la coincidir.

## Monitoramento e alertas

O diagnóstico `Observe()` retorna, sem modificar qualquer dado:

| Estado | Ação operacional |
|---|---|
| `Aligned` | Bancos coerentes, mas não comprova fencing físico nem replay |
| `PrimaryRotatedWitnessPending` | Bloquear gameplay; inspecionar rotação interrompida |
| `PreparedTransition` | Não rotacionar; inspecionar prepare com o protocolo G3-J3e/j |
| `AnchorDivergence` | Incidente crítico: possível rollback ou ACK em voo |
| `EpochDivergence` | Incidente crítico: ordem inesperada ou geração faltante |

Falhas de rede, erro de permissão ou formato inválido devem gerar
alerta de indisponibilidade da autoridade. **Não** converter erros em
`Aligned`. Recomenda-se coletar métricas de frequência de diver-
gência, latência CAS e tempo em prepare pendente, sem expor segredos,
certificados HMAC ou credenciais nos logs.

## Política de credenciais e backups

Rotação da liderança e **rotação de senha de banco** são coisas
diferentes. Para senha/role: provisionar novo login com grants mínimos e
vínculo explícito por partida, implantar o novo secret via cofre,
desativar sessões antigas e revogar o login anterior. O epoch de
liderança continua obrigatório mesmo com novas senhas.

Além dos backups PostgreSQL usuais, manter cópias **imutáveis** (WORM,
retenção temporal legalmente habilitada, isolamento administrativo de
contas, object lock quando disponível) do WAL, da auditoria, do último
acknowledgment e do mapeamento de epochs da testemunha. O inventário
de backups deve possuir uma âncora externa que não possa ser sobrescrita
pelo DBA do primário ou do witness. Executar periodicamente restauração
em ambiente isolado, verificando digest, sequence, root e epoch contra
os registros independentes.

**O G3-J3k não instala armazenamento WORM, não provisiona contas
de cloud, não configura STONITH real e não concede segurança contra
um administrador que controla os dois bancos.** O operador deve
implementar `IEconomicV9FormerWriterIsolationVerifier` sobre seu
orquestrador confiável. A CI usa apenas prova controlada para validar
falha/recuperação; isso não certifica fencing físico de produção.

## Critérios de validação

O teste `G3J3kWriterRotationIntegrationTests` usa os dois servidores
PostgreSQL reais do workflow para verificar a ordem de rotação, falha
da prova de fencing entre os CAS, observação do estado parcial, retomada
sem rollback, idempotência, bloqueio do writer antigo, prepare pendente
e divergence de checkpoint. Outros gates cobrem kernel, hash,
benchmark e laboratório de promoção física.

**Não autoriza merge dos PRs #80 ou #81 nem modificação de `main`.**

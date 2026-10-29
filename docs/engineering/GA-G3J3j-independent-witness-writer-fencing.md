# GA — G3-J3j: testemunha externa durável e fencing de gerações de escritor

**Estado:** candidato G3-J3j, baseado no PR #79. **DRAFT, sem merge e sem implantação de produção.**

## A decisão de autoridade

O save local `GAFULL09` e o primário PostgreSQL com replicação física
não podem ser sua própria prova de ausência de rollback. O J3j adiciona
uma **segunda instância PostgreSQL independente**, fora dos membros
primário/standby e fora dos arquivos de save, para registrar de forma
monotônica a última âncora que o host *reconheceu*.

A implementação é `EconomicV9PostgresAcknowledgmentWitness`:
`IEconomicV9IndependentFailoverWitness` + `IEconomicV9AcknowledgmentWitness`.
A migração separada é
`docs/engineering/sql/GA-G3J3j-independent-witness.sql`; ela deve ser
aplicada **somente no banco independente da testemunha**, nunca no primário
e nunca em um secundário replicado por ele.

Cada jogo possui `writer_epoch > 0`, `acknowledged` (JSON canônico da
âncora, com SHA do checkpoint, raiz do journal, sequência, turno), número
de revisão e auditoria de operações. O CAS `ga_v9_witness_ack` é atômico
e só avança **exatamente um turno e uma sequência**, com os hashes
diferentes e o mesmo epoch esperado. O registro é imutável sob o login de
jogo exceto pela função CAS controlada. `ga_v9_witness_rotate` é reservada
ao operador/DBA e só incrementa epoch de um em um.

A testemunha não afirma autenticidade de ações de usuário e não substitui
um serviço de consenso ou assinatura externa. Uma credencial de aplicação
comprometida ainda pode propor um avanço ao witness CAS: a camada de
aplicação/coordenador, a autenticação, a separação do papel de administração,
o replay e auditoria continuam essenciais.

## Writers antigos: geração de liderança

O banco **primário** ganha a coluna `writer_epoch` por jogo, protegida
por ACL e definida por DBA. Funções novas no SQL do PR #77:

- `ga_v9_anchor_read_fenced(game_id, epoch)` rejeita epoch incorreto;
- `ga_v9_anchor_cas_fenced(game_id, expected, next, epoch)` só altera
  estado quando epoch e snapshot coincidem;
- a função legada de 3 parâmetros `ga_v9_anchor_cas` passa a usar
  epoch **zero**. Uma partida que recebeu epoch positivo impede commits
  pelo caminho legado, mesmo com o snapshot correto;
- o adapter `EconomicV9PostgresTrustedAnchorStore(..., writerEpoch: N)`
  transmite o epoch a cada leitura e CAS. Sem epoch, mantém compatibilidade
  só para jogos ainda não migrados.

Para evitar split brain, é **obrigatório** isolar o primário antigo e as
sessões de jogo antigas antes da rotação no novo primário. O epoch do banco
novo não se propaga magicamente para um antigo primário desconectado, que
continuaria aceitando transações se não houvesse fencing físico.

## Write-ahead + reconhecimento duplo

`EconomicV9WitnessedTrustedAnchorStore` encapsula o G3-J3f:

1. Lê witness e só permite prepare se epoch e âncora confirmada anterior
   coincidirem.
2. A lógica já existente prepara por CAS no primário, publica um único
   save/journal/head e finaliza por CAS no primário.
3. Antes de reconhecer o sucesso, relê o primário e então confirma por
   CAS a âncora `After` **no banco da testemunha independente**.
4. Relê witness. Só agora `TryFinalize` retorna sucesso. O coordenador
   só libera novo turno após esse retorno e sua leitura final.

Se a gravação independente falhar **depois da confirmação no primário**,
a aplicação deixa a partida bloqueada. Não interpreta timeout como
negação de gravação, não reverte journal e não retorna sucesso.

`ReconcileFinalizedAcknowledgment` é a recuperação estreita dessa janela:
requer writer local encerrado, epoch de witness válido, primário confirmado
sem prepare, exatamente um sucessor sobre a âncora anterior da testemunha,
**replay integral** e vínculo explícito do último journal ao checkpoint
e raiz anterior. Somente então tenta novo CAS monotônico da testemunha.

Se witness já avançou, usar leitura de witness e reconciliação do estado,
**não** submeter a mesma transição duas vezes. Em caso de divergência
irresolúvel, falha fechada.

## Procedimento de rotação

Orquestração por um operador de confiança, com serviços de jogo pausados:

1. Isolar fisicamente e confirmar fencing do antigo primário/escritor.
2. Confirmar o mesmo checkpoint/root no primário eleito e na testemunha;
   se divergentes, interromper.
3. Verificar inexistência de prepare e término dos commits pendentes;
   registrar auditoria do plano de mudança.
4. **DBA altera** `ga_trusted_v9_anchor.writer_epoch` de N para N+1
   por UPDATE condicional (exatamente uma linha). O serviço de jogo não
   possui UPDATE nessa tabela.
5. **DBA da testemunha executa**
   `SELECT public.ga_v9_witness_rotate(game_id, N)`. Só esse DBA
   deve possuir EXECUTE nessa função.
6. Criar novos adapters com `writerEpoch: N+1`, confirmar snapshot da
   testemunha e chamar o gate de admissão J3i antes de liberar turnos.
7. Clientes com epoch N, inclusive aqueles conectados à nova instância,
   não podem mais ler como escritor nem confirmar CAS.

Rotação não é transação distribuída. Interrupção entre etapas deixa o
serviço bloqueado até correção administrativa. **Nunca** atualizar
automaticamente epoch a partir de valores fornecidos pelo cliente.

## Provisionamento e segurança

- Duas instâncias PostgreSQL distintas, contas DBA distintas, TLS
  `VerifyFull`, cofres de credenciais e backups/WAL independentes.
- No witness, bootstrap administrativo da partida com uma única linha
  `game_id / writer_epoch=1 / acknowledged=anchor genesis canônica`.
  Adicionar associação de login em `ga_v9_witness_acl`.
- Conceder `USAGE` no schema e `EXECUTE` somente nas funções
  `ga_v9_witness_read` e `ga_v9_witness_ack`. Não conceder DML
  nas tabelas ou EXECUTE em `ga_v9_witness_rotate`.
- No primário, conceder as funções fenced ao login autorizado por jogo,
  associar `ga_trusted_v9_game_principal`, e fazer bootstrap do
  `writer_epoch=1` na mesma janela de provisionamento.
- Login de jogo não pode alterar epoch, ACLs, schema ou representação
  de snapshot. Administradores devem restringir `CREATEROLE` e
  propriedade do schema.
- A aplicação deve canalizar commits por
  `EconomicV9ExternalCasTurnCoordinator` + store testemunhado,
  impedindo acesso direto ao host ou adapter legado.

## Gates

O workflow de integração PostgreSQL sobe **DOIS servidores isolados**:
um banco para o CAS do jogo e outro, em container diferente, para witness.
Os testes G3J3j verificam dois compromissos, reconexão independente,
CAS anterior/epoch antigo rejeitados, rotação administrativa e persistência
da auditoria. Os testes com falha injetada verificam que uma confirmação
do primário seguida de falha no witness não reabre jogo; replay do save
e vínculo anterior são exigidos antes do ACK de recuperação. Corrupção do
journal impede qualquer avanço da testemunha. Mantêm-se kernel/hash/
benchmark cross-platform e laboratório de failover físico G3-J3i.

## Limites restantes

Este incremento **não** implanta serviço gerenciado de produção, não
implementa quorum do witness, não fornece assinatura/ledger WORM contra
DBA comprometido e não faz fencing físico da infraestrutura de produção.
A dupla confirmação DB + witness continua não sendo uma transação ACID.
Não há garantia de disponibilidade se witness falhar: o desenho é
intencionalmente *fail-closed*. O próximo passo de produção exige
orquestrador HA com fencing comprovado, revezamento de credenciais,
backups imutáveis e teste de perda do witness.

**DRAFT — NÃO REALIZAR MERGE SEM AUTORIZAÇÃO EXPRESSA.**

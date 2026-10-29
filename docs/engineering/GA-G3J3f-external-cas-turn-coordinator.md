# GA — G3-J3f: coordenador de commit com prepare externo e CAS

**Status:** candidato sobre PR #75; **DRAFT, sem merge**.

## Contrato de autoridade

O `EconomicV9ExternalCasTurnCoordinator` integra o mesmo
`EconomicCompleteV9DurableSaveHost` dos PRs #68–#75 com um
`IEconomicV9TrustedAnchorStore`, **obrigatoriamente fora da pasta
de saves e independentemente protegido**.

A implementação concreta do store deve garantir:

- `ReadSnapshot()`: leitura atômica/linearizável de âncora confirmada
  e prepare pendente do **mesmo jogo**;
- `TryPrepare(expected, requested)`: CAS durável, apenas se o snapshot
  completo confirmado for exatamente `expected` e não houver prepare;
- `TryFinalize(expected)`: CAS atômico do prepare **exato** para a
  nova âncora confirmada, limpando a pendência; não pode concluir uma
  intenção concorrente;
- ao retornar sucesso do prepare, a intenção deve já estar durável e
  observável; commit não depende de confirmação derivada do disco local;
- o backend externo deve possuir controles reais de autenticação,
  isolamento por partida, disponibilidade das chaves históricas, prevenção
  de rollback e garantias consistentes de CAS.

**Não fornecemos um banco externo/serviço real nesta entrega.**
A interface é a fronteira de integração; o fixture em memória é apenas
um teste de contrato, não é armazenamento confiável em produção.

## Protocolo de confirmação

Para `CommitMixed` e `CommitEcological`:

1. Obter digest, raiz de journal, sequência e turno do host único.
2. Decodificar seu bundle completo e validar correspondência ao host.
3. Materializar exatamente uma vez as entradas e, para movimento militar,
   copiar o certificado HMAC histórico e produzir um candidato
   determinístico, **sem publicar estado**.
4. Calcular o journal canônico `GJMX0001` ou `GAJRN001`, SHA-256
   e a âncora `After` prevista.
5. Ler a âncora externa confiável; exigir coincidência estrita com a
   `Before` local e ausência de prepare prévio.
6. **Prepare externo durável via CAS**, com vínculo `Before → After`,
   seguido de leitura independente que confirme o prepare.
7. Usar unicamente `EconomicCompleteV9DurableSaveHost.CommitMixed`
   ou `CommitProjected` para gravar bundle/journal e publicar head v2;
   comparar digest, journal root, turno e sequência com o que foi preparado.
8. **Finalize externo via CAS**, limpar prepare e reler a âncora
   independentemente para confirmar a mesma `After`.

Nenhuma segunda `CanonicalGameSession` é publicada.

## Interrupção e recuperação

Após qualquer prepare com resultado ambíguo, erro de publicação local,
erro de finalize ou falha na confirmação posterior, o coordenador conserva
um bloqueio lógico para novos turnos. Ele **não cancela prepare e não
promove automaticamente arquivos locais**.

A recuperação segura começa com o encerramento do host escritor e
leitura de um snapshot independente:

- **Confirmado + sem prepare:** `RestoreExternallyConfirmed` pode
  restaurar após replay integral da cadeia.
- **Prepare durável, local ainda na âncora antiga:** bloquear gameplay;
  `FinalizeRecoveredPublished` não pode confirmar. O serviço externo
  deverá decidir cancelamento ou retry sob exclusão adequada de
  escritores e política de recuperação própria.
- **Head local corresponde ao sucessor externamente preparado:**
  `FinalizeRecoveredPublished` executa `InspectExternalReconciliation`
  (incluindo replay integral, HMAC histórico, vínculo ao checkpoint e
  root anterior) e só então `TryFinalize` por CAS. Após êxito, relê
  a autoridade externa; ainda será necessário restaurar o host via
  `RestoreExternallyConfirmed` antes de reabrir turnos.
- **Head local diversa ou journal violado:** falha fechada,
  sem tentativa de finalização.

O retorno ou a exceção da operação externa pode ser perdido após o
CAS durável. Portanto, não existe rollback automático nem suposição de
que um timeout signifique ausência de gravação.

## Escopo e limites

O código implementa o **coordenador e contrato CAS**, mas não uma
transação distribuída ACID entre sistemas. O protocolo depende
estritamente das garantias do backend externo. Não há *fencing token*
durável vinculado ao escritor local, nem um adapter externo de produção.
O coordenador não impede que código legado que obtiver referência direta
ao host faça commits por fora; para implantar, a aplicação deve tornar
esse coordenador o **único caminho permitido** para confirmar turnos.

Não altera `GAFULL09`, head v2, journals, hash do `WorldState`,
permissões ou o motor do kernel. Continua sem prova de identidade pública,
pagamento, entrega ou liquidação de mercado. Demandas e retenções exigem
admissibilidade na aplicação; certificado HMAC é a autoridade militar
histórica do host, não assinatura do jogador.

## Validação

Os testes `J3f*` em
`GlobalArena.Tests/G3J3fExternalCasTurnCoordinatorTests.cs` simulam
uma autoridade CAS linearizável somente em memória:

1. commit misto + ecológico sucessivos confirmados e restaurados;
2. recusa do prepare antes de qualquer publicação local;
3. perda de acknowledgment após prepare durável, sem head publicada;
4. falha de finalize com head publicada: bloqueio, replay e finalize
   externo em recuperação;
5. âncora externa stale e certificado HMAC adulterado impedem prepare.

```sh
dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~J3f
```

**Critério de aceite:** kernel, estado/hash e benchmarks em Ubuntu,
Windows e macOS, sem regressões. **Nenhum merge autorizado**.

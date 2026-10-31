# GA-VS48 — SETTLE transacional atômico e idempotente, somente em laboratório

**Data:** 31/10/2026. **Base empilhada:** GA-VS47 PR #153, commit `debfff850e06133ff018169bc3976a794aaf797b`, 13/13 workflows aprovados. **Status:** laboratório em memória, draft, sem alteração de `main`.

## Escopo efetivo

A GA-ADR-ECON-005 estabelece `SNAPSHOT → EXPLORE → AUDIT → SELECT/AUTHORIZE → SETTLE` à vista, sem crédito. A VS46 executa uma auditoria financeira independente e a VS47 define uma seleção institucional e emite **recibo informativo**, não autorizado automaticamente a debitar dinheiro. A VS48 implementa **um laboratório in-memory de SETTLE**, ainda NÃO o caixa V14 real nem a persistência de jogos.

`ExperimentalAtomicCashSettlementLab` recebe (1) snapshot de origem VS47, (2) **mapa de abertura explícito de contas tipadas** domésticas, produtoras e transportadoras e (3) `IExperimentalCashSettlementAuthority`, contrato de um verificador institucional confiável INJETADO. **Não há implementação liberando todos os pedidos por padrão.** Uma string `ActorId` ou o hash do recibo, isoladamente, não autentica ninguém: a autoridade real, o registro durável das permissões e de revogação são requisitos de integração futuros. Não afirmar segurança de produção para o exemplo de laboratório.

## Preparação e publicação de um único lote

1. Exigir **mesma instância de snapshot**, mesmo digest, turno, world-state hash, revisão de rede e `expectedStateRevision`. A revisão funciona como CAS de uma única máquina em memória.
2. Chamar **`ExperimentalAuditedCashCandidateAuthorization.Authorize`** da VS47 novamente. Essa etapa valida política/decisão e reconstrói o journal financeiro na VS46. Exigir também permissão pelo `IExperimentalCashSettlementAuthority` do host.
3. Verificar se `DecisionId` já foi processado. Se a mesma decisão ainda estiver válida e corresponder ao **mesmo ator, candidato, digest e auditoria**, devolver **o mesmo comprovante** sem lançar novamente. Um ID reaproveitado com dados diferentes é rejeitado; outra decisão no mesmo snapshot não executa um segundo lote.
4. Construir um mapa temporário dos saldos, verificando que cada conta doméstica de abertura é idêntica à auditada. As contas adicionais de produtores/transportadores podem conter **saldos preexistentes declarados** e NÃO são zeradas automaticamente. Contas recebedoras inexistentes entram com **zero inicial explicitamente no mapa hipotético**, sem ligação a entidades persistidas.
5. Para cada lançamento `debit → credit`, exigir identidade tipada de origem/destino e fundos suficientes; aplicar somente na cópia. Reconferir a reserva doméstica `H`, os saldos líquidos finais de todos os consumidores e **conservação monetária total** `Σ abertura == Σ fechamento`, com frações exatas `BigInteger`.
6. Preparar **antes de publicar** o journal, o registro do ID processado e o digest da distribuição final de caixas. Sob um único lock de transação, substituir a cópia do estado, o registro de decisões e os lançamentos de uma vez; não há `await`, I/O nem callbacks depois desse ponto.
7. Emitir `ExperimentalCashSettlementProof` contendo identificação da decisão/candidato, hashes, revisão de estado, lançamentos, total monetário de abertura e total no fechamento. Repetir o mesmo pedido com a mesma autorização retorna o comprovante original (`WasAlreadyCommitted=true`).

`Inspect()` retorna cópia de contas com coleção apenas de leitura, revisão do laboratório e diário de transações imutável para os consumidores. **Não há save, rollback distribuído, aprovação externa durável nem consenso entre processos**. Segurança contra crashes e replay entre sessões NÃO é provada por um `Dictionary` que só existe na memória.

## Invariantes econômicos

- Nenhuma compra financiada por receitas de vendas ou fretes do mesmo ciclo: a conta doméstica é verificada contra o saldo de abertura e não recebe créditos durante a etapa.
- Custos de mercadoria e frete são exatamente os auditados pela VS46; dois lançamentos por remessa, um produtor e outro operador. Os beneficiários recebem seu crédito correspondente.
- A reserva residual `H` não se converte em dinheiro gastável, e contas de produtores/transportadores não são confundidas com contas domésticas.
- Nenhuma criação de moeda, passivo negativo, dívida, empréstimo, garantia, banco ou recibo parcial.
- Apenas um lote por snapshot/ciclo no laboratório; outra decisão é recusada. A seleção entre propostas concorrentes ocorre **antes** do primeiro commit.
- Nenhuma mutação de `CanonicalPhysicalTacticalSession`, `PhysicalFoodCashMarketState`, WorldGen, produção, infraestruturas, movimento, save V17, journal histórico, estado hash ou UI Godot.

## Testes e benchmark

- Commit válido de **107 créditos familiares com H=5**, remessas totalizando 20 em produtos e 82 em fretes; casa termina com 5 e receitas são creditadas aos produtores/operadores. Saldos de abertura anteriores de produção/transporte permanecem intactos.
- Replay do mesmo `DecisionId` entrega comprovante idêntico e **não** duplica lançamentos; ID usado com outro ator é rejeitado, e segunda decisão para o mesmo snapshot também.
- Permissão institucional negada, `Approves=false`, digest adulterado, snapshot/turno/rede obsoletos, versão CAS divergente ou conta doméstica subfinanciada **não publicam nenhum efeito**.
- Concorrência de 16 tentativas iguais é serializada: 1 publicação, 15 replays idempotentes. `Inspect` não permite mutar os saldos e journal.
- Benchmark `--vs48-atomic-settlement`: 8, 32 e 128 compradores, 2 transferências por comprador, saturação compartilhada por grupos, verificação de replay e conservação exata; mediana de 3 amostras após 1 aquecimento, SEM threshold de tempo. A preparação de propostas, preços e seleção institucional fica fora da cronometragem.

## Riscos e requisitos da ativação posterior

Este marco **não deve ser usado para atualizar uma partida real**. Para tornar `SETTLE` de fato canônico, será necessária uma ponte validada de identidades e propriedade econômica (pontos, tiles, operadores, receitas públicas), unidade monetária, fundos reais iniciais de TODOS os papéis, autenticação das permissões, materialização persistente e idempotente de decisões no MESMO journal/save transacional e CAS do world hash/turno/revisão real. A publicação atomizada em memória não prova atomicidade entre banco, arquivos, serviços externos ou múltiplos processos. O próximo gate deve testar uma transação integral num sandbox de mundo/save versionado, preservando backups e sem migrar usuários automaticamente.

Comando de benchmark de QA:

```powershell
dotnet run --project GlobalArena.Benchmarks/GlobalArena.Benchmarks.csproj -c Release -- --vs48-atomic-settlement
```

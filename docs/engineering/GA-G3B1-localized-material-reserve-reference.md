# GA — G3-B1: saldo de reservas materiais localizado e cotação de custo de obra

**Status:** draft de domínio e simulação; ainda NÃO integrado a `WorldState` ou a hash canônico.  
**Base:** PR #53 (G3-A: retenção deliberada e demanda extra), sobre PR #52 (G2).  
**Revisão futura obrigatória:** PR #50 propõe `WorldState` v9 com instalações, enquanto `main` permanece v8. Nenhuma mutação de ambas neste incremento.

## 1. Decisão mínima de armazenamento

A economia produz oferta temporária por turno. Só a **retenção explícita** na origem, solicitada e validada contra a produção pelo G3-A, pode aumentar uma reserva entre turnos. A aquisição adicional no destino **não aumenta** reserva antes de haver compra/entrega comprovadas.

O domínio de reserva contém:
- `EconomicMaterialReserveBalance(EconomicPointId, CommodityId, RawQuantity)`: saldo positivo de mercadoria por ponto econômico, não uma entidade `City` ou warehouse;
- `EconomicMaterialReserveTurnCursor(CivilizationId, LastAppliedTurn)`: último turno creditado por civilização, que recusa reexecutar o mesmo período;
- `EconomicMaterialReserveBook`: snapshot imutável, linhas ordenadas, sem duplicatas, com leitura `RawAvailable`.

**Localização:** ponto econômico (`EconomicPointId`), não tile fino; a ligação espacial do ponto ainda é um contrato pendente, e não se presume que a âncora estratégica seja a identidade física precisa.

## 2. Transição econômica pura

`EconomicMaterialReserveAccrualResolver.AccrueOriginHolds(world, owner, turn, previous, plannedFlows)`:

1. exige civilização existente e turno positivo maior que o último turno creditado da mesma civilização;
2. usa apenas `plannedFlows.OriginHolds` para creditar unidades materiais; a demanda adicional em `DestinationAcquisitions` não ganha crédito;
3. exige que cada produção retida pertença à civilização;
4. verifica overflow e preserva demais saldos/turnos, incluindo outra civilização;
5. retorna snapshot novo sem alterar estado antigo nem `WorldState`.

**Limitação deliberada:** o resolvedor pressupõe que `plannedFlows` tenha sido criado pelo G3-A a partir dos fluxos corretos do turno; ele não possui prova autenticada da origem do evento nem integração ao scheduler. O cursor bloqueia replay por mesmo turno no livro candidato, mas **não autoriza o processamento econômico global**. É um núcleo reutilizável, não contabilização oficial da partida.

## 3. Construção — somente consulta de viabilidade material

`EconomicConstructionMaterialQuoteResolver.Evaluate(world, owner, book, requirements)` informa saldo e falta de cada material, por `EconomicPointId` e `CommodityId`.

- Emitem-se requisitos de catálogo futuramente; números do fixture não são receitas oficiais.
- O item no ponto A não paga uma obra localizada no ponto B sem transporte/relocalização de reserva.
- O avaliador recusa requisitos duplicados e pontos alheios.
- Mesmo quando `IsMateriallyAffordable == true`, **NÃO há débito**, obra ou posse de infraestrutura resultante. Construção deve depender de evento de gameplay validado, consumo transacional de saldo e atualização de ativo.

## 4. Limites de implementação deliberados

- Nenhuma propriedade nova em `WorldState`, alteração de formato de hash ou pretensão de persistência oficial.
- Nenhum comando de construção, recolonização, colonização, transferência de habitantes ou obra financiada.
- Nenhum crédito de bens adquiridos em outro local: o atual `FoodTradeDeliveryReconciliationResolver` trata atestações *declaradas* apenas de alimentos; não prova entrega física universal de pedra e madeira.
- Nenhuma criação de recurso quando compra falha, nem estoque automático da oferta não vendida.
- Nenhum armazém/terminal obrigatório, nem "cidade" em qualquer etapa.

## 5. Testes

`GlobalArena.Tests/G3BMaterialReserveBookTests.cs` cobre:
1. retenção em turnos sucessivos e imutabilidade de estado;
2. intenção de compra adicional sem crédito material;
3. rejeição de replay, com permissão para civilizações diferentes processarem mesmo turno;
4. viabilidade/escassez material da obra sem mutação;
5. escassez local apesar de saldo em outro ponto;
6. ordenação canônica e duplicatas;
7. overflow e tentativa de crédito estrangeiro;
8. rejeição de custos de obra duplicados ou estrangeiros.

```sh
dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~G3BMaterialReserveBookTests
```

## 6. Próximo gate G3-B2: autoridade de estoque e aplicação de obras

Antes de afirmar que o jogo acumula recursos oficialmente, é necessário:
- reconciliar a base com a proposta de hash v9 do PR #50 e, se aprovado, introduzir **uma** alteração de formato para incluir reservas e cursores de turno, com hashes-testes e regra de migração;
- fixar a localização fina (ou outro vínculo territorial verificável) dos pontos econômicos;
- vincular o plano de retenção à produção realmente apurada no turno;
- atestar compra física para estoque de destino sem confundir mera alocação comercial com entrega;
- implementar comando de obra com despesa em estoque **e** instalação/conexão persistente na mesma transição (falha atômica);
- validar transferência de residentes e trabalhadores em expansão sem gerar população.

**Sem merge sem autorização explícita.** A implementação de G3-B1 é preparação testável, não uma entrega que já roda no loop autoritativo.

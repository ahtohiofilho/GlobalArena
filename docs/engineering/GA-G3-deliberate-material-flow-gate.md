# GA — G3-A: intenção de reserva física e aquisição adicional para estoque

**Status:** draft técnico; implementação de preparação de fluxos, sem construção, sem estoque persistente, sem autorização de merge.  
**Dependência:** PR #52 (G2) — referência econômica de dois pontos.  
**Compatibilidade:** preserva `CanonicalWorldStateHasher` v8 da main; não modifica os PRs #49 e #50 (D2/D3).

## 1. Contrato estrito

A economia neolítica usa fluxos de produção por turno e demanda do consumidor. A única via de estoque intertemporal será a **intenção explícita** de reservar material produzido na origem ou de comprar adicionalmente para reservar no destino.

Este incremento implementa exclusivamente a transformação canônica dos fluxos **antes de executar o mercado**:

- `EconomicMaterialOriginHoldOrder(point, commodity, activity, q)`: retira `q` da produção ofertada pela atividade no ponto de origem. **Não cria um estoque persistente.** Reserva total remove a linha de oferta comercial (nunca cria `EconomicSupplyFlow` de quantidade zero).
- `EconomicMaterialDestinationAcquisitionOrder(point, commodity, q)`: soma `q` à demanda comercial daquele ponto e mercadoria, podendo criar linha de demanda inexistente. **Não garante compra, pagamento, recebimento nem estoque.**
- `EconomicDeliberateMaterialFlowResolver.Resolve(world, issuer, flows, holds, purchases)`: valida identidade, propriedade e quantidades; produz `EconomicDeliberateMaterialFlowResult.MarketFlows` mais as ordens canônicas correspondentes.

O efeito é refletido no mercado existente sem nova curva artificial, impostos, empresas, salários, emissão de moeda ou intervenção indevida na formação de preços.

### Atenção: duas interpretações diferentes de *reservation*

O código já possui `EconomicTurnSourceReservation`, mas seu `RawUnitReservationCost` representa **custo/preço de reserva do produtor** usado em cálculo comercial. **Não significa que mercadorias foram guardadas.** As novas ordens empregam `RawQuantity` e têm semântica física de retirada deliberada da oferta.

## 2. Restrições

1. Todas as quantidades usam a mesma escala `RawQuantity` dos fluxos.
2. A quantidade retida na origem deve ser positiva e no máximo igual à oferta da identidade exata `(EconomicPointId, CommodityId, Activity)`.
3. A compra pretendida deve ser positiva e referenciar ponto econômico existente e pertencente à civilização emitente.
4. Uma ordem por chave física para cada tipo; duplicatas recusadas, sem agregação silenciosa.
5. Soma da demanda com aquisição adicional verifica overflow.
6. Operações são puras, ordenadas canonicamente, sem alteração de `WorldState` ou de hash.
7. Ordens pertencentes a outra civilização são rejeitadas. Mercado de outras civilizações continua intacto.
8. Quantidade não comprada permanece **demanda frustrada**, nunca estoque criado automaticamente.

## 3. Referência executável

`GlobalArena.Tests/G3DeliberateMaterialFlowTests.cs` demonstra:

- redução simultânea de oferta agrícola e mineral com aumento de demanda de reserva em A/B;
- envio do resultado ao `EconomicMarketAllocationResolver` existente;
- reserva integral que remove uma oferta sem quantidade zero;
- formação de nova demanda comercial, mesmo sem consumo recorrente daquele item no ponto;
- equivalência de permutações;
- rejeição de reserva excessiva, ordens duplicadas, pontos estrangeiros e desconhecidos, origem sem produção correspondente, overflow e identidades inválidas;
- imutabilidade do hash canônico na transformação.

Executar:

```sh
dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~G3DeliberateMaterialFlowTests
```

**Esta etapa não declara resultados aprovados sem CI.** Os valores são fixtures, não calibração de produção/preços definitiva, nem IDs do catálogo canônico.

## 4. O que o G3-A deliberadamente NÃO resolve

- **Estoque autoritativo** por local/mercadoria, hash e migração de saves.
- **Crédito físico na origem** após reservar produção, além do comando do jogador no kernel.
- **Crédito físico no destino** após mercado, custo de aquisição e *atestado de entrega*; o contrato `FoodTradeDeliveryReconciliationResolver` é referência de auditoria, não substitui uma operação de estoque.
- **Retirada de materiais para obras**, custo de oportunidade e estado de construção.
- **Ocupação ou colonização de tile fino:** `EconomicPointRuntimeState` tem âncora `StrategicCellId`, mas o mapeamento para `PhysicalTacticalTileId` ainda não é persistente no estado econômico; não identificar pontos como cidades por padrão.
- **Migração versus deslocamento diário:** a distinção entre residentes e trabalhadores já existe; criar um novo ponto econômico não pode multiplicar ambos. Para o próximo gate é necessário definir comandos explícitos de transferência e localização.

## 5. Próximo gate: G3-B (submetido a revisão antes da mudança de hash)

1. Contrato versionado de `MaterialReserve` com localização canônica e saldo, decidindo representação para estratégico x fino;
2. evento que credita a reserva de produção retida, sem expô-la simultaneamente à venda;
3. conciliação de compras para estoque **apenas na quantidade realmente entregue**, sem apropriar consumo populacional;
4. comando transacional de construção que **consome material de estoque** antes de gerar infraestrutura operacional;
5. ocupação num novo tile por transferência de população/trabalho, sem criação gratuita, controlada pela geografia, território e política de residência;
6. revisão conjunta dos esquemas/hashes propostos no PR #50 antes de adicionar outra seção persistente.

**Sem merge automático:** branch empilhada sobre G2; alterações de persistência e migração dependem de gate de versão independente.

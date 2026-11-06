# GA-ECON-OFFER-AVAIL-01 — Prova de disponibilidade física, sem oferta fictícia

**Data:** 2026-11-06  
**Base:** GA-ECON-ABSTRACT-SUPPLY-01 / PR #269  
**Política:** contrato GA-CANON-DIRECTIVES-01; FAST por PR, checkpoint multiplataforma por pacote de 3–5 incrementos.

## Decisão arquitetural

O incremento anterior verifica produção física BRUTA do ciclo e gera uma cotação abstrata hipotética. Isso NÃO demonstra o que ainda existe para ser ofertado depois do consumo, porque:
- o alimento produzido pode ser consumido por residentes no próprio ciclo;
- estoques de períodos anteriores continuam disponíveis, mesmo que sua produção não pertença ao ciclo atual;
- alimentos colocados sob custódia de expedições não são estoque do mercado local;
- retenções e compromissos comerciais ainda não têm evento canônico genérico de execução.

A nova fronteira usa **o estoque local realmente fechado**, não a produção bruta, como teto físico para uma hipótese de oferta. O estoque pode existir sem compradores e compradores podem existir sem estoques.

## Implementação

`CanonicalPhysicalMarketAvailabilityWitness.FromCurrentExecutedCycle(session)` reúne dois recibos executados:
1. `CanonicalExecutedPhysicalCycleProvenance`: estoque local de abertura, trabalho realmente produtivo, uso alimentar residente e estoque local de fechamento por tile/mercadoria; total de alimentos consumidos por canal.
2. `CanonicalPhysicalLotCycleWitness`: lotes de custódia física e atribuição FIFO, com conservação material. FIFO é **convenção de contabilidade** e não identidade biológica/atômica de um lote fungível.

Invariantes:
- `opening + produced - residentConsumed = closingLocal` para cada tile e mercadoria;
- `sum(closingLocal)` deve reconciliar exatamente todos os lotes locais e `WorldState.Stocks`;
- rações de expedição são agrupadas em separado por `JourneyOrderId`/commodity, sem reinseri-las no mercado local;
- consumo alimentar total efetivamente executado = consumo local + rações de viagem;
- ambos os recibos devem corresponder ao MESMO hash, ao MESMO ciclo e à MESMA sessão; evidências antigas são rejeitadas;
- toda aritmética acumulada dos totais usa BigInteger, sem criar overflow oculto.

`QuoteLocalCandidate(...)` aplica `PopulationPriceKernel` ao estoque de fechamento **do tile** com moradores reais alocados naquele tile, retencão hipotética **explicitamente fornecida** (nunca presumida), escala raw explícita, poder de compra N experimental explícito e curva de preços experimental explícita. A cotação segue:
`D=P*N`, `x=S/D`, `p=f(x)`, `R_ref=p*D`.

A população usada é a força de trabalho residente no tile (não são compradores inventados), e viajantes sob custódia própria de expedições não viram demanda local duplicada. Este é um PREVIEW de um mercado local, não um mercado regional que admite teletransporte entre tiles.

## Limites de evidência — não promover sem journal de ofertas

`ClosingAvailableRaw` é estoque físico ao fechar o ciclo, **não** prova de estoque plenamente livre de compromissos comerciais ou políticos. A contabilidade atual não fornece certificado total de reservas futuras. `HypotheticalRetentionRaw` é uma hipótese do chamador, **não** retirada/compromisso real de mercadoria.

Por isso:
- `ProvesActualGoodsWereOfferedForSale = false`
- `ProvesOutstandingLocalCommitmentsAreFullyAccounted = false`
- `AuthorizesActualMarketSettlement = false`
- `ProvesRealPurchasingPowerN = false`
- `ModifiesCashStocksSavesOrHash = false`

Nenhuma produção é vendida automaticamente. Nenhum `min(S,D)` determina volume físico; o índice D apenas pressiona preço. Não houve criação de carteira, crédito, caixa, imposto, estoque invisível, ingresso nutricional nem modificação de cadência.

Os modelos históricos `AdaptiveThirty` e `LocalCashSettlement` continuam somente para replay e jogabilidade legada. O alvo permanece **cadência por mobilidade** e **poder de compra abstrato**, sem substituir essas decisões por hipóteses temporárias.

## Homologação

`GaEconOfferAvailability01Tests` cobre:
- fechamento físico obrigatório;
- consumo real deduzido antes da disponibilidade;
- cotação sobre o estoque fechado, distinta da produção bruta;
- retenção hipotética validada, inclusive retenção integral;
- oferta fisicamente maior do que D em cenário de poder de compra baixo, sem teto de consumo;
- observação do modo cash antigo sem consultar seus saldos;
- segregação de carga de viagem e consumo de rações;
- abertura de estoques de ciclos anteriores sem rebatizá-los como produção nova;
- replay de save e hash, invalidação de recibos após movimento/realocação;
- parâmetros inválidos, classes sem mercadoria física e não-mutação de estado.

FAST compila Godot Release e executa esta classe automaticamente pelo seletor GA-CI-FAST-02. Os workflows completos permanecem explicitamente skipped no PR ordinário; não contá-los como aprovação.

## Próxima etapa operacional

`GA-ECON-OFFER-COMMIT-01`: definir um comando CANÔNICO e VERSIONADO que, a partir do estoque local disponível, registre separadamente:
1. retenção material efetiva e outras obrigações já vinculadas;
2. reserva/compromisso de oferta ao mercado;
3. destino econômico e execução de transferência sem gerar estoques ou dinheiro artificiais;
4. prova de que a mesma unidade física não possa ser consumida, vendida ou colocada em expedição duas vezes;
5. persistência, journal e replay do novo modo sem alterar formatos legados V9–V30.

Somente depois dessa etapa poderá o motor atestar `ActualMarketOffer = true` ou habilitar liquidação real. A curva de preço f e a derivação econômica de N permanecem decisões separadas e calibráveis.

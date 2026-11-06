# GA-ECON-MARKET-DEFAULT-01H — ledger de cobertura física das ofertas do mercado

**Data:** 06/11/2026.  
**Base:** GA-ECON-MARKET-DEFAULT-01G, PR #287, HEAD `7e043ec8e1b5148f292811dbab58533a2fcabbba`.  
**Status:** auditoria determinística e independente sobre **destinação material realmente executada**. Não registra vendas, pagamentos ou transporte. PR Draft sem merge.

## Decisão e objetivo

O mercado automático tem, de um lado, ofertas físicas reais em `MarketOffer` (01D/01E/01F) e, de outro, uma via existente de **uso material local efetivamente registrado** (01C), enquanto a valorização marginal de 01G é apenas índice. 01H liga essas evidências por **ordinal do compromisso de origem e ação executada**, sem atribuir venda fictícia a um consumo ou destino apenas proposto.

O novo `CanonicalMarketExecutedPhysicalCoverage.FromCurrent(session)` percorre cada compromisso realmente emitido pelo journal. Cada compromisso tem exatamente **um** estado: oferta pendente, oferta liberada ao estoque livre, material fisicamente usado localmente, material fisicamente usado por grupo produtivo identificado, retenção deliberada viva ou retenção liberada. O registro expõe ordinal de origem, ordinal de uso, mercadoria, tile, quantidade, atividade e número real de trabalhadores beneficiários, quando existentes.

## Regras de reconciliação

- Uma oferta comprometida históricamente corresponde exatamente à soma entre **pendente + liberada + usada**. Retenções não entram na oferta.
- **Pendente** deve coincidir com `CanonicalDefaultMarketPoolPreflight.AlreadyCommittedOfferRaw`.
- **Retenção viva** deve coincidir com `DeliberatelyRetainedRaw`; retenção liberada não é retida de novo.
- Material efetivamente usado exige ação `use_committed_material` ou `use_committed_material_for_workers`, recibo de origem consumido, quantidade/tile/mercadoria idênticos, atribuições materiais FIFO que somem a quantidade integral e identidade de execução única; tudo verificado contra o testemunho de custódia do motor.
- `EligibleAwaitingDestinationRaw` é **livre atual + oferta pendente**, não incorpora de novo quantidades históricas de ofertas liberadas. Mercadoria liberada e reofertada pode aparecer duas vezes na estatística de atos históricos, mas somente uma vez no saldo físico atual.
- Não misturar quantidades cumulativas históricas com instantâneo de estoque corrente. O somatório atual é reconciliado ao pool global e `LiveMarketOffersRaw`.
- Material usado é **destinação física executada**, não venda. Uso sem grupo identificado não gera comprador fictício; uso com grupo real continua sem contrato comercial, transferência de propriedade ou pagamento.
- Alimento legado `CommodityId(1)` continua **pendente**, não vira ingestão nutritiva; a via de uso material exige madeira ou pedra. Não derivar seis alimentos de agregado.
- O objeto só leitura guarda identidade da sessão, hash e contagem do journal e recompõe a auditoria inteira em `VerifyIndependent()`; mudanças de estoque, liberação ou novo turno invalidam a fotografia.

## Fronteira de autorização

`AuthenticatesRealOfferAndMaterialUseJournals=true` atesta recibos físicos existentes. Entretanto:
`AuthenticatesRemoteMarketDeliveries=false`, `AuthenticatesResidentFoodIngestion=false`, `ProvesCompletedMarketSales=false`, `CanAuthorizeMonetaryRevenueOrTax=false` e `MutatesCanonicalStateOrSave=false`. Não existem vendas executadas ou incidências fiscais neste incremento. O índice de receita 01G não se torna receita contabilizada por mera associação a um uso material.

## Testes

`GaEconMarketDefault01hTests` verifica:
- produção livre de ciclo real sem oferta e sem falsa venda;
- compromisso parcial, retenção e uso efetivo com grupo de trabalhadores real via 01C;
- prova de origem, quantidade, ação de uso e replay de save/hash idêntico;
- libertação e nova oferta da **mesma** quantidade sem double count do saldo;
- lote automático 01D íntegro e pendente, sem registrar produtos como vendidos;
- uso material legado sem grupo, preservado como sink físico sem consumidor inventado;
- sessão estrangeira, evento/hash obsoleto e indisponibilidade de receita/tributação.

## Próximo gate de produto

A conclusão material parcial serve de **insumo obrigatório** para o clearing comercial, mas ainda falta: contratos reais de transferência/titularidade, transporte efetivamente executado quando remota, destinação final material para toda oferta e recibo de liquidação institucional (nenhuma carteira pessoal). É proibido promover `TotalExecutedPhysicalMaterialRaw` a vendas ou aplicar automaticamente uma receita estimada.

Quando a execução comercial for implementada, a mesma estrutura de reconciliação deverá ser expandida a recibos de entrega, transferência de propriedade e pagamento autenticados — **não** substituir o ato de consumo físico por uma simples declaração de comprador. A meta de oferta/venda de toda produção não retida e o ledger de seis alimentos ainda não estão alcançados. O motor legado e saves V9–V34 permanecem inalterados.

# GA-ECON-MARKET-DEFAULT-01J — entrega física local sem consumo em custódia exclusiva de destinatário real

**Data:** 06/11/2026  
**Base:** GA-ECON-MARKET-DEFAULT-01I, PR #289, HEAD `3ef01836ee7b36b40670f2754b8f4ff63472b884`.  
**Status:** ação canônica experimental e versionada, ainda não ativada por padrão e sem liquidação comercial; PR Draft, sem merge.

## Requisito efetivamente avançado

Até 01I, o journal conhecia reserva de mercado, liberação de reserva e uso material com consumo físico. Ainda não podia **transferir a posse física para destinatário produtivo sem consumi-la**. Tratar essa operação como `use_committed_material` apagaria material que apenas mudou de custódia. Tratar como `release_physical_stock` devolveria ao saldo livre sem reter identidade do beneficiário.

01J introduz a operação `TransferMarketOfferToLocalRecipient(commitmentActionOrdinal, recipientActivity)`. Ela transfere a quantidade **integral** de um único compromisso `MarketOffer` de madeira/pedra para a custódia exclusiva do grupo produtivo real, identificado por tile, atividade e quantidade de trabalhadores efetivamente alocados naquele momento.

### Fluxo físico

1. Exige compromisso ativo existente, real e local de madeira (3) ou pedra (2), atividade válida e grupo real com trabalhadores naquele mesmo tile.
2. `CanonicalPhysicalLotLineageTracker` move os mesmos lotes (origens FIFO conservadas) de `MarketCommitment` para a **nova custódia `LocalRecipientCustody`**, utilizando como identidade única o ordinal do compromisso original.
3. A operação registra a ação canônica `transfer_market_offer_local_recipient` com origem, destinatário no mesmo tile, atividade/contagem dos trabalhadores reais, mercadoria, quantidade e compromisso de origem; a revisão e o hash evoluem sem criar estoque adicional nem consumir o material.
4. O compromisso passa a `Transferred=true`, saindo do saldo `IsLive` das ofertas. É **mutuamente exclusivo** com `Released` e `Consumed`. Uma segunda transferência, liberação ou uso pelo caminho antigo recusa.
5. O recibo `CanonicalExecutedLocalRecipientCustodyTransfer` conserva ordinal de transferência, ordinal de origem, quantidades, proveniência física, trabalhador beneficiário e hashes antes/depois. `VerifyPhysicalStockCommitments` reconcilia as três custódias exclusivas e o journal em cada ação.
6. A auditoria 01H expõe `TransferredToRealLocalRecipient` com `TotalTransferredToRecipientRaw`, sem somá-lo como oferta pendente, estoque livre ou material consumido. O histórico de ofertas continua incluindo todos os atos reais, mas os saldos presentes não são duplicados.
7. O lote 01D não reoferta bens já transferidos: só considera estoque livre e ofertas vivas, enquanto os bens do receptor permanecem segregados e fisicamente contabilizados.

### Versionamento e replay

Os envelopes canônicos novos são **V37** (custódia sem tesouraria) e **V38** (com tesouraria dormente), aplicados **exclusivamente** a sessões com a nova ação. O payload canônico continua V9–V17, conforme tecnologia da partida. O campo `Transferred` foi acrescentado à linha de compromisso com valor default `false` omitido em JSON, preservando a representação byte a byte dos saves V9–V36.

A reabertura aceita a ação de transferência somente no envelope V37/V38, reproduz-a em sua ordem histórica e compara toda a sequência de journal, hash, lotes/compromissos e payload final. Saves antigos não passam a executar transferências. `GA-CI FAST` e testes de replay são gates mínimos; antes de ativação jogável, exigem-se checkpoint completo e Debug Windows/validação visual.

## O que NÃO está comprovado

- **Custódia física real** não implica **transferência de titularidade comercial legal**. A identidade do grupo é a atividade e seus trabalhadores presentes, **não** um contrato ou pessoa jurídica.
- Não houve venda liquidada, formação monetária calibrada, imposto, salário, preço pago, comprador distante, transporte ou custeio de frete.
- A nova custódia é distinta do estoque local livre: o bem foi entregue ao grupo, **ainda não foi consumido nem liberado para construção**. Um futuro comando deverá executar uso ou devolução da custódia do receptor, sem dupla baixa.
- A transferência está restrita a madeira/pedra. O alimento legado agregado não é transferido nem convertido em ingestão/six-food ledger.
- Não alterar a interface comum do Godot nem promover o novo evento à modalidade padrão antes de contratos reais de propriedade, entrega, liquidação e compatibilidade.
- `D=P×N` continua sinal de pressão abstrata e a curva VS78 continua índice não monetário: nenhum lançamento no Estado é autorizado por este incremento.

## Testes

`GaEconMarketDefault01jTests` cobre: transferência não consumptiva para grupo real; totais de custódia e reconciliação; reabertura V37 bit-idêntica e atribuições FIFO; não duplicação de transferência/uso/liberação; mistura V35 + nova ação; retenção e alimento não aceitos; falha de envelope falsamente V35; save legado V31 idêntico; oferta automática após handoff sem revenda do material recebido.

**Próximo gate:** operação canônica para uso físico final ou devolução explícita da custódia recebida e posterior liquidação de transferência de propriedade/comercialização por atores econômicos realmente reconhecidos. Todo material não retido deve ter destino, mas o objetivo completo de venda automática continua em aberto até autenticar as entregas e a escrituração sem comprador fictício.

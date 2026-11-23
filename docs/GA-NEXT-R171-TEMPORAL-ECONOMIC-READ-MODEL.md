# GA-Next R171 — Consulta financeira temporal do jogador

## Escopo implementado

A R170 já publica o evento SYSTEM de vendas temporais no journal e registra uma única liquidação no caixa do WorldState depois do replay. A R171 expõe essa evidência pela API **read-only** do host, sem iniciar outro cálculo, emitir outro ciclo econômico ou alterar o mercado.

### Contrato de leitura

- `FoundationTacticalHumanAiTurnHost.GetLastClosedTemporalEconomics(viewer)` retorna `null` antes do primeiro fechamento temporal autoritativo. Não gera prévia para ordens pendentes.
- Depois do fechamento all-ready, a API deriva o resultado da última `FoundationTacticalCollectiveReceipt.TemporalEconomicSettlement` publicada com replay e hash aprovado.
- A interface exige que o solicitante seja uma das civilizações humanas registradas. Tentativa por AI ou civilização externa é rejeitada.
- Cada leitura confere hash do `WorldState`, revisão, turno, ciclo liquidado, unidade de carga, conservação monetária e os créditos da civilização consultada.
- O DTO contém somente os valores de `SellerNetScaled`, `TaxReceiptsScaled`, `OwnerTollIncomeScaled` e `TotalCashCreditScaled` do jogador, além da quantidade física agregada de mercadorias, ciclo e hash. Não expõe linhas financeiras de outras civilizações.
- A retenção do recibo é em memória no próprio host e acompanha apenas o último fechamento temporal conhecido. Não constitui histórico persistente, não substitui journal e não constrói um overlay de comércio/rotas.
- O host legado continua usando `GetLastClosedEconomicTradeView`; não foi alterado para produzir artificialmente dados temporais.

### Gate e governança

Validação do host em cenário com fechamento temporal real, leitura nula antes da liquidação, consulta correta após publicação e rejeição de civilização AI e fotografia antiga. PR #564 permanece Draft.

O processo de desenvolvimento utiliza Gate D, conforme `docs/GA-NEXT-DUAL-CI-GATES.md`. Para homologação e merge permanece obrigatório Gate H, inclusive kernel macOS.

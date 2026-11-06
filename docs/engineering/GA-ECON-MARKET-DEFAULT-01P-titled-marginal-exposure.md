# GA-ECON-MARKET-DEFAULT-01P — exposição marginal de valor dos materiais efetivamente titularizados

**Data:** 06/11/2026  
**Base:** PR #295 / GA-ECON-MARKET-DEFAULT-01O, HEAD `1338ed44513a5cf2b924c5e98754f6ecbc6f9898` (quatro gates FAST, kernel, hash e benchmark aprovados).  
**Estado:** avaliação econômica local, somente leitura, PR Draft; sem ativação de venda, receita ou imposto.

## Por que este incremento

Até 01O, o projeto executava produção/estoque, oferta, entrega física, revisão de contrapartes, registro **no jogo** da titularidade e sua reversão. No entanto, o modelo VS78 de preços marginais era aplicado a propostas ou à oferta ainda comprometida, sem observar diretamente os volumes que **realmente receberam título econômico**. O 01P agrega os eventos canônicos existentes para estabelecer o primeiro elo preço–titularidade–material físico que não dependa de supor uma venda.

`CanonicalTitledLocalMaterialMarginalExposure.FromCurrent(session, rawUnitsPerEconomicUnit)` é um snapshot **reconstruível** por tile e mercadoria, apenas para madeira (3) e pedra (2) locais.

### Conciliação

A fonte de evidência é exclusivamente `CanonicalMarketExecutedPhysicalCoverage` e `CanonicalPhysicalTacticalSession.LocalSimulatedTitlePositions`, que por sua vez exigem registros do journal 01J/01K/01N/01O. Cada registro é contado uma única vez:

- `HistoricallyRegisteredTitleRaw`: quantidade **histórica** com título do jogo algum dia reconhecido.
- `ReversedTitleRaw`: título expressamente revertido pelo 01O; fica fora de qualquer cotação atual, mesmo que o material ainda esteja fisicamente aguardando devolução.
- `CurrentlyTitledRaw`: título reconhecido ao destinatário **ainda não revertido**.
- `StillInRecipientCustodyRaw`: dos titulados atuais, bens fisicamente guardados com o destinatário.
- `PhysicallyUsedByRecipientRaw`: dos titulados atuais, bens realmente consumidos em atividade produtiva autenticada pelo 01K.

A conservação é:
`HistoricallyRegisteredTitleRaw = ReversedTitleRaw + CurrentlyTitledRaw` e `CurrentlyTitledRaw = StillInRecipientCustodyRaw + PhysicallyUsedByRecipientRaw`. Um mesmo lote jamais aumenta ambos os lados da igualdade nem entra em duas destinações. Entregas físicas **sem título** não entram no valor corrente; retenção deliberada e bens oferecidos mas não entregues não são vendas.

### Preço marginal

A pressão física de demanda vem **somente de habitantes reais do tile** (soma das alocações de trabalho atuais); a escala `rawUnitsPerEconomicUnit` é fornecida explicitamente e deve ser positiva. **N=1 é neutro e não calibrado**: não representa moeda, carteira, teto de consumo ou poder de compra institucional ratificado.

Reutiliza diretamente `CanonicalEndogenousLocalPriceComparison.Calculate(titledVolume, realResidents * rawUnitScale, sensitivity=1|2)`, da VS78. Obtém preço médio relativo, preço marginal final relativo e **valor bruto hipotético indexado**, com precisão racional via `BigInteger`. Curvas suaves/acentuadas permanecem cenários de referência, não parâmetro econômico definitivo.

A cotação observa a **posição atual** de títulos e população, não é prova de que esses preços eram válidos quando ocorreram atos passados. O uso material já executado continua contando na exposição relativa do lote titularizado, mas isso não significa que foi pago; valores não se convertem em créditos em nenhuma conta. Uma reversão retira imediatamente a exposição atual, sem gerar reembolso.

### Separações contratuais e técnica

**Confirmação de título no jogo ≠ venda comercial realizada.** O resultado expõe explicitamente:
- `IsPurchasingPowerCalibrationRatified=false` e `CanCapPhysicalAbsorptionUsingBuyerDemand=false`;
- `InventsBuyerBalancesOrLoans=false`;
- `ProvesAnExecutedCommercialSale=false`;
- `PostsProducerRevenueOrStateTaxes=false`;
- `ChangesWorldSaveOrPhysicalLots=false`;
- `ExecutesUnprovenRemoteTransport=false`.

`VerifyIndependent` recompõe todas as linhas e valores a partir de uma nova auditoria dos recibos. `RequireCurrent` rejeita sessões estrangeiras e referências desatualizadas por qualquer evento canônico posterior. São mantidos todos os formatos de save **V9–V44**, sem nova versão de envelope ou novo tipo de journal.

### QA

`GaEconMarketDefault01pTests` verifica:

1. titularidades reais de madeira/pedra e índices marginais positivos sobre o volume não revertido; sem publicação de receitas;
2. material utilizado pelo destinatário altera somente o status de custódia, não duplica título;
3. reversão zera exposição atual antes de devolver fisicamente o lote; devolução real conserva o estoque;
4. oferta entregue mas **não titularizada** não cria exposição comercial nem compradores presumidos;
5. coexistência de títulos, uso, reversão e entrega ainda sem título; ausência de dupla contabilização e replay;
6. hash/save imutáveis na avaliação, snap­shot obsoleto e parâmetros inválidos recusados.

**Gate:** FAST no HEAD, suite completa multiplataforma se necessário por risco de regressão na integração de auditoria; sem merge ou ativação no gameplay.

## Próximo passo

Para sair de índices e chegar à **execução de uma venda**, o jogo precisará de um evento de compensação comercial com evidência explícita de entrega + direito econômico + **formação de preço calibrada** + aceitação de contraparte + origem institucional de receita. É proibido inventar carteiras para pessoas, permitir crédito implícito, impor teto arbitrário por demanda ou confundir índice relativo com moeda. O próximo incremento deverá priorizar o contrato executável de troca, não acumular camadas de preflight redundantes.

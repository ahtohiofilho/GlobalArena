# GA-VS33 — Suprimentos físicos adquiridos para expedições (V16)

Data: 31/10/2026. Base: GA-VS32 / PR #136, 34/34 workflows verdes.

## Contrato canônico
- Uma expedição longa pode ser ordenada pelo comando explícito `OrderProvisionedLongDistanceTransit`, informando quantidade positiva em unidades fixas de alimento (raw = 1.000.000 por unidade).
- A partida faz, em uma transação atômica sobre o WorldState, cotação do mercado alimentar local existente, validação de estoque e dinheiro disponíveis, débito físico da oferta na origem, transferência de microcréditos do comprador doméstico ao produtor e criação do lote de alimento associado ao ID da viagem. O preço cotado é liquidado à vista, com arredondamento monetário para cima; não há empréstimo, saldo negativo, fonte de dinheiro ou criação de alimento.
- Viajantes com lote V16 deixam de compor a demanda comercial na origem. Durante cada liquidação econômica **antes** da chegada, seu consumo é debitado exclusivamente da própria carga. Alimento não atendido é contabilizado como carência; o jogo não promete coleta instantânea nem antecipa um acampamento.
- No turno de chegada, o grupo volta a integrar a população de destino antes da liquidação local; eventual sobra permanece em `TravelFoodLedgers` como alimento de propriedade do grupo no destino, **não é automaticamente revendida nem incorporada ao estoque comercial**.
- Uma nova ação de journal, `depart_remote_food`, codifica a quantidade efetivamente comprada. O replay recalcula preço, dinheiro, estoque e movimento, verificando o hash de cada ação. O save V16 carrega identificador do lote, origem, destino, compra, saldo, consumo, preço e pagamento, além da situação de chegada.
- A versão de hash 16 vincula cada lote e sua conservação física, sem alterar um byte da serialização/hash de V9–V15. Viagens do regime V15 podem coexistir e mantêm seu consumo ancorado no mercado de origem. O botão V15 original continua existindo.

## Limites
- Sem execução das paradas e coletas hipotéticas da VS32, sem compras em waypoints, sem logística de outros modais e sem novos custos de frete.
- O alimento residual chegou como propriedade particular, não como stock de produtores. Uso, venda e futura carga de retorno exigirão uma regra explicitamente homologada.
- O processo não modifica a arquitetura do calendário VS31, velocidades, geografia, identidade política nem outros comandos legados.

## Gate
- xUnit Windows/Linux: aquisição real sem crédito, conservação alimentar/monetária, demanda sem duplicidade, consumo por ciclos, chegada, replay e coabitação V15/V16.
- Dotnet builds em Debug/Release; workflows de QA legado continuam obrigatórios.
- A ação real tem UI separada e identificada de forma distinta da simulação VS32.

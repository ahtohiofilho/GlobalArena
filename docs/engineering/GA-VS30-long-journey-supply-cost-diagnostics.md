# GA-VS30 — Diagnóstico de suprimentos e custo físico de oportunidade das viagens

Data: 31/10/2026. Base: GA-VS29/PR #133, **34/34 checks aprovados**. Diretriz GA-ADR-EXP-006 / issue #131.

## Objetivo
Mostrar ao jogador duas consequências **fisicamente fundamentadas** da viagem de longo curso: (1) demanda alimentar dos trabalhadores enquanto viajam; (2) produção que **deixam de realizar na atividade original**. Nada disso deve ser confundido com cobrança nova, preço de transporte, ração já comprada ou colonização de tiles intermediários.

## Modelo read-only
`PhysicalJourneySupplyProjection` consulta o mesmo `WorldState` e calendário do V15. Para uma rota **planejada** usa a estimativa canônica de chegada da VS28; para uma viagem **em andamento**, lê a ordem e o progresso canônico V15. Os dois modos devolvem:

- Turnos de movimento restantes e turno final de chegada;
- contagem exata de **liquidações econômicas** com o grupo em viagem, usando `PhysicalEconomicCadence.IsSettlementTurn` e o último ciclo efetivamente liquidado;
- demanda basal de alimento dos viajantes durante esses ciclos: `quantidade de trabalhadores × ciclos previstos × FoodDemandRawPerWorker`. É a mesma população que já consumia sob o V15. **Não é consumo adicional criado pelo diagnóstico**;
- produção física sacrificada em comparação contrafactual com continuar trabalhando na origem: `trabalhadores × ciclos previstos × rendimento primitivo canônico da atividade de origem`. Não é lucro, tributo, PIB ou dinheiro; diferentes recursos não devem ser somados como se fossem fungíveis;
- estoque alimentar **agora** no tile de origem e um limite superior de cobertura **somente com esse estoque**, indicando diferença para a demanda. Essa cobertura NÃO reserva estoque nem afirma que ele esteja disponível ao grupo: outros residentes competem por alimento e poderão ocorrer produção e compras futuras.

A chegada é resolvida pelo motor **antes** da liquidação econômica do turno final, portanto o diagnóstico conta somente liquidações **estritamente anteriores** ao turno de chegada. O cálculo funciona nas faixas 1/1, 1/2 e seguintes **sem alterar a política de cadência**.

## UI
Na mesma gaveta de ferramentas avançadas já existente: botão **DIAGNÓSTICO FÍSICO DE SUPRIMENTOS**. Se há viagem longa ativa entre origem e destino indicados, inspeciona essa ordem; caso contrário, estima uma possível partida para aquele destino, sem executá-la. A leitura mostra demanda alimentar, produção abandonada, estoque de origem e diferença física, com avisos contra interpretar cobertura como reserva ou tarifa. O resultado é descartado após mudança do estado ou seleção, evitando apresentação de dados desatualizados. O viewport tático não perde área.

## Segurança de estado e persistência
- Sem qualquer mutação, novo evento, gasto, receita, débito, transferência ou consumo físico. O projetor só lê `State`, `PrimitiveYieldAt` e `StockAt`.
- Saves V9–V15 e hashes mantidos; a exposição pública do **mesmo** `FoodDemandRawPerWorker` permite que novos serviços referenciem exatamente o parâmetro já usado pelo motor. Nada é duplicado nem alterado em gameplay.
- Não há **ainda** estoque portátil de rações, contratação de frete, abastecimento em escalas, preço/custo monetário, limite de viagem ou penalidade arbitrária por distância. A transferência de propriedade de alimento dependerá das regras econômicas aprovadas; nenhum recurso de outros produtores será requisitado implicitamente.
- Sem alterar movimento adjacente, chegada V15, cidades emergentes, alocação de trabalhadores, riscos ou controle político.

## Automação
- xUnit GA-VS30: origem e destino não adjacentes, calendário 1/1 e 1/2, quantidade de alimento por trabalhador, oportunidade produtiva, cobertura só por estoque, viagem V15 em andamento, save/replay, ausência de mutação e conservação de população.
- Godot E2E verdadeiro: botão de diagnóstico prospectivo antes de viajar, estado preservado, botão de ordem V15, diagnóstico ativo, save e reabertura preservando o mesmo diagnóstico; gate visual e screenshots continuam.
- Windows/Linux: Godot Debug/Release e regressões de VS15 e VS20–VS30. QA manual não exigido para leitura técnica já coberta por E2E.

## Próxima decisão
Para introduzir ração fisicamente transportada, será necessário especificar como trabalhadores adquirem alimento dos produtores **sem confiscar estoque e sem inventar crédito**, como se calcula capacidade/carga e como se liquida consumo sem contabilização dupla da demanda basal na origem. Essa política deve ser tratada antes de mutar o motor V15.

PR draft empilhada sobre GA-VS29, sem merge isolado na `main`.

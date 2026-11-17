# GA-Next — Gate 01B.2D-17: descarte estrito de roteamento global com input idêntico

**17/11/2026 · implementação experimental no coordenador progressivo C# · Draft sobre #409 · sem merge, Godot ou economia liquidada**

## Problema e decisão

O Gate 16 já removeu o trabalho repetido do bloco de rota independente. O gargalo remanescente é a proposta global de mercado: após recalcular a alocação segmentada com fretes congestionados, o coordenador sempre chama RouteFixedMarket e sua auditoria, inclusive se essa nova alocação reproduz exatamente o input de um candidato já roteado. Uma rejeição de proposta, por si, **não** prova que seria seguro dispensar o roteamento.

Este gate introduz apenas um **guard conservador de identidade completa** depois de executar o solver global, antes do segundo roteamento. Não altera solver, preços, demanda, congestionamento, frete, tarifas, bônus, limiar de ganho ou orçamento de propostas.

## Condição segura de reaproveitamento

IsSameRoutedMarketInput compara duas alocações globais sob a mesma fotografia econômica congelada:

1. número e ordem de entregas; Seller, SourceOccupation, SourceTile, Buyer, BuyerTile, Commodity, DeliveryOrdinal, GoodsMicro e cada RouteTile em ordem;
2. número de mercados, MarketTotals completos (identidade, demanda, quantidade), ProductiveLotCount e RealMarketCount;
3. quantidades auditadas de bens disponíveis e alocados;
4. mesmo ambiente físico e econômico já garantido pelo estado imutável do coordenador: censo, incidência Goldberg, políticas, preços, estradas, escalas, segmentação e limites.

O caminho original que produziu o incumbente já executou RouteFixedMarket com o mesmo input completo. O resultado dessa função é determinístico e auditado. Portanto, **somente quando todas as condições coincidirem**, o coordenador pode usar o próprio plano físico auditado e seu potencial BigInteger exato, sem repetir roteamento, auditoria e scoring.

Não aceitar igualdade parcial de compradores ou volumes: se os caminhos basais mudarem, o multipath pode aceitar outro resultado ou escolher o fallback original, alterando o potencial. A primeira proposta após mudanças de topologia, censos, compradores, cotações ou estradas também precisa cumprir a prova estrita; caso contrário, executa o caminho normal.

## Contabilidade e governança de falha

Mesmo quando houve identidade, a proposta global continua sendo avaliada e contabilizada, como **rejeitada**; NextProposalIndex, CompletedPasses, estabilidade e histórico monetariamente não liquidado permanecem com o mesmo significado. IdenticalMarketRouteElisions é telemetria do novo guard, zerada no início de cada época econômica, não parte da função de preço ou da decisão do jogador.

O ponto interno AdvanceWithOriginalMarketRoutingForTests preserva a decisão do Gate 16 de não recalcular a rota apenas fixa, mas **desativa este novo guard**. É a referência A/B exata da alteração. A versão pública Advance usa os dois cortes com os mesmos contratos de conservação.

Não usar um limite superior otimista de receita para saltar o roteamento: isso poderia suprimir uma falha de carga física, um orçamento exaurido ou um destino inalcançável. O guard atual evita esse problema porque o **mesmo input** já foi roteado com êxito no ambiente atual.

## Evidências exigidas

- Fixture de um trabalhador real que produz e demanda no próprio tile: um único mercado real com todos os bens vendidos localmente. Reproduzir a mesma solução após solver global deve acionar IdenticalMarketRouteElisions, preservar o incumbente, incrementar rejeição e reproduzir potencial, contadores e convergência da referência sem guard.
- Cenário Goldberg com dois mercados e frete congestionado: comparar a trajetória de propostas 1 a 1, inclusive cotação reavaliada, mudanças auditadas de titular/custo/tarifa/escala e limiar de ganho elevado. Mesmos compradores, quantidades, rotas, potencial, frete, histórico e estabilidade em relação ao solver original.
- Stress existente com **112 tiles / 8 produtores reais / 104 mercados** e conservação de 8.000.000 micro-unidades. Comparar uma proposta global sob a mesma condição. Se não houver identidade, **não** alegar otimização: o fallback deve produzir o mesmo resultado da referência.
- Arquivo JSON schema ga-next.identical-market-route-elision-ab.v1 por runner, certificando equivalência, StateHash íntegro, ausência de ciclo fechado, sinal explicitando se houve corte e tempo de parede/bytes gerenciados acumulados na thread.
- Validador PowerShell na CI Windows/Ubuntu. Testes completos históricos e cinco workflows verdes no HEAD antes da homologação.

## Limites

A operação global Coordinate continua sendo executada integralmente; apenas **o reroteamento estritamente duplicado** pode ser omitido. Não esperar ganho em todas as propostas ou mundos: o novo guard pode não acionar em uma amostra de 104 mercados, situação considerada corretamente auditada. A métrica de tempo é uma observação de parede por runner, não tempo CPU exclusivo ou SLA; bytes medidos não são pico de RAM. Este gate não demonstra eficiência em milhares de nós.

A próxima oportunidade para CPU está dentro do solver global e do plano de rotas quando houver alterações reais na alocação, onde precisaríamos de solução incremental sem descarte indevido.

**Mudanças mantidas em Draft, sem merge nem liquidação.**

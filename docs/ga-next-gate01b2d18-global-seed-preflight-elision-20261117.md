# GA-Next — Gate 01B.2D-18: preflight compartilhado sem distribuição sequencial descartada

**17/11/2026 · C# experimental · PR Draft empilhado sobre #410 · sem merge ou liquidação econômica**

## Diagnóstico

O `FoundationPhysicalGlobalConcaveFlowPlanner.Coordinate` instanciava uma **distribuição marginal sequencial completa**, produzindo candidatos, calculando rotas, otimizando quantitativos e auditando as entregas. Porém a única saída consumida pelo solucionador global era `seed.AllocationAudit.MarketInputs`, que já era obtida com a projeção oficial de demanda física a partir dos mesmos trabalhadores. O próprio solucionador global então reconstruía o grafo, calculava caminhos e resolvia o fluxo segmentado exato com novas auditorias. Esse trabalho intermediário era descartado sem alterar a decisão global.

A oportunidade é eliminar **essa alocação temporária que não agrega nenhuma restrição** ao solver global, sem cortar etapas da solução escolhida, sem reduzir precisão ou introduzir aproximação de preços.

## Contrato da implementação

- A validação real de trabalhadores, política de demanda e política de cargo e todas as **cotações positivas e únicas por linha de demanda real** foi centralizada em `FoundationPhysicalMarginalMarketDistributor.ValidateMarketAndQuotes`.
- O distribuidor sequencial público `Distribute` continua funcionando com a mesma validação e com seu processo de distribuição, reconstrução de rotas e auditoria. Não há mudança da API pública.
- O solucionador global utiliza diretamente os dados do preflight, o mesmo `BuildGraph` físico, a busca de caminho original `RouteFrom`, o mesmo `FoundationPhysicalConcaveCommodityFlow.Solve` segmentado `BigInteger`, reconstrução de cada entrega e auditoria física final `FoundationPhysicalDeliveryAllocationAuditor.Audit`.
- O caminho interno `CoordinateWithSearchWorkspace(..., useSequentialSeedReference: true)` preserva integralmente a estratégia anterior, executando `Distribute` antes da alocação global, sem utilizar a alocação temporária como restrição. `false` (padrão) usa apenas preflight completo. O parâmetro está no API `internal` da pesquisa, não na API pública de jogo.
- O orçamento de augmentations, a estrutura de fluxo residual, a segmentação da curva marginal, os custos físicos, os pedágios e a função econômica são **idênticos** entre os caminhos.

## Restrições importantes

O plano sequencial descartado tinha sua própria auditoria. Sua remoção não autoriza prescindir das invariantes fundamentais. O novo caminho valida catalogação de commodities e quotes, usa `BuildGraph` para validar a topologia/custo/titularidade, usa o solver global para provar a acessibilidade das ofertas e a conservação, e **audita todas as entregas finais** independentemente. Quantidades incompatíveis com a conversão física não serão aceitas; falhas de fluxo e orçamento seguem fechadas, não retornam lote parcial.

A/B adversarial compara modo de referência e otimizado com **112 tiles Goldberg conectados, 8 produtores físicos, 104 mercados reais e 8.000.000 micro-unidades**, em 2, 4 e 8 segmentos por mercado. Igualdade exata obrigatória em: quanta por destino, identidade e quantidade de cada entrega, caminhos, fluxo agregado, frete, número de augmentations, potencial segmentado/integrado, `StateHash`, inexistência de `LastClosedCycle` e zero liquidação.

Também comparar falha fechada quando houver cotação faltante, cotação duplicada, rodovia com custo inválido e orçamento insuficiente do fluxo global.

## Observabilidade

A CI de Ubuntu e Windows executa o teste A/B, valida os três arquivos JSON por runner com `scripts/GA-Next-Global-Seed-Elision-AB-Report.ps1`, publica resumo e preserva artifacts `ga-next-global-seed-elision-<OS>`. Cada arquivo possui schema `ga-next.global-seed-elision-ab.v1` e registra duração de parede e alocações gerenciadas da thread.

Os números são uma medição isolada por runner, sujeitos a escalonamento, JIT e ruído de CI. **Não são tempo puro de CPU, RAM máxima, garantia estatística ou prova de escalabilidade em milhares de nós.** Diferenças de tempo e memória não constituem um critério de aceitação; exatidão e conservação constituem.

## Governança

**Gate sujeito à CI do HEAD do Draft PR**, 5 workflows integralmente verdes e relatórios A/B presentes. Nada na `main`, em Godot, nos preços, na política tarifária ou no estado autoritativo do mundo é alterado. A próxima melhoria deverá basear-se em custos internos efetivamente medidos, não na exclusão de cálculos econômicos necessários.

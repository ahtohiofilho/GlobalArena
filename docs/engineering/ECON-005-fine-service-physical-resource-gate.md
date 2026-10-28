# GA ECON-005 — Gate D1: proveniência física de serviços e transferências

**Estado:** PR independente e empilhado sobre #41. Proposta de integração de tipagem e auditoria; **não é** implementação do clearing GA-PROP-001, da seleção de regimes nem de liquidação.

## Fonte de verdade reaproveitada

O repositório já possui `EconomicTransportServiceQuoteResolver.Resolve(...)`, que valida, para uma geografia fina declarada:

- Âncoras econômicas válidas na mesma malha fina;
- Ativos construídos em arestas reais e na resolução geográfica informada;
- Continuidade espacial dos ativos em ordem de circulação;
- Correspondência dos pontos de origem/destino;
- Estado operacional, acessos fechados e versões de tecnologia;
- Declaração de exatamente cada mudança de tecnologia em `EconomicTransportServiceOffer.TransferAfterLeg`;
- Identidade da oferta selecionada e preço de frete ex ante, congelado naquele turno.

**Não construir segundo roteador.** Esta camada aceita a seleção *apenas* a partir desse resolvedor e converte sua proveniência tipada em incidência de recursos físicos. O adaptador `EconomicFineServicePhysicalResourceAdapter.Resolve` chama novamente o resolvedor de cotações em cada invocação, contra o estado de geografia e acesso fornecido.

## Nova correspondência tipada

`EconomicPhysicalAssetResourceBinding(assetId, physicalResourceId)` define um recurso de custo para um ativo. Nenhuma aresta geográfica é convertida implicitamente em recurso. Ativos diferentes têm identidades de recurso distintas; um ativo compartilhado entre dois serviços reutiliza a mesma identidade física.

`EconomicPhysicalTransferResourceBinding(serviceId, afterLegIndex, physicalResourceId, fineTransferTile)` declara a interface/custo de transbordo. O adaptador exige a transferência se, e somente se, duas pernas consecutivas **mudam de tecnologia**. A localização declarada deve coincidir com a célula fina de junção obtida da cadeia topológica validada. Recursos de transferência nunca podem ser aliases de ativos de travessia; o mesmo terminal pode ser referenciado por diferentes serviços apenas na **mesma** célula fina. A enumeração de serviços, ativos e correspondências não altera a seleção determinística.

A saída é `EconomicReclearingQuotedRoute` com cotação congelada e lista de `EconomicPhysicalResourceId`; pode ser passada ao `EconomicReferenceScenarioReclearingProbe` do #41, que calcula quantidades efetivamente negociadas e custos operacionais hipotéticos sem mudar o estado do jogo.

## Limitações explícitas

1. **Uma correspondência declarada não comprova que o terminal foi construído.** Ainda não há livro patrimonial autoritativo de instalações de transbordo. Para evitar criar um subsistema ou instalações fantasma no lançamento, o adaptador só verifica posição/coerência física dos identificadores e não realiza settlement. Integração produtiva exigirá autoridade de catálogo de instalações/serviços, inclusive propriedade, operação e bloqueios pertinentes.
2. A descoberta de `EconomicFineTransportServiceItineraryDiscovery` do repositório é **monotecnológica**; transferências ainda são ofertas explicitamente declaradas, não descobertas espontaneamente pela malha. Isso não altera esse contrato.
3. Uma rota desativada ou bloqueada não produz serviço validado; transferências declaradas para uma oferta não selecionada são rejeitadas (fail closed) no adaptador de cenário.
4. O resolvedor atual seleciona uma cotação por par origem/destino, não várias concorrentes simultaneamente. Não inferir arbitragem ou repartição multimodal completa.
5. Os perfis `EconomicDensityResourceCostProfile` representam **custo real** com economias de densidade, não tarifa; a cotação comercial não é recalculada retroativamente.
6. Nenhum dinheiro, estoque, contrato bancário, empréstimo, garantia, dívida, mutação de `WorldState`, evento autoritativo ou seleção automática de equilíbrios é criado.
7. O GA-PROP-001 completo, incluindo preços de origem endógenos, potencial de preferência, `H` e regras de participação, **ainda é uma etapa futura**.
8. A cobertura de performance e integridade precisa incluir mundos reais grandes e serviços multi-origem; os testes atuais são referências pequenas.

## Testes e ordem de consolidação

`dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~EconomicFineServicePhysicalResourceAdapterTests`

Incluir regressões integrais do kernel, hashes e benchmarks em Windows, Linux e macOS; não proclamar homologação enquanto a CI não terminar.

Ordem de PRs: #38 (auditoria) e #39 (exploração convexa) independentes; #40 empilhado sobre #39; #41 sobre #40; **este PR #42 sobre #41**. Não fazer merge de uma branch base ainda não integrada nem modificar a `main` implicitamente.

**Próxima necessidade econômica:** ligar o clearing GA-PROP-001 aos preços de origem endógenos, assegurando que cada candidato seja resolvido e auditado antes de qualquer decisão ou liquidação à vista. Não abrir sistema financeiro.

# GA-Next R66 — contrato de poda marítima compatível com custo marginal de frete

**Status:** gate de segurança experimental. Não integrado ao solver econômico, preços, propriedade ou settlement.

## Diagnóstico do código efetivamente existente

1. `FoundationPhysicalTradeRouteCandidatePlanner.RouteFrom` cobra **physical freight + external tariff somente quando o dono da aresta difere do seller**, com aritmética inteira `checked`, desempate estável e caminho físico real. Proprietário nunca fica isento do custo físico.
2. `FoundationPhysicalGlobalMarginalCoordinator.Coordinate` agrupa a árvore de rotas por **(tile de origem, owner)**, distribui lotes de produção reais e utiliza cotações externas/curvas marginais. O arquivo é explícito: não certifica equilíbrio simultâneo global nem liquidação financeira.
3. `FoundationPhysicalLaggedFreightCyclePlanner.CoordinateWorldCandidate` recebe uma projeção canônica do `WorldState`. `CoordinateUniversalCandidate` exige `HasSettledProvenance` no congestionamento de ciclo anterior; não pode usar resultado de preview como insumo do turno. `FoundationPhysicalSettledFreightQuoteUpdater.FromClosedAudit` atualiza o sobretaxa somente a partir de auditoria *fechada*, e não recalcula o turno corrente.
4. `FoundationPhysicalGlobalConcaveMultiPathPlanner` possui uma pesquisa separada para frete quartico prospectivo por carga; isto **não é equivalente** ao contrato da cotação de ciclo congelada. O bound aqui **não** é automaticamente válido para qualquer roteamento que altere o custo durante a busca.

## Resultado do gate

Criado `FoundationPhysicalMaritimeMarginalFreightBound.Evaluate` exclusivamente para verificar a regra de decisão, com parâmetros tipados em **micro-moeda por unidade inteira da MESMA carga**, do MESMO snapshot e do MESMO vendedor:

`LB = landToA + (physicalCoastA + tollThirdPartyA) + positiveSeaLegMinimum + (physicalCoastB + tollThirdPartyB) + landFromB`.

- `tollThirdPartyX` é tarifa externa **apenas** quando existe titular e ele difere do vendedor; proprietário ainda paga `physicalCoastX`.
- A tarifa do **mesmo snapshot** deve ser não negativa. A sobrecarga de congestionamento anterior já deve estar **incorporada** na cotação `PhysicalCostMicroPerWholeCargoUnit`, sem somá-la duas vezes. Não inventar custo com base no volume atual.
- `positiveSeaLegMinimum = 1` somente quando a função inteira R54 garante pelo menos um micro-moeda na perna completa. Não aplicar **por aresta mar-mar**.
- Se não há melhor rota terrestre **factível nesse mesmo domínio de custo**, retornar `CanExcludePair=false`.
- Se `LB > LandFeasibleUpperBound`, excluir o par seguramente. Se `LB = LandFeasibleUpperBound`, manter para preservar desempates. `UInt128` impede overflow silencioso do limite.
- Registros com tarifa sem titular, IDs costeiros coincidentes, proprietário inválido ou mínimo marítimo nulo são rejeitados.

## O que esse contrato NÃO prova ainda

- A função isolada não verifica se duas arestas correspondem de fato a travessias terra-mar naquele `WorldState`; os testes escolhem arestas costeiras reais, mas a produção requer um adaptador mundo-vinculado que valide isso.
- O chamador deve fornecer os dois caminhos terrestres e a rota terrestre superior calculados por **mesma identidade de seller, mesma mercadoria/unidade de carga e mesmo snapshot fechado**. Não usar um upper bound físico R62 em uma cotação de frete monetário de outra política; não usar um limite de outra mercadoria ou outro proprietário.
- Não substitui o `RouteFrom` terrestre (que já tem regras de owner e tarifa), nem calcula um novo caminho multimodal, nem contabiliza pagamento de tarifa, carga, receita ou impostos.
- Se um modelo futuro admitir subsídios (custos negativos), travessias oceânicas de custo zero ou múltiplas pernas com desconto não separável, **suspender a prova**, sem forçar o resultado.
- Não usar este gate para promover cotações do ciclo seguinte a frete atual, nem para aceitar as políticas quarticas de `GlobalConcaveMultiPathPlanner` sem uma demonstração própria.

## Critério de integração e retorno marginal

**Próximo avanço autorizado:** um adaptador apenas de cotação que recebe `WorldState` congelado, valida as travessias costeiras, o mesmo seller e carga, lê os custos de `FoundationPhysicalRoutingEdgeCost` projetados pelo mesmo fechamento e calcula limites terrestres pelo planejador existente. Deve apresentar testes end-to-end A/B de custo marginal e IDs de aresta sem alterar distribuição ou liquidação.

**Não autorizado:** reescrever tarifas, ativar o roteador experimental R55–R65 no waterfilling, acrescentar outro cache, duplicar o congestionamento nem fazer merge das PRs empilhadas. Antes de outro esforço de desempenho, medir custo de coordenação, não mais repetir testes de Dijkstra. Se o ROI decrescer novamente, reconsiderar o contrato econômico antes de otimizar.

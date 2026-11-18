# Global Arena — Gate de pré-conciliação financeira do mercado físico

## Por que ainda NÃO liquidar financeiramente o jogo

O jogo já possui censo autoritativo v11 de trabalhadores, demanda positiva por atividade, produção física, rota/entrega física auditada, preço-base universal e um boundary de frete defasado que só publica após **um fechamento econômico real**. Mas ainda faltam pelo menos dois insumos autoritativos:

1. **Infraestrutura pertencente ao mundo:** os custos, capacidades e proprietários das arestas ainda são fornecidos pelo chamador dos coordenadores como dados de laboratório. Não é seguro permitir que um cliente declare ser proprietário de uma rota e provoque tarifas ou congestionamento no histórico econômico.
2. **Mercado único por tile e produto:** o algoritmo marginal herdado atualmente diferencia \`(tile, civilização compradora, produto)\`. Em tiles com trabalhadores de civilizações diferentes, isso produz dois preços marginais separados, embora o usuário tenha definido uma função comum para **um mercado físico compartilhado**. Integrar silenciosamente esse algoritmo como liquidação seria uma violação da regra.

O objetivo deste gate é preparar **contabilidade reproduzível, exata e rejeitável**, sem certificar uma venda inexistente ou alterar WorldState, StateHash, tesouro ou visibilidade dos jogadores.

## Contrato de preço inegociável

Para qualquer tile, produto ou civilização:

\[
P(Q) = \sqrt{D / Q},\quad Q>0
\]

A escala base é **1 moeda inteira por item inteiro em Q=D**; em micro-unidades de preço: \`1_000_000UL\`. Trabalhadores, inclusive produtores, geram demanda positiva. A pré-conciliação recalcula \`D\` do censo real e \`Q\` das entregas auditadas, não aceita cotação arbitrária. Quando um mercado tem Q=0, ele não possui preço de transação (não se inventa estoque nem preço inicial).

O preço realizado por item no lote é o **preço marginal final do respectivo mercado**, determinístico por raiz quadrada inteira. Não é o benefício integrado \`2√(DQ)\`, que o algoritmo pode usar como função auxiliar de otimização, mas que **não é o preço recebido pelo vendedor**. Esse detalhe exige testes independentes antes da futura liquidação.

**Limitador protetivo:** \`FoundationPhysicalEconomicAccountingReconciler.EvaluateCandidate\` rejeita qualquer cenário com mais de uma civilização demandante na mesma combinação \`(tile, produto)\`. Não são inventados preços diferentes nem se fundem artificialmente mercados apenas na hora de pagar. O solver de destinos deverá ser corrigido para consolidar demanda por mercado físico antes de liberar o commit.

## Dinheiro: precisão determinística sem centavos artificiais

Todo valor monetário é representado por um \`BigInteger\` em escala racional comum:

\[
\mathrm{MoneyScale} = 10^6\;(\mathrm{goodsMicro})\;\times 10^6\;(\mathrm{priceMicro})\;\times 10000\;(\mathrm{taxBasisPoints})=10^{16}.
\]

Para cada entrega real, calcula-se:

- \`gross = q_goods × priceMarket\`.
- \`physicalFreight = cargoMicro × soma(custo físico fixo das arestas percorridas)\`. Esse custo inclui **apenas** congestionamento defasado que já estava congelado antes do water-filling; não recalcular frete segundo a carga do turno atual.
- \`externalToll = cargoMicro × soma(tarifa externa de aresta cujo dono ≠ vendedor)\`. Titular é isento.
- \`tax = gross × taxaExplicitamenteInformada / 10000\`, **somente após existir receita**, com taxa explícita para toda civilização inclusive quando zero.
- \`netSeller = gross − physicalFreight − externalToll − tax\`.
- \`ownerTollIncome\` reconcilia exatamente com a soma dos pagamentos de pedágio externo, sem duplicação ou carteira física de compradores.
- A soma de todas as receitas é **igual** à soma de fretes físicos, tarifas, impostos e saldos líquidos dos vendedores. Sem arredondamento silencioso por entrega. Um retorno líquido negativo rejeita a proposta em vez de obrigar o trabalhador a vender por prejuízo.

Não há compradores com carteira, moeda de inventário, estoque físico, tributo inventado, tesouro modificado nem pagamentos autoritativos. \`IsCommitted\` permanece permanentemente false nesta etapa.

## Integração técnica

- \`FoundationPhysicalEconomicAccountingReconciler.EvaluateCandidate\` recebe somente o censo físico, incidência canônica, política de demanda, conversão em cargo, plano de frete fixo e taxas explícitas.
- Reexecuta a auditoria física independente, verifica as mesmas entregas, demanda e quantidades, tarifa/ownership de cada rota e qualquer divergência de unidade.
- Prepara linhas imutáveis por transação e contas por civilização, incluindo cobrança isenta para o dono, crédito de pedágio e imposto sobre receita. Nenhum método produz \`FoundationSettledEconomicTradeCycleArtifact\`; o PR #418 é respeitado.
- A infraestrutura está explicitamente marcada como fonte externa **ainda não autoritativa**. Antes de publicação de um ciclo, ela deve estar no WorldState ou em registro autoritativo equivalentemente vinculante ao hash.
- Este gate não altera \`FoundationPhysicalGlobalMarginalCoordinator\`, \`FoundationPhysicalLaggedFreightCyclePlanner\`, custo de congestionamento, StateHash, jornal de turnos, Godot nem rotinas de IA.

## Próximo gate

Dar ao mercado uma identidade física \`(tile,commodity)\` para a demanda agregada de múltiplas civilizações (sem preço artificial de civilização), e ligar infraestrutura/custos/proprietários a um estado de mundo autenticado. Depois disso, implementar liquidação e publicação com journal/replay financeiro, tomando esta pré-conciliação como verificação independente antes do commit.

A saída deste gate é **um recibo contábil candidato**, nunca uma venda final. A cautela é intencional: uma “integração” que permite inventar infraestrutura ou dois preços para o mesmo mercado violaria as regras centrais de Global Arena.

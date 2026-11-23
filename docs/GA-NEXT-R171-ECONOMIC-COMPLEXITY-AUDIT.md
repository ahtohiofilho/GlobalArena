# GA-Next — Auditoria R171: origem da complexidade econômica

## Contrato simples que queremos preservar
- Toda mercadoria produzida é vendida/consumida no ciclo; não há inventário físico ou carteira de compradores.
- Demanda e produção vêm de trabalhadores reais. Os registros temporais R164/R167/R168 dividem a presença física, a demanda e o esforço produtivo por nó e por fração de turno, sem criar trabalhadores fictícios.
- Uma curva universal de preço por mercadoria e nó depende de oferta/demanda; transporte e tarifa aplicam custos físicos/monetários de arestas.
- **Nenhum volume transportado deve reprificar frete no mesmo turno por congestionamento.** Custo de aresta é fixado pelo estado da infraestrutura e investimento no turno.
- Determinismo e conservação são obrigatórios; otimização matemática infinitesimal não é objetivo.

## Rastreamento do pipeline R168-R171

1. `FoundationTemporalEconomicTurnProjector.Project` distribui tempo do trabalhador entre nós, identifica atividade, demanda e esforço. Não executa solucionador de mercado.
2. `FoundationTemporalNativeMarketInputs.FromTemporal` agrega essas frações em **linhas de demanda e lotes físicos usuais**, sem criar uma nova fórmula.
3. `FoundationTemporalNativeMarginalMarketPlanner.Coordinate` transforma o estoque de infraestrutura em **custos fixos por aresta** com `FoundationPhysicalAuthoritativeTransportProjector.FromWorld` e chama `FoundationPhysicalGlobalMarginalCoordinator.Coordinate`, com teto padrão **64 passagens**.
4. `FoundationPhysicalMarginalMarketDistributor.DistributeOneLot` resolve um produtor por **bisseção inteira** do preço marginal. A complexidade é aproximadamente logarítmica na faixa representável, não uma iteração por micro-unidade.
5. **Origem dominante da iteração extra**: `FoundationPhysicalGlobalMarginalCoordinator` passa por TODOS os lotes; em cada passagem recalcula `DistributeOneLot` com oferta de outros produtores já entregue; exige uma passagem completa sem movimentação para certificar estabilidade. O teto de 64 passagens e a exigência de ponto fixo não vieram da movimentação temporal nem do congestionamento.
6. `FoundationTemporalFinancialReconciler.Evaluate` reutiliza o coordenador, audita vendas, calcula impostos e créditos monetários.
7. `FoundationTemporalEconomicJournalGate.CloseAndReplayWithInvestments` resolve uma vez e executa **replay independente**, que volta a derivar a liquidação. Portanto, o peso do coordenador pode ser pago pelo menos duas vezes na mesma resolução autoritativa; esse custo decorre de validação, não de demanda fracionária.

## Congestionamento: fora do caminho ativo
`FoundationPhysicalEndogenousCongestionFeedback` ainda existe como laboratório de planejamento e implementa retorno custo-volume, mas o caminho `TemporalNativeMarginalMarketPlanner → TemporalFinancialReconciler → TemporalEconomicJournalGate` NÃO o chama. `FromWorld` declara expressamente que seus custos fixos não dependem de capacidade/carga; a capacidade antiga permanece por compatibilidade histórica. A presença desses tipos no repositório não significa que congestionamento volte à economia autoritativa.

## Questão arquitetural aberta
A regra de redistribuição global é uma escolha anterior, **independente** da movimentação fracionária. Ela tenta encontrar um ponto fixo em que produtores não mudam individualmente suas escolhas diante das vendas dos demais. Isso não equivale a um equilíbrio global demonstrado. Seus muitos passes não são uma consequência necessária de trabalhadores distribuírem sua demanda por múltiplos nós.

## Próximo gate — medição antes de alterar matemática
1. Em cenários reais pequenos, médios e escaláveis, coletar `ProductiveLotCount`, `Passes`, `MovedGoodsByPass`, tempo do cálculo de distribuição, tempo do replay e custo total por turno, **sem introduzir otimizações**.
2. Comparar o primeiro `Distribute` com a solução iterada em receitas, quantidades e rotas, quantificando o ganho incremental por passagem e a proporção de casos que realmente precisam mais que poucas passagens.
3. Se o ganho marginal for pequeno, simplificar o critério de estabilização e/ou eliminar a camada de reequilíbrio global **por decisão de regra econômica**, não reduzir cegamente 64 para outro número.
4. Manter teste de conservação, determinismo e a invariância do preço universal. Nunca publicar resultado instável como se fosse matematicamente estável.
5. Nenhuma mudança no solucionador ou na fórmula foi realizada por esta auditoria.

## Plataforma
Windows + Linux passam a ser gates obrigatórios; `optional-macos-determinism.yml` preserva auditoria manual de determinismo. A remoção do macOS das matrizes automáticas não é declaração de incompatibilidade e não implica certificação Apple.

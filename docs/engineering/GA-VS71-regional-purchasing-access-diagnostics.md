# GA-VS71 — Diagnóstico regional de poder de compra e acesso ao frete comercial

**Baseline:** VS70 PR #177, commit `e937995fa741bc40b0f614bb07f511b9646eceac`, **36/36 workflows aprovados**. A VS71 nasce empilhada em Draft, sem merge em `main`.

## Decisão

A VS70 diferenciou o poder de compra **abstrato** de cada povoamento a partir da produção física normalizada por residente. A VS71 não cria salários, transferências, estoque, comércio efetivo ou outro algoritmo de mercado: ela explica, sob o **mesmo mundo**, **os mesmos serviços e preços nominais**, como os compradores/tile e produtos aparecem na seleção de frete prevista pelos modelos VS65–70.

São **dois** cenários alternativos e não cumulativos, `baseline` uniforme e `regional-productivity`, cada um auditado pelo pipeline já existente. A VS71 fornece um painel numérico **read-only por comprador e commodity**, com:
- residentes físicos do tile, fator de poder de compra aplicado, orçamento bruto hipotético, H protegido, gasto máximo previsto e compromisso total planejado;
- para **cada** demanda física corrente, quantidade demandada, quantidade selecionada para frete, número de ofertas legalmente elegíveis presentes no portfólio selecionado e fração da demanda que o frete hipotético poderia atender;
- indicador de cobertura média por comprador, média regional das frações por par comprador/commodity, mínimo e máximo poder bruto per capita;
- contadores de pares comprador/commodity sem frete institucional ofertado e sem remessa selecionada.

A diferença entre **ausência de oferta comercial autorizada** e **ausência de remessa selecionada** é essencial: um comprador pode ter um serviço disponível e não comprá-lo por seu orçamento, pela produção/disponibilidade física ou pelo custo do corredor. A projeção **não atribui causalidade única** ao segundo caso.

### Uma distinção obrigatória

**Frete planejado não é consumo efetivo.** Um morador pode produzir para autoconsumo, utilizar recursos locais ou atender necessidades por mecanismos que não fazem parte do catálogo de serviços autorizados da VS64. Cobertura zero por frete não significa fome, exclusão total do mercado ou poder de compra nulo. Além disso, o score é uma **média de frações adimensionais**: nunca se somam unidades físicas brutas de pedra, alimentos e madeira como se fossem uma mercadoria única.

Os números monetários são uma representação provisória do **poder de compra abstrato**, não carteira, depósito, salário, crédito, dívida, emissão de moeda ou receita futura. O resíduo H não é depósito bancário e permanece protegido para **cada** povoamento. O modelo não propõe política de redistribuição ou ranking de bem-estar.

## API, custos e verificações

`CanonicalPrimitiveFreightPortfolio.DiagnoseRegionalFreightAccess(candidates, postedPrices, baselinePolicy, congestionPolicy, region, rawQuantum, maximumQuantaPerCandidate, maximumConfigurationsPerComponent)`.

`CanonicalPrimitiveRegionalAccessDistribution` contém recibos imutáveis `Baseline` e `Regional` e a `Comparison` VS68. A fonte de produtividade regional deve vir do **mesmo objeto de descoberta de sítios** usado no portfólio; simplesmente compartilhar um objeto de sessão não autoriza misturar políticas de descoberta diferentes.

`VerifyIndependent()` primeiro exige que a VS68 reexecute e audite os lotes VS67/VS66, os budgets VS65 e corredores. Em seguida reconstrói por um caminho de agregação próprio os livros por comprador e commodity a partir de fluxos físicos reais, lotes selecionados e projeção VS62/70, cotejando racionais exatas, H, cargas indiretas, orçamento total, demanda física e os indicadores finais. Reexecuta o diagnóstico para todos os compradores mesmo quando **nenhum lote foi selecionado**, sem fabricar uma preview de frete inexistente.

Os limites de até 12 candidaturas, máximos de quanta/combinações por componente e dois cenários da VS70 permanecem intactos. Qualquer preço faltante, candidato falsificado, política incompatível, origem regional distinta, save restaurado ou revisão de rede torna o recibo inválido.

### QA

As regressões usam os cenários físicos já validados:
1. Um comprador com três produtos e moradores no tile fornecedor, conferindo todas as demandas, H e médias de fração calculadas independentemente;
2. Duas estradas autorizadas com compradores distintos, verificando a contagem de oferta institucional separada por commodity (nunca inferida para outro produto);
3. Preços que tornam todo frete inacessível, mas preservam a projeção completa da população, demanda e poder abstrato, sem construir remessas fantasmas;
4. Comparação uniforme/regional mantendo residentes, demanda física, H e ofertas, com diferenciação de poder de compra normalizado;
5. Reordenação de candidatos e preços, alteração de arrays do chamador após o snapshot, provas de sessão obsoleta, falsificações e preços incompletos.

Benchmark `--vs71-regional-access`: 8/32/128 consultas repetidas **sobre um cenário fixo conectado de três mercadorias**, aquecimento e três amostras, aferindo custo da auditoria/relatório; não representa 128 cidades nem simulação de mundo massivo.

CI Linux: regressões VS40–VS71, benchmark Release e Godot Mono Release. CI Windows: regressão integral, benchmark Release e Godot Mono Debug. Homologação somente após **todos** os workflows do head final em `completed/success`.

## Governança

`CanAuthorizeRealSettlement=false`, `IsCommitted=false`. Os moradores continuam simultaneamente produtores e consumidores; cidades formais não são fundadas. Nenhum novo sistema financeiro, micromanagement, contrato de transporte, estoque ou SETTLE é liberado por este relatório. O PR fica em Draft, empilhado sobre VS70, com `main` intocada.

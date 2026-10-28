# GA ECON-005 — Gate E4: exploração e auditoria independentes de suportes logísticos

**Estado:** PR de avaliação hipotética empilhado no #45. Nada altera `WorldState`, preços autoritativos, estoque, instalações, finanças ou comandos do jogador.

## Fronteira da decisão

É incorreto comparar dois caminhos alterando apenas uma remessa do estado antigo e depois supor que as demais quantidades, preços de origem, diversidade e custos compartilhados permanecerão iguais. Um novo suporte deve ser resolvido **do zero**, como candidato econômico independente.

`EconomicInteriorSupportExplorer.Explore` recebe:
- Uma única população, orçamento, oferta por origem e regras GA-PROP-001 compartilhadas por TODOS os candidatos.
- Identificador estável explícito do suporte de referência (baseline).
- Dois ou mais suportes completos, cada qual com incidências de itinerários e perfis reais de custo dos recursos físicos; cada suporte recebe suas próprias tarifas iniciais apenas como semente numérica.

Para cada suporte, executa `EconomicInteriorPhysicalMarketProbe.Evaluate` do #45: calcula preços de origem, compras, residual `H`, cargas compartilhadas e tarifa média de recuperação de custo em um suporte interior de rotas fixas. Um suporte que não converge é **rejeitado**, não convertido artificialmente em disponibilidade ou aprovação.

## Auditoria adicional e independente

Depois da solução, a classe `EconomicInteriorSupportExplorer` reconstrói sem reutilizar os resíduos reportados pelo solucionador:
1. Identidades de compradores/origens, suporte positivo e dimensionalidade;
2. Utilidade e disposição marginal de pagar `M_ij` a partir da **função canônica GA-PROP-001**;
3. Orçamento residual individual `H_i + sum_j(p_j + t_ij)q_ij = B_i`;
4. Venda total por origem `sum_i(q_ij)=S_j` e recebimento bruto `p_j*sum_i(q_ij)`;
5. Cargas em microgramas a partir de todas as vendas e incidências OD, por `EconomicPhysicalResourceId`;
6. Custos físicos agregados recomputados por recurso usando os limites racionais do #40;
7. Identidade de recebimentos `compras = receita das origens + frete cobrado`.

Um candidato é comparado com a referência **somente após** passar essa auditoria. A variação de custo físico real é expressa pelo intervalo racional assinado:

```text
delta.lower = candidate.realCost.lower - baseline.realCost.upper
delta.upper = candidate.realCost.upper - baseline.realCost.lower
```

A classificação `CertifiedCostDecrease`/`CertifiedCostIncrease` é permitida apenas se **todo o intervalo** tiver sinal definido; cruzar zero retorna `Indeterminate`.

A saída também contém, por agente, `ΔU` (utilidade, incluindo o custo de oportunidade do residual H) e `ΔRevenue` de cada origem. `NoBuyerUtilityLossWithinTolerance` e `NoOriginReceiptLossWithinTolerance` são indicadores **descritivos** sob orçamentos e ofertas fixos; **não** demonstram lucros líquidos, viabilidade política, direitos de acesso, ótimo global, estabilidade ou autorização institucional.

## Benchmarks e limitações

Testes exercitam:
- Suporte fechado compartilhado versus alternativa de caminho direto, inicialmente sem carga;
- Suporte idêntico sob chave diferente (sem inventar melhoria);
- Determinismo com permutações de ordens;
- Mudança do custo real compartilhado alterando quantidades, preços e utilidades;
- Rejeição de suportes inválidos, perfis ausentes, chaves repetidas;
- Não mutação do estado canônico.

**Limites deliberados:** ambos os suportes resolvem mercados `q_ij>0,p_j>0,H_i>0`; ainda não existem algoritmos completos de canto, entrada dinâmica não convexa, busca global de todos os suportes, escolha institucional ou settlement. A nova classe não estabelece que uma rota existe fisicamente: em uso autoritativo exigirá proveniência do serviço #42, confirmação de ativos e terminais, acessos, segurança de unidades e liquidação à vista. Não infere um contrato financeiro do fato de haver receita ou custos positivos.

A comparação de utilidade e receita não diz que algum suporte deverá ser adotado: mesmo uma alternativa que melhore indicadores selecionados pode afetar grupos ausentes, custos de transição e instituições. Uma só raiz estacionária por suporte não equivale a prova de unicidade.

## Compromisso de design: sem micromanagement e sem itens intermediários

O suporte logístico aceita quantidades **agregadas** e massa definida para as mercadorias que forem canônicas no futuro. Não cria nenhuma classe de objeto, receita de ferramenta, peça ou armadura. A palavra "armadura", mencionada anteriormente como exemplo, **não é uma decisão de catálogo**; equipar soldados pode ser uma consequência automática de insumos agregados, sem item intermediário de armaduras.

**A complexidade interna só é justificada pelo comportamento estratégico resultante; o jogador não gerencia as remessas individuais.**

## Aceite

`dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~EconomicInteriorSupportExplorerTests`

Exigir a CI completa em Linux/Windows/macOS (kernel, hash e benchmark) no SHA do PR. Não fazer merge automático. Dependências: #39 → #40 → #41 → #42 → #43 → #44 → #45 → este incremento; #38 permanece auditoria independente.

Próxima fronteira: avaliar suportes de canto e ativação de um corredor frio mediante lotes finitos e políticas de participação, após auditoria de acessibilidade. Manter seleção e liquidação como etapas separadas do ECON-005.

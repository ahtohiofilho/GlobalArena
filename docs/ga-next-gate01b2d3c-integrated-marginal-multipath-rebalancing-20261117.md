# GA-Next — Gate 01B.2D-3C: custo adicional total e reequilíbrio multipath

17/11/2026 · núcleo C# sobre PR #395 · Draft, sem merge · sem alteração visual

## Objetivo e enquadramento

O divisor multipath do PR #394 atribui parcelas por custo prospectivo, sem revisar um caminho antigo após a inserção de novas cargas. O PR #395 realimenta preços de frete na escolha de compradores, mas mantém esse mecanismo inicial de roteamento. Este corte acrescenta uma otimização discreta por **custo incremental TOTAL da rede**, sobre as mesmas entregas reais já auditadas. Os destinos e volumes vendidos não são alterados; apenas os trajetos físicos podem mudar.

## Potencial de custo e regra exata

Para cada aresta física Goldberg e com custo físico livre r, referência de carga positiva C e volume compartilhado L em CargoMicro:

- A taxa congestionada do Gate 01B.2D-1 é r × [1 + (L/C)^4].
- O potencial total inteiro de uma aresta é F(L) = r × [L + ceil(L^5 / C^4)]; F(0) = 0.
- Para inserir uma parcela de volume b sobre uma aresta já com L, o aumento exato é ΔF = F(L+b) − F(L).
- Para uma civilização que não é titular, acrescenta-se o termo linear b × tarifa externa da aresta; o titular continua pagando o custo físico, mas não essa tarifa.
- O potencial global é a soma dos custos físicos de todas as arestas mais os termos tarifários aplicáveis a cada entrega.

**Significado matemático:** F(L) representa o total de carga multiplicado pela taxa congestionada, com arredondamento inteiro conservador. O reequilíbrio compara a **variação do custo total** causada por mover uma parcela, não uma cobrança efetiva nem a integral matemática da taxa de custo marginal. Todo cálculo utiliza BigInteger; nenhum volume máximo artificial ou comprador fictício é imposto.

## Implementação

FoundationPhysicalMarginalPathRebalancer.Rebalance recebe censo físico v11, topologia fina, política completa de demanda e conversão logística, plano multipath auditado, custos livres, escalas por aresta e orçamentos explícitos de passes e fragmentos.

1. Revalida trabalhadores, compradores, ofertas, caminhos e titularidades no auditor #388/#386. Confere cada entrega-filha com sua entrega-pai original e conserva exatamente quantidades, mercadoria, dono e destino.
2. Divide cada parte em até maxFragmentsPerPart fragmentos representáveis sem arredondamento na unidade comum CargoMicro. Este número é orçamento de processamento e granularidade, não limite de produção, demanda ou capacidade.
3. Remove temporariamente um fragmento da carga do caminho atual e, por Dijkstra com BigInteger, encontra o trajeto de **menor aumento integral de custo** para recolocá-lo, com frete compartilhado e tarifas de terceiros explícitas.
4. Muda o trajeto apenas se o custo calculado cair estritamente. Cada passagem com mudanças precisa reduzir estritamente o potencial global. Empates preservam a rota anterior.
5. Exige uma passagem inteira sem nenhuma mudança para certificar estabilidade **local discreta**. Caso contrário, esgotar maxImprovementPasses causa exceção; não publica um resultado parcial como equilíbrio.
6. Agrupa fragmentos da mesma entrega e rota, reaudita 100% das mercadorias e compara **cada carga de aresta** com a auditoria independente.
7. Expõe custo inicial/final em unidades comparativas, contagem de trocas e passes, fragmentos e plano físico final. Não realiza vendas monetárias, débito de tarifa ou impostos.

## Integração com o Gate 01B.2D-3B

O coordenador conjunto de mercados e múltiplas rotas do PR #395 recebe parâmetros opcionais maxMarginalPathPasses e maxFragmentsPerPathPart. O default maxMarginalPathPasses=0 **preserva integralmente o comportamento anterior**. Quando ativado, melhora fisicamente as rotas **antes** de recalcular o custo que será usado na próxima decisão do mercado; a mesma exigência de igualdade exata entre taxa calculada da carga final e taxa usada para escolher compradores continua válida. O resultado reporta PathOptimization quando o recurso está ativo, e null no modo legado.

Ponto fixo de trocas de parcelas não garante ótimo logístico contínuo, equilíbrio de Wardrop ou preço monetário de equilíbrio global. A estabilidade econômica externa ainda pode falhar fechado em caso de oscilação.

## Validação e limite de escala

A nova suíte FoundationPhysicalMarginalPathRebalancerTests verifica:

- Fórmula inteira com cargas zero, C e 2C.
- Diamante Goldberg, uma entrega inicialmente toda numa única rota: realocar parcelas para um segundo percurso deve **reduzir estritamente** o custo total sem alterar volume ou comprador.
- Proprietário e tarifa diferenciada de terceiros sem isenção fictícia de frete físico.
- Falha fechada quando um único passe não basta para estabilizar a nova distribuição.
- Vendas locais com catálogo completo e nenhuma estrada artificial; hash, censo e ausência de ciclo fechado preservados.
- Integração opcional à realimentação de preços de frete do PR #395 sem regressão do modo antigo.
- **Rede Goldberg conectada de 112 tiles físicos, 8 produtores e 96 consumidores não produtores: 104 mercados em tiles distintos**. Percursos compartilham arestas do componente conectado; medição de duração e passes como diagnóstico, sem SLA arbitrário em runners GitHub. Isso não prova ainda escala de milhares de produtores e mercados.

Testes C# Windows/Ubuntu, regressões Godot Mono 4.7.2, kernel/hash multiplataforma e benchmark histórico são obrigatórios.

## Próximos gates e governança

Ainda faltam a otimização econômica conjunta verdadeiramente simultânea, o benchmark em grafo muito maior, custos/capacidades derivados de infraestrutura **confirmada** no estado autoritativo, formação de preço de transação, liquidação de receitas, tarifas, impostos e tesouros, journal e publicação do último ciclo econômico. Sem esse fechamento, o host mantém NoClosedCycle e nenhuma linha comercial é exibida no Godot.

PR Draft empilhado sobre #395, sem merge, main e interface aprovada preservadas.

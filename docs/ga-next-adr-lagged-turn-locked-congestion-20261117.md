# GA-Next — decisão de arquitetura: frete fixo no turno; congestionamento defasado entre turnos

**Decisão aceita em 17/11/2026 · substitui a meta de equilíbrio simultâneo mercado/congestionamento · sem merge na main**

## Contexto

A sequência Gates 19–22 investigou custo de memória e CPU do fluxo residual exato para calcular alocações comerciais e transporte com congestionamento endógeno. Embora algumas otimizações tenham preservado a precisão e reduzido custos, o projeto voltou a gerar uma fila de micro-otimizações sem produzir um jogo economicamente operacional. Essa direção **não é mais a prioridade nem é requisito de arquitetura**.

O jogo precisa de decisões de mercado determinísticas, justas, compreensíveis e econômicas o suficiente para vários milhares de nós. **Não é necessário** que o custo de congestionamento da produção/venda no turno T se torne simultaneamente consistente com o próprio volume desse turno T.

## Regra aprovada

1. Cada aresta física possui custo de infraestrutura de referência, com capacidade física de carregamento e titularidade/tarifa externa declaradas. Via local (sem cruzar arestas) tem frete zero; todas as arestas e respectivas tarifas continuam contabilizadas.
2. Ao iniciar o primeiro turno econômico, **nenhum congestionamento pregresso é inventado**: custo físico cotado é apenas o custo de infraestrutura. Nos demais, o custo físico é o custo atual de infraestrutura acrescido de **uma sobretaxa de congestionamento trazida do último ciclo econômico encerrado e auditado**. Mudança da infraestrutura efetiva atualiza o custo básico imediatamente; alteração da referência de capacidade invalida a sobretaxa antiga daquela aresta.
3. A cotação física e a tarifa de cada aresta são **imutáveis durante todo o turno**, inclusive durante as sucessivas propostas/buscas de water-filling. O produtor escolhe mercados pela receita marginal líquida de frete fixo e tarifa aplicável à sua civilização. O mesmo trabalhador que produz também demanda. Não se inventam mercados, estoque, carteira ou demanda mínima arbitrária.
4. O mercado ajusta suas próprias quantidades/marginais com water-filling \`P0 * sqrt(D/Q)\` e os custos fixos. O jogo **não recalcula congestionamento nem executa nova distribuição por causa da carga produzida neste turno**.
5. Depois que o ciclo é **efetivamente encerrado e auditado**, somam-se as cargas atravessando cada aresta (todas as mercadorias, sentidos e civilizações, com cargo micro-unitário comum) e calcula-se um custo alvo de congestionamento para **o próximo turno**, usando uma função fixa de volume/capacidade. O método já existente usa \`C_target = C_free * (1 + (V / V_ref)^4)\`. O expoente é parâmetro legado, não requisito para resolver o turno em curso.
6. Para reduzir o *zig-zag* de mudanças de rota, atualizar **apenas a sobretaxa** entre ciclos pela regra de amortecimento: \`S_(t+1) = S_t + (S_target(V_t) - S_t)/4\`, em representação determinística de micro-unidades, com passo mínimo de um micro quando a diferença não é zero. \`S_0=0\`; \`C_quote(t) = C_free(t) + S_t\`. Aqui o denominador 4 é um ponto de partida, não um número empiricamente ótimo.
7. Não exigir que \`C_quote(t)\` corresponda ao congestionamento medido no próprio \`V_t\`. **O descompasso é intencional**: representa a defasagem de informação entre decisões comerciais e congestionamento, evita resolver um ponto fixo interno e mantém uma única avaliação de congestionamento por aresta e por ciclo.

### Exemplo

Se A e B estiverem separados por uma aresta cuja infraestrutura imponha 100 por unidade física, o comércio interno de A enfrenta frete zero. Uma oferta pode alcançar B quando seu retorno marginal líquido superar a alternativa de A: para quem vende em B, deve-se subtrair os 100 e a tarifa de terceiro aplicável. **Não há garantia de atendimento local cronologicamente primeiro**: o mercado com maior valorização líquida pode atrair mercadoria imediatamente; é a competitividade marginal (não a distância isolada) que escolhe o destino. Se a rota congestionou no ciclo T, o ajuste de frete só valerá em T+1.

## Correção normativa: a função-base é universal, não existe um preço-base por civilização

**Resolução explícita da direção de produto (17/11):** a função de preço marginal é comum a todos os itens e mercados; o valor normalizado \`P0\` **não pode variar arbitrariamente por comprador, civilização ou distância**. \`P_i(Q_i) = P0 * sqrt(D_i/Q_i)\`, para \`Q_i > 0\`. A única variação entre mercados de um mesmo bem, mantida a quantidade, vem da demanda efetiva \`D_i\` (trabalhadores e seus pesos de consumo), não de um coeficiente cultural ou geográfico oculto. O custo físico de frete e a tarifa de terceiro reduzem o **retorno líquido do vendedor**, não alteram a função de valoração local.

O legado possui \`FoundationCommodityMarketPlanningQuote.QuoteMicroPerWholeCommodityUnit\` por mercado, e algumas provas de estresse intencionalmente atribuem valores 1000 vezes maiores aos mercados de uma civilização. Isso pode ser útil como **teste adversarial independente**, mas não descreve a economia aprovada. Como salvaguarda de transição, o novo \`FoundationPhysicalLaggedFreightCyclePlanner\` rejeita cotações-base heterogêneas até que os contratos antigos sejam remodelados para um único \`P0\` universal.

**A primeira gota exige uma definição discreta.** A derivada \`P(Q)\` diverge em \`Q=0\`; portanto, num modelo estritamente contínuo, todos os mercados vazios têm preço marginal infinito e não é matematicamente válido ranqueá-los pela subtração de um custo finito. Para a primeira unidade física representável \`delta>0\`, porém, o benefício integrado \`U(delta)-U(0)=2*P0*sqrt(D*delta)\` é **finito**. Com o mesmo \`D\`, o mesmo \`P0\` e zero disponibilidade inicial, a unidade inicial deve ficar no destino local, que não paga transporte; um destino remoto precisa oferecer um ganho líquido maior para vencer. Um mercado com mais demanda efetiva \`D\` pode competir imediatamente apesar da distância: essa exceção é **consequência legítima da população**, não um preço-base arbitrário.

A alta dificuldade histórica do comércio de longa distância deve emergir dos custos e da **existência efetiva de rotas**: travessias oceânicas inexistentes no estágio neolítico não podem ser inventadas por um algoritmo de menor caminho, mesmo com preços altos. Custos de frete economicamente proibitivos só impedem troca se o ganho integrado da primeira quantidade representável não os cobre; como \`P(Q)\` cresce perto de zero, isso **não é garantido apenas por um custo grande mas finito** para qualquer granularidade e população. Exigir correspondência física real da rota é essencial.

## Benefícios de escopo

- Um único cálculo fixo de custo e distribuição comercial por ciclo; nenhuma iteração \`volume atual → preço novo → nova rota → novo volume\`.
- Arcos/fretes recalculados somente se as condições do **novo ciclo** mudarem.
- Potência quartica (ou forma futura escolhida) só precisa ser calculada **uma vez por aresta**, após o ciclo, não dentro do water-filling.
- Mantém determinismo, desempates canônicos e auditorias físicas.
- Evolução de preços ocorre pelo comportamento de mercado entre ciclos, sem saltos artificiais de arredondamento nem milhares de iterações buscando centésimos.

## Limites e controles obrigatórios

**Amortecimento não é prova de convergência universal.** Quando a topologia tem rotas substitutas e demanda discreta, mesmo cotações suavizadas podem oscilar. A diretriz é aceitar uma variação economicamente inteligível entre turnos e **monitorar** amplitude das oscilações antes de introduzir qualquer correção adaptativa; nunca reativar uma busca de ponto fixo no mesmo turno para resolver isso.

**Custos e preços são coisas distintas.** A cotação de frete vem dos volumes do turno fechado anterior. Já a avaliação marginal de cada mercado continua dependendo da quantidade que recebe no turno corrente. A receita financeira, o imposto e as tarifas reais ainda precisam de liquidação futura: não confundir candidata cotação ou entrega auditada com pagamento realizado.

**Regra de não escoar abaixo do custo:** comparar preço marginal ao custo de transporte é correto para identificar destinos competitivos. Porém, os solvers existentes têm a restrição de que **toda produção exogenamente emitida é distribuída**, sem estoque. Em uma situação extrema na qual não exista destino com retorno líquido não negativo, será necessário compatibilizar o **volume produzido** com a demanda efetivamente viável antes de impor um bloqueio rígido de frete. Não criar estoque fantasma nem descartar carga já produzida silenciosamente. O protótipo abaixo preserva a restrição histórica enquanto a política de produção é consolidada.

**Fechamento autoritativo:** o repositório ainda distingue *candidate planning* de ciclo econômico fechado. Um estado do protótipo obtido de planejamento não é histórico autoritativo e **não deve** alimentar um turno real sem ter sido efetivamente liquidado e registrado pelo kernel. Quando a integração de fechamento for implementada, deve fornecer os volumes reais do ciclo anterior, não um plano de mercado provisional.

## Primeira implementação isolada

\`FoundationPhysicalLaggedFreightCyclePlanner.CoordinateCandidate\`:

- entrada: censo real, demanda por atividade, quotes externas, arestas da infraestrutura, escalas físicas, quote state anterior opcional e amortecimento determinístico;
- congela o grafo e todas as taxas físicas do ciclo;
- executa **uma** coordenação de water-filling de custos fixos. O produtor ainda pode fazer passes canônicos **dentro desse solve** (limitados, sem feedback de congestionamento); não há re-solve por alteração de carga;
- usa apenas o resultado de frete auditado para construir os novos estados de cotação de **próximo** ciclo;
- retorna \`NextCycleQuotes\`, custos congelados, leitura de cargas, plano auditado e indicador \`RepricedWithinCycle=false\`;
- valida cobertura exata das arestas e escalas, soma de carga por aresta e conservação de produção, preserva titularidade/tarifa externa e não modifica o host nem cria ciclo fechado.

A implementação é experimental e **não** significa que a publicação autoritativa de ciclo econômico já está conectada. Os coordenadores anteriores de congestionamento intra-turno são referências históricas de pesquisa, **não metas de otimização para o novo motor**. Manter suas regressões separadas até a migração de seus consumidores, sem continuar a campanha de Gates de micro-otimização do solver residual.

## Próxima entrega de produto

Integrar exclusivamente a abordagem *turn-locked + lagged* ao fechamento autoritativo do mercado, persistir o estado por aresta e mostrar na UI as cargas do ciclo anterior pela espessura das rotas, com a cor representando a titularidade. Só depois medir custo de um turno inteiro nos tamanhos reais de mundo para decidir se há gargalo relevante. **Não abrir novas otimizações de BigInteger por inércia.**

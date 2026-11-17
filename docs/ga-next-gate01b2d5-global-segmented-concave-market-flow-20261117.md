# GA-Next — Gate 01B.2D-5: alocação global de produtores por fluxo côncavo segmentado

**17/11/2026 · incremento C# independente · Draft empilhado sobre #397 · sem merge · sem Godot**

## Gargalo herdado e hipótese

O cenário original de #396/#397 contém **oito vendedores do mesmo grão**, 104 mercados de demanda positiva de trabalhadores reais e 112 tiles Goldberg conectados. O coordenador anterior reotimiza um vendedor de cada vez; mesmo com preferência por parcelas incumbentes e aceitação de ganho integrado positivo, **não estabiliza em 256 passagens**. O orçamento insuficiente falha fechado e permanece um diagnóstico legítimo, não uma venda.

Hipótese deste gate: substituir as respostas sucessivas dos vendedores, apenas em uma rota de pesquisa **independente e opt-in**, por um **único problema global de fluxo** com conservação exata de cada lote e rotas de custo distinto por vendedor. Isso remove o ciclo de melhor resposta entre vendedores no problema de planejamento **com custos fixados**.

## Formulação

Para cada mercadoria, um arco fonte→vendedor carrega o lote inteiro de um trabalhador, com capacidade física exata. Cada arco vendedor→mercado alcançável tem custo linear não negativo de transporte físico mais tarifa do proprietário estrangeiro sobre a rota mais barata desse vendedor. Um conjunto de arcos mercado→sumidouro modela o benefício **côncavo**, com benefício integrado alvo

`U_j(Q) = 2 × P0_j × sqrt(D_j × Q)`.

A função real de raiz quadrada não foi substituída por preço constante ou limite fixo de demanda. Para tornar a otimização global finita e auditável, porém, o núcleo resolve uma **aproximação declarada por cordas lineares** da mesma função, com intervalos de quantidade inteira. O benefício de um intervalo é a diferença de utilidade integrada em seus extremos dividida por sua largura, em precisão inteira `BigInteger` (64 bits fracionários para a raiz). Os declives não aumentam; os novos intervalos são ativados conforme os anteriores são preenchidos. A ordenação dessas cordas **subestima** a utilidade original entre os extremos.

O fluxo usa **caminhos aumentantes de custo mínimo com arcos residuais reversos**, custos reduzidos e potenciais, o que permite trocar vendedores entre mercados mesmo depois de uma atribuição inicial. Quando o algoritmo termina, ele minimiza o custo total para **o objetivo côncavo segmentado declarado**, sem coordenar cada vendedor em passagens iterativas. Empates são resolvidos pela ordem canônica dos arcos e nós.

**Escopo do ótimo:** o fluxo obtido é global para o **modelo segmentado**, não prova de ótimo para a utilidade contínua exata, nem equilíbrio de preços simultâneos. O campo `SegmentationUnderstatementScaled` calcula apenas a diferença entre utilidade integrada verdadeira avaliada **na mesma distribuição** e objetivo segmentado da mesma distribuição; **não é** um certificado de distância do ótimo contínuo.

## Censo, custos e auditoria

O adaptador `FoundationPhysicalGlobalConcaveFlowPlanner.Coordinate` consome o mesmo censo v11 e entradas explícitas de demanda, conversão, cotação e custos de arestas. A validação prévia do distribuidor existente comprova o catálogo, os compradores reais, a granularidade logística e os caminhos válidos; sua alocação sequencial de partida **não** restringe a solução global.

A otimização é separada por mercadoria e usa:
- oferta real e positiva de cada trabalhador produtor; nenhuma parte do lote pode desaparecer;
- demanda positiva de cada trabalhador/atividade no mercado de sua própria civilização; sem teto máximo de consumo;
- menor rota física de cada vendedor com isenção da tarifa apenas para o proprietário do trecho;
- custo em uma única unidade fixa de comparação por micro-unidade e `BigInteger`;
- auditoria final de cada quantidade, vendedor, comprador e travessia de aresta via `FoundationPhysicalDeliveryAllocationAuditor`.

Mercadorias sem oferta permanecem com demanda real e entrega zero; não é inventada venda. Rota inexistente fica ausente do grafo de fluxo. Se qualquer vendedor ficar sem mercado alcançável, a validação recusa o plano.

## Budget, determinismo, governança

`segmentsPerAverageMarket` define a granularidade explícita das cordas em relação à quantidade média por mercado; `maxFlowAugmentations` limita o número de caminhos aumentantes aceitos. Se a rede não puder conservar todos os lotes ou se o budget acabar, o solver lança exceção **sem devolver distribuição parcial**. São registrados o número de aumentos, o objetivo segmentado e o potencial integrado no plano, sem chamá-los dinheiro ou receita.

O coordenador histórico #391/#397, os valores padrão de suas duas flags, o multipath #396, o host, o StateHash e todas as cenas Godot **não são alterados** neste PR. O novo caminho é chamado diretamente e explicitamente em testes de pesquisa. Não há ciclo econômico fechado, tesouro, cobrança real de tarifa, journal monetário, preço liquidado ou overlay de fluxos. `GetLastClosedEconomicTradeView` permanece `NoClosedCycle`.

## Gate de aceitação

A suíte `FoundationPhysicalGlobalConcaveFlowTests` cobre:
1. Dois produtores, um com apenas um comprador alcançável, que exige realocação pela rede residual; quantidades exatas e sem venda desaparecida.
2. Um mercado em extremos exatos de intervalos, cujo valor segmentado coincide com sua utilidade integrada; sem float.
3. Falha segura de orçamento de aumentos e recusa de produção sem rota / frete negativo.
4. A **mesma configuração física adversarial** de oito vendedores concorrentes por um só grão, 96 compradores adicionais, 104 mercados e todas as arestas internas conectadas dos 112 tiles: conservação de 8.000.000 micro-unidades, auditoria por entrega e aresta, determinismo da ordem de cotações, ausência de mutações de snapshot/hash e `NoClosedCycle`.

O CI Windows/Ubuntu e os testes multiplataforma existentes devem passar antes de declarar o gate homologado. A execução de 104 mercados **não** comprova ainda a escalabilidade para milhares de produtores/mercados.

## Riscos explicitamente postergados

- **Erro de discretização**: mais segmentos aumentam fidelidade e custo computacional. O benchmark deve reportar tempo e diferença de potencial da mesma alocação, sem chamar isso uma prova de gap de ótimo.
- **Escala**: construir arcos para todos os pares vendedor/mercado alcançáveis cresce com sua multiplicação. Para dezenas de milhares de trabalhadores, será preciso poda segura, decomposição e/ou caminhos reduzidos.
- **Congestionamento endógeno**: os custos são fixos durante uma solução global. A interação com o roteador multipath, saturação da infraestrutura e realimentação #395/#396 exige gate separado com estabilidade/consistência auditada; não presumir equilíbrio.
- **Economia**: a função de planejamento e seu potencial não equivalem a receita liquidada. Nenhum resultado será publicado como comércio até existir uma etapa explícita de liquidação.

**Critério de honestidade técnica:** este PR deve ser descrito como **primeira alocação global de fluxo para o surrogate segmentado**, não como solução definitiva da convergência econômica contínua nem como confirmação de produção pronto para lançamento.

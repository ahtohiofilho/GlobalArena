# GA-Next — Gate 01B.2B: candidatos de mercado e rotas econômicas físicas

**17/11/2026 · incremento C# sobre PR #388 · Draft/sem merge · núcleo experimental preparado, não liquidação do mercado**

## Diretriz de produto preservada

O Global Arena necessita de comércio espacial efetivo: o mesmo trabalhador produz e demanda, a produção não desaparece e não há compradores fictícios. Mais adiante, os **resultados realmente liquidados** do último ciclo alimentarão o modo de mapa econômico: **espessura da linha pelo volume que passou em cada aresta e cor pelo titular da infraestrutura**, congelados naquele ciclo.

O PR #388 consegue **reconciliar** alocações já propostas com trabalhadores, compradores e trajetos reais. Neste corte, implementamos o **primeiro seletor determinístico** desses destinos e de seus caminhos — **um lote inteiro para um comprador**. Ainda **não** é o mercado de equilíbrio nem o water-filling com preço marginal; isso virá quando as regras forem formalizadas e testadas.

## API criada

`FoundationPhysicalTradeRouteCandidatePlanner.Plan(census, incidence, demandPolicy, conversionPolicy, quotes, edgeCosts)` consome:
- **Censo físico autoritativo** v11 e oferta dos trabalhadores efetivamente produzindo; demande por mercadoria derivada da atividade em todos os tiles habitados (PR #384).
- **Catálogo e unidade física comuns** explícitos (PR #388), com conversão de quantidade de item em `CargoMicro`; não infere peso, tamanho ou mercadoria.
- **Cotações explícitas por `(tile, civ, commodity)`**: `FoundationCommodityMarketPlanningQuote.QuoteMicroPerWholeCommodityUnit` é uma expectativa monetária **para escolher um candidato**, não preço liquidado. Toda linha de demanda positiva precisa ter a própria cotação; nada é preenchido com coeficiente padrão, valor fixo por item ou origem.
- **Custos explícitos por aresta**: `FoundationPhysicalRoutingEdgeCost` recebe a identidade `PhysicalTacticalEdgeId` exata, estado de titularidade histórico fornecido, custo físico monetário positivo por unidade logística e tarifa do não titular (somente se `Owned`). Aresta **sem custo declarado é intransitável**, não uma estrada natural gratuita. A infraestrutura natural poderá participar quando seus atributos reais forem fornecidos, sem presumir valores.
- Todos os valores são `ulong` em microunidades e comparações em `Int128`/produtos `UInt128` com `checked`; resultados fracionários abaixo da escala de precisão causam falha explícita em vez de fabricar arredondamento de preço.

## Algoritmo do corte (uma regra provisória de seleção, não o clearing)

1. Validar a matriz e **todas** as cotações de compradores reais, e construir um grafo esparso com apenas as arestas para as quais a infraestrutura/custo foi explicitamente informada.
2. Para cada par distinto `(tile de origem, civilização proprietária da produção)`, executar e **armazenar** uma árvore de menores custos com Dijkstra. O custo de atravessar a aresta é `custo físico + tarifa externa` para não titulares; o titular é isento apenas da tarifa externa, **não** do esforço físico. Desempate canônico por custo, número de saltos e identidade do predecessor.
3. Para cada **lote físico efetivo** de produção (já agregado por `tile,civ,ocupação,mercadoria`), avaliar os destinos com demanda positiva e cotação explícita:
   ```text
   quoted = goods_micro × quote_micro_per_whole_good / 1e6
   freight = cargo_micro × shortest_path_physical_rate_micro / 1e6
   toll    = cargo_micro × shortest_path_nonowner_toll_rate_micro / 1e6
   net planning score = quoted - freight - toll
   ```
   Compara **lotes inteiros**; em empate exato, comprador de menor ID físico e depois menor civ. Não há limite fixo de `D` nem teto artificial de compradores, e o custo pode superar a cotação (uma alocação ainda é gerada se houver comprador alcançável), porque a produção precisa encontrar destino.
4. Selecionar um **único** destino para cada lote nesta versão, reconstruir o caminho sobre centros dos tiles táticos, e produzir `FoundationPhysicalCommodityDeliveryCandidate` com ID canônico determinístico.
5. Submeter **todas as escolhas** ao auditor do PR #388: a quantidade deve corresponder a **100% da capacidade real**, compradores devem existir, rotas devem ser fisicamente válidas, titularidades explícitas e volume por aresta reconciliado por commodity (PR #386).
6. Expor internamente ao host `FoundationTacticalHumanAiTurnHost.PlanTradeCandidates(...)` **somente leitura**, a partir do último snapshot confirmado. Não altera `WorldState`, não produz recibo de ciclo, não deposita receitas, não retém inventário e não envia dados de comércio de todas as civilizações ao cliente Godot.

## Limitações intencionais e riscos de produto

- **Preço**: as cotações são **sinais externos** para comparar candidatos, não equilíbrio de oferta e demanda. Preços, preços pós-frete e receitas não são escritos no mundo.
- **Física logística**: os custos são por unidade logística comparável, fornecidos explicitamente. Não há curva de congestionamento, investimento de estrada, benefício de custo, tarifas dinâmicas automáticas, capacidade saturável ou imposto. Esses termos serão integrados ao resolver de frete autoritativo posteriormente.
- **Distribuição**: um lote inteiro vai para um comprador. Isso pode concentrar consumo em excesso e **não aplica retornos marginais decrescentes**. O projeto exige evoluir para divisão das quantidades (*water-filling*) sem um teto arbitrário da demanda; a seleção atual é um candidato auditável, não uma regra econômica definitiva do jogo.
- **Conectividade**: arestas não fornecidas são bloqueadas, inclusive conectividade natural (cujo custo ainda não foi modelado para o mundo v11). Há sempre demanda do próprio produtor em seu tile e portanto, se as cotações estiverem completas, uma alocação local é um fallback fisicamente válido, mas **não otimiza o comércio real sem custos/valores calibrados**.
- **Titularidade**: as cores do overlay só devem refletir a titularidade real capturada no ciclo econômico, não as propriedades dos tiles ou as cores do vendedor.
- **Autorização**: o planejador é **interno** ao simulador; seu resultado global nunca é exposto como visão de cliente. `GetLastClosedEconomicTradeView` do PR #387 permanece `NoClosedCycle`.
- **Escalabilidade**: agrupar pessoas em lotes e reusar a árvore por `(tile,civ)` evita recomputar milhares de Dijkstra para cada trabalhador ou mercadoria. Em mapas grandes, este é o caminho inicial; ainda faltam benchmarks específicos de congestionamento/frete para múltiplas origens.

## Testes/gates

1. Cenário real G(18,0), 12 trabalhadores: sob cotações iguais, cada lote encontra comprador real no mesmo tile sem **inventar estradas** ou cargas sobre arestas; 100% da oferta é alocada e o hash não muda.
2. Incentivo externo forte em outro mercado seleciona compradores de outra civilização através de caminho real validado; cada aresta recebe volume auditado.
3. Em um triângulo real da malha física, uma rota mais longa porém barata pode superar a aresta curta com tarifa alta para quem **não** é titular. O titular escolhe a aresta direta e fica isento apenas da tarifa.
4. Sem alternativas, não titular paga a tarifa explícita; valores distinguem custo físico e transferência por tarifa, ainda sem efetuar arrecadação.
5. Cotações incompletas, duplicadas, estranhas ou nulas, política de frete ausente/inválida, titularidade inconsistentes ou edge ID de outro Goldberg são rejeitados; aresta omitida não vira estrada gratuita.
6. Permutar trabalhadores, cotações e arestas não altera a escolha, os IDs de entrega nem a rota publicada como **candidata**.
7. Ordens de profissão pendentes não afetam o planejamento. Depois de E, novos lotes e os trabalhadores retomados pela IA entram no próximo candidato; jamais se publica ciclo financeiro apenas por confirmar trabalhadores.
8. Dez mil trabalhadores individuais na mesma origem são projetados em um lote/entrega e não em dez mil caminhos.
9. CI Windows/Ubuntu executa testes específicos `FoundationPhysicalTradeRouteCandidatePlannerTests` e todas as regressões históricas; Godot 4.7.2, kernel/hash e benchmark prévios devem permanecer aprovados.

## Roadmap: o que falta antes de desenhar o comércio REAL

**Gate 01B.2C:** substituir as cotações externas pelo **equilíbrio do mercado**, com retorno marginal decrescente e divisão de cada oferta entre compradores reais ao longo das rotas; converter capacidade e congestão de arestas em custos endógenos e preservar titularidade/tarifas com governança estável.

**Gate 01B.3:** liquidação efetiva de receitas, impostos e tarifas para os tesouros, com journal/hash e fechamento de ciclo econômico autoritativo. Somente então chamar o publication boundary do PR #387, gerar last-cycle per-edge loads e liberar o overlay de mapa do PR #385.

**Governança:** PR Draft empilhado sobre PR #388, sem merge e sem alteração visual. A matemática de comparação deste corte é apenas uma ferramenta testável de candidato; não é uma nova regra de preço canonizada.

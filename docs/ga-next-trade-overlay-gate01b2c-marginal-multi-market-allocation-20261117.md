# GA-Next — Gate 01B.2C-1: distribuição marginal de lotes entre múltiplos mercados físicos

**17/11/2026 · implementação matemática incremental · Draft sobre PR #389, sem merge**

## Decisão técnica e relação com o produto

Os PRs #383/#384 publicam a oferta potencial de cada trabalhador e a demanda positiva em cada tile; o PR #386 reconcilia travessias físicas por aresta; #387 protege o último ciclo econômico concluído; #388 audita que todo lote físico foi alocado; #389 escolhe **um** destino por lote via rota de menor custo, respeitando titularidade e tarifa explícitas.

Este corte substitui a escolha integral de um destino por um **alocador progressivo de múltiplos compradores** com retornos marginais decrescentes — sem comprador, moeda, teto de demanda ou origem preferida artificiais. É um **planejador econômico de CANDIDATOS**, não a liquidação do ciclo econômico. Nenhum dado é apresentado como comércio concluído no cliente Godot.

## Curva e algoritmo

Para um comprador real `i`, mercadoria `c`, demanda física positiva `D_i`, quantidade já prevista `Q_i` e coeficiente de valor inicial `P0_i` **explicitamente fornecido**:

```text
p_i(Q_i) = P0_i × sqrt(D_i / Q_i), quando Q_i > 0
p_i(0) = não precificado (limite marginal infinito; NÃO adicionar volume fictício)
m_i(Q_i; s) = p_i(Q_i) - [custo físico + tarifa do não titular]_(s→i,c)
```

`D_i` vem dos **trabalhadores reais** na atividade atual; `P0_i` é um parâmetro **externo de planejamento**, não um preço de equilíbrio/canonizado. Todos os seis perfis por produto continuam expressamente definidos no `FoundationActivityCommodityDemandPolicy`; a matriz da fixture **não** vira política do jogo.

O solver percorre lotes de oferta **em ordem canônica**, compartilha o total `Q_i` de todas as entregas já atribuídas ao mercado e resolve cada lote entre os mercados acessíveis, maximizando uma aproximação por níveis de oportunidade marginal `m_i`. **Não garante equilíbrio simultâneo global entre todos os produtores**; um mercado recebe menos nova quantidade depois de ter recebido bastante de lotes anteriores.

Para um nível marginal de referência `λ`:

```text
Q_i*(λ) = floor[ (P0_i² × D_i) / (λ + custo_i)² ]
ΔQ_i*(λ) = max(0, Q_i*(λ) - Q_i anterior)
```

Para `λ+custo_i <= 0`, qualquer quantidade restante do lote é admissível porque o custo excede o nível de comparação, não porque exista demanda máxima. O solver **bissecta o nível λ usando BigInteger exato** (com precisão monetária comparativa de 10⁻⁶ microunidade), não itera uma vez por microunidade produzida. A quantidade de cada destino é expressa numa **granularidade de transporte exata** derivada do coeficiente físico de conversão `CargoMicroPerWholeCommodityUnit`. A fronteira discreta redistribui o resto **proporcionalmente à quantidade admissível de cada comprador**, com desempate canônico para quanta residuais. A validação final exige conservação exata de todos os lotes.

Sem `Q` positivo inicial, não existe preço publicado nem estoque inventado; a fórmula é usada apenas na seleção marginal de **propostas de entrega**.

## Transporte, propriedade e segurança do núcleo

- Reutiliza `FoundationPhysicalTradeRouteCandidatePlanner.BuildGraph/RouteFrom/Reconstruct` do PR #389. Arestas ausentes são **intransitáveis** nesta etapa — não existe estrada natural de custo zero. Custo físico e tarifa são **inputs explícitos**; titular isento apenas da própria tarifa.
- O custo de comparação por unidade da mercadoria é obtido a partir da conversão física explícita do catálogo e do custo por unidade logística. Cálculos de comparação utilizam `BigInteger`, inclusive quando a taxa implica frações de microunidade de moeda. **Nenhum valor é debitado/creditado**.
- Cada lote pode gerar vários `FoundationPhysicalCommodityDeliveryCandidate` (uma por mercado que receba quantidade positiva), cada um com uma rota válida entre os centros dos tiles reais.
- A etapa chama **obrigatoriamente** `FoundationPhysicalDeliveryAllocationAuditor.Audit` (#388) e, por ele, `FoundationPhysicalTradeEdgeLoadAuditor.Audit` (#386). Quantidades, origens e destinatários devem bater com trabalhadores e demanda autoritativa; cargas por aresta e titularidade devem ser verificáveis.
- Não há preferência arbitrária por civilização, nem limiar positivo de exibição de mercado, nem limite de volume demandado. A intensidade `D` é parâmetro de atratividade **não teto de compra**.
- A quantidade física de uma parcela deve ser exatamente convertível para `CargoMicro`. Se o lote não admite partição inteira na precisão do catálogo, a configuração falha **sem arredondar volume para cima ou para baixo**.
- `FoundationTacticalHumanAiTurnHost.DistributeMarginalMarketCandidates(...)` é interno ao servidor e lê **apenas a fotografia já confirmada**. Não altera `WorldState`, jornal, replay ou hash; ordens pendentes não afetam mercados.
- Sem custo/rota externa declarados para outras regiões, permanecem apenas compradores acessíveis localmente. Não se fabrica transporte para mostrar um overlay interessante.

## Limites, riscos e próximos gates

**Limitações explícitas do Gate 01B.2C-1:**
- O planejamento marginal é **sequencial por lote**, não um water-filling *simultâneo* de toda a oferta. Isso ainda pode dar prioridade ao primeiro lote; o benefício esperado da próxima fase é considerar oportunidades marginais globais entre produtores.
- O custo das arestas é estático durante este planejamento. Não existe realimentação de congestionamento, investimento por aresta, tarifa automática endógena, saturação progressiva, rota dinâmica ou custo `volume^4` do laboratório histórico. Esses requisitos pertencem à calibração/iteração de transporte posterior.
- `P0` é parâmetro externo de planejamento e a curva `sqrt(D/Q)` é hipótese matemática original já usada no laboratório — **não afirmamos que preços de mercado estejam certificados**. A utilidade pode ser aproximada mesmo com q discreto, e a capacidade de demanda não é limitada.
- Não há produção real de seis grãos com atributos contínuos do terreno no mundo atual; os IDs de mercadorias do cenário são **placeholders de teste**.
- Não há tesouro por tile, preços finais, imposto percentual pós-receita, receitas e tarifas liquidadas, journal de settlement, persistência de ciclo econômico ou evento de publicação.
- `GetLastClosedEconomicTradeView` do PR #387 continua **`NoClosedCycle`**, mesmo após qualquer planejamento. O overlay de rotas **não** será ativado nesta etapa.

**Próximos gates:** (1) resolver a oportunidade global entre vários lotes e mercados com custos atualizados por congestionamento físico, observando o desempenho com milhares de tiles; (2) calibrar os preços e receitas pós-custos; (3) liquidar os tesouros e publicar um ciclo econômico realmente fechado; (4) visualizar somente esse último ciclo no Godot, com espessura por volume e cor pelo titular histórico.

## Matriz de testes

`FoundationPhysicalMarginalMarketDistributorTests` no Windows e Ubuntu:

1. Um lote, dois mercados **no mesmo tile**, `D=1` e `D=3`: volumes ~25%/75% para valor base igual, ambos acima de sua intensidade de demanda quando a produção é 10 unidades, **sem limite artificial D**.
2. Terceiro comprador remoto com cotação explícita e rota física real: o volume da aresta corresponde **à fração efetivamente entregue**, não à produção toda; titularidade correta, sem preço ou arrecadação.
3. Dois lotes sequenciais usam a quantidade já entregue a cada mercado para calcular os próximos valores marginais; conservação integral.
4. Sem aresta declarada, comércio remoto não é criado e a entrega permanece nos consumidores locais.
5. Cotações incompletas, duplicadas, zeradas, catálogo de carga incompatível e divisibilidade física inválida falham sem mercado fictício.
6. Permutar ordem de trabalhadores/cotações não altera destino, quantidade, caminho nem ID das propostas.
7. Turno de trabalho pendente vs confirmado: apenas o confirmado muda a oferta; IA retoma trabalhador viajante, capacidade real sobe de 11 para 12 unidades e hash/overlay são preservados.
8. Dez mil trabalhadores físicos num mesmo tile resultam em **um lote agregado, duas entregas candidatas e nenhum objeto de transporte por trabalhador**.
9. Regressões completas C#, Godot Mono 4.7.2, kernel, hash e benchmark de mercado.

**Governança:** PR Draft empilhado sobre #389; sem merge e sem alteração da interface aprovada.

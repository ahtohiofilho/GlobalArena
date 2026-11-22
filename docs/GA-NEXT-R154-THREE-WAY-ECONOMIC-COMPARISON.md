# GA-Next R154 — Comparação econômica contrafactual de três cenários

**Estado:** comparador de pesquisa pré-commit; **não** modifica o mercado autoritativo, fretes, preços publicados, caixa, diário ou turnos.

## Motivação e isolamento causal

A R153 fornece custos de frete alternativos a partir de serviço logístico calculado com trabalhadores reais, sem congestionamento. Porém, transformar um produtor em operador reduz a produção. Comparar diretamente uma economia inteira com a outra confundiria o efeito da menor oferta com o efeito de operar as estradas ou os portos.

A R154 calcula três soluções de mercado separadas, todas com o **mesmo mundo, topologia, inventário de infraestrutura, capital confirmado, políticas de consumo, conversão de carga e impostos**:

1. **A — todos produzindo:** censo anterior à realocação, fretes autoritativos sem desconto adicional por logística;
2. **B — trabalhadores realocados, sem desconto operacional:** censo posterior à realocação, mesmos fretes autoritativos de A;
3. **C — trabalhadores realocados, com serviço operacional:** exatamente o censo B, mas com as cotações sombra da R153.

A comparação A→B isola a **oportunidade de produção perdida**. B→C mede a **reação potencial do mercado ao desconto operacional**. A→C permite avaliar os dois efeitos juntos. Não é lícito assumir antecipadamente que A→C produza ganho econômico líquido.

## Procedimento

- Rejeitar mudanças de identidades, proprietários, localizações físicas, itens, produção potencial e multiplicadores de demanda dos trabalhadores. Só são permitidas mudanças explícitas de `Producing` para `Other / LandTransport` ou `Other / PortOperations`.
- Exigir o mesmo binding físico e o mesmo inventário completo de transporte; nenhum investimento, posse ou tarifa pode ser trocado entre cenários.
- Projetar somente as ocupações logísticas efetivamente existentes em B, usar a distribuição de trabalho R151 e coeficientes R152 e calcular serviço pela R150.
- Construir as cotações sombra R153; manter frete, propriedade e tarifa originais em A e B.
- Reconstruir **o mesmo conjunto de ligações de gelo bloqueadas** nos três mercados.
- Resolver independentemente cada cenário com o planejador de mercado já existente, um frete unitário fixado antes do water-filling, sem retroalimentação de tráfego.
- Reconciliar cada plano pela função já existente de contabilidade econômica; devolver apenas resultados pré-commit.

## Indicadores disponíveis

Para A, B e C: produção vendida/consumida, quantidade de entregas, ligações efetivamente utilizadas, carga total atravessando arestas, receita bruta, custo físico de frete, tarifas de terceiros, tributos pós-receita e receita líquida dos vendedores. Valores monetários são expressos na escala racional exata do reconciliador, **não em moeda já creditada ao tesouro**.

Também estão disponíveis: trabalho operacional efetivamente distribuído, serviço gerado por aresta, comparativo de frete por aresta, produção perdida A→B, diferenças de receita bruta e líquida B→C, e diferença líquida final A→C.

A comparação pode apresentar menos rotas com mais vendas, mais rotas com menor receita, ou efeitos opostos por civilização — nenhuma direção é forçada.

## Invariantes de segurança econômica

- **Sem congestionamento:** o volume comercial observado nunca entra no cálculo do frete no mesmo turno ou no seguinte.
- **Sem dupla contagem de trabalhadores:** o trabalhador retirado da produção mantém identidade, localização e demanda, mas não vende sua mercadoria enquanto opera a infraestrutura.
- **Sem inflação de capital:** nenhuma troca de infraestrutura entre A, B e C; capital confirmado pode alterar alocação e produtividade sob os contratos R151–R152, mas não é criado por esta projeção.
- **Sem liquidação:** nenhum candidato publica rotas, credita caixa, movimenta estoque, grava eventos ou muda o hash do estado.
- **Sem promessa de melhoria econômica:** avaliar ganhos e perdas antes de qualquer adoção da cotação sombra pelo fechamento autoritativo.

## Gate seguinte

Homologar o CI e examinar resultados de um cenário real com trabalhadores terrestres e, em etapa posterior, portuários. Antes de permitir a fórmula no mercado efetivo, calibrar separadamente o benefício direto do capital já embutido no frete e o benefício do trabalho operacional para evitar retornos econômicos indevidamente duplicados.

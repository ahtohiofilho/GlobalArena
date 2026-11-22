# GA-Next R152 — Produtividade operacional com retornos marginais decrescentes

**Status:** função experimental determinística, ainda **não integrada ao frete, ao mercado ou ao fechamento dos turnos**.

## Contrato matemático

A R151 já define a parcela do trabalho alocada por aresta em cada nó. A R150 já transforma o **trabalho efetivamente entregue** por cada ligação em serviço, usando um coeficiente de produtividade. A R152 define como calcular esse coeficiente a partir do capital monetário **efetivamente confirmado**, sem alterar a distribuição de pessoas.

Para cada aresta terrestre (LAND) ou portuária (PORT), a classe operacional recebe do chamador:
- `B`: serviço natural por unidade de esforço, positivo, em microsserviço por unidade inteira de trabalho.
- `G`: potencial adicional de produtividade em pontos-base, positivo (10.000 pontos-base = 100% da produtividade natural).
- `Q`: cotação monetária original de construção dessa aresta, positiva.
- `K`: investimento monetário comprometido na aresta, obtido somente do estado autoritativo.

Então:

`P(K) = B × [1 + (G/10000) × K/(Q + K)]`

A implementação calcula `floor(P(K))` com aritmética `BigInteger`, mantendo precisão micro e rejeitando overflow. LAND e PORT possuem pares `(B,G)` explícitos e independentes; **não há valores universais ocultos**.

### Propriedades físicas

1. Com `K = 0`, o serviço por unidade de esforço é exatamente `B`, independentemente de a aresta ser cara ou barata de construir.
2. Para `K > 0`, a produtividade cresce; o incremento marginal decresce, atingindo um limite superior assintótico explícito `B × (1+G/10000)`. Com quantização inteira, pequenos investimentos podem gerar incrementos zero; a função matemática contínua subjacente é estritamente crescente e côncava.
3. O trabalhador continua disponibilizando **a mesma quantidade de esforço**: produtividade não fabrica pessoas ou horas de trabalho.
4. O custo de construção `Q` atua como **denominador de dificuldade de investimento** e nunca como produtividade natural.
5. O ganho não depende de volume de carga, tráfego simultâneo, preços, demanda, receitas ou saturação: **não há congestionamento**.

### Atenção à incidência do mesmo capital

O investimento confirmado já participa da distribuição de trabalhadores na R151:

`W = 1 + K/Q`

Na R152, **o mesmo capital também pode melhorar a produtividade por unidade de esforço**, mas esses são *dois canais físicos distintos*: (1) a parcela relativa dos trabalhadores alocados à aresta e (2) a quantidade de serviço produzida pela parcela recebida. Ambos ficam expostos e testáveis; não se multiplica automaticamente o capital mais uma terceira vez no frete. Isso NÃO significa que o ganho econômico total será linear — a combinação dos dois canais pode concentrar capacidade e precisa ser avaliada em um gate de mercado posterior.

A R152 publica fatores `FoundationOperationalEdgeProductivity` que podem ser consumidos pelo projetor R150. Ela não modifica dinheiro, infraestrutura, ordens, preços, rotas, frete ou diário autoritativo. Nenhuma mudança visual é necessária.

## Critérios para futura integração ao mercado

- Decidir se serviço significa carga efetivamente atendível por ciclo ou outra unidade física canônica.
- Evitar que o serviço calculado reduza o frete e, simultaneamente, limite o fluxo de forma que conte duas vezes o mesmo efeito sem intenção física explícita.
- Preservar o mercado natural mínimo e a rede mar–mar conforme contratos anteriores.
- Manter o custo unitário sem dependência do volume transportado; sem ciclos `volume → custo → volume`.
- Testar perda de produção ao deslocar trabalhadores para logística, ganhos de serviço, recursos físicos e replay completo antes de habilitar a nova função no fechamento econômico.

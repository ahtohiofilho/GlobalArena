# GA-Next R151 — Capital operacional normalizado e custo de construção

**Status:** contrato de pesos operacionais do projetor físico, sem modificação do frete ou do mercado.

## Problema resolvido

Até a R150, o projetor de trabalho atribuía à infraestrutura natural um peso igual ao **custo monetário de construção da aresta** e acrescentava a esse número o investimento realizado. Isso tratava erroneamente **dificuldade de construir** como se fosse **infraestrutura disponível**: uma estrada difícil, sem qualquer investimento, capturava uma parcela maior dos trabalhadores da cidade.

O custo de construir não é capital investido. A variável monetária é também inadequada para somar diretamente a um esforço físico.

## Regra R151

Toda ligação terrestre ou portuária física elegível tem a mesma **unidade natural de peso operacional**: `W_natural = 1_000_000` micro-pesos (uma unidade). A quantidade de capital **efetivamente comprometido e homologado** é normalizada pela **cotação original da própria aresta**:

`W_investido_micro = floor(investimento_monetario_micro * 1_000_000 / custo_original_de_construcao_monetario_micro)`

`W_total = W_natural + W_investido_micro`

No nó, a regra previamente aprovada de alocação permanece:

`Trabalho_i = Trabalho_no * W_total_i / soma(W_total_nas_arestas_do_grupo)`

O resto inteiro permanece distribuído pelo algoritmo determinístico R145. O projeto rejeita arestas inválidas, mar aberto, custos ausentes/zero e resultados fora da faixa representável. A aritmética intermediária utiliza `BigInteger`.

### Consequências explícitas

- **Sem investimento**, todas as arestas de um mesmo grupo têm o mesmo peso natural, independentemente do custo original de construção.
- **Investimentos iguais** em ligações com custos originais diferentes não equivalem à mesma quantidade de infraestrutura normalizada; construir onde é mais caro exige mais dinheiro para obter a mesma fração equivalente de infraestrutura.
- **Investir o equivalente à cotação original** adiciona uma unidade de peso operacional, além da unidade natural.
- A normalização possui resolução micro: investimentos positivos extremamente pequenos podem ainda não alterar o peso no turno em que são insuficientes para uma unidade micro; não inventar arredondamento para cima.
- Custos monetários históricos são utilizados **como denominadores de conversão**, não como estoque, multiplicador direto do serviço, frete ou preço.
- O capital investido permanece registrado no estado autoritativo existente, sem atributo paralelo ou infraestrutura fictícia.

## Separação entre alocação e produtividade

O capital controla **a distribuição do trabalho** entre as ligações do mesmo nó. A R150 transforma o esforço alocado em serviço com um coeficiente de produtividade **independente, explícito e fornecido pelo chamador**. A R151 não aplica automaticamente o capital outra vez sobre a produtividade nem modifica o frete unitário.

Esta separação evita dupla aplicação inadvertida do mesmo investimento, mas **não encerra a decisão econômica**: no modelo final, a qualidade da infraestrutura provavelmente deverá afetar também o quanto de serviço uma unidade de trabalho consegue realizar. Essa relação exige um coeficiente físico de produtividade e um gate próprio, e não deve ser escondida na normalização dos pesos.

## Limite de aprovação

A R151 não acrescenta congestionamento, capacidade comercial obrigatória, consumo físico de materiais, trabalho em cada aresta, ordens adicionais, UI ou calibração definitiva. A economia permanece byte a byte na mesma base de regras. As fórmulas de alocação e produtividade só serão integradas ao mercado quando uma política simples e explícita for aprovada e testada contra frete, receitas e replay.

# GA-Next R64 — gate de escalabilidade e contrato de integração ao frete

**Estado:** gate exploratório, sem integração ao coordenador econômico, sem merge em `main`.

## Contexto quantitativo

A prova de exclusão do R61 e sua execução opcional no comparador R62 eliminam um par costeiro `(a,b)` somente se `approachLand(a) + coast(a) + 1 + coast(b) + approachLand(b) > feasibleLandOnly`. A estrita desigualdade preserva empates. R63 demonstrou 80,3% de redução no tempo agregado do comparador em 3.242 tiles, com nove vitórias marítimas em doze consultas, e 81,1% em 1.442 tiles. Os testes R55–R63 são a referência experimental.

## Gate A — mais nós e múltiplos mercados

- Executar malha Goldberg estratégica `(4,0)`, correspondente a **5.762 tiles táticos** no mesmo refinamento; não inventar rotas portuárias nem criar ativos além das arestas reais.
- Selecionar quatro pares reais de localidades terrestres costeiras, distribuídos deterministicamente, em perfis `natural` e `coast256` (este é um estresse financeiro debitado do caixa, **não** coeficiente recomendado para balanceamento).
- Comparar oito decisões completas de uma perna: referência não podada e R62 podada, com cache marítimo R57 inicialmente aquecido e igualmente compartilhado. Alternar ordem de chamadas; reportar tempo de parede agregado, alocação no thread, limite de entradas do cache, pares potenciais/descartados, busca marítima adicional, proporção de vencedores por modal.
- Preservar o vencedor GLOBAL; quando o mar vence, preservar **exatamente** o preço micro, aresta de embarque e aresta de desembarque. Quando a terra vence, a segunda melhor opção marítima pode sumir — isso é correção, não divergência.
- Não extrapolar latências unitárias de CI para duração de turno completo. Um CI SUCCESS comprova o caso medido; teste de estresse progressivo e repetição permanecem necessários para milhares de mercados.

## Gate B — segurança de integração econômica

Não conectar esta escolha ao waterfilling até que **todos** os seguintes invariantes tenham validação executável sobre o próprio coordenador:

1. **Custo não negativo e limite inferior:** provar no contexto de frete marginal que todos os adicionais por aresta/civilização (tarifa de terceiros, custo devido a congestionamento e coeficientes físicos) são não negativos. Uma rota terrestre candidata de custo `U` tem de ser realmente **factível no mesmo snapshot, para o mesmo item e proprietário**. Não transportar o `U` físico da pesquisa para uma cotação monetária de outro domínio. O `+1` marítimo só é aplicável à política em que há perna mar-mar positiva e desconto pareado nunca a zera.
2. **Titularidade real:** arestas terra-terra e terra-mar pertencem ao titular vigente no snapshot. O proprietário isento de tarifa não significa transporte sem custo físico. Para terceiros, tarifa nunca é subtraída do custo físico. O mar-mar natural não tem dono, capital, congestionamento nem tarifa.
3. **Capacidade e congestionamento:** terra e costa são arestas finitas; custo congestionado da mesma aresta vale para o volume computado segundo o modelo autoritativo e é aplicado UMA vez à travessia. Limite físico simples só pode podar tarifas/congestionamento quando for matematicamente dominado por todo custo marginal positivo admissível; se a política introduzir descontos, subsídios ou taxas negativas, suspender a poda até nova prova.
4. **Desconto da perna:** travessias costeiras A e B determinam um único benefício multiplicativo de investimento congelado; não escrever desconto em cada aresta marítima. Um caminho pode exigir múltiplas pernas — a prova R62 diz respeito a **exatamente uma**; para generalizar é obrigatória outra demonstração, com identificação e custo dos transbordos.
5. **Mercado e demanda:** o trabalhador físico gera demanda no nó onde está associado; trabalhadores em aresta continuam demandando no nó associado; demanda não é carteira e não cria estoque. Os preços universais e a preferência natural pelo mercado local no início do mundo não podem ser modificados. Vendas têm de ser conservadas, receitas e impostos liquidam no caixa, e construção consome **somente dinheiro**, nunca material físico.
6. **Determinismo e desempenho:** same world+snapshot+inputs → mesma rota, mesmos microcustos e mesmo hash Win/Linux/macOS. Documentar quantos mercados são realmente consultados por produto/ciclo e medir custo de construção do grafo, atualização de custo marginal, memória máxima e caches, não apenas Dijkstra. Quando o retorno marginal cair, interromper otimização e reconsiderar formulação do problema.

## Decisão condicionada

Se Gate A conservar exatidão e ganho de tempo/memória em 5.762 tiles, R64 autoriza APENAS desenvolver um adaptador experimental que receba a cotação congelada do solver autoritativo sem liquidar vendas. **Não** faz merge ou habilita o roteador para o jogador automaticamente. Gate B só estará aprovado quando suas invariantes estiverem demonstradas com testes end-to-end, inclusive propriedade/tributação/congestionamento.

Se Gate A falhar, parar e reavaliar o modelo estrutural de seleção de mercados, conforme critério de retorno marginal estabelecido.

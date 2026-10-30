# GA-VS13B — Correção do preço com um comprador local

## Evidência na GPU Windows (30/10/2026)
O usuário obteve no VS13: tile físico 2037; oferta projetada 3,546; necessidade de alimento 4,000; oito trabalhadores; orçamento por trabalhador 40 (total hipotético 320); quantidade potencialmente vendida 3,546; preço 0,0000; gasto 0; resíduo H 320.

**A interface de cotação e o HUD compacto funcionam**, mas não é um preço coerente de escassez, com poder de compra positivo. Não mascarar o problema com formatação nem inventar gastos.

## Causa
O resolvedor GA-PROP-001 EconomicSingleVarietyOriginClearing.Resolve calcula a demanda por ConsumerDemand(buyer, supply, price), limitando a quantidade individual à oferta. Sua bissecção utilizava if (TotalDemand(mid) > supply). Com **um único comprador**, o valor nunca ultrapassa a oferta, mesmo quando o comprador está disposto a pagar preço positivo por toda a quantidade. A busca tende a zero.

## Correção
Trocar a condição para TotalDemand(mid) >= supply, interpretando igualdade como trecho ainda restrito pela oferta. A bissecção agora encontra o maior preço que sustenta a venda da quantidade ofertada. Mantém zero quando nenhum comprador aceita pagar nem frete e não inventa preços negativos.

No cenário da captura P=8, H=320, q=3,546, demanda 4: preço de referência esperado aproximadamente **60,443**, despesa **214,331**, H residual **105,669**, venda potencial 3,546. Valores **apenas hipotéticos**, não renda oficial, salários ou liquidação da partida.

## Gates
- Unitário do algoritmo com um comprador: preço positivo, condição marginal, receita = despesa, oferta = vendas, H positivo.
- Regressão do VS13 para os valores observados, sem alteração de WorldState, eventos ou save.
- Preservar múltiplos compradores, frete alto, sobra física, saves V9–V11.
- Compilar Godot e testar Windows/Linux.

## Governança
Incremento isolado empilhado sobre PR #106 (HUD compacto + VS13), preservando #104, #102 e main. A demanda basal multicommodity permanece congelada na issue #103. Nenhum merge automático.

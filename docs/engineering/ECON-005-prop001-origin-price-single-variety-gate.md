# GA — ECON-005 / Gate E1: preferências GA-PROP-001 e primeiro preço de origem endógeno

**Estado:** implementação opt-in, não autoritativa, em PR empilhado sobre o #42.  
**Escopo:** prova numérica reproduzível de `GA-PROP-001` sem novo subsistema bancário, sem liquidação e sem alterar o kernel legado.

## Contrato matemático recuperado

O novo `EconomicFoodPreferenceEvaluator` aceita um vetor ordenado de quantidades por **variedade de origem** (uma coluna por produtor/origem), população `P>0`, consumo monetário residual `H>0` e parâmetros de regras `sigma>0`, `nu>=0`, `lambda>=0`. Calcula:

```text
z[j] = q[j]/P, f = sum(z)
g(x)=x/(x+sigma)
D = (sum(g(z[j])) - g(f))/(n-1)  se n>1;  D=0 para n=1
E = f^4/(1+f^4)
V = 1+nu*E*D
U = P*(2*log(1+f)+lambda*log(V)) - (2P)^2/H
WTP[j] = (H/(2P))^2 * [
   2/(1+f) + lambda*nu*( E'(f)*D +
     E*(g'(z[j])-g'(f))/(n-1) )/V
]
```

A parcela de diversidade na derivada é zero quando `n=1`. `WTP` é derivada monetária `U_q/U_H`, **não** um preço comercial escolhido pelo motor. O domínio válido exige `q>=0`, `H>0`, `P>0`, sem preço ou saldo devedor negativo. `H` representa numeraire/consumo residual e **não** depósito bancário. Para evitar overflow de `f^4`, o ramo `f>1` calcula `E` por potências inversas.

## Primeiro clearing de referência com preço de origem ENDÓGENO

Para um único produtor de **uma variedade** com oferta exógena finita `S>0`, compradores `i` com `P_i, B_i>0` e frete comercial `t_i>=0` previamente fornecido:

```text
H_i = B_i - (p_origin+t_i)*q_i  > 0
WTP_i(q_i,H_i) = (H_i/(2P_i))^2 * 2/(1+q_i/P_i)
0 <= sum_i(q_i) <= S
p_origin >= 0
p_origin*(S-sum_i(q_i)) = 0
```

A equação `WTP_i = p_origin+t_i` vale para `q_i>0` em demanda interior; consumidores no canto `q_i=0` não têm WTP acima do preço entregue. Para `n=1`, a demanda individual é estritamente decrescente em `q_i` para preço entregue fixo, e a demanda agregada diminui com `p_origin`. Isso permite **bisseção determinística** (96 etapas máximas por inversão), com preço de origem gerado pelo clearing. O zero é permitido como preço de origem quando a oferta não é esgotada; o restante permanece explicitamente não vendido, não vira venda fictícia nem recebe preço negativo.

A implementação retorna volume por comprador, `H_i`, disposição marginal a pagar, preço entregue, custo de frete, recebimento do vendedor, mercadoria não vendida e resíduos de orçamento. A identidade monetária do cenário é auditada:

```text
sum_i (p_origin+t_i)*q_i
 = p_origin*sum_i q_i + sum_i t_i*q_i
```

Este é um **diagnóstico hipotético de pagamentos**, não um ledger efetivo nem uma transferência entre agentes. Nenhum dinheiro é criado.

## Limites a NÃO ultrapassar ao revisar

1. **O clearing endógeno implementado é SOMENTE da especialização de UMA variedade, com uma origem**. O avaliador de preferências suporta `n>=1`; ele ainda não é um solucionador conjunto para `n>1`. O benchmark de três mercados/variedades GA-ECON-004C continua prova independente em Python, não foi transplantado para produção.
2. Fretes `t_i` são **cotações congeladas fornecidas pelo chamador**. O resolvedor ainda não alimenta automaticamente o clearing pela camada #42 nem refaz preços e cargas em um laço de ponto fixo com economias de densidade. Custo operacional real `C_r` e tarifa comercial `t` não são automaticamente iguais.
3. Números são `double` em **unidades econômicas inteiras de mercado** do benchmark, não os valores monetários persistentes `ulong` em ponto fixo. É uma fronteira numérica experimental com testes, **não** autorização para registrar um `double` como dinheiro liquidado. O contrato de conversão/precisão deverá ser explicitado antes de ligar o modelo ao estado autoritativo.
4. Não há fonte endógena de oferta, salários, impostos, propriedade dos produtores ou estoque persistente. Permitir oferta não vendida evita impor a igualdade de venda integral fora do benchmark fechado.
5. Não há seleção entre diferentes regimes logísticos, nem prova de concavidade global no caso multivariedade, nem mecanismo de contrato, aprovação, liquidação ou crédito.
6. O custo monetário residual `H` é positivo por construção; nunca usar estado de orçamento matematicamente inválido para forçar pagamento ou empréstimo. O solucionador pode avaliar preços/quantidades candidatos hipotéticos e descartá-los, mas não pode postar movimentos no mundo.

## Testes

`dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~EconomicFoodPreferenceAndSingleOriginClearingTests`

Os testes cobrem: identidade analítica `U_q/U_H=WTP` por diferenças centrais de 3 variedades, divisor `n-1`, caso `n=1`, quantidade zero, domínio de `H`, numéricos grandes, formação positiva do preço de origem, conservação, efeito de orçamento, efeito de frete, igualdade de compradores, mercadoria não vendida, invariância de permutação e rejeição de entradas inválidas.

Requeridos: regressão integral do kernel, hash de estados e benchmark de frete nos 3 sistemas operacionais no commit final do PR. Manter em draft enquanto a CI estiver executando.

## Integração futura dentro do escopo do lançamento

Após revisão: adicionar clearing de **múltiplas variedades** com preços de origem endógenos simultâneos, complemento dos mercados, restrições individuais e auditoria da topologia/ativos (#42), com algoritmo e parâmetros explicitamente limitados. Depois registrar critérios para seleção econômica e liquidação **à vista**, usando as autoridades existentes de turno. Nada de bancos, dívidas, empréstimos, garantias ou inadimplência.

# GA ECON-005 — Gate E2: clearing interior simultâneo de múltiplas variedades e origens

**Status:** PR experimental C# empilhado no #43, sem alteração autoritativa de turno e sem crédito.

## Objetivo

O ECON-004C demonstrou, em Python/SciPy, que três regiões A/E/F e três variedades podem apresentar configurações estacionárias distintas com preços de origem e orçamentos residuais endógenos. Até o PR #43, o C# tinha apenas um avaliador de preferências para `n>=1` e um clearing **de uma variedade**. Esta entrega fecha a lacuna de cálculo do **suporte interior de um mercado multivariedade com tarifas congeladas**, sem presumir que isso resolve toda a instituição comercial.

## Contrato matemático

Para compradores `i`, produtores/origens `j` e cotação comercial `t[i,j]>=0`, números em **unidades reais de referência**:

```text
q[i,j] > 0                   (suposição explícita de INTERIOR)
H[i] > 0                     (consumo monetário residual)
p[j] > 0                     (preço endógeno na origem)
M[i,j](q[i,*],H[i]) = p[j]+t[i,j]
H[i] + sum_j(q[i,j]*(p[j]+t[i,j])) = B[i]
sum_i(q[i,j]) = S[j]
```

`M` vem do `EconomicFoodPreferenceEvaluator` do #43, com diversidade, abundância e resposta à quantidade das demais variedades. `p[j]` **não é entrada**; é variável que satisfaz simultaneamente as condições marginais de todos os consumidores e o mercado físico de cada origem.

A matriz de frete `t[i,j]` é informada explicitamente em cenário de **cotação congelada**. Frete cobrado não é idêntico ao custo real da infraestrutura; a função de custo com densidade do #40 não recebe tarifas ou renda automaticamente.

## Método numérico e limites

- `EconomicMultivarietyInteriorClearing.Resolve` ordena compradores e origens por ID e exige exatamente uma tarifa por par comprador/origem.
- Desconhecidas: `m*n + m + n` (fluxos, residuais e preços de origem) e **o mesmo número de equações**.
- Inicialização determinística com oferta dividida entre destinos, `H=0.55B` e preços estimados **a partir da disposição marginal**, não impostos de fora.
- Sistema de Newton com jacobiano de diferenças finitas, eliminação gaussiana determinística com pivotamento parcial e **busca linear amortecida** que apenas avalia estados hipotéticos com `q>0, H>0,p>0`.
- Máximo de 100 iterações de Newton, 36 tentativas de redução de passo, 256 incógnitas, tolerância de resíduo 2e-9. Singularidades, domínio inválido, inviabilidade, não convergência ou erro de auditoria provocam exceção; não são convertidos em crédito nem resultado económico.
- Auditoria final independente das identidades por orçamento, oferta, disposição marginal a pagar e decomposição de pagamentos em receitas dos produtores e frete.
- Não existe semente aleatória, mutação de `WorldState`, registro contábil, política institucional de adoção ou escolha de equilíbrio global.

## Prova canônica ECON-004C

Com parâmetros `sigma=.1, nu=2.8, lambda=1`, populações `[10,12,9]`, ofertas `[12.5,15,11.25]`, orçamentos `[50.4,60.48,45.36]` e **tarifas congeladas** extraídas da solução fechada (corredores já ativos) do benchmark independente, o novo C# deve reproduzir:

```text
p_origem_A ≈ 2.0155351015549514
p_origem_E ≈ 1.9405700202450464
p_origem_F ≈ 2.0524466395000958
H ≈ [23.394790049546597, 28.159443167641353, 21.016323457444326]
q[dest,origin] ≈
[ [7.4113718516844305, 2.527371850238366, 2.509820389770114],
  [2.599662077310181, 10.185362753085748, 2.4313725771044927],
  [2.488966071005388, 2.2872653966758842, 6.308807033125392] ]
```

A comparação inclui testes em C# com tolerância 1e-7 por grandeza, teste de simetria, mudanças de orçamentos e fretes, determinismo por permutação, rejeição de tabelas de tarifas incompletas e **rejeição explícita de mercados de canto** que não satisfazem `q>0`.

## O que AINDA NÃO está resolvido

1. **Não é clearing com preço de frete endógeno no mesmo ciclo.** As médias `C_r(L)/L` dos recursos logísticos foram congeladas como entrada deste teste. A prova ECON-004C recalcula custos e tarifas na mesma raiz de equilíbrio: falta iterar simultaneamente mercados, incidências físicas, cargas, preços de transporte e preços de origem, com tratamento rigoroso de recursos inicialmente vazios por remessas finitas.
2. **Não decide equilíbrios estacionários múltiplos, entrada coletiva em corredor frio nem estabilidade histórica.** O mesmo suporte pode ter mais de uma raiz. Uma única inicialização e critério de convergência não provam unicidade ou optimalidade global.
3. **Não aceita estados de canto.** Oferta não esgotada, preço de origem zero e fluxo zero em algum par são possibilidade econômica legítima, mas ficam para um solucionador complementar ou algoritmo de suporte ativo. O código rejeita em vez de inventar compras/financiamento.
4. **Não comprova ótimo global do consumidor.** As condições marginais são necessárias para um ótimo interior; com interações de diversidade, precisaremos verificar utilidade local/segunda ordem ou comparar diferentes candidatos antes de qualquer autorização de mercado.
5. **Não recebe diretamente identificadores de serviço #42.** O quadro de fretes fornece preços ex-ante e pode posteriormente vir dos serviços físicos validados; nenhuma conversão silenciosa de `ulong` fixo para `double` monetário é feita.
6. **Não contém pagamentos reais, renda futura, bancos, juros, contratos de crédito, garantias, contabilidade persistente ou liquidação autoritativa.** O residual `H` é numeraire/consumo, não depósito.

## Aceite

`dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~EconomicMultivarietyInteriorClearingTests`

Requer CI completa de kernel, hashes e benchmark de frete em Windows, Linux e macOS no mesmo SHA. **Não alterar a main** até revisão e observância da ordem #39 → #40 → #41 → #42 → #43 → este PR; #38 é auditoria independente.

## Próximo incremento sem expansão de escopo

Ligar as tarifas de recursos `EconomicPhysicalResourceId` aos caminhos e volumes de #42 e resolver o sistema simultâneo de cargas e ofertas em suportes fixos, com **candidatos finitos** para novas rotas. O auditor económico e a escolha institucional serão separados do solucionador: apenas estados admissíveis poderão ser efetivados por liquidação à vista do kernel.

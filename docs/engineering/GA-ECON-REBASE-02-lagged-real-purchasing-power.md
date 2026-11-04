# GA-ECON-REBASE-02 — Poder de compra derivado de atividade econômica real

**Base:** ECON-REBASE-01 R2 / PR #249; pacote isolado `GlobalArena.Economy`.
**Natureza:** primeiro cálculo auditável em dados comparáveis, ainda **não
uma regra promovida ao jogo ou ao sistema de saves**.

## Problema e abordagem

A formulação ratificada é `D=P*N`, `x=S/D`, `p=f(x)`,
`R_ref=p*D`. O índice `N` deve evoluir por economia real, e não
por uma decisão arbitrária `N=N+0,05`, por carteira doméstica fictícia
ou pela mesma receita nominal já inflada por `N`.

Este incremento implementa **um candidato dimensionalmente coerente**:
índice de output FINAL fisicamente realizado por habitante, a preços
de referência **congelados no período-base**.

```text
V_base = soma_por_mercadoria(q_base * preco_referencia_base)
V_prev = soma_por_mercadoria(q_prev * preco_referencia_base)
N_para_ciclo_atual = (V_prev / P_prev) / (V_base / P_base)
D_atual = P_atual * N_para_ciclo_atual
```

São quantidades físicas observadas de bens economicamente destinados,
não capacidade potencial ou fictícia; preços de referência fixos são
pesos de comparabilidade entre mercadorias distintas, **não** um
valor de demanda por pessoa ou orçamento público/privado.

No primeiro ciclo completo, a base define `N=1` para o ciclo
seguinte; os demais ciclos podem apresentar N maior/menor conforme
valor **real** por habitante. A decisão do preço-referência do
período-base **não é feita aqui**: os pesos são entradas explícitas,
e a futura integração só poderá aceitá-los de um preço autorizado
e congelado, com prova de proveniência. A fórmula de preços `f`
não é escolhida por este incremento.

## Contrato temporal e blindagem contra drift

- `RealCompletedCycleObservation`: ciclo econômico **concluído**,
  residentes e vetor de produção final efetivamente destinada
  atribuível àquela economia; sem dinheiro, preço atualizado, crédito
  ou inferência de "falta" a partir de quotas.
- `FrozenBaseUnitValuation`: preços-unitários fixos para **o mesmo
  conjunto de mercadorias** no período base e no período observado;
  a avaliação fica constante se apenas os preços **nominais atuais**
  mudarem, sem produção física correspondente.
- `FrozenRealPurchasingPowerBasis`: salva internamente a base, os
  pesos imutáveis e a produção real-base, exigindo base positiva.
- `RealPurchasingPowerDerivation.FromPriorCycle`: exige
  `ciclo_preco = ciclo_evidencia+1`, compara valor real por habitante,
  devolve `AbstractPurchasingPowerIndex` para o **próximo ciclo**.
  Não muda preços, inventário, população, journal ou estado da sessão.
- Soma em ordem estável de mercadoria, `decimal` com overflow
  explícito; inputs ordenados/validados sem percorrer compradores
  individuais. Custo da derivação O(n) por basket já congelado,
  e custo da quote O(1) por mercado/mercadoria.
- A base congelada requer universo de mercadorias idêntico em cada
  período. Bens novos sem pesos-base devem exigir **política explícita
  de rebase de índice**: nunca ignorar silenciosamente inovação.
- Se o valor econômico real observado é zero, o serviço recusa
  inventar `N=0`, `N=1` ou piso fictício. Produção zero não prova
  renda zero quando comércio, reservas ou rendas diversas existem.
  O que fazer nesse caso depende de dados e política econômica real.

## Escopo e limites essenciais

A fórmula é **um indicador de capacidade produtiva real per capita**,
não prova completa de renda líquida, bem-estar ou poder de compra.
Para virar o `N` canônico, precisamos validar se rendas de comércio,
receitas líquidas, acesso à distribuição, logística e disponibilidade
de bens devem entrar como evidências distintas. Não duplicar
produção e circulação de insumos: usar apenas produção FINAL
com destino físico, uma vez por origem e mercadoria, auditada no
journal. Sem provas, o usuário da biblioteca poderia fornecer
quantidades fictícias; esta biblioteca **não autentica** observações
de jogo. Não usar o mercado `FoodCash` V12 como fonte automática.

O indicador usa pesos de preço de referência fixos para somar valores
de alimentos, madeira, pedra etc. Esses pesos não significam demanda
numérica por habitante. Também não são uma licença para escolher
coeficientes com objetivo de forçar resultados.

## Testes

`GaVs111RealPurchasingPowerDerivationTests` cobre:
neutralidade, crescimento/queda real, efeito da população,
R=p×D via Quote existente, pesos-base imutáveis, ordem
determinística, universo de produtos, períodos errados,
zeros sem fallback artificial, dados inválidos e 1.000
categorias/100 milhões de habitantes sem loop individual.

**Próximo incremento prioritário:** ligar observações a
ciclos físicos canônicos verificados e um conjunto rastreável
de preços de referência aprovado, **antes** de promover
`N` como regra executável ou persistir o índice. A proposta
de base-prices é a hipótese mais simples sem feedback de
preços nominais; pode ser rejeitada pela validação do jogo
sem preservar compatibilidade com métricas legadas.

**Governança:** PR Draft empilhada; sem merge ou
alteração destrutiva no motor físico/visão Godot.

## Correção de infraestrutura do gate (04/11)

O primeiro HEAD `c38d39957c67dfed4c5bf02c29899c489416a0b0` teve
`Cross-Platform Kernel Regression Validation` com conclusão global
`cancelled` porque o job Windows excedeu o limite de **15 minutos**
durante a finalização. Os logs registram **2465/2465 testes aprovados**
no Windows e o artefato `kernel-regression-windows-latest`
enviado com êxito, mas o status global não pode ser declarado `success`.
A PR eleva exclusivamente o tempo máximo do workflow para **25 minutos**,
sem alterar testes, filtros, preços, demanda, consumo ou gameplay.
A validação formal deve se referir ao novo HEAD, não ao cancelado.

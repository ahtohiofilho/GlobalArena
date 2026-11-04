# GA-ECON-REBASE-01 R2 — Motor econômico novo, sem herdar quotas artificiais

**Status:** núcleo experimental isolado, com testes de contrato. Não substitui
ainda a liquidação e os ciclos executados no Godot. Base da PR: VS107, não
VS108/VS109. Toda mudança no antigo motor deve ser versionada.

## Invariantes de produto — 04/11/2026

```text
P = número de pessoas reais no mercado comprador
N = índice adimensional de poder de compra relativo (neutro inicial 1)
D = P * N                        (pressão de demanda econômica efetiva)
S = produto fisicamente oferecido, após retenção deliberada
x = S / D                        (escassez relativa)
p = f(x)                         (curva escolhida por mercadoria/ruleset)
R_ref = p * D                    (receita econômica de referência)
```

Não canonizar `D = P × quantidade_necessária_por_pessoa`. Não criar
`500_000`, `0,5`, créditos individuais ou teto de consumo. `D` é uma
pressão econômica e **não** uma quantidade física consumida. A oferta
pode ultrapassar `D` livremente; `D` nunca limita produção, absorção,
estoque voluntário ou alimentação.

A versão R1 usava `p=f(x,M)` com `x=S/P`. A R2 **substitui** essa
posição: o índice `N` entra na demanda efetiva, antes de avaliar a
função univariada de preço. Não existe uma camada de multiplicação
adicional sobre o preço ou uma segunda entrada oculta na função.

`R_ref=p*D` segue o cálculo solicitado para a curva econômica:
**não** comprova sozinho pagamento, propriedade, frete, recebimento
fiscal, volume vendido ou consumo nutricional. Nenhuma monetização ou
transferência é criada neste primeiro núcleo.

## Poder de compra: preservar a intenção original

As civilizações iniciam com `N=1`. Ao longo do jogo, seu poder
de compra pode aumentar ou diminuir conforme **capacidade econômica
real**, nutrição, produtividade, organização, tecnologia e acesso.
Esses fatores são possíveis fontes de evidência, **não pesos já
aprovados nem multiplicadores aplicados duas vezes**.

Para não produzir inflação circular:

- Calcular `N` a partir de dados reais do **ciclo anterior**, não de
  preços/receitas do mesmo mercado que se está calculando.
- Não usar receita **nominal** do ciclo passado isoladamente para
  recalcular `N` sem separar variações de preços de capacidade real.
- Não somar alimento, madeira e pedra em unidades físicas diretamente;
  diferentes mercadorias exigem uma comparação de valores homogênea
  antes da agregação.
- Não inventar taxa universal, piso/teto arbitrário, saldo familiar,
  crédito, orçamento `B=40`, regra de conversão, renda ou estoque
  de alimentos fictício.
- Não duplicar a produtividade como ganho de oferta `S` e um bônus
  extra ao `N` sem justificativa econômica independente.
- Para múltiplas populações compradoras/mercados externos, definir
  agregação explicitamente antes de integrar frete ou preço remoto.

**O cálculo de N a partir do estado ainda não está implementado.** A
API aceita o valor **normalizado de fonte autenticável no futuro** e
verifica que ele é positivo. Isso é uma fronteira intencional de
implementação, não uma regra de atualização escondida.

## Implementação

`GlobalArena.Economy` é novo assembly .NET 10 isolado, sem
dependência de Simulation/Runtime/Kernel/World/Godot:
- `PhysicalOffer`: S = disponível - retenção explícita;
- `AbstractPurchasingPowerIndex`: valor relativo positivo; neutro 1;
- `PopulationPriceKernel.Quote(P, S, N, f)`: cálculo `O(1)`
  aritmético `decimal`, com uma invocação de `f(x)`;
- `PopulationPriceQuote`: P, N, D, S, x, p, R_ref, flag de oferta.
- `Quote(P,S,f)` preservado como atalho neutro N=1.

Nenhuma curva `f` foi escolhida para o jogo. A função `2/(1+x)`
existe **somente nos testes de comportamento**, não em regras de
produção. O ponto `x=1` é uma normalização de referência a ser
calibrada para cada mercadoria, sem impor quantidade de consumo.
Complexidade desta operação isolada é O(1), mas isso não certifica
desempenho global do jogo ou de frete.

## Áreas a preservar vs. reconstruir

**Preservar:** mundo esférico Goldberg, malha/terrenos, renderização,
UI tática, trabalhadores, movimento, caminhos, frete e custos físicos,
registro de ações e hashes; verificar conservação e determinismo.

**Não importar como regras novas:** `FoodDemandRawPerWorker`,
`FoodUnmetRaw`, quotas VS25, perfis de demanda em quantidade por
trabalhador, preços cash-food V12, envelopes B/H e diagnósticos de
faltas artificiais. Esses campos permanecem no legado para replay,
não para governar o núcleo novo.

**Futuros incrementos, após confirmação do contrato de preços:**
derivação temporal auditável de N, calibração da curva f por bem,
adaptação à oferta física real, transporte sem teletransporte, absorção
efetiva, nutrição e produtividade com defasagem, receitas/transações
separadas, persistência versionada e benchmarks em mundo denso.

## Critérios de aceitação e segurança

A suíte `GaVs110PopulationPriceRebaseTests` valida P, N, D, x,
R_ref, neutralidade, monotonicidade condicional à curva de exemplo,
independência do volume físico consumido, retenção e falhas de domínio.
Godot e replay legados permanecem intactos. Fast CI compila a nova
biblioteca e tests; checkpoint completo é outra trilha. Não alterar
o status Draft nem executar merge automaticamente.

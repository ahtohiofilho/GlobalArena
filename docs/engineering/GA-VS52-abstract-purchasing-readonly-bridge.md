# GA-VS52 — Ponte somente leitura para poder de compra abstrato e identidades físicas

**Base:** GA-VS51 PR #157, head `94cc4325c8cbd00755cdfbc930cd6a64d9d314b0`, 12/12 workflows aprovados. **Status:** laboratório, PR draft; `main` e saves intocados.

## Diretriz canônica preservada

O **poder de compra da população é abstrato e agregado**, não uma carteira de dinheiro individual. Cada trabalhador residente também representa um consumidor. A VS52 converte temporariamente essa capacidade em **unidades numéricas por ciclo** para alimentar a validação de demanda `alpha` da VS45 e os gates monetários VS44–VS51.

O valor resultante é uma **capacidade de consumo para simulação**, não dinheiro preexistente, salário pago, ativo bancário, tributação nem transferência do caixa V14. Nunca interpretar `ExperimentalBuyerCashBudget` como saldo persistente do jogador ou de cada trabalhador.

## Contrato implementado

`ExperimentalAbstractPurchasingPowerProjector.Project` recebe:

- uma sessão física canônica **somente leitura**;
- ofertas de produção/demanda com `EconomicPointId` explícitos;
- âncoras de comprador `EconomicPointId → PhysicalTacticalTileId`, uma por buyer e por site, com fator não negativo de produtividade;
- âncoras de produtor, também explícitas, sem supor que um `EconomicPointId` corresponda automaticamente a um tile;
- serviços logísticos declarados e preços postados na **mesma unidade hipotética** da política, que deverão ser calibrados futuramente;
- `ExperimentalAbstractPurchasingPolicy` com ID, versão, unidades básicas por residente e reserva `H` por residente.

A fonte de residentes é `EmergentSettlementProjection.FromSession`, que soma **os trabalhadores efetivamente alocados aos tiles físicos**. Pessoas que ainda estão viajando são reportadas separadamente e **não são contadas duas vezes**. Pessoas em tiles ainda não representados nos compradores são reportadas como `UnprojectedResidentWorkers`; não geram compradores sintéticos ocultos nem são perdidas dos totais físicos.

Para cada comprador ancorado:

`GrossPurchasingUnits = residentWorkers × baseUnitsPerResident × productivityFactor`

`ProtectedH = residentWorkers × invisibleUnitsPerResident`

`Spendable = GrossPurchasingUnits − ProtectedH`

O cálculo usa `BigInteger` e `FoodTransportCostFraction` exatos. Fator de produtividade individual do **tile**, e não de pessoas, permite ensaiar diferenças de poder de compra sem definir salários, bancos, crédito ou acumulação privada. Não se canoniza nesta fase a função definitiva de produtividade econômica; a política é explicitamente **experimental e versionada**.

## Fronteiras de identidade e titularidade

- **Comprador:** exige âncora física única e cobertura completa da demanda. O mesmo conjunto de trabalhadores não pode financiar dois compradores.
- **Produtor:** exige âncora de localidade com residentes para cada ponto econômico da oferta. Um mesmo `EconomicPointId` que compra e produz deve identificar **o mesmo tile**.
- **Transportador:** cada serviço declarado é listado como `ExperimentalUnresolvedTransportOperator`, com origem, destino e serviço, **sem criar um dono, conta financeira, identidade jurídica ou renda**. O jogo ainda não possui uma titularidade canônica de operadores de frete. A VS52 torna essa dependência explícita, em vez de imputá-la a produtores ou compradores.
- **Povoamento:** continua emergente; nenhum fundador formal de cidade ou nova entidade política é criado.

## Interação com a economia financeira legada

A VS45 continua tendo um **bridge numérico V14** para validações históricas separadas; a VS52 apresenta o caminho alternativo canônico para **orçamento temporário abstrato derivado de população**, sem consultar os saldos `PhysicalFoodCashMarketState`. Não misturar os dois modelos silenciosamente nem usar dinheiro ou receita de produtor como poder de compra familiar.

Para decisões/settlement reais no futuro será necessária uma política explícita para reconciliar **as unidades abstratas de compra com preços, fretes e contabilidade monetária institucional**, além de autenticação dos transportadores e conservação financeira completa. Este adaptador não realiza nem autoriza liquidação.

## QA

- 8 trabalhadores residentes no tile inicial com base 5 e reserva 5/8 por trabalhador → **40 unidades brutas, H=5, disponível=35**, independentemente do caixa V14 de 320 créditos.
- Demanda de quatro unidades com preços entregues 10 → `alpha=7/8` e despesa projetada 35.
- Fator produtivo 0,5 ou 2,0 altera o poder de compra agregado de 20 a 80 sem criar caixa.
- Trabalhadores em trânsito não se tornam moradores antecipadamente; chegada a segundo tile permite novo comprador, preservando o total populacional físico.
- Identidades duplicadas, âncoras faltantes, fornecedores não ancorados e serviços duplicados são recusados. Serviços válidos são registrados sem titular financeiro.
- Mesmo ponto econômico atuando como comprador e produtor é permitido, **desde que o tile seja o mesmo**.
- Nenhuma alteração em `CurrentHash`, `ExportSaveJson`, eventos, Godot UI, mercados, turnos ou `main`.
- Benchmark `--vs52-abstract-purchasing`: repete 8/32/128 vezes a projeção do **mesmo povoamento** para detectar regressão de custo e mutações, sem extrapolar para 128 compradores.

## Limites e próximo marco

O adaptador recebe **as âncoras e o fator de produtividade explicitamente do chamador**. Ainda não existe um emissor institucional confiável de titularidade de frete nem regras definitivas de produtividade/poder de compra entre civilizações. Não confundir a semântica do orçamento VS44 com dinheiro existente em caixa V14.

O próximo marco deverá consolidar critérios de conversão de unidade econômica, preços e fretes e o mapa de titularidade, com auditoria da população e identidade da fonte canônica, antes de ligar este orçamento ao mercado ativo ou ao `SETTLE` persistente. Ações de compra reais continuam bloqueadas.

# GLOBAL ARENA — ECON-REBASE-01: recuperação da arquitetura econômica

**Decisão de produto, 04/11/2026:** recomeçar a arquitetura econômica caso
isso seja necessário, sem arrastar o desvio de demanda escalar e pagamentos
fictícios. Este incremento cria um **núcleo novo e independente**; NÃO
destrói o jogo, não faz migração e não promove preço executável.

## Contrato mínimo vinculante

A demanda econômica basal é **a população real** do mercado:

```text
D = P                       (pessoas residentes do mercado)
S = oferta física do bem    (mesma unidade física daquele bem)
x = S / D
preço = f(x, modificadores explícitos futuros)
receita de referência = preço × D
```

O ponto `x=1` é a referência de equilíbrio. A lei `f` é passada pelo
chamador e **não foi escolhida** neste incremento. Pode ser racional;
a forma e os coeficientes devem ser calibrados separadamente, sem
atribuir um "valor necessário por pessoa" ou impor limite de consumo.
Uma **função ilustrativa nos testes** não vira lei do jogo.

**Nota sobre receita:** o produto `p × P` é a receita de referência
solicitada para o modelo de preço. Ele **não é comprovante de mercadorias
entregues, tributo, crédito, saldo, dinheiro criado ou lucro liquidado**.
A contabilidade de entrega física e as regras de liquidação, quando
existirem, precisam demonstrar conservação separadamente. Sem oferta
positiva, não existe cotação executada nem receita imputada.

A população é contagem de pessoas, não taxa de mercadoria por pessoa.
Não existem `500_000`, `0,5`, demanda basal em unidades do produto,
`B=40`, saldo familiar, carteira, crédito ou multiplicador `D=P*k`
no novo motor.

## Núcleo isolado

`GlobalArena.Economy` é uma biblioteca .NET 10 pura, sem referências a
`GlobalArena.Simulation`, `GlobalArena.Runtime`, `GlobalArena.World`,
`GlobalArena.Kernel` ou ao Godot. Até que o novo motor esteja pronto, o
Godot e os saves históricos **continuam no caminho antigo, sem alterações**.

`PhysicalOffer.FromAvailable(available, retained)` apenas subtrai
retenção física deliberada. `PopulationPriceKernel.Quote(P, S, f)` faz
uma divisão e uma avaliação de função por mercado/produto: execução
assintótica **O(1)**, sem integração discreta por cada unidade produzida,
sem simulação de compradores individuais, sem grafo de transporte e sem
alocações obrigatórias internas. Uma contagem grande de mercados e
performance final requerem benchmarks em mundos densos; O(1) não prova FPS.

## O que reaproveitar e o que não promover automaticamente

**Preservar e aproveitar por contrato:** geometria esférica Goldberg,
geração e biomas, tiles e fronteiras, movimentação e tempos de viagem,
caminhos e custos físicos de transporte, posse/conservação material,
Godot, UI tática, saves imutáveis, CI e testes de determinismo.

**Isolar como LEGADO / NÃO CONFERIR AUTORIDADE ECONÔMICA NOVA:**
`CanonicalPhysicalTacticalSession` V9–V20 no regime cash-food,
`FoodDemandRawPerWorker`, `FoodDemandAt`, `FoodUnmetRaw`,
`PhysicalFoodAvailabilityProjection` VS25,
`EconomicResidentDemandProfile.RawDemandRatePerResident`,
`EconomicDemandProfile.RawDemandRatePerWorker`,
as hipóteses de orçamento/alpha/B/H e seus testes históricos.
VS78/82/84/87–92 podem ter matemática/auditorias úteis, mas **não
são especificações do novo mercado**, nem invariantes de receita.
VS108/109 ficam como trabalhos históricos enquanto essa arquitetura
reorientada é homologada; não devem ser promovidas como motor novo.

**Ainda pendente, sem suposições:** escolha da curva `f(x)` e
modificadores de preço, como reconciliar receita de referência com
comércio físico (incluindo o caso de oferta quase zero), direitos e frete,
alocação de destino, estoque deliberado e reservas, consumo físico
efetivo, nutrição por quantidade/diversidade, atraso nutricional na
produtividade, tributação, persistência versionada e benchmark real.

O modelo de nutrição é independente do cálculo de preço. Nenhum
percentual de "demanda atendida" pode ser inferido de `P` e `S`.

## Plano seguro de substituição

1. **ECON-REBASE-01 (esta PR):** estabelecer novo núcleo autocontido
   `D=P`, `x=S/P`, `p=f(x)`, `R=p×P`; teste sem qualquer
   multiplicador de demanda, prova de independência dos assemblies.
2. **ECON-REBASE-02:** especificar e calibrar `f`, eventuais modificadores
   e unidades por mercadoria; registrar interpretação exata da receita,
   sem remendar o V12 antigo.
3. **ECON-REBASE-03:** ligar a entrada ao snapshot físico real;
   conservar produção/retensão/movimentação/destino, sem compradores ou
   inventários virtuais; testes determinísticos e benchmark em mapas grandes.
4. **ECON-REBASE-04:** consumo físico e nutrição com efeitos em produtividade
   no ciclo seguinte, em versão de estado nova, sem teto `D`.
5. **Troca controlada:** só depois de validar gameplay, migrar
   explicitamente saves ou manter o leitor antigo isolado. Não
   reeditar o passado nem exigir compatibilidade econômica com as
   saídas de um modelo rejeitado.

## Política de aceitação

O CI FAST deve compilar **`GlobalArena.Economy`** via referência
do projeto de testes e passar `GaVs110PopulationPriceRebaseTests`.
A suíte histórica verifica que a física antiga não foi acidentalmente
alterada, mas **não pode exigir que o novo mercado replique seus
preços/consumos**. A PR permanece Draft, sem merge em main, sem
alteração do jogo ativo.

**Precedência:** decisão expressa de 04/11/2026 substitui os trechos
da ADR-ECON-007 R2 que permitiam escalar a demanda por taxas por
residente ou integrar receita marginal sobre toda a oferta. Mantêm-se
as partes compatíveis: formação endógena de preços, absorção sem
quota de demanda, retenção deliberada, custos físicos e ausência de
orçamentos monetários exógenos.

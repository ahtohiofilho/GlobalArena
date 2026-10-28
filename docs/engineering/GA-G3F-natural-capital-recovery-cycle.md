# GA — G3-F: ciclo de capital natural, extração e recuperação

**Status:** candidato determinístico de simulação ecológica, dependente de G3-E v2 (#59).  
**Não autoritativo:** `EconomicNaturalCapitalBook` ainda é externo ao `WorldState`, ao save, ao hash canônico e ao scheduler. Não representa um estoque comercial do jogador.

## 1. Distinção fundamental

- **Capital natural:** madeira ainda em pé, pedra ainda disponível na superfície, vitalidade do solo; pertencem fisicamente à célula estratégica. Nenhum armazém, depósito comercial ou venda pode ser inferido deles.
- **Produção do turno:** mercadoria realmente retirada/cultivada por trabalhadores. A oferta resulta da produtividade v2, modulada pelo capital natural disponível.
- **Reserva comercial deliberada:** somente itens já produzidos que o jogador explicitamente guardou, previstos no G3-A/B1; nunca nasce automaticamente de mercadoria não vendida.

## 2. Componentes

`EconomicNaturalCapitalPolicy` exige explicitamente:
- escala física de madeira/elemento de vegetação no máximo de aptidão (`RawWoodAtFullEndowment`);
- escala física de pedra superficial (`RawStoneAtFullEndowment`);
- taxa de regeneração natural de madeira;
- taxa de recuperação do solo;
- desgaste de vitalidade por unidade de produção agrícola.

**Nenhum desses números está calibrado.** Não há valor fixo escolhido pelo motor nem multiplicador artificial de posse ou de instalação. Os saldos representam estoques ambientais finitos cuja escala deverá emergir de modelagem física validada posteriormente.

`EconomicNaturalCapitalInitializer.Initialize(generated, policy)` constrói um livro imutável cobrindo as células estratégicas com:
- capacidade lenhosa = aptidão v2 de vegetação × escala de madeira;
- biomassa lenhosa inicial = capacidade;
- pedra superficial inicial = aptidão v2 × escala de pedra;
- vitalidade do solo inicial = aptidão local de solo v2.

`EconomicNaturalCapitalTurnResolver.Resolve(..., previous, policy, mapping, workerDemand, turn)` usa a produção v2 do G3-E e ajusta a oferta real antes do mercado:

1. requer `turn = previous.LastAppliedTurn + 1`, vínculo correto com mundo e valida que os saldos não excedem as aptidões geográficas;
2. **agricultura:** rendimento proporcional à razão `soil_vitality / initial_soil_suitability`; produção causa desgaste explícito. Ausência de desgaste ou de aptidão não é substituída por bônus;
3. **floresta:** retirada compartilhada limitada à biomassa em pé. Após a retirada, recuperação proporcional à lacuna até a capacidade e à aptidão local de vegetação;
4. **pedra:** retirada limitada ao saldo superficial, **sem reposição espontânea em turnos seguintes**;
5. **múltiplos produtores na mesma célula:** dividem a mesma madeira/pedra disponível proporcionalmente às respectivas ofertas potenciais, com resíduos fixos distribuídos por maior resto, depois `EconomicPointId`; não é possível consumir cada árvore duas vezes;
6. todos os trabalhadores continuam causando demanda, mesmo se já não houver madeira, pedra ou produção agrícola; nunca se cria vendedor/comprador ou estoque de mercadorias para forçar liquidação;
7. a transformação é pura, devolvendo `RealizedFlows` e `NextBook`, sem mutação dos valores de entrada.

Recuperação de solo e de floresta ocorre depois da produção do turno, preservando o balanço para o turno seguinte. A pedra **não** se recupera naturalmente por esse modelo; abrir acesso a novos afloramentos/escala geológica é uma regra física futura diferente, não um respawn artificial.

## 3. Regras matemáticas

Com `D=1_000_000` e arredondamento para baixo:

```text
wood_limit  = min(potential_wood_output, standing_wood)
stone_limit = min(potential_stone_output, surface_stone)
food_output = floor(base_food_output * soil_vitality / soil_suitability)

soil_wear  = min(soil_vitality,
                 floor(food_output * soil_wear_per_food_unit / D))
wood_after = standing_wood - extracted_wood
stone_next = surface_stone - extracted_stone

wood_regrowth = floor((wood_capacity - wood_after)
                      * wood_recovery_fraction * woody_endowment / D²)
soil_recovery = floor((natural_soil_suitability - soil_after_wear)
                      * soil_recovery_fraction * water_availability / D²)

wood_next = wood_after + wood_regrowth
soil_next = soil_after_wear + soil_recovery
```

Os fatores climáticos e geográficos não são `buffs`: descrevem condições físicas já calculadas. É possível ficar sem pedra superficial numa célula, mas isso não impede ocupar outras terras acessíveis ou executar produção básica de madeira/alimento onde houver potencial.

## 4. Testes

`GlobalArena.Tests/G3FNaturalCapitalTurnTests.cs` prova:
- produção de madeira e pedra limitada à retirada física; depois de esgotadas, permanecem sem nova oferta; demanda dos trabalhadores persiste;
- recuperação da floresta e do solo em descanso, com pedra imóvel;
- dois pontos não conseguem extrair a mesma madeira em duplicidade;
- desgaste do solo reduz safra posterior sem reduzir demanda;
- execução de turnos fora da ordem, replays e saldo falsificado são rejeitados;
- ordenação do livro não altera resultados;
- parâmetros inválidos e estoques superiores à capacidade são recusados.

```sh
dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~G3FNaturalCapitalTurnTests
```

**Aprovação depende da CI multiplataforma; não presumir.**

## 5. Gate de integração

Esta versão é adequada somente como **referência executável**:

- o livro ecológico deve ser persistido e autenticado junto do `WorldState` e de sua política/versão; somente a existência de `LastAppliedTurn` no objeto isolado não evita replay de snapshots antigos;
- política física deve ser calibrada com geografia efetiva, unidades materiais, taxa de reposição e duração do turno;
- vincular o cálculo a um comando/scheduler autoritativo e apurar eventos, estoque comercial intencional e quantidade efetivamente extraída;
- mapear adequadamente `StrategicCellId` e `PhysicalTacticalTileId`, com ponte e rios reais;
- consumo de recursos para construir instalações e estradas continua separado, aguardando conciliação da versão de hash com PR #50;
- o G3-E permanece v2 **opt-in** e seu PR #59 deve ficar aprovado pela CI antes de esta pilha ser considerada consistente.

**Sem merge, sem alteração de main ou dos PRs #49/#50.**

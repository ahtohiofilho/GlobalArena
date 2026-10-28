# GA — G3-B2: gasto material candidato e transferência conservativa para ponto econômico adjacente

**Estado:** experimento C# em PR rascunho, sobre G3-B1 (#54); não é uma construção concluída nem um sistema de colonização.  
**Objetivo:** provar transições sem dinheiro fictício, sem população criada e sem conflito prematuro com o hash v9 do PR #50.

## 1. Gasto de materiais

`EconomicConstructionMaterialDebitCandidateResolver.Debit(world, owner, reserves, requirements)`:

- reutiliza o verificador de propriedade, identidade, duplicatas e viabilidade de G3-B1;
- exige pelo menos um insumo, todos disponíveis **no ponto econômico exato**;
- efetua **debito imutável e conjunto** das quantidades de madeira/pedra declaradas, sem crédito em outro ponto, empréstimo implícito ou saldo negativo;
- remove linhas de saldo zeradas e preserva cursores de turnos;
- nunca deixa gasto parcial se um dos insumos for insuficiente.

**Fronteira:** devolve um `EconomicMaterialReserveBook` candidato, independente do `WorldState`. Isto não constitui um comando válido para gastar recursos em gameplay: não cria obra, não atesta propriedade do tile fino, não muda o hash e não oferece defesa contra reaplicação externa de um mesmo snapshot. A construção autoritativa deverá consumir materiais **e** instalar o ativo em uma única transição aprovada.

## 2. Transferência de população e trabalhadores

`EconomicAdjacentSiteRelocationResolver.Apply(generatedWorld, world, owner, order)`:

- usa `WorldState` **v8 já existente**, vinculado ao mundo gerado, com censos iniciados;
- exige um `EconomicPoint` de origem do jogador e identidade de destino inédita;
- exige que origem e destino sejam **células estratégicas terrestres adjacentes já controladas pela mesma civilização**;
- transfere quantidades positivas de residentes e trabalhadores (trabalhadores não podem exceder moradores);
- move alocações de atividade pelo tipo econômico correto, sem converter agricultura em mineração; trabalhadores não alocados só podem sair da parcela não alocada da origem;
- atualiza `EconomyRuntimeState` e `PopulationRuntimeState` juntos via `WorldState.WithEconomyAndPopulation`;
- mantém os demais domínios do `WorldState`, inclusive controle territorial, intactos. O hash da economia/população muda pelo mecanismo já existente.

**Fronteira física:** uma `StrategicCellId` ainda não é um `PhysicalTacticalTileId`. Este resolvedor cria um novo ponto econômico em célula estratégica controlada, mas não prova acesso por tile fino, travessia de rio, consumo de recursos de fundação, duração de movimento ou colonização de célula não controlada. **Não é** um comando/evento do scheduler; a chamada pura é referência de uma futura operação autoritativa.

## 3. Exemplo verificável

Uma área de origem possui **10 residentes, seis trabalhadores**. Três trabalham com agricultura, um com mineração e dois estão sem alocação.

Uma transferência leva quatro residentes, três trabalhadores (dois agrícolas e um mineiro) para nova célula terrestre adjacente já controlada.

| Campo | Origem antes | Origem depois | Novo ponto |
|---|---:|---:|---:|
| Residentes | 10 | 6 | 4 |
| Trabalhadores | 6 | 3 | 3 |
| Agricultura | 3 | 1 | 2 |
| Mineração | 1 | 0 | 1 |
| Não alocados | 2 | 2 | 0 |

A soma dos habitantes e trabalhadores não muda; o segundo mercado só cresce quando ali existir demanda própria conforme as regras do jogo.

## 4. Testes / aceite

Arquivo: `GlobalArena.Tests/G3B2ConstructionDebitAndSiteRelocationTests.cs`.

Provas: debit exato por origem; falha integral sem débito parcial; remoção de saldo zero sem transferir saldo entre locais; ordem canônica; conservação populacional; atualização do hash do mundo; rejeição de ocupação fora do território, trabalhadores inexistentes, atividade trocada, identidade repetida e mundo vinculado incorreto.

```sh
dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~G3B2ConstructionDebitAndSiteRelocationTests
```

Os números de materiais são fixtures e não receitas balanceadas. Os testes não validam infraestrutura real ou trajetória fina.

## 5. Próximo gate de integração

Somente após revisão dos PRs #49/#50 e dos contratos de localização fina:

1. adicionar `EconomicMaterialReserveBook` à transição **autoritativa** e ao hash versionado, com política de migração e hashes de regressão;
2. autenticar e reconciliar o resultado de retenção por turno e entrega no destino contra o mercado, sem assumir que intenção de compra virou estoque;
3. validar receita tecnológica de construção e consumi-la atomicamente enquanto registra ativo efetivamente construído;
4. transformar o resolvedor de transferência em comando com tempo, destino fino, elegibilidade territorial e recursos de fundação; colonização fora de território controlado exigirá contrato distinto;
5. validar caminhos, pontes e terminais somente quando fisicamente necessários, conforme G1.

**Importante:** PRs #49/#50 continuam sem merge, e este incremento também não deve ser integrado sem autorização explícita.

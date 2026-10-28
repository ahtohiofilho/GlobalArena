# GA — G3-C: trabalhadores únicos e produção primitiva sem construções

**Status:** revisão candidata em branch empilhada sobre PR #56; não autoriza merge, não altera `main`, PR #49 ou #50.

## Decisões corrigidas com o jogador

1. **Trabalhador é a unidade econômica e de consumo.** Cada trabalhador alocado ou não alocado contribui para a demanda por trabalhador. Trabalhadores alocados também produzem conforme atividade e potencial; NÃO existe uma segunda população independente para conduzir decisões neolíticas.
2. **Produção primitiva não exige construção.** Agricultura rudimentar, coleta/corte de madeira e coleta superficial de pedra exigem trabalhadores, disponibilidade natural e eficiência positiva no local, mas nenhum investimento obrigatório para começar a operar. Infraestrutura construída é melhoria de produtividade ou tecnologia, nunca uma barreira arbitrária à extração básica.
3. **Ocupar um novo local acessível não exige madeira nem pedra.** A forma básica da fundação precisa de trabalhadores que saem de um ponto existente e de um destino terrestre estratégico adjacente livre. Madeira e pedra podem financiar melhorias futuras, opcionalmente, mas não são pré-condições da ocupação básica.

## Implementação

### Florestas

Introduz `EconomicActivityKind.Forestry = 4`, **sem renumerar** `Agriculture = 1`, `Mining = 2`, `TradeLogistics = 3`. Ampliam-se validadores de produção, oferta, alocação de mercado e preço de reserva para aceitar a floresta como fonte produtiva distinta. Portanto madeira não é furtivamente classificada como mineração.

`EconomicPrimitiveWorkerEconomyResolver.Resolve(generated, world, primitivePotentials, workerDemandProfiles)`:

- exige vínculo com o mundo gerado correto e células terrestres;
- usa `EconomicProductionDemandResolver` que já produz conforme a alocação de atividades e calcula **demanda sobre `point.Workforce`**, sem qualquer coeficiente de moradores;
- não exige `City`, fazenda pronta, campo madeireiro, pedreira, estação, estrada, reserva ou novo estado persistente;
- **não cria automaticamente recursos onde falta potencial:** os potenciais e seus valores de produtividade são fornecidos pelo chamador e ainda devem ser ligados a indicadores geográficos específicos aprovados.

**Limitação geográfica:** o gerador atual possui `StrategicResourcePotentialFieldSet.GeneralPotential`, não mapas autoritativos distintos para fertilidade agrícola, madeira e pedra. O resolvedor aceita potenciais explícitos e verifica terra; isso demonstra a mecânica, mas NÃO autentica a disponibilidade de floresta ou pedra de cada célula. Esse gate precisa de fields especializados ou política derivada validada.

### Fundação básica (sem recurso)

`EconomicPrimitiveWorkerMoveOrder` contém só trabalhadores e alocações de suas atividades. `EconomicNeolithicSiteFoundingResolver.ProposeBasic(..., reserves, move)` propõe reivindicação de célula terrestre vizinha livre e criação de ponto econômico com trabalhadores transferidos, sem qualquer receita de construção e **preservando integralmente o livro candidato de reservas**.

Por compatibilidade com `WorldState` v8, que ainda contém `PopulationRuntimeState` e o hash de residentes, a operação apenas **espelha** o número de trabalhadores nos campos de census e exige que na origem `Residents == Workforce`. Isso é uma costura técnica legada, não autorização para usar duas populações no design do jogo. Um ponto com contagens divergentes é rejeitado em vez de produzir duplicações ou consumo duas vezes.

O antigo `Propose(..., recipe)` permanece **apenas como caminho candidato de fundação material**, para compatibilidade com os testes e como possível construção melhorada; seus requisitos de madeira/pedra **não** se aplicam à ocupação básica.

## Testes executáveis

`GlobalArena.Tests/G3CPrimitiveWorkerEconomyTests.cs` cobre oferta das três atividades sem instalações, demanda de todos os trabalhadores, demanda de trabalhadores sem atividade alocada, ausência de produção sem potencial explícito, comércio de madeira sem terminal e rejeição de mundo incompatível/atividade produtiva não alocada.

`GlobalArena.Tests/G3CWorkerOnlyBasicFoundingTests.cs` cobre fundação com reservas vazias, permanência de reservas existentes, conservação de força de trabalho/espelhamento do census legado, demanda após a migração, recusa de census legado incompatível, de trabalhadores inexistentes, destino ocupado, alocação errada e hash obsoleto.

```sh
dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~G3C
```

**Aprovação depende da CI e regressão multiplataforma, não é presumida.**

## Fronteiras arquiteturais ainda abertas

- Retirar a semântica independente dos residentes dos caminhos restantes da economia, sem quebrar saves/hash já estabelecidos. `EconomicResident*` são caminhos históricos opt-in e não são adotados como regra econômica neolítica.
- Concretizar potenciais físicos individualizados para agricultura, florestas e pedra, com disponibilidade geográfica apropriada e sem níveis artificiais.
- Ainda não há comando autoritativo de colonização ou construção, nem estoque integrado ao `WorldState`.
- A expansão ainda checa adjacência em **células estratégicas**, não rota transitável em tile fino; rios visíveis, pontes e travessias continuam gate físico necessário.
- A compatibilização de hash com o PR #50 permanece pendente; `Forestry=4` é novo valor enum e exige futura revisão dos cenários, hashes de regressão e consumidores que enumeram atividades, embora o formato de `WorldState` não tenha sido alterado neste incremento.

**Sem merge automático.** A nova trajetória é: produzir e consumir com trabalhadores; coletar/cultivar sem construção; ocupar área próxima com força de trabalho; investir materiais opcionalmente para melhorar infraestrutura e produtividade.

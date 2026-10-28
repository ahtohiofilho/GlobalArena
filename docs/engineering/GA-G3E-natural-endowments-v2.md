# GA — G3-E: aptidões naturais distintas, correlação espacial e produtividade v2

**Status:** derivação ambiental experimental e opt-in; ainda NÃO é geologia, cobertura florestal ou fertilidade de solo autoritativas para saves.  
**Dependência:** G3-D (#58), G3-C (#57); nenhum merge automático.  
**Sem alteração:** `WorldState`, hash canônico, assinatura v1 do mundo, PRs #49/#50, economia do G3-D v1.

## 1. Motivo

O G3-D apresentou três potenciais separados, mas ainda usava bioma florestal binário e um campo geral sem correspondência geológica. O G3-E introduz **três qualidades ambientais distintas**, mantendo extração rudimentar disponível sem custo de construção e a força de trabalho como origem de produção e demanda.

- `SoilSuitability` — qualidade aproximada para plantio, influenciada por relevo baixo e menor exposição rochosa.
- `WoodyVegetation` — índice de vegetação lenhosa potencial, contínuo e correlacionado com condições térmicas/umidade da região; não depende do rótulo binário `Forest` e pode representar vegetação rala em outras terras.
- `ExposedStone` — disponibilidade aproximada de pedra superficial, derivada de um substrato rochoso específico e relevo.
- `LithologyProxy` — campo de substrato fictício **reprodutível**, amostrado de domínio PRNG isolado (`PrimitiveLithology = 10`) e suavizado com a vizinhança estratégica terrestre. Não equivale a uma classe de rocha geológica real.

## 2. Contrato de versão

`StrategicPrimitiveNaturalEndowmentFieldSet.DerivationVersion = 2`.

O modelo anterior `StrategicPrimitiveResourcePotentialFieldSet.DerivationVersion = 1` e seus fluxos **continuam intactos**.

`EconomicNaturalEndowmentWorkerEconomyResolver(derivationVersion)` exige explicitamente **2**, rejeitando outros valores. Isso impede que um chamador selecione implicitamente uma fórmula diferente sem perceber.

**Atenção:** o contrato de fase autoritativo e o cabeçalho de save/replay ainda precisam persistir esta versão escolhida. A v2 não é adicionada por enquanto ao cálculo da `WorldGenerationCanonicalSignature` nem ao hash do `WorldState`; portanto, até essa integração, o resolvedor é referência candidata e não deve ser ativado silenciosamente em partidas persistidas.

## 3. Fórmulas de referência

Escala fixa: `D = 1_000_000`. Elevations variam de `[-D,+D]`, então `relief = floor(rawRelief/2)` normaliza para `[0,D]`.

Para cada célula, temos `rawRock` gerado do domínio `PrimitiveLithology`; a média espacial inclui peso 2 da própria célula e peso 1 de cada vizinho terrestre não-polar:

```text
rock   = floor((2*rawRock[i] + ΣrawRock[vizinho]) / (2 + vizinhos_válidos))
woody  = floor((2*localWoody[i] + ΣlocalWoody[vizinho]) / (2 + vizinhos_válidos))
localWoody = floor(moisture * (D - abs(temperature)) / D)
soil   = floor((D - relief) * (D - rock) / D)
stone  = floor(rock * (D + relief) / (2*D))

food_productivity = floor(soil * water_availability *
                           (D - abs(temperature)) / D²)
wood_productivity = woody
stone_productivity = stone
```

Água e `PolarIce` recebem **zero** em todas as aptidões. Em outras terras, o índice é contínuo: sem requisito compulsório de fazenda, terminal, acampamento, pedreira, armazém ou estrada. Sem produção quando a produtividade efetiva for zero. Sem rendimento mínimo fictício.

**Por que a suavização?** Células vizinhas compartilham condições espaciais. O domínio de rocha é distinto do ruído genérico de recursos, mas o valor amostrado não representa observação geológica: é uma hipótese procedural para testar consistência de gameplay.

## 4. Relação com economia

`EconomicNaturalEndowmentWorkerEconomyResolver`:

- valida assinatura de mundo/estado e versão de derivação;
- determina um rendimento por trabalhador com os campos v2 para `Agriculture`, `Forestry`, `Mining`;
- reutiliza `EconomicPrimitiveWorkerEconomyResolver` + `EconomicProductionDemandResolver`;
- preserva demanda de todos os trabalhadores, inclusive sem alocação produtiva ou em `TradeLogistics`;
- não cria mercadorias se a extração local for inviável e não introduz reservas automáticas.

## 5. Testes e critério de aceite

`GlobalArena.Tests/G3ENaturalEndowmentWorkerEconomyTests.cs` verifica:
1. determinismo entre gerações de mesma semente e assinatura mundial inalterada;
2. variação do substrato sob sementes diferentes;
3. normalização e ausência de produção em água/gelo;
4. relações aritméticas de solo/pedra e suavização espacial da vegetação;
5. produtividade por trabalhador das três atividades sem estruturas, com consumo de todos os trabalhadores;
6. trabalhadores sem alocação gerando demanda sem oferta;
7. atividade agrícola inviável em terra polar não criando produção, mantendo demanda;
8. versão inválida e mundo incorreto rejeitados.

```sh
dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~G3ENaturalEndowmentWorkerEconomyTests
```

CI multiplataforma e regressão completa continuam obrigatórias.

## 6. Pendências de fidelidade física

Estes ainda são **proxies experimentais**, não quantidades reais de árvore, solo arável ou rocha:
- implementar estratigrafia e afloramentos, solo e cobertura vegetal por métodos próprios quando o worldgen suportar;
- articular escalas estratégicas/fino e condições reais de acesso;
- definir a dinâmica temporal de regeneração florestal, fertilidade, exaustão de pedras acessíveis e tempo de descanso/rotatividade agrícola, sem limites arbitrários;
- versionar explicitamente o contrato autoritativo de derivação e validar hashes de replay;
- calibrar produção observando efeitos econômicos, sem aumentar produtividade por bônus fixo de posse, cultura ou construção.

**Não integrar sem autorização expressa.**

# GA — G3-D: produtividade primitiva derivada da geografia estratégica

**Status:** referência testável, versão derivada `1`. Não é um novo campo geológico/edáfico autoritativo e não é uma instalação construída.  
**Base:** G3-C (#57). Sem merge na `main`, sem alteração dos PRs #49/#50 e sem modificação do hash canônico.

## Objetivo

Substituir, no cenário básico, a entrada manual de produtividade por um cálculo **determinístico a partir do mundo gerado**. Trabalhadores são produtores quando alocados, e todos geram demanda; a produção primitiva continua **sem requisitos de material, fazenda, pedreira, acampamento madeireiro ou terminal**.

## Contratos adicionados

- `StrategicPrimitiveResourcePotentialFieldSet.Derive(generatedWorld)` produz três `StrategicScalarField` alinhados às `StrategicCellId` da topologia, com `DerivationVersion = 1`:
  - `Agriculture`: aproximação baseada na disponibilidade de água, conforto térmico e suavidade do relevo, multiplicados em ponto fixo.
  - `WoodGathering`: há potencial onde o bioma gerado é `Forest` ou `Rainforest`; o coeficiente é a umidade local. Sem bioma florestal, não existe oferta madeireira primitiva nesse proxy.
  - `SurfaceStone`: média do potencial geral de recursos existente com a variação do relevo — **um proxy de acessibilidade de pedra superficial**, não um depósito geológico comprovado.
- Todos os três potenciais são **zero em água e gelo polar**. Em terra, cada coeficiente está dentro de `[0, 1_000_000]`. Valor zero não é substituído por rendimento mínimo fictício.
- `EconomicPrimitiveCommodityMapping(Food, Wood, Stone)` mantém IDs válidos e distintos, definidos pelo catálogo do chamador — nenhum ID numérico obrigatório é inserido no motor.
- `EconomicGeographicPrimitiveWorkerEconomyResolver.Resolve(generatedWorld, world, mapping, workerDemandProfiles)` monta potenciais de produção com base no tipo de atividade de cada grupo de trabalhadores e no campo estratégico correspondente, então reutiliza `EconomicPrimitiveWorkerEconomyResolver` e `EconomicProductionDemandResolver`. Não cria uma linha de oferta quando a atividade não possui potencial. O consumo é calculado pelo total `EconomicPoint.Workforce` em todos os casos.

## Fórmulas de referência (ponto fixo)

Seja `D=1_000_000`. Como o gerador aceita elevações no intervalo `[-D,+D]`, a diferença local de elevação pode estar em `[0,2D]`. Para uso nas fórmulas, `relief = floor(raw_relief / 2)` normaliza essa diferença para `[0,D]`.

- `agriculture = floor(water * (D - abs(temperature)) * (D - relief) / D²)`
- `wood = moisture` somente em `Forest/Rainforest`; zero nas demais classes
- `surface_stone = floor((general_resource_potential + relief)/2)` em terra sem gelo polar

Todos são **coeficientes de produtividade por trabalhador**, não unidades de recursos armazenadas nem garantias de que o depósito não se esgote. A produção econômica concreta multiplica coeficiente e trabalhadores alocados pela escala de fluxo já existente.

**Atenção:** esses coeficientes não são uma descrição científica exata do solo, floresta, minério e pedras de cada tile. O `GeneralPotential` da geração atual é um campo genérico; ainda faltam camada de geologia, fertilidade/uso da terra, cobertura florestal e regeneração/extração por período. As fórmulas são candidatas e precisam de calibração com gameplay, não devem ser tratadas como física final.

## Compatibilidade e determinismo

1. A derivação **não consome PRNG** nem modifica o mundo gerado, seu `WorldGenerationCanonicalSignature`, `WorldState`, seus hashes ou a política global de produção.
2. Há uma versão separada (`DerivationVersion=1`) porque alterar coeficientes em futuras versões pode modificar o resultado de um turno **mesmo que a assinatura mundial antiga permaneça igual**.
3. Para integração autoritativa com save/replay será necessário incluir a versão da derivação no contrato de simulação e/ou assinatura mundial versionada, sem reescrever v8 e sem criar v9 concorrente ao PR #50.
4. A localização ainda é `StrategicCellId`; potencial por `PhysicalTacticalTileId` permanece pendente. Nenhuma reivindicação de geografia fina ou de travessia de rio é feita.
5. A função derivada não automaticamente habilita uma atividade. É necessário alocar trabalhadores em agricultura, floresta ou mineração. Quem não está alocado continua gerando demanda; isso não fabrica produção.

## Testes G3-D

`GlobalArena.Tests/G3DGeographicPrimitiveProductionTests.cs`:

- mesma semente gera exatamente os mesmos três campos; assinatura do mundo não muda;
- valores normalizados e zero em água/gelo polar;
- madeira somente em bioma florestal e relacionada à umidade;
- produção igual ao coeficiente geográfico por trabalhador, sem estruturas, com demanda dos seis trabalhadores;
- trabalhadores alocados em floresta inexistente não produzem madeira, mas ainda consomem;
- trabalhadores sem atividades não produzem e continuam gerando demanda;
- identidades de mercadoria duplicadas/inválidas e vínculo de mundo incorreto falham.

```sh
dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~G3DGeographicPrimitiveProductionTests
```

**CI é gate obrigatório.** Nenhum merge autorizado.

## Próximo gate (G3-E)

Calibrar a geografia com recursos independentes (fertilidade agrícola, cobertura florestal, afloramentos/pedras), mantendo determinismo, assinar a versão dos campos quando integrada a saves, resolver granularidade estratégica x fina e só então derivar a eficiência de melhorias construídas.

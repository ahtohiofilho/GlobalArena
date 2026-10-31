# GA-VS39 — Infraestrutura terrestre existente e modalidades condicionadas (prévia)

**Data:** 31/10/2026. **Base:** GA-VS38 / PR #144, 7/7 workflows success. **Status:** PR em draft, stacked.

## Diretriz central

O jogador continua a controlar unidade e destino — não deve organizar embarques, passos entre tiles ou linhas de transporte manualmente. O processador absorve complexidade econômica e física. Este marco **não altera a jogabilidade** e não implementa construção, disponibilidade econômica, navios ou combate.

## Reutilização do sistema existente

A economia já possui `EconomicTransportBuiltAsset`, identificando um crossing real da malha **qualificada pela resolução**, uma tecnologia e sua versão de catálogo, propriedade operacional e disponibilidade por `StrategicEdgeAccessSnapshot`. Não criar outra identidade de estrada ou uma rede gratuita.

A nova `ExperimentalLandInfrastructureSnapshot` faz a leitura **opt-in** desses ativos e combina-os com `CanonicalLandEdgeFactsSnapshot` da VS38.

- **Infraestrutura ≠ modalidade**. Qualidade da via: trilha natural, estrada de pedra, rodovia. Ferrovia é uma capacidade independente que pode coexistir com a estrada no mesmo corredor. Rodovia substitui a classe rodoviária anterior: não aplicar bônus cumulativos de pedra + rodovia.
- **Modalidade** hipotética: caminhada, animais de carga, veículo rodoviário ou ferroviário. Caminhada e animais podem usar travessias de terra primitivas; veículo rodoviário exige estrada construída; veículo ferroviário exige ferrovia contínua. O fato de existir ferrovia não torna a aresta uma estrada para veículos rodoviários.
- A infraestrutura deve ser **explicitamente construída, operacional e não bloqueada**, por crossing qualificado da malha tática. Ativos desconhecidos, resolução ou tecnologia não mapeada, e obras sobre água são rejeitados. Estrada nenhuma dá passagem oceânica.
- O catálogo `(TechnologyTypeId, TechnologyCatalogVersion) -> ExperimentalLandInfrastructureKind` é fornecido **explicitamente pelo chamador**. Os nomes StoneRoad / Highway / Railway não introduzem IDs de tecnologia canônicos novos nem alteram catálogos existentes.
- A rede é um **snapshot da revisão informada pelo chamador**; mudar obras cria outra projeção, mantendo a anterior imutável. Nenhuma escrita em save/hash.

## Contrato de roteamento

O `ScalarPhysicalRouteProjection` VS36 ganha uma extensão **opcional**: `IConstrainedScalarPhysicalEdgeCostProvider`. A prévia ignora ligações que o modal não pode usar, em vez de atribuir custo infinito ou construir uma ponte implicitamente. Se existe terra conectada mas não existe caminho desse modal, retorna a nova disponibilidade read-only `NoCompatibleLandTransportMode`. O roteamento legado e seus formatos de save não consomem esse novo status.

`ExperimentalModalLandEdgeCostProvider` aceita modalidade escolhida, fatos geográficos e rede de infraestrutura. Traduz a cotação **experimental** VS37 por percentuais **explicitamente fornecidos pelo chamador**, não por constantes de balanceamento ativadas.

Isso **não calcula preço de frete**, consumo de combustível, amortização, capacidade, saturação, custo de transbordo ou tarifa. Também não confunde pontos de movimento com receita monetária. O itinerário econômico canônico mantém seu próprio provider de descoberta e função de preço da viagem **inteira**, sujeita a ativos construídos e tecnologia versionada.

**Importante:** a VS39 não permite trocar de modal no mesmo nó; isso exige uma entidade de terminal/transbordo e a política comercial de itinerários já pesquisada. Não inventar terminal porque linhas têm um nó em comum.

## QA / gate

- Trilha natural caminhável sem ativos, **nunca** usada por veículo rodoviário ou ferrovia;
- Estradas instaladas apenas onde os ativos existem, continuidade por duas arestas para uma única rota de veículo;
- Rodovia substitui estrada de pedra, ferrovia coexiste sem desconto duplo;
- Ferrovia parcial não cria passagem remota nem transbordo automático; a escolha continua pelo motor;
- tecnologia + versão desconhecidas, geometria grosseira vs fina, ativos repetidos e obras sobre água recusados;
- obras inoperantes/fechadas não habilitam modal;
- revisões independentes, sessão isolada, equivalência caminhada VS38 com calibragem 100%, save/hash e UI intactos;
- xUnit Linux/Windows, suite inteira no Windows, builds Godot Debug/Release.

## Fases futuras (não implementadas)

1. Política de perfis de mobilidade e velocidade, talvez massa/velocidade categóricas 1–5 com avaliações de UX antes de adotar uma ficha extensa.
2. Cotações de **frete agregadas** por modalidade e rede, com custos fixos, escalas, transbordo, congestionamento/saturação contínua, volume e cache versionado por ciclo econômico. Não conceder serviços gratuitamente.
3. Ativação física por pontos apenas em save/replay versionado, preservando V13–V17.
4. Travessias artesanais e aquáticas, rios/estreitos, profundidade e capacidade real de embarcações (issue #140), sem micromanagement.

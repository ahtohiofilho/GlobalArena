# GA-Next R163 — identidade canônica de cidades e residência

## Contrato

A cidade tem identidade estável própria (`FoundationCityId`), proprietário (`CivilizationId`) e localização fundadora física (`PhysicalTacticalTileId`). Uma pessoa tem um **domicílio** (`FoundationWorkerResidenceRuntimeRecord`) separado de sua localização tática já autoritativa no `PhysicalWorkerRuntimeRecord`.

Viajar, operar fora da cidade ou deslocar-se militarmente NÃO altera o domicílio. A população da cidade é obtida **contando vínculos de residência**, sem cópia independente de um inteiro que possa ficar defasado.

## Escopo de implementação

- Novo `FoundationCityResidenceRuntimeState` imutável, com catálogo canônico de cidades e um domicílio por trabalhador, IDs próprios, ordenação estável e validação de duplicatas.
- `WorldState.CityResidences`, inicialmente não implantado, e `DeployInitialCityResidences` para promoção explícita de um censo físico completo. Invariantes impõem exatamente um domicílio por trabalhador, cidade pertencente à civilização do morador, tiles da mesma malha e todas as cidades dentro do planeta.
- Todos os caminhos de cópia/revisão `WorldState` preservam o ledger após sua implantação; as operações normais de movimento físico não reescrevem residência.
- Hash canônico **v16 opt-in** inclui IDs, donos e tiles das cidades, e IDs dos trabalhadores e seus domicílios. Estados anteriores sem ledger continuam usando os bytes v8–v15 existentes. A presença de v16 não aciona artificialmente os blocos monetários v13–v15.
- Novo overload `FoundationDemographicGrowthPlanner.Plan(world, survivors, turn)` deriva os pesos das cidades do ledger e preserva o sorteio mundial da R162. O overload legado com cidades explícitas permanece disponível, mas ainda exige co-localização experimental.

## Limites deliberados

A implementação **não funda cidades de forma automática**, não escolhe sua localização, não cria residentes, não escolhe atividades e não dispara os nascimentos do turno. A implantação inicial de cidade e domicílio é uma promoção explícita do estado físico, ainda não um evento do diário. A próxima fase deverá estabelecer as transições de fundação, nascimento e migração residencial autorizadas e reproduzíveis.

O ledger não inventa o conceito de cidade a partir de aglomerações de trabalhadores num tile: esses fatos precisam ser atribuídos explicitamente. Tampouco é uma população física independente; o trabalhador só pertence a uma residência, mesmo quando está viajando.

## Teste de aceite

O teste cria cidades identificadas para duas civilizações, promove um estado físico existente de versão anterior para v16, desloca um trabalhador viajante para outro tile e confirma que sua cidade de residência continua com a mesma população. A R162 continua distribuindo novos habitantes com os pesos de **residência**, não a distribuição momentânea de viajantes. Regressões garantem hash determinístico, preservação por `AdvanceRevision`, rejeição de residência faltante, duplicata ou da civilização errada, e integridade dos casos R162.

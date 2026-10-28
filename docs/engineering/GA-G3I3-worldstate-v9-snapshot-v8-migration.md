# GA — G3-I3: WorldState v9 com instalações físicas e migração explícita v8 → v9

**Status:** revisão candidata sobre a BRANCH DO PR #50 (que já contém seção 35 e hash v9), não sobre o PR #64, cuja base permanece em WorldState v8. **Sem merge.**

## Por que a separação de branches é intencional

- PR #49: gate de atestação da transferência de mercadorias em tile físico fino
- PR #50: `TransportFacilitiesRuntimeState` e versão 9 de `CanonicalWorldStateHasher`
- PR #64: arquivo completo v8 de civilizações/território/economia/censo/unidades + sidecar ecológico
- **G3-I3:** primeiro consumidor **real e testável de v9**, criado sobre PR #50 sem transplantar um fake `WorldState` v9 para a pilha G3 v8.

Esses dois trilhos devem ser conciliados em uma integração explicitamente aprovada no futuro. A base de G3-I3 não contém os livros de capital natural e reservas dos PRs #52–#64; por isso **este PR não promete persistência do sidecar ecológico em v9**.

## WorldState snapshot v9

`EconomicWorldStateV9SnapshotCodec` produz bytes canônicos contendo:
- `GAWSNP09` + versão própria do codec, versão e SHA-256 do `WorldState` v9
- assinatura e versão do mundo gerado, número de células estratégicas e revisão
- todas as seções tradicionais: civs, território, relações, pontos econômicos e alocações de trabalhadores, censo legado, unidades militares
- **seção física de instalações de transporte**, correspondente à seção 35 do hash v9:
  - `FacilityId` em `UInt64`
  - parâmetros M/N do Goldberg fino em `UInt32` cada
  - `FineStrategicCellId` qualificado em `UInt64`
  - operacionalidade 0/1
  - quantidade e valores de pares dirigidos `FromTechnologyTypeId`/`ToTechnologyTypeId`, mais `TechnologyCatalogVersion`
- checksum SHA-256 de payload completo

É obrigatório:
1. mundo gerado original com `RuntimeWorldBinding` exato
2. formato 9; não aceitar o antigo arquivo `GAWSNP08` nem uma versão futura
3. reconstruir todas as seções, inclusive cada instalação e capacidade, por meio dos contratos existentes
4. recalcular o hash canônico v9 e comparar com o salvo
5. re-encode byte a byte igual para impedir codificações alternativas
6. recusar corrupção, truncamento, contagens inválidas, identidade física inválida, ordem divergente e status fora do domínio

`WorldState.WithRestoredRevision` é usado apenas pelo codec para recuperar uma revisão após validar o hash; não cria nova ação do jogador.

## Migração v8 → v9

`LegacyWorldStateV8Hasher` é a implementação de hash v8 **preservada**, que não lê instalações; não modifica a `CanonicalWorldStateHasher` v9 existente.

`EconomicWorldStateV8SnapshotCodec` importa estritamente o arquivo `GAWSNP08` do PR #64, calcula e valida o hash v8 e **rejeita emitir v8 com instalações**.

`EconomicV8ToV9WorldSnapshotMigrator.Migrate(generated, v8File, independentlyAcceptedV8Hash)`:
1. exige explicitamente digest aceito com `FormatVersion=8` recebido do host, nunca confia somente no arquivo
2. valida mundo, conteúdo e hash v8 do arquivo
3. verifica que não existem instalações no estado v8
4. mantém todos os domínios v8, revisão, trabalhadores e censo legado; acrescenta apenas `TransportFacilitiesRuntimeState.Empty` (sem inventar instalação)
5. grava snapshot `GAWSNP09` e verifica seu novo hash v9 e round-trip.

**Ausência de registro v8 não equivale a prova histórica de ausência de instalações.** A migração exige política/autoridade explícita e não deve preencher instalações a partir de conjecturas. Se instalações existem em um sistema externo, é necessário um procedimento separado de importação atestada.

## Testes

`G3I3V9WorldSnapshotMigrationTests.cs`:
- round-trip v9 de dois pontos econômicos/duas civs e duas instalações com diferentes estados, tiles e capacidades dirigidas;
- censos não inicializados e ausência de instalação preservados;
- migração explícita v8 → v9, sem perder estado nem criar instalações;
- recusa de hash legado aceito incorreto, mundo de outra semente, conversão de formato implícita e emissão v8 com instalações;
- identidade, estado e direção de capacidade alteram o hash;
- corrupção/truncamento/formato v8 disfarçado como v9 rejeitados;
- ordenação canônica dos registros de instalações;
- mundo v8 original preservado pela migração.

```sh
dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~G3I3V9WorldSnapshotMigrationTests
```

## Limites seguintes

**Não há ainda integração automática entre PR #50 e PR #64**, nem save v9 incluindo capital natural e reservas no mesmo arquivo; conciliá-los exige um novo PR de integração, após aprovação explícita dos merges pertinentes. Também faltam:
- um único head durável de save completo/sidecar, com política de recuperação de crash;
- migração auditada de sidecar v1 junto ao arquivo de mundo v8, sem alterar o saldo econômico;
- journal autenticado e ligação ao scheduler;
- reconciliação entre censo legado e decisão de que **trabalhadores são os produtores e consumidores econômicos**, sem fonte dupla de demanda;
- validação física por tile fino e atestação de entregas (PR #49), antes de transformar demanda em estoque entregue.

**Este PR não modifica nem realiza merge de #49, #50, #64 ou main.**

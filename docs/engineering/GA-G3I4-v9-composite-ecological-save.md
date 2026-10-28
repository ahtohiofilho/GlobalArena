# GA — G3-I4: pacote v9 completo com instalações, capital natural e reservas

**Status:** integração candidata baseada no PR #65 (e, indiretamente, PR #50), sem merge.  
**Escopo:** um arquivo canônico `GAFULL09` que reúne **WorldState v9 inteiro** e **sidecar ecológico G3**, com hashes e versões vinculados.

## 1. O que foi integrado

O `WorldState` mantém exatamente a versão 9 do PR #50: civilizações, território, diplomacia, pontos econômicos/trabalhadores, censo legado, unidades militares e seção 35 de **instalações de transporte com identidade, tile físico qualificado, operacionalidade e capacidades dirigidas**. O codec `EconomicWorldStateV9SnapshotCodec` é o já validado no PR #65 e **não** foi substituído por uma falsa leitura v8.

Os candidatos G3 são agora disponíveis no mesmo ramo de v9:

- `EconomicActivityKind.Forestry = 4` (sem renumerar atividades existentes);
- `StrategicPrimitiveNaturalEndowmentFieldSet` e domínio isolado `PrimitiveLithology = 10`, com aptidões v2 para solo, vegetação e pedra;
- `EconomicPrimitiveWorkerEconomyResolver`, `EconomicNaturalEndowmentWorkerEconomyResolver` e `EconomicNaturalCapitalTurnResolver`;
- `EconomicMaterialReserveBook`, `EconomicDeliberateMaterialFlowResolver` e `EconomicMaterialReserveAccrualResolver`;
- `EconomicEcologicalCheckpoint`, `EconomicEcologicalTurnCommitResolver` e `EconomicEcologicalSidecarCodec` + ferramentas de replay/arquivo;
- `EconomicPrimitiveCommodityMapping` em tipo próprio, exigindo três identidades válidas distintas.

**Sem dupla demanda:** cada trabalhador pode produzir conforme a atividade e causa demanda pelo perfil do seu ponto; o census legado não é outra fonte de consumo. **Sem custo de instalação inicial:** agricultura, madeira e pedra primitivas dependem de ambiente e trabalhadores. **Sem estoque comercial espontâneo:** só a retenção deliberada de produção física gera saldo reservado.

## 2. Arquivo único GAFULL09

`EconomicCompleteV9SaveBundleCodec` inclui:

1. magic `GAFULL09`, versão de envelope 1 e comprimentos explícitos;
2. bytes canônicos completos de `EconomicWorldStateV9SnapshotCodec` — incluindo instalações físicas da seção 35;
3. bytes completos de `EconomicEcologicalSidecarCodec` — incluindo hash **v9** desse mesmo mundo, versão do potencial natural, saldos de recursos, reservas, políticas e cursores;
4. checksum SHA-256 do envelope.

A leitura reproduz o mundo e recalcula seu hash v9 **antes** de ler o sidecar; este deve corresponder ao mundo carregado, incluindo a identidade/capacidade das instalações. Alterações em um dos lados invalidam a associação. Um re-encode final precisa ser idêntico byte a byte. `EconomicCompleteV9SaveBundleArchive` grava arquivos completos imutáveis `<checkpointSha256>.gafull09`, com write/flush/rename sem sobrescrita.

**O compromisso do G3 contém o formato e digest do `WorldState`**: o próprio compromisso muda se a seção de instalações mudar, mesmo que os saldos ambientais permaneçam iguais. Ele não altera `CanonicalWorldStateHasher.FormatVersion = 9`.

## 3. O que isto NÃO conclui

- **Não importa automaticamente o sidecar G3 do v8** para v9. O G3-I3 migra o `WorldState` v8 isoladamente com hash aceito externamente, mas o compromisso antigo do sidecar está vinculado ao hash v8. Um migrador ecológico deverá validar primeiro o pacote `GAFULL08` e seu compromisso v8, reconstruir os livros sem criar/remover saldo e **reafixar explicitamente** esses saldos ao novo hash v9. Misturar os dois sidecars sem migração é rejeitado.
- **Ainda não existe head durável único do novo pacote completo**, nem migrador/testes de crash transacional para `WorldState` + ecologia. O archive não sabe qual é o checkpoint aceito mais recente; o host deve fornecer um digest confiável externo. Não confundir snapshot completo com commit autorizado.
- **Não existe comando de turno autoritativo, pagamentos e entregas físicas certificadas** nessa etapa; o resolvedor ecológico continua referência de proposta determinística.
- A derivação geográfica v2 e as capacidades ecológicas ainda são proxies e precisam de calibração; não são dados geológicos reais por tile fino.
- O v9 preserva o census legado para compatibilidade, mas as decisões econômicas neolíticas usam apenas trabalhadores como origem de produção/consumo.
- O PR #50 permanece aberto. Sem merge explícito de #49/#50 e pilha G3, esta branch **não altera main**.

## 4. Testes de integração

`G3I4V9EcologicalCompositeSaveTests.cs` cobre:
- roundtrip v9 de um turno com instalação operacional, capacidades dirigidas, trabalhadores e reservas;
- instalação com status modificado altera hash v9 e compromisso G3, impedindo anexar sidecar antigo;
- semente incorreta, corrupção interna com checksum externo recalculado, truncamento e versão inválida recusados;
- archive completo imutável e necessidade de seleção do digest aceito;
- replay ecológico determinístico produz bytes idênticos de pacote v9.

```sh
dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~G3I4V9EcologicalCompositeSaveTests
```

**Aceite técnico somente após CI no commit exato em Ubuntu, Windows e macOS (kernel, hash e benchmark).**

## 5. Próximo gate G3-I5

Elaborar **migração ecológica v8 → v9** sem perda de reservas e capital natural, com autenticação de digest do pacote antigo, versionamento explícito do compromisso, mapa de identidade de instalação separadamente atestado, testes de vetores antigos e replay, seguido de **head transacional único do pacote v9 completo** e journal de eventos.

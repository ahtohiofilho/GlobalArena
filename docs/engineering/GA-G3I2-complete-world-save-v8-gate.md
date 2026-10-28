# GA — G3-I2: save completo v8 em pacote canônico, gate explícito para v9

**Status:** referência executável de arquivo completo para o estado atual **WorldState v8**, empilhada sobre G3-I1 (#63). Sem merge.  
**Não é:** importador de v9, migração de v8 para v9, integração ao scheduler, nem transação distribuída entre armazenamento do save e serviço de confiança.

## Motivação e decisão de versão

G3-I1 já grava checkpoints ecológicos reais, mas solicita ao carregador um `WorldState` correspondente carregado por outro sistema. G3-I2 elimina essa dependência **para o formato v8 atual**: agora é possível carregar um único arquivo contendo todas as seções de estado v8 **e** capital natural/reservas materiais.

O PR #50, ainda aberto e empilhado sobre o PR #49, propõe um novo estado `TransportFacilitiesRuntimeState`, hash v9 e seção canônica 35 com identidade de instalação, tile fino qualificado e capacidades de transferência. **Não aceitar v9 como v8**; um carregador que fizesse isso perderia instalações sem aviso. G3-I2 recusa explicitamente formatos diferentes de v8. Não cria uma segunda v9 nem uma v10 concorrente.

## 1. WorldState v8 completo

`EconomicWorldStateV8SnapshotCodec` possui codec binário canônico de formato próprio 1, magic `GAWSNP08`, hash do mundo v8, vínculo do gerador (versão + assinatura + contagem de células), revisão e as seções existentes:

- civilizações e células iniciais, inclusive ausência explícita de célula materializada;
- controle territorial estratégico;
- diplomacia;
- pontos econômicos com trabalhadores, proprietários, células e distribuição entre atividades — `Forestry` continua um tipo produtivo próprio;
- census **legado** (bit de inicialização + linhas), que não se transforma em segunda população geradora de demanda;
- unidades militares.

Os inteiros usam big-endian; as coleções vêm em ordem canônica; contagens são verificadas antes de alocar arrays, com tamanho mínimo por linha. O checksum SHA-256 do arquivo protege contra corrupção comum. No carregamento, o `WorldState` é **reconstruído** através dos contratos existentes, o hash canônico é recalculado e deve ser idêntico ao hash gravado. Há um teste de re-encode byte a byte idêntico.

`WorldState.WithRestoredRevision(ulong)` apenas reconstitui a revisão lida no arquivo, passando pelas invariantes do construtor. Não é comando econômico ou autorização de turno.

O codec requer um `WorldGenerationResult` que reproduza a assinatura e a contagem corretas; o mundo físico gerado ainda precisa ser reconstituído a partir das entradas de geração. Não salva arquivos de física ou geografia fina.

## 2. Bundle único

`EconomicCompleteV8SaveBundleCodec` usa magic `GAFULL08`, versão 1, tamanhos explícitos, bytes completos de `WorldState`, bytes completos do sidecar G3-H e checksum do envelope.

`Decode(generated, bytes)`:
1. valida magic, versão, comprimentos e checksum;
2. carrega **somente v8** com o codec de mundo;
3. reidrata o sidecar com o mundo recém reconstruído, validando hash do mundo, capital natural, mercadorias e integridade semântica do compromisso;
4. compara re-encode byte a byte contra o arquivo original.

Assim, nenhuma reserva acompanha outro mundo, revisão ou contrato de hash.

`EconomicCompleteV8SaveBundleArchive` armazena bundles completos imutáveis sob `<SHA256 do checkpoint>.gafull`, com arquivo temporário, flush e renomeação no mesmo diretório. Refusa colisão de nome com bytes diferentes/corrompidos. Carrega um arquivo referenciado por **digest fornecido externamente**, não escolhe automaticamente qual save é o último.

## 3. Segurança / limites

- **Full snapshot ≠ commit autoritativo**: esses bytes contêm todo o estado v8 carregável, mas não provam que quem os criou possuía permissão. Somente o host/servidor pode manter o digest aceito em meio confiável.
- A combinação de checksum interno/externo e re-encode canônico é integridade, não assinatura digital. Um atacante pode fabricar arquivo auto-consistente se controlar todos os dados e não houver digest externo confiável.
- O archive `*.gafull` é **imutável e isolado**, mas não possui uma head transacional conjunta com o `EconomicEcologicalDurableCheckpointHost` do G3-I1. Não executar ambos como dois árbitros diferentes: uma integração futura deverá ter **uma única head** para save completo.
- Turnos e ordens comerciais ainda dependem do gate de produção ecológica de G3-G e não são efetivados no `PhasedTurnResolutionEngine`.
- O catálogo de recursos permanece candidato e não define receitas canônicas; produção primitiva continua sem materiais obrigatórios; trabalhadores geram oferta e demanda; sem créditos por compra remota não entregue.

## 4. Testes

`GlobalArena.Tests/G3I2FullV8SaveBundleTests.cs`:

1. round-trip completo com 2 civilizações, território, diplomacia, atividade florestal, força de trabalho, população legada, unidade militar, revisão 11, estoque natural e reservas;
2. census legado não inicializado continua não inicializado;
3. mundo de outra semente, corrupção e truncamento recusados;
4. formato desconhecido e arquivo rotulado como hash v9 explicitamente recusados;
5. checksum externo recalculado não mascara corrupção interna;
6. arquivo completo em disco, idempotência, corrupção recusada;
7. arquivo antigo válido continua identificável, mas não prova ser o mais novo sem digest confiável.

```sh
dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~G3I2FullV8SaveBundleTests
```

## 5. Gate para v9/v10 e migração

Para integrar PR #50 sem perda de instalações:

1. conciliar branches #49/#50 mediante autorização de merge separada, mantendo as identidades físicas e capacidades logísticas;
2. criar codec de **WorldState v9** explicitamente versionado, com todos os campos da seção 35 e testes de roundtrip/fixtures;
3. criar migrador **aprovado** de snapshot v8 + sidecar v1 para v9 ou posterior com política clara para dados ausentes. Sem fabricar instalações, população, estoque ou entregas;
4. selecionar um só formato autoritativo pós-v9 para reservas e capital natural e migrar seus hashes junto da física de worldgen;
5. publicar o save completo num único head transacional, com histórico e journal de eventos, integrando ao scheduler;
6. provar replay multiplataforma, falhas de disco e importação cruzada com vetores canônicos independentes.

**Sem merge.** G3-I2 é um avanço de save completo **v8** com fronteira de migração deliberadamente bloqueada, não a conclusão do save autoritativo pós-v9.

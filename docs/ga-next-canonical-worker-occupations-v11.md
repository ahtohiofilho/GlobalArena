# GA-Next — Occupação produtiva individual canônica (v11)

**16 novembro 2026** · **PR #372** sobre PR #371 · Draft sem merge.

## Motivação e fonte histórica

No cliente pré-GA-Next (GA-VS16), os marcadores representavam **grupos
de trabalho** por `Agriculture` (verde #61d98b), `Forestry` (marrom
#b88656), `Mining` (cinza #c4c7d4), usando discos coplanares
com a geometria tática. O PR #371 recuperou os discos e os seus
hit-tests, mas a fundação v10 ainda armazenava apenas
`PhysicalWorkerActivity` (Producing, Traveling, Military, Other)
e `CommodityId`; nenhuma dessas duas propriedades definia as três
atividades históricas.

**Escolha de arquitetura:** criar `PhysicalWorkerOccupation` no
registro de cada indivíduo, independente do **estado atual**. Um
trabalhador da mineração pode estar em deslocamento sem perder sua
ocupação, e uma unidade militar sem atividade produtiva tem o valor
`Unassigned`. Não reaproveitar `EconomicActivityKind` do legado,
pois sua enumeração atual (Agriculture/Mining/TradeLogistics) não
descreve a extração vegetal.

## Contrato de estado e migração

`PhysicalWorkerOccupation : uint`:

- `Unassigned = 0`: ausência *explícita* de profissão produtiva;
- `Agriculture = 1`;
- `Forestry = 2`;
- `Mining = 3`.

**Null não significa Unassigned**: null nos registros legados indica
schema v8/v9/v10, que não registrava profissão. A migração **explícita**
`WorldState.ClassifyTacticalWorkerOccupations` requer um mundo
ligado e censo v10 integral com posições físicas G(18,0) reais,
além de um par `PhysicalWorkerId → PhysicalWorkerOccupation`
para **cada** trabalhador existente. Falham migrações parciais,
duplicadas, tipos desconhecidos, `Unassigned` para quem estiver
produzindo e tentativa de v10→v11 implícita via `WithWorkers`.

Nenhum `WorkerId`, proprietário, tile tático, região de apresentação,
capacidade de trabalho, demanda, mercadoria ou estado do turno pode
mudar nesse processo. A revisão não se altera; o novo hash v11 muda
porque a profissão passa a ser um fato canônico.

`CanonicalWorldStateHasher` preserva a serialização **exata** dos
v8, v9 e v10. Somente no v11 acrescenta um `uint32` de ocupação
após os dados G(18,0) de cada indivíduo. O journal v10 de
deslocamento conserva agora as profissões de um censo v11 ao
executar cada evento; o mesmo executor e replay verificam a
integridade de ambos os formatos. O coletivo humano + IA
aceita hash 10 ou 11 sem downgrade durante um turno.

## Dados iniciais demonstrativos (NÃO worldgen final)

A antiga preview de seis trabalhadores continua disponível como v10
sem ocupações, para regressão. A nova preview do jogo atribui as
profissões de forma **explícita e apenas ilustrativa**:

| Trabalhador | Proprietário | Estado inicial | Ocupação v11 |
|---|---|---|---|
| #1 | Humano | Producing | Agriculture |
| #2 | Humano | Producing | Forestry |
| #3 | Humano | Military | Unassigned |
| #4 | IA | Traveling | Mining |
| #5 | IA | Producing | Agriculture |
| #6 | IA | Military | Unassigned |

A IA #4 retoma `Producing` no primeiro turno como no PR #369.
As ocupações não determinam, por enquanto, que `CommodityId 1`
ou `2` seja alimento, madeira ou pedra; tampouco derivam
produtividade do terreno. São dados de cenário, **não** fonte
de liquidação econômica. O jogo ainda precisa da política de
worldgen e do resolver comercial tático completo.

## Regras visuais — continuidade do PR #371

- Os discos estão estritamente no plano dos hexágonos, com
  câmera ortográfica inclinada e parâmetros VS16 mantidos.
- **v11:** preenchimento por profissão produtiva; `Unassigned`
  usa tonalidade neutra própria. Viajando: aro âmbar
  independente, preenchimento preservado.
- **v10:** mantém cores por estado operacional, sem tentar
  inferir profissão de mercadoria.
- Hover mostra número individual, profissão, estado, produto,
  tile G(18,0). Tab mostra profissão para o trabalhador
  selecionado. Clique seleciona ID individual; não emite
  ordem nem altera `WorldState`.
- Uma bolinha representa um trabalhador **real**; se houver
  três trabalhadores no mesmo tile, a apresentação fica
  no arranjo de três discos da VS16. Não colocar três
  bolinhas artificiais por tile nem inventar pessoas.
- O cenário inicial ocupa **dois produtores humanos**
  próximos à câmera inicial e o minerador permanece no
  lado da IA; para examinar todas as três cores, a
  CI desloca apenas o foco da câmera até o tile da IA.
  Esse foco não teleporta nem muda a posse da pessoa.

## Gates e próximos passos

- .NET Windows/Ubuntu: migração e validação, compatibilidade
  dos hashes, conservação de trabalhadores/produção/demanda,
  movimento fino e replay coletivo v11.
- Godot 4.7.2 Mono headless + Xvfb real: E2E v9, v10 e v11;
  clique/hover na câmera oblíqua, paleta VS16, dois turnos,
  mineral da IA, cinco capturas de framebuffer no novo modo.
- PowerShell: abridor auto PR372 fixado nos blobs exatos da CI,
  worktree isolada e sem importação manual.

**Fora do gate:** trabalho simultâneo em três setores no mesmo
tile, política definitiva de distribuição de trabalhadores
por terra/água, preços, produtividade agrícola/mineral/florestal,
liquidação de mercado, animação de viagem com duração e zoom
perspectivo detalhado por hover. Estes serão incrementos
futuros distintos, preservando o desempenho para milhares
de pessoas e de tiles.

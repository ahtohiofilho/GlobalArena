# GA-VS03 — Trabalho no tile tático; setores estratégicos somente de leitura

Data: 29/10/2026. Status: **implementação de referência interativa**.
Decisão solicitada: eliminar a mecânica obrigatória de "fundar cidade"
e a localização econômica fictícia no centro de um hexágono
estratégico.

## Regras de modelo adotadas

1. Uma unidade de trabalhador **produz e demanda consumo**.
   A localização econômica é `PhysicalTacticalTileId`,
   identidade global contínua do Goldberg refinado.
2. Não existe entidade de cidade, centro urbano, povoamento,
   pré-condição de fundação ou ação "criar cidade". Concentrações
   de trabalho e infraestrutura poderão ser interpretadas
   visualmente como cidades emergentes.
3. `m:<tileOrigem>:<tileDestino>:<trabalhoOrigem>:<trabalhoDestino>:<quantidade>`
   desloca trabalhadores **já existentes** para um tile físico
   adjacente, opcionalmente mudando sua atividade. Zero no tile
   anterior é simplesmente abandono; nenhum estoque de madeira
   ou pedra é cobrado para começar uma atividade primitiva.
4. O setor estratégico não tem população independente.
   `StrategicDisplayTotals` apenas soma as alocações táticas.
   Um tile físico atravessando a fronteira entre dois ou três
   hexágonos estratégicos é **uma identidade única**. Para
   evitar dupla contagem em uma visualização que exige um
   número inteiro por setor, usa-se o **menor ID incidente**
   somente como convenção explícita de bucket de apresentação.
   Isso NÃO confere propriedade, ocupação política, fronteira
   real, instalação ou residência estratégica.
5. `G(2,0)` (42 setores) e `G(12,0)` (1.442 tiles físicos)
   são apenas a escala **com suporte comprovado** no teste:
   refinamento Class I scale-six, com 1.242 tiles interiores,
   120 compartilhados entre setores e 80 incidentes a vértices
   estratégicos. Não atribuir esses valores ao tamanho final.
6. Produção agrícola/coleta de madeira/pedra e demanda
   alimentar são **projeções por tile físico**, com base na
   geografia fina e trabalhadores de cada atividade. O
   modelo de prévia não liquida mercado, não altera estoques,
   não resolve sobrevivência e não é um turno econômico completo.

## Limites e preservação de trabalho

`EconomicPhysicalTacticalWorkerSession` é um **estado de referência
em memória**, não uma migração dos saves e jogos anteriores. O
`WorldState` antigo e o `EconomicPointRuntimeState` continuam
a existir para compatibilidade, mas não são fonte de verdade
para a nova interface GA-VS03.

A cena `GlobalArena.Godot/main.tscn` passa a usar
`scripts/ga_vs03_main.gd`; a antiga cena GA-VS02 continua
acessível em `legacy_vs02.tscn` somente como teste de
regressão, não como regra de jogo recomendada. **Não** há
alteração do projeto Godot anterior em
`tools/GlobalArena.Godot` nem do projeto local no Windows.

O foco tático mostra somente tiles físicos da região e de
seus setores adjacentes (buffer para visualização), mas sua
identidade, localização e conexões pertencem ao **mesmo
grafo global fino**, não a mini mapas duplicados.

A escolha inicial no protótipo procura um tile de terra com
maior potencial agrícola dentre os que possuem ao menos
uma terra vizinha trabalhável; isso evita a localização
arbitrária na tundra da demonstração VS02. Não equivale
a afirmar equilíbrio alimentar ou que a seleção inicial
da civilização definitiva está resolvida.

## Execução no Windows

Atualizar uma cópia GA-VS02 limpa para a branch GA-VS03
em **outro worktree**, preservando os ajustes locais do
Godot 4.7 e o projeto original. Depois:

```powershell
dotnet build .\GlobalArena.Console\GlobalArena.Console.csproj -c Release
dotnet .\GlobalArena.Console\bin\Release\net10.0\GlobalArena.Console.dll tactical-bridge 0 1
```

Importar `GlobalArena.Godot/project.godot` no Godot
4.7.2.mono. Na visão estratégica, selecionar o setor
e clicar **Abrir malha tática do setor selecionado**.
Na visão tática, selecionar um tile ocupado como origem,
selecionar uma terra adjacente como destino, escolher
as atividades de saída/chegada, a quantidade e clicar
**Alocar trabalhadores no tile tático**.

A prévia econômica separa ofertas/demandas por tile,
não simula cidade; o indicador de pessoas no estratégico
é agregação de números táticos, não uma população
distribuída pelo setor. Ação inválida é recusada no motor
e não entra no histórico do visualizador.

## Qualidade/escopo do PR

- Testes .NET de conservação, movimento, trabalho, demanda,
  determinismo e tiles compartilhados 2/3 setores.
- Teste JSON via processo real: 1.442 identidades, 42
  resumos, ordens inválidas rejeitadas sem estado parcial.
- Smoke de cena Godot real (Linux headless) com cliques,
  alternância estratégico/tático e transferência 8=5+3;
  a ponte C# também roda nativamente no Windows da CI.
- Não declarar QA visual da versão Windows concluído sem
  rodar manualmente no Godot local.

## Próximo ROI

1. Inspecionar o modelo com clique real no Windows.
2. Integrar essas atividades por tile à interface
   Godot original, preservando zoom, bordas discretas
   de todos os territórios e sem enfraquecer vizinhos.
3. Promover a localização tática ao save/turno canônico
   de modo versionado, removendo a duplicação econômica
   estratégica legada com migração explícita.
4. Produzir/consumir por ciclo econômico, liquidar
   mercado, manter estoques físicos e calcular produção
   segundo o potencial físico real de cada tile; depois
   conectar transporte e combate ao mesmo grafo fino.

**Não abrir novas frentes de HA, PostgreSQL, custódia ou
"fundação de cidades" enquanto o loop offline não estiver
realmente funcionando.**

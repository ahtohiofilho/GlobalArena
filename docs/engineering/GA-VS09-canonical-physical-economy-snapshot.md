# GA-VS09 — Primeira publicação da economia física no WorldState canônico

Data: 29/10/2026. Próximo marco após **GA-VS08 aprovado visualmente no
Windows**, com contornos rentes à superfície e sem alterar topologia.
O GA-VS07 já comprovou no Windows 6 turnos/1 ciclo, consumo,
estoque local e reabertura de save.

## O problema de arquitetura

`CanonicalGameSession.WorldState` existente armazena:
- `EconomyRuntimeState.EconomicPoints` ligados a células
  **estratégicas**;
- `PopulationRuntimeState.PointPopulations` ligado aos
  pontos econômicos; e
- hash de estado versão **8** com testes conhecidos.

Mas a economia escolhida para o jogo usa um único conjunto de
**trabalhadores residentes/produtores/consumidores em tiles
táticos físicos globais**. Não inventar cidades, população
estratégica, ou dois censos independentes só para conectar o
protótipo.

## Incremento implementado

- `PhysicalEconomyRuntimeState` em `GlobalArena.Runtime`:
  estado **imutável** de trabalhadores, atividades e
  estoques por `PhysicalTacticalTileId`, com mundo
  canônico vinculado, G(3,0)→G(18,0), turno de movimento,
  revisão dos trabalhadores e número de ciclos econômicos.
- Recusa IDs de outra resolução, vínculos de mundo
  errados, trabalhadores com atividade desconhecida/zero,
  duplicação de atividade no mesmo tile, estoque repetido
  e inconsistências no ciclo de seis turnos.
- A soma de pessoas é calculada **apenas** a partir das
  alocações físicas; não há campo de residentes ou
  cidades redundantes. O estado de população estratégico
  legado fica não inicializado quando o domínio físico
  é autoritativo.
- `WorldState.WithPhysicalEconomy` incorpora a economia
  física **atomicamente**, preservando os demais domínios
  e sua revisão. É proibido combinar simultaneamente a
  autoridade física com pontos econômicos estratégicos
  preenchidos ou censo antigo inicializado.
- `CanonicalWorldStateHasher` conserva o hash **v8
  bit a bit** quando o campo físico é nulo. Se ele
  existe, usa **v9** e inclui todos os IDs dos tiles,
  suas atividades e pessoas, turnos, revisões, estoques
  e números de ciclo no digest canônico. Não depende
  de ordem de chegada das listas.
- `PhysicalEconomyWorldStatePublisher.Stage` confere
  assinatura do mundo gerado pelo jogo e da sessão
  econômica antes de criar uma revisão canônica única.
- Interface Godot original publica ao iniciar sessão,
  mover trabalhadores, avançar turnos e reabrir save.
  O painel indica a revisão do **WorldState físico**.
  Logs `GA-VS09-CANONICAL-PHYSICAL-PUBLISHED` incluem
  `hash_format=9`, quantidade de trabalhadores,
  entradas de estoque e digest.

## Limites importantes

**Esta mudança representa a primeira publicação canônica
dos dados econômicos físicos, não a migração integral
do resolvedor de turnos e do mecanismo de salvamento.**

O resolvedor físico de seis movimentos continua em
`EconomicPhysicalTacticalOfflineSession`, agora
publicando snapshots completos e imutáveis em
`CanonicalGameSession`. Próximos passos devem
substituir a execução de ações diretas no laboratório
por comandos/turnos mediados pelo núcleo, com
eventos registrados e commit da fase. O save JSON
`ga-vs07-neolithic-offline.json` ainda é o
formato de replay da sessão física, com re-publicação
canônica ao reabrir; ele NÃO é a versão integral
de save/load do WorldState v9.

O renderizador tático 01C experimental ainda tem
alguns biomas diferentes do mundo físico V1 onde
a economia é calculada. Isto deve ser conciliado
antes de apresentar o gameplay como produto final.

Não existem transporte mercantil, preços endógenos
liquidados, entrega de comida entre tiles, fome
populacional, movimento de pessoas por distância,
combate, IA, multiplayer ou vitória/derrota no
novo loop. **MKT-01 permanece bloqueado.**

Não há exigência de roçado, mina, serraria
ou outras instalações no Neolítico inicial:
agricultura e coleta arcaica surgem com
trabalhadores + potencial natural, conforme
GA-ADR-ECON-007. A matemática de investimento
físico e vagas para instalações avançadas
permanece futura.

## QA / evidência

CI Windows e Linux verificam:
1. O hash conhecido do `WorldState` sem o domínio físico
   permanece no formato v8 inalterado.
2. O estado físico publicado muda o hash para v9,
   com alteração de posições, emprego, estoques e tempo.
3. Fotos de estado/estruturas não mutam
   retroativamente quando o produtor avança.
4. Falha atômica para vínculo a outro mundo e
   estruturas de trabalhadores duplicadas.
5. Renderização Godot C# original compila com
   a nova publicação e os testes VS07 continuam verdes.

O teste visual Windows específico deste marco
verifica no painel `WorldState físico CANÔNICO`,
a revisão crescente e publicação no Output
(`hash_format=9`) ao executar 6 turnos e
restaurar save. Não mexer no VS08 já validado.

## Critério seguinte

Promover `EconomicPhysicalTacticalOfflineSession`
a fase de resolução canônica, com ordens
validadas e eventos, mantendo o prazo de seis
movimentos por ciclo. Só depois integrar o
mercado/estoque e logística física às
demandas e trocas endógenas de itens.

Sem merge na `main` ou alteração de GPP
devido a este incremento.

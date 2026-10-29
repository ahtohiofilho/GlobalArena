# GA-VS07 — Turnos econômicos rudimentares, estoques por tile e save offline

Data: 29/10/2026. **Primeira fatia de progressão econômica física
testável dentro da interface Godot original.** Não é ainda a
migração ao `CanonicalGameSession.WorldState` nem o loop completo
de comércio, transporte, combate ou fome.

## Por que este incremento

O laboratório VS05 demonstrou alocação de pessoas em `PhysicalTacticalTileId`,
mas a oferta/demanda eram prévias e toda ação era perdida ao encerrar.
A tentativa VS06 de exigir infraestrutura rudimentar foi revogada
(PR #93 fechado sem merge). Permanece a diretriz:
**agricultura e coleta arcaica de madeira/pedra começam diretamente
com trabalhadores em terreno apto, sem instalação obrigatória.**
Infraestrutura avançada, vagas, capital, insumos e equações de
produtividade continuam assuntos posteriores.

O `WorldState` legado mantém `EconomicPointRuntimeState` e
`PopulationRuntimeState` vinculados a pontos estratégicos.
Não é seguro publicar silenciosamente nossa malha física nele
sem atualizar o formato do hash, eventos e saves de todas as
partidas anteriores. VS07 estabelece um caminho de migração
auditável, **mas ainda isola a nova sessão** para evitar
dois estados alegadamente canônicos.

## Modelo de progressão

`EconomicPhysicalTacticalOfflineSession` compõe o
`EconomicPhysicalTacticalWorkerSession` existente, sem
alterar as regras validadas de movimento adjacente.

- Cada comando `AdvanceMovementTurn()` incrementa um
  relógio discreto. Não representa ainda duração de viagem
  nem resolução simultânea de ordens de múltiplos jogadores.
- A cada **seis turnos de movimento**, ocorre uma liquidação
  econômica rudimentar. Nos demais turnos, nenhum estoque
  ou consumo físico é alterado.
- A oferta física do tile, dos trabalhadores e do potencial
  do recurso é **adicionada ao estoque do próprio tile**.
  Não se transporta gratuitamente para um depósito global
  ou para a célula estratégica correspondente.
- A demanda alimentar local é atendida com alimento
  estocado **no mesmo tile**, depois da produção local do
  ciclo. `consumed = min(stock, demand)`. A falta
  alimentar `unmet = demand - consumed` é registrada
  explicitamente, sem estoque negativo.
- A falta de alimentos **ainda não reduz população**
  e não desencadeia fome, morte, emprego, remuneração,
  mercados ou preços. A demanda de 0,5 unidade bruta
  por trabalhador por ciclo e as produtividades geográficas
  são os parâmetros PROVISÓRIOS já existentes no VS05;
  não declaram economia equilibrada ou política alimentar
  definitiva.
- A ordem de liquidação é determinística (ofertas e
  demandas ordenadas). Arithmetic `checked` com preparação
  de estoques antes da mutação final impede liquidação
  parcial caso ocorra overflow.
- Transferir trabalhadores muda apenas sua localização
  e atividade. **Ainda não consome um turno de movimento**,
  nem implementa viagem, custo ou velocidade.

## Persistência

- `ExportSaveJson()` produz um envelope JSON versionado
  (`Version=1`), com payload e SHA-256.
- `RestoreSaveJson()` valida envelope, tamanho máximo,
  hash, versão e o histórico de ordens; recria um mundo
  determinístico pelo seed/Goldberg, reproduz movimentos
  e ticks e exige equivalência exata de trabalhadores,
  revisões, relógio, estoques físicos e ciclos. Dados
  adulterados ou inconsistentes são recusados.
- SHA-256 serve **para detectar corrupção acidental e
  inconsistência de replay**, não para autenticar contra
  adulteração maliciosa (não há chave privada/assinatura).
- Na HUD Godot original, o save é gravado em
  `user://ga-vs07-neolithic-offline.json`, substituindo
  o arquivo via temp no mesmo diretório. A restauração
  valida o estado *antes de substituir a sessão ativa*
  e exige a topologia G(3,0)→G(18,0).
- Salvar e reabrir esta sessão **não salva nem substitui**
  `CanonicalGameSession.WorldState` ou os antigos saves
  do jogo. É uma etapa de estabilização antes da migração
  definitiva versionada.

## UI manual Windows

Na visão tática plana da interface original, iniciar
laboratório neolítico e, se desejado, deslocar três
trabalhadores sem construir infraestrutura.
Depois, acionar **AVANÇAR TURNO DE MOVIMENTO
(ECONOMIA A CADA 6)** seis vezes. Observar contador
6/1, produção/consumo efetivos, falta alimentar e
estoques locais (alimento, pedra, madeira). O painel
também mostra que esses estoques **não são globais**.

Clicar **SALVAR PARTIDA OFFLINE**, fechar/reabrir o
laboratório e **REABRIR SAVE**. O contador, distribuição
dos trabalhadores e estoques devem permanecer idênticos.
Avançar até o turno 12 deve executar outro ciclo e
atualizar os estoques novamente.

## QA automatizado

- `GaVs07PhysicalTacticalOfflineLoopTests`: 6 testes,
  6º/12º turno, estoque local de madeira/pedra,
  consumo ≤ disponível, demanda não atendida explícita,
  divisão 8=5+3 por tiles sem transporte implícito,
  save/reload determinístico e continuidade até turno 24,
  rejeição de envelope adulterado e contador incoerente,
  ordens inválidas atômicas e identidades físicas incorretas.
- CI: build do projeto Godot original e testes no .NET 10
  Linux e Windows; regressões VS01/VS02/VS03/VS05.
- **Visual Windows Godot 4.7.2.mono ainda deve ser
  aprovado pelo proprietário**. Build green não substitui
  teste visual.

## Roadmap e marketing

Próximo passo: converter o domínio econômico físico para
`WorldState` canônico único com eventos/hashes/save
versionados (e sem população estratégica fictícia), depois
ligar transporte, trocas, mercado, IA, política temporal
de movimento, fome e combate mínimo. Resistir à expansão
de HA/finanças/infraestrutura desnecessária ao MVP.

**Marketing:** apenas MKT-00 leve até uma demonstração
realmente representativa do jogo. MKT-01 não é liberado
por esta etapa isolada.

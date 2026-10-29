# GA-VS10 — Autoridade canônica do laboratório físico e save V9

Data: 29/10/2026. Branch empilhada sobre GA-VS09/PR #97, sem
merge na main. VS08: gate visual dos contornos rentes à superfície
aprovado pelo proprietário no Windows/Intel UHD.

## Contrato de execução

A malha do Godot original G(3,0)→G(18,0) continua inalterada.
O laboratório passa a ter **uma única fonte de verdade executável**:
\`CanonicalGameSession.Snapshot.PhysicalEconomy\`. Não são mantidos
\`EconomicPhysicalTacticalOfflineSession\` nem
\`EconomicPhysicalTacticalWorkerSession\` mutáveis durante a partida.

- A instalação inicial, em um novo mundo, reutiliza a distribuição
  determinística de oito trabalhadores do VS05, sem infraestrutura
  arcaica obrigatória e sem construir pontos estratégicos/cidades.
- Uma transferência adjacente faz leitura do snapshot canônico,
  valida identidade e terreno, preserva quantidade total e publica
  novo \`WorldState\` com revisão incrementada. Movimento ainda é
  instantâneo e não consome tick (política temporal futura).
- Cada \`AdvanceMovementTurn()\` publica nova revisão. Somente
  os ticks 6, 12, 18... liquidam produção física rudimentar de
  alimento/madeira/pedra e consumo alimentar por tile. Sem transporte
  gratuito, estoque global ou redistribuição implícita.
- Falta alimentar é contabilizada sem redução populacional automática.
  As taxas são provisórias do VS05/VS07, não uma economia definitiva.
- Cada evento carrega revisão e hash SHA256 V9 anterior/posterior.
  Eventos são um **log verificável de auditoria/replay**, não um
  segundo objeto de trabalhadores ou estoques. O WorldState é a
  autoridade de consulta e commit.

## Save, importação e compatibilidade

O save \`user://ga-vs10-canonical-physical.json\` é V9, com
envelope SHA256, seed, versão de worldgen, parâmetros de malha,
assinatura do mundo, estado canônico final, eventos e hash de cada
revisão. O carregamento recria mundo e ordens determinísticamente
e compara toda a representação reconstituída, rejeitando
inconsistências antes de substituir a sessão da interface.

A importação explícita do antigo VS07 só ocorre caso não exista
save V9 no caminho novo. Primeiro verifica-se o save legado e seu
replay, depois traduzem-se as ações para eventos V9 e comprovam-se
trabalhadores, ciclos e estoques. **O arquivo VS07 original não é
reescrito**. O usuário deve salvar em V9 para persistir a migração.

SHA256 do payload é detecção de inconsistência, **não assinatura
antifraude**. Ainda não existe protocolo de rede, transações entre
participantes, bloqueio simultâneo de comandos ou rollback de
partidas remotas.

## QA e limites

- CI Linux e Windows: VS07, VS09, VS10, hash V8/V9, save/restauração,
  economia de 6 turnos, conservação e comparação diferencial com
  a liquidação VS07.
- Godot original: build nos dois sistemas. Gate visual Windows
  continua necessário para aprovar a interface VS10, especialmente
  seleção, movimento, HUD, reabertura e contornos.
- Não alterar terreno, zoom, câmera, bordas, layout ou WorldGen.
- Mercados, preços, transporte, energia, guerra, fome e políticas
  de turnos simultâneos permanecem fora desta fatia.
- MKT-00 leve; MKT-01 bloqueado até existir demonstração do jogo
  com alcance representativo. Sem merge na main neste incremento.

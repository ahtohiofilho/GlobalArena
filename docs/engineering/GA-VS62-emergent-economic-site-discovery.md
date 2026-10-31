# GA-VS62 — Descoberta de pontos econômicos emergentes

Base: PR VS61 #168, commit `2a4df7bdb4992c18201b7507b204393cec155ce4`.
Branch empilhada, sem merge na `main`, SETTLE real bloqueado.

## Contrato

`session.DiscoverPrimitiveEconomicSites(policy)` observa apenas os
povoamentos físicos com trabalhadores residentes na sessão canônica. Um
povoamento não é entidade criada, cidade fundada, guarnição ou Estado:
é um **tile habitado**. Cada tile possui a identidade econômica estável
`EconomicPointId(tile.FineStrategicCellId.Value)`, **escopada ao mundo
e à topologia física**; nenhuma sequência de IDs ou registro extra é
persistida. IDs não são globalmente exclusivos entre mundos.

A consulta fornece:

- Sites ordenados com tile, ID, moradores/trabalhadores,
  distribuição agrícola/florestal/mineradora e oferta/demanda locais.
- Fluxos agregados da região habitada, usando exatamente o kernel
  `CanonicalLivePrimitiveTradeProjector.BuildFlows` extraído da VS61.
- Anchors determinísticos: um comprador por tile habitado e produtores
  somente em tiles com oferta produtiva positiva. Não duplica
  residentes para gerar poder de compra.
- Total de trabalhadores residentes e em trânsito, hash físico,
  revisão da rede e revisão de trabalhadores. `RequireCurrent`
  invalida por mudanças na sessão física ou na autoridade de rede.
- `ChangesSince(previous)` retorna sites adicionados, removidos e
  alterados, com comparação exata de quantidades, por identidade
  física, na mesma sessão e sob a mesma política. Serve para UI
  e sincronização incremental; **não** altera o mundo nem guarda
  índices permanentes ou executa um subsistema assíncrono.

A descoberta de sites e o cálculo de oferta/demanda são **somente leitura**.
Alocações e produtividade determinam a capacidade produtiva *por ciclo*,
não estoque ou expedição já agendada. O trabalhador em viagem é excluído
do mercado residencial do destino até sua chegada; a saída e a chegada
produzem os deltas correspondentes. Taxas basais de consumo de cada item
são explícitas no parâmetro versionável da aplicação; o motor ainda não
determina preferências nem preços finais endogenamente.

## Adaptação à VS61 / VS60

`discovery.PreviewTrade(...)` monta compradores e produtores a partir
dos IDs efetivamente encontrados, sem aceitar coleções manuais de
povoamentos. Ele inclui no cenário somente os **endpoints envolvidos
nas lanes explicitamente fornecidas** e chama integralmente VS61 →
VS60 → VS59 → VS58 → VS57/VS52. Inputs restantes continuam
obrigatórios: catálogo, preços, H/poder de compra abstrato,
política/calibração, serviços físicos construídos, direitos da sessão,
frete, quantum e limite finito.

Por desenho, a VS62 **não descobre a malha de serviços comerciais**,
não inventa rotas, não efetua despesas, reservas ou liquidação, e não
converte caixa V14 em poder de compra abstrato. Identidades de serviços
históricos com IDs arbitrários não são remapeadas silenciosamente:
os novos serviços precisam vincular explicitamente IDs de endpoints
já descobertos. Este é o limite que antecede a descoberta de rotas.

## Garantias e QA

- Identidade de tile não muda se a atividade mudar; sites são
  acrescentados só depois da chegada física; tiles sem residentes
  deixam de ser reportados sem fundar/dissolver cidades.
- Save/restore produz os mesmos IDs e fluxos a partir dos mesmos
  trabalhadores, mas previews e snapshots de outra sessão não
  mantêm autoridade.
- Mudanças de direitos/rede invalidam snapshots anteriores.
- Deltas de políticas de consumo diferentes são rejeitados.
- Mesmo caso sem modificação retorna delta vazio e não altera save.
- Teste integral reidentifica uma rota **realmente construída** com
  IDs derivados dos tiles, concede direito explícito, seleciona e
  audita sem fazer SETTLE.
- Benchmark `--vs62-discover-sites`: 8/32/128 consultas e deltas
  idempotentes para **dois** sites (não 128 cidades);
  execução em Linux e Windows com testes e builds Godot.
- Gate PR VS62: Linux VS40–VS62 + benchmark + Godot Release;
  Windows testes integrais + benchmark + Godot Debug.

## Próximos passos (VS63, não incluídos)

Explorar a descoberta de *ofertas elegíveis* de serviço entre sites
observados, com caminhos e direitos construídos já existentes,
sem confundir mobilidade geográfica com criação automática de
transporte comercial. Medir escalabilidade e evitar enumeração
quadrática de pares de sites.

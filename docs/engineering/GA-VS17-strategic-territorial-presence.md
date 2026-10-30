# GA-VS17 — Presença territorial estratégica derivada da população física

Data: 30/10/2026. Base: VS16/PR #113. Gate incremental de jogabilidade, sem alterar main.

## Distinção conceitual obrigatória
O laboratório físico V13 nasce com 8 trabalhadores, **zero civilizações registradas** e **zero territórios politicamente controlados**. Colocar trabalhadores num tile não equivale, por si só, a ocupar militarmente, anexar, fundar um Estado ou conquistar o território. A política de conquista necessita de modelo separado de fundação, conhecimento, guarnição e conflito; essas regras **não são inventadas** nesta entrega.

## O que o jogador passa a observar
- O globo estratégico selecionado mostra **presença populacional física por território**, diferenciando trabalhadores produtivos, em deslocamento e quantidade de tiles físicos habitados. Apresenta em linha própria a **soberania política**, ainda indefinida no laboratório.
- O inspetor de cada tile tático exibe presença demográfica nos 1–3 territórios estratégicos incididos pelo tile. A presença muda em consequência das ordens V13: saída para trânsito, retorno à produção e chegada ao novo tile.
- Não há nova unidade econômica, conta de dinheiro, fronteira política ou ordem de conquista.

## Projeção sem duplicação
Usar exclusivamente o mapa de incidência oficial **G(3,0) → G(18,0)**, que já fornece para cada tile físico um conjunto validado de 1, 2 ou 3 células estratégicas. Cada trabalhador físico corresponde a **6 sextos de trabalhador-equivalente**:
- Tile interior a um território: 6 sextos atribuídos ao único território.
- Tile em aresta entre dois territórios: 3 sextos atribuídos a cada um.
- Tile sobre junção de três territórios: 2 sextos atribuídos a cada um.

Esse é um **indicador abstrato de presença territorial**, não uma divisão real das pessoas nem uma definição de propriedade. O somatório global dos indicadores deve ser exatamente `6 × trabalhadores físicos totais`.

A população em trânsito mantém presença vinculada ao **tile de partida** até a chegada. Na chegada, passa ao destino; ninguém surge antes, e a presença militar/política não é atribuída automaticamente.

## Persistência e integridade
`StrategicTerritorialPresenceProjection` é uma projeção pura do `PhysicalTacticalEconomyRuntimeState` atual e da incidência oficial. Não muda `WorldState.Territory`, `WorldState.Civilizations`, `WorldState.PhysicalEconomy`, eventos, dinheiro, save ou hash. **Save e hash permanecem V13**. Reabrir um V13 recalcula exatamente a mesma presença para todos os territórios.

## Critérios de QA
- Primeiro cenário: oito trabalhadores e total de **48 sextos** entre todos os territórios, sem qualquer soberania política.
- Casos de fronteiras 1/2/3: equivalentes divididos sem dupla contagem.
- Durante ordem de 2 trabalhadores: 12 sextos em trânsito, ainda vinculados à origem; após 2 turnos, presença ativa no destino e zero em trânsito.
- Save V13 em trânsito → restore → projeção de cada território idêntica, revisão/hash idênticos; chegada reproduzível.
- Interface: selecionar território no globo, observar a presença e a mensagem de soberania independente; abrir tático, inspecionar tile e ler a distribuição por incidência. Ausência de população não deve aparecer como conquista nem falha de geração do mundo.
- Testes + build Godot Windows/Linux, regressões V13 e visual Windows/Intel UHD antes de promover.

## Limites e dependências
Este marco não define civilizações iniciais, fundação de colônias, influência política, guerras, ocupação militar ou disputa de fronteiras. Não usar presença ponderada para marcar conquista. O próximo slice político precisará de critérios explícitos, evitando confundir povoamento com controle. Demanda basal (#103), poder de compra endógeno (#111) e contabilidade privada adicional continuam congelados.

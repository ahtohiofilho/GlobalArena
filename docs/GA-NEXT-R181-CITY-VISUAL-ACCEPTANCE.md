# GA-Next R181 — gate visual da janela da cidade

## Escopo

Validação exclusivamente da estrutura R180 já aprovada, com **cena Godot 4.7.2 efetivamente renderizada**. Nenhuma mudança na economia, no worldgen ou nos símbolos cartográficos das cidades.

## Critérios de aceitação

- Abrir uma cidade existente e **em foco** no mapa cartográfico G18, usando o mesmo fluxo de seleção do jogo.
- A janela aparecer sobre o mapa, com abas **Visão geral** e **Ligações**; sem alterar o modo mapa nem introduzir uma rota comercial de múltiplas arestas.
- A aba Ligações apresentar exatamente as arestas incidentes ao nó, sem inserir fluxos ou mercados artificiais.
- O detalhe de uma ligação expor o nó vizinho e os números do estado físico autoritativo.
- Comparar hash do host antes e depois das consultas: nenhuma alteração do estado do jogo.
- Guardar **três PNGs do framebuffer real** (visão geral, ligações e detalhamento) com evidências do CI, para avaliar legibilidade e composição. O teste rejeita janela fora da árvore visual ou fora do viewport.

## Limites e diretrizes

O gate visual automatizado comprova que os elementos aparecem, mas a homologação estética depende de conferir os PNGs. O objetivo continua mapa central + janela da cidade com abas + detalhamentos somente quando necessários.

**Não restaurar as três bolinhas.** A representação cartográfica canônica usa um símbolo único em estilo de mapa antigo, diferenciado pelo porte do assentamento; esta PR não substitui os marcadores em uso.

Windows/Linux obrigatórios, macOS facultativo. PR empilhada sobre a R180, Draft, sem merge.

## Ajustes após inspeção dos PNGs

O texto da Visão geral é alinhado ao topo, evitando espaço vazio excessivo. O atalho da cidade passa a ser **Y**, como no esboço canônico; a tecla **C** fica reservada à futura janela da civilização. Nenhuma nova entidade de UI ou regra econômica foi introduzida.

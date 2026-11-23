# GA-Next R180 — Cidade, abas e ligações físicas imediatas

## Decisão de arquitetura visual
- O mapa permanece a tela principal, com ou sem coluna lateral (TAB); as telas de civilização e cidade são janelas próprias com abas.
- A janela da cidade é acessível por **C | CIDADE**. O primeiro incremento tem duas abas: **Visão geral** e **Ligações**. Não adicionamos dezenas de painéis especulativos.
- A aba Ligações mostra **somente as arestas diretamente incidentes ao nó físico selecionado**, não caminhos arbitrários nem objetos de rota comercial persistente.
- Selecionar uma ligação revela um detalhamento local simples (nó vizinho, frete físico unitário, tarifa, investimento e titular). Uma janela histórica extraordinária, gráficos e agregações por conjuntos de arestas ficam fora deste escopo, sujeitos a ROI.
- O nome amigável **Ligação** designa a aresta, mantendo IDs físicos e precisão no kernel.
- A UI atual usa a identidade do nó cartográfico selecionado enquanto não existe uma nomenclatura definitiva para cidades. Não inventa população, nome, porte ou fluxos.
- A camada C# `FoundationLocalConnectionsProjector` só lê `WorldState.Transport.Edges` e `Incidence.FineTopology.Edges`, com verificações de assinatura e vizinhança; os custos físicos são derivados pela função autoritativa existente, **sem congestionamento**.

## Guardrails
- Sem novos algoritmos de rota, agregação econômica, caches de infraestruturas, estoques ou cálculo de mercado.
- Não restaurar o símbolo regressivo de **três bolinhas** para cidades. O marcador canônico segue a linguagem de cartografia antiga com variação de porte; este incremento não o modifica.
- A janela não altera o `WorldState`, não cria ações de investimento fictícias e não obriga a existência de rotas longas.
- O modo normal de jogo continua map-first, sem coluna quando TAB está fechado.
- Windows e Linux obrigatórios; macOS opt-in. PR empilhada em R179, Draft e sem merge.

## Validação
Teste do projetor exige exatamente as arestas incidentes e compara cada valor com o estado físico. Compilação do Godot em CI e smoke histórico de cena são necessários antes de considerar o incremento tecnicamente aprovado. A inspeção estética em cartografia interativa continua como gate posterior.

# GA-NEXT-TRADE-OVERLAY-001 — fluxo do último ciclo econômico por aresta física

**17/11/2026 · especificação para implementação futura · Draft · sem alteração visual ou de simulação nesta entrega**

## 1. Objetivo e decisão de produto

No Global Arena, a distribuição espacial do comércio deve ser legível diretamente sobre o mapa. A visualização preferencial é uma **camada de comércio opcional** (*Trade / Transport overlay*) na qual:

- **Espessura da linha = volume efetivamente transportado naquela aresta durante o último ciclo econômico concluído**, agregado entre todas as mercadorias, produtores, consumidores e civilizações, sem cancelamento entre direções opostas.
- **Cor da linha = titular autoritativo da infraestrutura daquela aresta na data daquele ciclo**, segundo a identidade civilizacional já usada no cliente (vermelho, azul, verde na demonstração).
- A aresta sem infraestrutura titulada aparece com indicação **neutra**; identidade desconhecida/oculta não deve ser confundida com aresta livre ou sem titular.
- A visualização é um **resultado retrospectivo confirmado**, não previsão, rota potencial, capacidade instalada, ordens pendentes, demanda insatisfeita, preço, receita, número de caravanas ou fluxo em tempo real.

Essa é a primeira versão funcional. Congestionamento, composição comercial e comparação com infraestrutura são dimensões posteriores, não mensagens extras codificadas na cor ou na espessura agora. O mapa habitual permanece despoluído por padrão.

## 2. Fronteira autoritativa: dados que já existem e que ainda faltam

### Verificado no código empilhado até o PR #384

1. `GlobalArena.Simulation/Foundation/PhysicalWorkerTradeCycle.cs` possui **um laboratório independente**: `FoundationTurn.EdgeLoads` (volume por identificador de aresta **local do laboratório**, em `double`), `FoundationTurn.Trades` com `EdgeIds`, `FoundationEdge.OwnerCivilization` e `ExternalTariff`. Ele explicitamente **não é o pipeline autoritativo `WorldState`**, nem registra liquidação financeira.
2. `FoundationTacticalHumanAiTurnHost` já publica turnos físicos individuais e replay v11. Os PRs #383 e #384 projetam **insumos** de oferta e demanda por trabalhadores, ocupações e mercadorias; **não publicam o trajeto efetivamente escolhido nem carga por aresta do último ciclo econômico**.
3. A malha fina canônica G(18,0) tem tiles `PhysicalTacticalTileId`, topologia bidirecional em `PhysicalTacticalIncidenceMap.FineTopology` e a identidade qualificada `PhysicalTacticalEdgeId`. Esta identidade significa **fronteira geométrica compartilhada atravessada** entre dois tiles adjacentes, não uma estrada desenhada sobre a linha de fronteira.
4. O contrato da plataforma determina que o **C# publica uma visão confirmada e autorizada** e o **Godot só a desenha**. O jogador aprovou a UI atual como funcional/provisória; não há autorização para novo redesign de dock ou menus.

**Consequência:** é incorreto colorir as arestas reais do G(18,0) usando `FoundationTurn.EdgeLoads` ou `FoundationEdge.OwnerCivilization` sem uma conversão de identidade e origem autoritativa validada. Também é incorreto preencher a camada com rotas mais curtas calculadas no renderizador, vendas potenciais ou valores inventados.

## 3. Contrato de leitura pretendido (ainda NÃO implementado)

Uma porta somente leitura do núcleo, obtida **após** fechamento do ciclo econômico, deverá entregar um objeto equivalente ao modelo conceitual abaixo; nomes exatos de tipos são propostos, não classes já criadas:

```text
LastClosedEconomicTransportView
  Availability           = NoClosedCycle | Ready | AuthorizedSubset
  WorldBinding           = identidade do mundo/seed/topologia
  FineTopology           = GoldbergParameters canônicos
  EconomicCycleOrdinal   = identidade estável do ciclo econômico liquidado
  PublishedRevision      = revisão que confirmou o ciclo
  PublishedHash          = hash/referência verificável da publicação
  CargoUnit              = unidade física/carga logística COMPARÁVEL entre itens
  Edges[]                 = ordenação crescente por PhysicalTacticalEdgeId
    EdgeId                = PhysicalTacticalEdgeId qualificado no mesmo mundo
    TotalCargoMicro       = carga agregada do ciclo, inteira, >= 0
    InfrastructureOwner   = Known(CivilizationId) | NoOwner | NotVisible
    HasInfrastructure     = fato autoritativo, não inferido pela linha
    CommodityVolumes?     = opcional, somente se emitido pelo núcleo
    Congestion?           = opcional, não codificado na primeira UI
    TariffRevenue?        = opcional, somente com liquidação financeira real
```

**Controles do contrato:**

- `NoClosedCycle` não é um ciclo fechado com volume zero. A UI deve mostrar uma indicação textual de **“No completed economic cycle yet”** e não desenhar nenhuma rota, até haver resultado.
- `Ready` com lista vazia significa **ciclo confirmado sem transporte**. Não desenhar linhas nem sugerir erro.
- `AuthorizedSubset` representa dados efetivamente permitidos à civilização; o cliente não calcula/recebe em segredo volumes, rotas ou titulares de fog-of-war. **Ausência de dado não é zero**.
- O titular e o volume ficam **congelados no mesmo snapshot econômico histórico**. Se um dono muda depois do ciclo, a linha desse ciclo mantém a cor histórica; um painel futuro pode exibir o titular atual em campo separado, explicitamente rotulado.
- `InfrastructureOwner` significa proprietário/titular registrado da infraestrutura da **aresta**, **não** proprietário do tile, produtor do bem, consumidor, civilização que pagou tarifa, maior investidor, transportador ou dono da rota inteira. Se ainda não houver modelo autoritativo de titularidade, usar estado **não disponível** e não pintar cores de civilização por suposição.
- Um identificador de aresta do laboratório `FoundationEdge.Id` não é `PhysicalTacticalEdgeId`; a versão aplicada ao mundo deve usar a identidade qualificada da topologia correta. Rejeitar mundo/topologia incompatíveis.
- Não atribuir `TariffRevenue = 0` quando o sistema não a liquidou: usar ausência explicitamente tipada.
- Não serializar `double` de laboratório como se fosse volume de carga autoritativo. O C# define a **unidade logística comum de carga** antes de somar produtos distintos; se ainda não houver unidade comparável, bloquear a leitura `All commodities` ou oferecer apenas filtros unitários semanticamente válidos. Não combinar toneladas, unidades, valor monetário ou trabalhadores por soma arbitrária.
- Fluxos negativos, quantidade `NaN`, IDs inválidos, duplicados na mesma lista final, titular inválido, ciclo futuro, revisão incorreta ou excedente numérico devem falhar na publicação do read model.

## 4. Agregação do volume (no núcleo, não no Godot)

Para cada transação/entrega comercial **efetivamente atribuída no ciclo**, percorrer os trechos do seu itinerário **físico e autoritativo**. A carga total de uma aresta é:

```text
V(edge, cycle) = Σ [CargoMicro(trade) × Traversals(trade, edge)]
                 em TODAS as transações confirmadas do ciclo
```

Na primeira versão, os trajetos físicos simples deverão atravessar cada aresta no máximo uma vez; se futuramente trajetos multiestágio puderem repetir uma aresta, contabilizar as passagens reais e explicitar esse critério, nunca duplicar a mesma transação por erro de join. O uso em sentidos opostos **soma** o volume (não se anula).

Invariantes de auditoria independentes:

1. `EdgeLoads[e]` = soma das cargas reais dos trechos autorizados que cruzaram `e` naquele ciclo.
2. `Σ_e EdgeLoads[e]` = `Σ_trade CargoMicro(trade) × TraversalCount(trade)`. **Não** confundir com produção total: uma carga que cruza quatro arestas conta quatro vezes no volume **aresta–ciclo**.
3. Uma venda local que não cruza arestas **continua sendo venda** mas não gera linha no mapa de transporte.
4. `Σ_trade SoldAmount` = produção vendida (quando a garantia de venda integral for implementada/verificada) é uma auditoria comercial **separada**; a camada visual não certifica essa propriedade por si só.
5. O mesmo itinerário não é materializado várias vezes ao recalcular preço, market clearing ou rotas candidatas durante a busca. Somente a **solução final congelada** conta.
6. Um volume zero não mostra linha; nenhum volume é inferido da capacidade, congestionamento futuro, largura da estrada ou nível de investimento.

## 5. Geometria e apresentação no Godot

- **Aresta física desenhada como corredor entre os CENTROS dos dois tiles adjacentes**, cruzando sua fronteira compartilhada. Não traçar a linha *ao longo* da fronteira poligonal (que representa separação de tiles, não eixo de transporte).
- A projeção 3D/superfície deve seguir a esfera e o relevo já apresentado, com offset visual pequeno para evitar z-fighting; não planarizar o mundo, não reutilizar as bordas estratégicas ocultas e não criar novas identidades geométricas para comércio.
- **Uma primitiva por aresta visível com volume positivo**, não uma curva por par origem–destino. A UI recebe dados já agregados e estáveis, sem construir rotas, preços ou propriedade.
- **Espessura monotônica e suavizada, em pixels de tela**: como ponto inicial de calibração *apenas visual*, usar `w = 1.5 px + 5.5 px × sqrt(min(V / P95, 1))` para `V > 0`, onde `P95` é o percentil 95 dos volumes positivos **visíveis e autorizados no snapshot completo**, calculado uma vez por ciclo/filtro, não de novo a cada movimento da câmera. `P95 > 0`; para `V = 0`, não desenhar. Valores acima do P95 saturam em 7 px e continuam disponíveis numericamente ao selecionar. Os parâmetros finais de espessura são uma hipótese de UX, não regra econômica.
- **Cor** usa a identidade civilizacional autoritativa (provisoriamente primárias RGB). `NoOwner` recebe cor neutra discreta; `NotVisible` não revela titular (pode omitir traço ou exibir a marca de informação restrita se autorizada). Proibir reuso da cor de profissão do trabalhador como se fosse dono da infraestrutura.
- Toggle **desligado por padrão**: entrada `Trade` no menu de camadas de mapa; tecla `T` é uma candidata a atalho, sujeita à verificação de conflito antes de implementação. Ligar/desligar não avança turno nem altera `WorldState`. Ao iniciar novo turno, manter o snapshot do **último ciclo econômico fechado** até existir outro. Não limpar/recalcular em todos os turnos de trabalhadores.
- Para mapas grandes, gerar buffers/meshes agrupados por classe de cor/largura ou solução equivalente, com *frustum/viewport culling* e nível de detalhe; não criar um `Node3D` por transação nem reconstruir milhares de linhas por frame. Pan, zoom, abertura do dock e modo M não mudam os dados.
- O mapa tático é a primeira superfície-alvo. Se no futuro houver visualização estratégica, **agregar de maneira conservativa com escala definida**; não duplicar carga na costa nem multiplicar volume porque uma borda estratégica representa várias arestas táticas.

## 6. Interação e painel

- Primeiro MVP: ativar/desativar camada; selecionar **por clique** uma aresta desenhada para inspecionar. Não reintroduzir hierarquias de hover que impeçam manter aberto um painel.
- Detalhe sempre identifica **último ciclo fechado**, volume total e titular histórico com identidade/estado explícito; opcionalmente conteúdo por mercadoria se o núcleo o publicou.
- `Capacidade`, `congestionamento`, `tarifa cobrada` e `receita tarifária` ficam **indisponíveis** até serem computados e registrados; não mostrar zeros fictícios.
- A inspeção não substitui o clique no tile habitado, não bloqueia drag/drop dos trabalhadores nem rouba os atalhos `M/TAB/Esc/E/G/P/X/A/V/N`. A coluna operacional continua limitada à altura da viewport e com rolagem completa (PR #382).
- Legenda mínima: **“Width: transported volume · Color: infrastructure owner · Source: last completed economic cycle”** e identificação de `NoOwner/NotVisible` quando aplicável. Uma opção posterior poderá alternar camada **Infrastructure** (capacidade instalada) distinta da camada **Trade** (uso efetivo); não confundir as duas.

## 7. Roadmap técnico de implementação — PRs menores e verificáveis

### Gate 01 — publicar o resultado econômico autoritativo por aresta

**Dependência anterior:** mercado real do GA-Next, usando dados dos PRs #383 e #384, deve resolver itinerários físicos e registrar carga real por aresta de topologia qualificada **na liquidação do ciclo**, junto com sua unidade logística e titularidade histórica. Sem isso a camada permanece não disponível. A publicação precisa vir de uma projeção de leitura do host, não de cálculo de UI nem do resultado `double` do laboratório.

**Testes:** fechamento vs ordens pendentes, identidade do mundo, titular histórico, auditoria por aresta, bidirecional, venda local sem tráfego, ausência sem demanda, sem compra/rota fictícia, replay/hash do ciclo e isolamento de civ não autorizada.

### Gate 02 — projetor de leitura e normalização visual

Projetar os records finais em estrutura de lote imutável, ordenação por `PhysicalTacticalEdgeId`, filtro explícito de visibilidade e cálculo da escala visual sem mutação do ciclo. Separar o read model numérico do código de desenho; reusar o buffer quando a câmera mudar.

**Testes:** carga crescente nunca encolhe linha, mesmo ciclo produz mesma geometria após pan/zoom, estabilidade do P95 entre mudanças de câmera, desconhecido não colore como neutro/terceiro, zero e ausência não se confundem, cores RGB coerentes, controle de saturação.

### Gate 03 — camada de cliente Godot e homologação Windows

Godot 4.7.2 Mono: primeiro overlay no mapa tático, toggle separado, espessura proporcional e clique por aresta, sem alterações em ordens de trabalhadores. Capturas reais em Xvfb sob zoom e dock aberto/fechado, teste de rolagem e bordas/centros, benchmark com muitos milhares de arestas e homologação visual interativa Windows. Integrar o cabeçalho/legenda ao layout atual sem redesenho artístico prematuro.

## 8. Critérios de aceite de produto

1. Com ciclo fechado contendo transporte, aparecem **somente** ligações físicas efetivamente utilizadas, com linhas maiores para volume maior (respeitando teto visual) e cor do titular da infraestrutura no instante do fechamento.
2. Com ciclo inexistente, a camada declara **não haver resultado**; com ciclo fechado sem transporte, declara **0 ligações utilizadas**, sem criar linha artificial.
3. Uma aresta com uso por várias civilizações/mercadorias aparece **uma única vez** com volume somado e **uma única cor de titular**. Comércio nos dois sentidos soma na mesma aresta.
4. Alterar o titular ou as ordens **após** o ciclo não reescreve a fotografia histórica; apenas novo ciclo liquidado substitui a camada.
5. Arestas finas permanecem alinhadas aos centros dos tiles sobre a esfera, não aos contornos do território. Mudança de câmera não altera volume nem titular.
6. Exibir/ocultar, selecionar aresta ou abrir painel não gera evento econômico, não muda hash, não move trabalhadores e não expõe informação que o cliente não pode ver.
7. O custo de renderização é monitorado em mapas com milhares de tiles e arestas, sem um objeto visual por entrega comercial.

## 9. Estado deste PR

**Somente especificação e decisão arquitetural.** Nenhum fluxo de transporte do último ciclo, titular histórico, botão/atalho de comércio, modelo de publicação ou overlay Godot foi implementado aqui. A experiência visual aprovada provisoriamente nos PRs #379 e #382 permanece intacta. Nenhum merge automático; os gates de programação seguintes exigem PRs, testes e homologação separados.

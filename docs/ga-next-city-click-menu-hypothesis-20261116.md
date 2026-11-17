# GA-Next — UI experimental: menu da cidade somente por clique

**16 de novembro de 2026 · PR #377 sobre #376 · Draft, sem merge**

## Feedback e decisão

Na homologação do PR #376, o usuário identificou o problema central:
**o mouse não consegue manter a janela aberta** ao atravessar o espaço
entre o tile, a lista de atividades e o trabalhador. A diretriz
aprovada para o estágio atual é **suspender completamente a interação
por hover** e trabalhar com cliques explícitos.

Isso é uma **hipótese de UX reversível** e **não** define um objeto
`City` ou uma nova regra econômica. A palavra "cidade" segue como
atalho de linguagem para um tile G(18,0) habitado.

## Contrato click-only

- **Movimento do cursor:** não abre nenhum menu, não expande nem fecha
  listas, e não mostra o antigo inspetor transitório ao passar nas
  bolinhas. Não há callback `MouseEntered` ou `MouseExited` no menu
  de atividades.
- **Clique em um tile habitado:** abre um cartão persistente com
  a população real e as três atividades — agricultura, extração
  vegetal e mineração — com contagens derivadas do censo v11.
  Clicar novamente no mesmo tile mantém o mesmo cartão. Se houver
  várias civilizações no tile, a apresentação escolhe primeiro a
  civilização humana quando existir, e senão a primeira detentora
  por ID, sem conceder direito de comandar gente da IA.
- **Clique em uma atividade:** expande a lista de trabalhadores reais
  daquela atividade. Clique novamente nela a recolhe; clique em
  outra substitui a lista. Não existem trabalhadores artificiais
  para preencher setores vazios.
- **Clique no trabalhador:** seleciona o `PhysicalWorkerId` individual
  usando o evento de seleção já implementado para as bolinhas
  coplanares. Não cria ordem nem altera hash/posições por si só.
- **Persistência:** o cartão permanece aberto após mover o cursor
  livremente pelo mapa e após selecionar trabalhador. Fecha por
  botão **Fechar**, por **Esc**, ou pelo clique em outro tile.
  Se o outro tile estiver vazio, só fecha e seleciona o destino
  físico como de costume. Se estiver habitado, abre o menu novo.
- **Ordens:** A/V/N, G/P/X/E e Tab continuam funcionando sob os
  mesmos controles de autoria, ordens pendentes e replay v11.
  A mudança de ocupação atualiza os números somente depois de E.
- **Camadas:** as três bolinhas permanecem exatamente no mesmo
  plano do mapa com a câmera oblíqua homologada; os objetos
  Godot nativos no cartão capturam cliques somente dentro de seus
  limites. Não há sombra/overlay cobrindo toda a viewport nem
  modificação na física dos tiles.

## Responsabilidade de produto

Este PR não implementa a tela de gerenciamento da cidade, crescimento
urbano, múltiplos setores econômicos por indivíduo, contratos de
construção nem transações de mercado. O clique no tile abre apenas
um menu de consulta e seleção, **não uma entidade administrativa**.

A revisão posterior pode optar por outro gesto, um painel lateral
permanente ou uma janela de cidade: neste momento, a meta é
**eliminar fricção de interação** e preservar decisões individuais.

## Gates de homologação

O novo teste Godot real, `FoundationGodotTileClickMenuSmoke`, exige:

1. Mouse sobre a capital: **nenhuma janela nem tooltip abre**.
2. Clique na capital: menu persistente com seis trabalhadores
   e as três contagens 2/2/2.
3. Mover o mouse para longe do tile: menu continua aberto.
4. Mover sobre agricultura: lista ainda fechada; **clicar** na
   atividade abre os trabalhadores #1/#4.
5. Mover sobre extração vegetal: lista agrícola continua; **clicar**
   nela abre #2/#5.
6. Clicar individual #4 mantém menu aberto e seleciona só #4 sem
   alterar hash ou câmera.
7. N → X → N → E, mesmo turno e journal que PR #375:
   trabalhador #4 muda de agricultura para mineração, mesma
   localização e população 12; contagens do cartão 1/2/3.
8. Esc fecha somente o cartão, mantendo a seleção.
9. Testes v9/v10/v11 legados e PR374/375 separados permanecem
   preservados. Godot Mono 4.7.2 headless/Xvfb capturam quatro
   screenshots reais, e Windows/Ubuntu validam o núcleo, script
   PowerShell e regressão geral.

**Rollback:** se o usuário rejeitar esse gesto, voltar para a
interface de marcadores v11 do PR #375 não altera estado ou migração
econômica alguma. O código do menu vive em uma partial de Godot
`TacticalContextPreview.TileClickMenu.cs`, inteiramente separada
do núcleo.

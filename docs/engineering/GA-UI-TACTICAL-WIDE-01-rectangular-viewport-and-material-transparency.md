# GA-UI-TACTICAL-WIDE-01 — viewport retangular horizontal e transparência do consumo material

## Decisão de produto (05/11/2026)

**Diretriz atualizada:** o mapa tático esférico deixa de utilizar a janela **quadrada** que era uma decisão do MVP anterior. O usuário aprovou uma área de mapa **mais larga que alta**, pois a visualização com barras laterais deve aproveitar o espaço real disponível no desktop. Isso substitui a diretriz anterior de viewport 1:1; a origem do terreno/malha 3D e a fisicalidade tática não mudam.

**Base:** REBASE-12, PR #260, HEAD `86c8401168fb4405efca10d6ac4c6fdc56114496`, homologado no CI com 85/85 workflows e 6/6 testes de kernel/hash; validação visual manual ainda era pendente.

## Implementação

- Retirar o `AspectRatioContainer(Ratio=1)` `GAQASquareMapSlot`, que centralizava um quadrado e deixava faixas horizontais sem uso. O `GAQAMapRegion` é filho direto do `HBoxContainer GAQAContent` e expande tanto em X quanto em Y. O `SubViewportContainer.Stretch = true` preexistente ajusta a renderização às novas dimensões do espaço.
- Reduzir a proporção da barra direita rolável para **27%** da largura (clamp de **350–455 px**), preservando controles de produção/construção, inspeção e navegação; manter o restante para o mapa. A 1600 × 900 espera-se uma área horizontal próxima de 1,3:1 sem letterbox quadrado, sem criar cabeçalho/rodapé.
- Reduzir a largura mínima de inicialização para 420 px para evitar deslocamento visual excessivo antes do primeiro ciclo de layout. Continua válido o redimensionamento via janela.
- Atualizar o título da janela, que ainda anunciava `VS106`, para `Global Arena — Controle tático (DEBUG)`. A compatibilidade do namespace de saves `user://` é preservada.
- Acompanhar proporção real da área e ausência de espaço desperdiçado no teste **`UiVisualQA.gd`** que renderiza telas táticas e globo estratégico em 1600×900 e tira três capturas sob Xvfb. Novo gate: largura do mapa / altura ≥ 1,15 e separação para barra ≤ 15 px. Sem novos handlers de input, mudanças na câmera, picking ou coordenadas canônicas.
- **Transparência econômica:** incorporar na projeção read-only da instalação os insumos efetivamente usados (`IncorporatedWoodRaw`/`IncorporatedStoneRaw`) do journal físico de `assemble_primitive`. O HUD passa a distinguir **estoque atual** de **materiais já incorporados** e afirma que `CONSTRUIR` não cobra a mesma pedra/madeira outra vez. Valores obtidos de eventos reais, não de taxas hipotéticas.
- Testes GA-VS121 reforçados: comprovação das quantidades incorporadas na projeção e igualdade byte a byte dos estoques ao iniciar a etapa de construção, pois essa etapa só reserva trabalhadores.
- Nenhuma alteração nas regras de recursos, produção, impostos, crédito, poder de compra `N`, save ou hash canônico.

## QA e gates

Exigir build Windows+Linux Godot, FAST GA-VS121, regressão kernel/hash multiplataforma, benchmark, Godot E2E e **GA-QA-VISUAL-002 com screenshots**. O gate visual é condicionado ao label `ga-ci-full`, o qual deve ser solicitado explicitamente na PR de UI. Gate CI de screenshot é evidência de geometria e render sob Xvfb; a aceitação de usabilidade definitiva requer captura da versão nova no Windows pelo usuário.

Se a tela não ficar suficientemente larga no formato de monitor do usuário ou a barra lateral ficar demasiadamente comprimida, ajustar o breakpoint/porcentagem com base em evidência visual. A prioridade é legibilidade do mapa e do painel, não maximização cega da largura.

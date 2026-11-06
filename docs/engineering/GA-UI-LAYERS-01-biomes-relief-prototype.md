# GA-UI-LAYERS-01 — protótipo de duas camadas físicas sincronizadas

**Data:** 06/11/2026  
**Base:** PR #276 (diretriz orientadora GA-UI-DIRECTION-01).  
**Status:** experimento de apresentação, sujeito a avaliação visual no Windows; não consolida política definitiva de atalhos/cantos.

## Objetivo e hipótese testável

Verificar, em Godot real, se uma **única camada visual compartilhada** de Biomas / Relevo, alternada por **um atalho simples (M)** e descoberta por **um menu mínimo no canto superior direito**, entrega alto ROI sem sobrecarregar o mapa. Não criar quatro botões permanentes nem sequência de atalhos. A avaliação do usuário determinará se esse menu discreto merece permanecer no viewport.

## Fonte única de verdade

A implementação lê a mesma `WorldGenerationV2PhysicalComposition` que já alimenta a visão física `G(18,0)`, com elevação, nível do mar e biomas indexados pelo mesmo tile.

- **Biomas (padrão):** mesma paleta canônica homologada no PR #275, sem alterações visuais por padrão.
- **Relevo:** rampa hipsométrica (oceano: variação de profundidade relativa; terra: elevação relativa acima do mesmo nível do mar). Mínimo/máximo são extraídos **do próprio campo de elevação 01C da sessão**, não da calibração HD.
- **Gelo:** mantido visualmente claro na apresentação por convenção, sem alterar o campo de altitude.
- **Ambas as escalas:** `CanonicalPhysicalTerrainPalette.FromCanonicalCell` é compartilhada por `GlobePreview` e `TacticalContextPreview`.
- **V1 legada sem 01C:** nunca inventar datum relativo. Permanecer com as cores físicas V1 já verificadas; a UI recusa habilitar Relevo quando o campo 01C não está disponível.

A rampa revela **altitude relativa**, NÃO inclinação em graus, altimetria em metros, geologia simulada, nova geometria 3D ou regra de movimento.

## Interação

- Um único menu `CAMADAS · BIOMAS` / `CAMADAS · RELEVO` no canto superior direito do viewport, com apenas duas opções. Não ocupar os demais cantos.
- Atalho **M**, em ambas as visões, alterna Biomas ↔ Relevo. Não disparar com Ctrl/Alt/Meta, autorepeat, edição numérica/textual ou menu ativo.
- A camada selecionada é **estado de apresentação**, compartilhado nas duas escalas. Continua ativa ao alternar tático ↔ globo e ao restaurar um save durante a mesma sessão; reiniciar o app começa em Biomas. Não alterar bytes de save, journal ou hash.
- Tático: repintar a malha física existente e restaurar o enquadramento da câmera. Arestas, fronteiras opcionais, hover branco, origem/destino e HUD econômico permanecem.
- Global: reconstrução somente da superfície colorida G(18,0), sem reatribuir geometria estratégica invisível/picking e sem mudar zoom/orientação da câmera.
- **Menu e M são experimentais:** a versão final do teclado, personalização de atalhos e localização serão avaliadas em marcos posteriores.

## Aceite automatizado

`tools/GlobalArena.Godot/Tests/UiE2E.gd` deve demonstrar, na cena Main real:
1. Modo inicial Biomas e menu discreto presente.
2. Relevo acionado pelo menu muda **as cores** em ambas as malhas, preservando quantidades de vértices e malha global G(18,0).
3. Overlay estratégico segue desligado e anéis de vizinhos não retornam.
4. Reversão a Biomas restaura exatamente as cores iniciais de ambas as superfícies.
5. Sem mudança de formato de save nem implementação econômica; hash/journal não podem ser alterados por modo visual.

FAST inclui build Godot Linux Release e Windows Debug e workflow E2E isolado. **PASS técnico não substitui screenshots e opinião do usuário.**

## Aceite visual no Windows

1. Iniciar o jogo e confirmar que mapa tático permanece com as cores anteriores, sem novas linhas nem realces extras. Único acesso de camadas no canto, sem cobrir controles importantes.
2. Tecla **M**: conferir camada Relevo na tática e conservação do tile destacado pelo hover branco.
3. Abrir `VISÃO GLOBAL · MALHA TÁTICA`: relevo deve acompanhar o mesmo modo, com planeta e zoom iguais.
4. Alternar pelo menu para Biomas: a aparência já homologada do globo deve voltar.
5. Alternar tático/global e salvar/reabrir uma partida: modo de apresentação não deve afetar turnos, estoques, produção, hashes ou saves.
6. Conferir legibilidade na resolução menor e facilidade de achar as camadas sem saber o atalho.
7. Avaliar ROI: menu de canto é realmente útil para descoberta ou poderia migrar para outra região sem perder acessibilidade?

**Stop gate:** caso modo Relevo esteja pouco legível ou cobre terreno, ou menu esteja interferindo com mapa, corrigir/retirar no mesmo PR. Não iniciar modo Misto nem frete remoto automaticamente.

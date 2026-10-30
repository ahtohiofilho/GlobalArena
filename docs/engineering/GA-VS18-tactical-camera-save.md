# GA-VS18 — Persistência da câmera tática junto à partida física

**Motivação / evidência:** o usuário homologou o VS17, inclusive reabertura de save V13 no meio da viagem, mas identificou que pan, ponto de visão e zoom da câmera voltavam ao padrão após `REABRIR SAVE`. Causa verificada: `TacticalContextPreview.Open(focus)` recompõe o enquadramento e a lente iniciais.

## Contrato de correção

- O mundo, seus trabalhadores, viagens, estoques, receitas e soberania **continuam em save/hash V13 sem mudança de formato**. As regras de movimento e economia permanecem intocadas.
- Ao clicar `SALVAR PARTIDA CANÔNICA`, salvar também a câmera tática **no mesmo instante**: território do foco, enquadramento (1/2 anéis), projeção direta/equidistante, modo cover/fit, centro do mapa (X/Z no chart tático), zoom ortográfico e orientação (yaw).
- Salvar os parâmetros de apresentação num arquivo complementar ao save canônico, `<arquivo-da-partida>.view.json`. Esse arquivo inclui versão do formato visual e **SHA-256 do JSON canônico original**, para nunca aplicar câmera de outra revisão, outro save ou outro mundo. Gravação atômica por `.tmp` e `File.Move`.
- Ao reabrir, validar a partida V13 com o replay existente; só depois consultar o complemento. Reconstruir exatamente o mesmo chart focal e aplicar centro, zoom, orientação e configurações. A posição de visão pode ficar afastada do tile inicial; **não deslocar a câmera à origem por conveniência**.
- Caso o complemento não exista, tenha checksum/versão inválidos, esteja fora de cobertura, seja ilegível ou inválido, **reabrir o save canônico normalmente** com enquadramento padrão; logar a causa. Uma falha ao salvar a câmera não pode fazer o HUD informar falsamente que o mundo não foi salvo.
- Não armazenar posição visual em `WorldState`, não adicionar componentes econômicos, não atualizar hash físico nem causar comando ou revisão canônica ao girar/arrastar/zoom.
- Ao restaurar modo de projeção/anel/cover, atualizar também os respectivos botões, evitando divergência entre UI e cena.
- Um save existente de VS15/VS16/VS17 **sem complemento** permanece reabrível. Saves antigos V9–V12 também podem ganhar complemento, mas não exigem migração.

## Limites

- O marcador visual é associado ao **momento em que o usuário salvou**, não à última câmera que utilizou depois de salvar.
- `user://` Godot mantém os arquivos num diretório da aplicação. Copiar/exportar apenas o JSON físico para outra pasta não leva a câmera; copiar também o complemento é necessário para manter a visão.
- A câmera recuperada refere-se à visualização **tática**. Persistência de orientação e zoom do globo estratégico é escopo separado.
- Propostas macroeconômicas #103, #110, #111 permanecem como estavam. Sem novos mecanismos de colônia ou combate.

## QA Windows

1. Instalar VS18 em worktree isolada, abrir Godot; iniciar laboratório e emitir uma viagem de 2 trabalhadores, avançar 1 turno.
2. **Arrastar o mapa para longe do ponto inicial, alterar zoom e girar ±15°.** Anotar posição/zoom e clicar **SALVAR PARTIDA CANÔNICA**.
3. Mover novamente a câmera para outro lugar e mudar zoom; avançar até a chegada, então **REABRIR SAVE**.
4. Confirmar que voltou ao **turno anterior ao destino**, com grupo em trânsito e saldo/estoque preservados; a câmera deve **voltar exatamente ao pan, zoom e rotação do instante em que foi salva**, não à origem nem à última câmera antes de carregar.
5. Conferir `CÂMERA TÁTICA RESTAURADA` e `SAVE TEMPORAL V13 REABERTO E VALIDADO` no painel.
6. Reabrir save V13 **anterior ao VS18**, sem complemento: continuar funcionando com enquadramento padrão; erro de complemento não bloquear o mundo.
7. CI: testes de checksum da câmera e corrupção, replay V13, conservação histórica V14-V17, compilação Godot Windows/Linux.

PR draft empilhada sobre VS17/PR #114. Nunca mesclar a main sem aprovação explícita.

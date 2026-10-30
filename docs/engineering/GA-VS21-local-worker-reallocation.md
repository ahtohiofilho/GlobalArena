# GA-VS21 — realocação local de trabalhadores, sem deslocamento

Data: 30/10/2026. Base: GA-VS20/PR #119. Escopo mínimo de jogabilidade para os povoamentos emergentes.

## Motivação
A VS20 observa o núcleo habitado e permite que grupos se desloquem fisicamente para tiles adjacentes, porém a interface ainda não permitia trocar a ocupação dos trabalhadores **sem sair do tile**. Como cada trabalhador é residente, consumidor e produtor, uma alteração de atividade local não deve simular viagem nem fundação de povoado.

## Contrato canônico
- `CanonicalPhysicalTacticalSession.ReassignWorkers(tile, fromJob, toJob, amount)` aplica mudança de agricultura, madeira ou pedra para outra dessas atividades **dentro do mesmo tile**.
- A ordem exige quantidade positiva, atividades distintas e trabalhadores residentes suficientes na origem. Rejeições não alteram o estado nem o journal.
- A mudança é imediata na **alocação produtiva**, sem movimento físico nem turno adicional. A produção/consumo são processados na cadência econômica já vigente, sem ciclo artificial imediato.
- População total, residentes, viagens em trânsito, movimento, estoques, recibos, caixa privado e titularidade civilizacional permanecem conservados. A demanda continua derivada da população (não da profissão).
- A alteração cria uma ação canônica `reassign`, com origem e destino iguais, e incrementa revisão e hash. Replay reproduz a operação determinística e o save mantém seu envelope V14/V13 atual.
- No replay, o novo evento é validado pelo mesmo mecanismo de revisão e hashes antes/depois. O formato físico do envelope não ganha campos.
- Compatibilidade: saves V13/V14 **anteriores** continuam legíveis pela VS21. Saves que já contenham `reassign` exigem uma versão do executável que reconheça essa nova ação (VS21+).

## Controles
No HUD tático operacional existente: selecione um grupo e tile ocupado, escolha as atividades origem/destino e a quantidade; clique em **MUDAR ATIVIDADE NO TILE (SEM VIAGEM)**. Não é necessário selecionar um tile de destino. A ordem de deslocamento entre tiles continua separada, usando os dois turnos habituais. O botão de realocação só fica ativo para origem com trabalhadores e profissões distintas. Os anéis dos núcleos e a seleção de grupos são recalculados após a ordem.

## Fora do escopo
Nenhuma nova construção, recurso, instituição, apropriação de território, mercado intertile, transporte comercial, custo de mudança de atividade, custo de equipamento, velocidade de viagem ou regra de inflação. Não introduzir atribuição automática de profissões nem uma nova tela.

## QA / gate
1. Windows: abrir com `tools/launch-vs21.ps1`; conferir `GLOBAL ARENA | VS21` e log `GA_VS21_RUNTIME_ACTIVE`.
2. Selecionar o povoamento inicial (8: agricultura 4, madeira 2, pedra 2); realocar **2 de agricultura para madeira**. Esperado: **2/4/2** e 1 povoamento; dinheiro privado total de 320 créditos; nenhum viajante ou avanço no turno.
3. Verificar que o comando é desabilitado com profissões iguais, e que tentativas de realocar quantidade indisponível são rejeitadas sem mutação.
4. Ordenar viagem de 2 residentes para tile vizinho; durante o trânsito, realocar outro trabalhador na origem; conferir que a viagem e a chegada seguem intactas após dois turnos, produzindo 2 núcleos.
5. Salvar no meio da viagem após a realocação e reabrir: mesmos hashes e estado V14, inclusive câmera. Validar também replay legado V13 sem identidade civilizacional inventada.
6. CI Windows/Linux: `GaVs21`, `GaVs20`, `GaVs19B`, `GaVs15`, Godot Debug/Release, sintaxe do launcher e compatibilidade de workflow herdado.

**Governança:** PR draft empilhada na VS20; não integrar à `main` antes da cadeia predecessora. Aceite visual Windows pendente, independentemente de CI verde.

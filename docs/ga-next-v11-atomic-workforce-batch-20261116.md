# GA-Next — Lote atômico de reclassificação de trabalhadores

**16/11/2026 · incremento funcional sobre o PR #379 · hipótese operacional reversível · sem merge**

## Contexto de produto

O jogador aprovou o ciclo visual do PR #379 como **bom o suficiente, embora ainda artificial**. A otimização artística deve ficar congelada até existir mais funcionalidade. Esta entrega prioriza capacidade real no núcleo, não um redesenho do painel.

A próxima fundação agrícola já tem avaliador matemático em laboratório (PRs #299–#301), **mas falta uma fonte autoritativa de temperatura/umidade/relevo para o novo mundo jogável**. Não usar atributos experimentais meramente visuais do Godot como se fossem fatos produtivos. Essa integração fica fora do escopo, sem dados simulados.

## Contrato implementado

`FoundationTacticalHumanAiTurnHost.QueueHumanOccupationBatch(human, changes)` envia várias escolhas de ocupação de indivíduos reais no mesmo turno, continuando a usar o pipeline existente de journal v11. Cada mudança contém `PhysicalWorkerId` e uma das três ocupações produtivas vigentes.

- **Atomicidade na fase de planejamento:** verifica integralmente participante humano e escrita ainda aberta, IDs válidos, titularidade da civilização, presença no censo G(18,0), v11 com ocupação, destino produtivo válido, destino diferente do atual e ausência de IDs duplicados **antes** de alterar o dicionário de ordens.
- **Reuso da regra individual:** o verificador de transição produtiva é compartilhado com `QueueHumanOccupation` e validação final, evitando divergência de autorização em fluxos de single versus batch.
- **Substituição restrita:** se o lote inteiro for válido, substitui ordens pendentes **somente** das pessoas indicadas (inclusive uma ordem anterior de movimento, como no fluxo individual); mantém intactas as demais ordens. Lotes inválidos preservam integralmente o plano anterior, inclusive suas ordens de movimento.
- **Projeção de escala:** índice de trabalhadores próprios é construído uma vez para validação do lote; comandos são armazenados por ID num `SortedDictionary`. A etapa de pré-validação não precisa buscar linearmente toda a população para cada pessoa; demonstração com 36 trabalhadores por civ.
- **Autoridade e temporalidade:** enfileirar não altera `WorldState`, trabalhador, posição, capacidade produtiva, demanda, revisão, hash ou calendário. `X`/cancelamentos existentes continuam. Somente `SetHumanReady + TryCloseTurn` executam ordens, incluindo as decisões da IA, de forma ordenada por ID, com replay e hash v11.
- **Grupo vazio é legítimo:** um lote pode retirar todos os trabalhadores de um setor. Agrupamento apresenta zero, sem inventar placeholders econômicos e sem apagar trabalhadores.

## Restrições e exclusões

- A interface por clique/arrastar do PR #379 e os atalhos A/V/N, G/P/X/E permanecem exatamente como estavam. O novo método de lote está disponível **no host C#** para controle futuro de grupos, não é acionado pelo botão atual da UI. Não reivindicar um comando em massa visual já existente.
- Não há tela de seleção múltipla, novo catálogo, cidade no domínio, transporte monetário, produtos vendidos, receita, custo de mão de obra ou sincronização de rede.
- **Atomicidade do lote é atomicidade das intenções**, não autorização de pular o gate coletivo de turno. Qualquer commit ainda depende das mesmas regras do núcleo.
- O contrato de plataforma/cliente continua local e conceitual. A integração econômica agrícola, quando houver geografia física autoritativa, deverá ser tratada em PR próprio.

## Gates

1. Mesmo hash e mesmo conjunto de eventos de `QueueHumanOccupationBatch` versus três enfileiramentos individuais em ordens diferentes; todos antes do commit devem preservar o snapshot.
2. Lote misto (válido + trabalhador estrangeiro, duplicado, mesmo cargo, enum inválido, Unassigned ou ID inexistente) rejeitado sem apagar ordens pendentes anteriores.
3. Fora do papel humano, após Ready ou em v10 sem ocupações, negar mutação.
4. Testar 36 pessoas por civ, 72 no total, removendo integralmente um tipo de ocupação; IDs e censo continuam conservados; replay v11 íntegro.
5. Rodar os testes de C# no Windows/Ubuntu e a suíte Godot 4.7.2 de regressão sem mudar interface.

**Governança:** novo PR Draft empilhado sobre #379, preservando todas as aprovações visuais e sem merge automático.

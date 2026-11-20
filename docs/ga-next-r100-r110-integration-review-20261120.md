# GA-Next — candidato de integração R100–R110 (2026-11-20)

## Propósito e limites

Este branch de **revisão consolidada** reproduz exatamente a ponta funcional de R110, mantendo R99 como base de comparação. Não há merge na `main`, mudança de defaults, cherry-pick seletivo, rebase, alteração de histórico ou autorização de escala nova. O documento é a única alteração adicional ao código já auditado.

**Base:** `feature/ga-next-r99-physical-g18-spherical-globe-20261120` (PR #489).
**Ponta funcional anterior ao manifesto:** `73509c8e725d239402333f0f6eca28b4f3aedd4c` (R110, PR #500).
**Escopo do diff auditado antes do manifesto:** 51 commits, 23 arquivos, 0 commits behind da base R99. O novo manifesto adiciona 1 arquivo e 1 commit ao candidato.

## Sequência preservada

| Incremento | PR | Entrega principal |
| --- | --- | --- |
| R100 | #490 | Rotas econômicas reais autorizadas na esfera |
| R101 | #491 | Apresentação tática desacoplada de G18 fixo |
| R102 | #492 | Bootstrap de perfis G3→G18 e G2→G12 (QA) |
| R103 | #493 | Dois turnos econômicos reais em G2→G12 |
| R104 | #494 | Pedido único e imutável de geração/refinamento |
| R105 | #495 | Refinamento compartilhado com host de trabalhadores |
| R106 | #496 | Diagnóstico de escalas Class I alternativas |
| R107 | #497 | Auditoria de conectividade de territórios físicos |
| R108 | #498 | Auditoria de corredores por centros físicos |
| R109 | #499 | Auditoria de arestas/vértices reais dos polígonos |
| R110 | #500 | Contrato de resolução documentado e cancelamento pontual |

## Gates verificados antes de consolidar

- Todos os workflows reportados nos SHAs dos PRs #490–#500 terminaram em `success` em 2026-11-20; entre eles Godot 4.7.2 Mono, fechamento econômico, benchmark de frete, hash cross-platform e regressão cross-platform.
- O Godot real testou `GA_NEXT_R110_WORKER_BOARD_CANCEL_PASS` em headless e Xvfb: trabalhador humano selecionado, cancelamento somente de ordem pendente, nenhum commit de turno/censo/hash, reatribuição e preservação das ações X e E.
- Em QA G2→G12, dois ciclos liquidados produziram 57 e 37 arestas de rotas autorizadas no fixture assinado; padrão G3→G18 mantido.
- PRs originais preservados como Draft para rastreabilidade. A revisão consolidada não os declara individualmente merged.

## Risco de integração histórica

A comparação GitHub entre `main` e a ponta R110 indicou **1221 commits ahead e 0 behind**, com listagem de arquivos truncada aos primeiros 300. Portanto o presente candidato deliberadamente **não** tem `main` como base. A cadeia anterior até R99 (1170 commits ahead da `main`) requer uma estratégia separada, com revisão de CI, dimensionamento de diff e autorização explícita antes de qualquer merge.

## Checklist para liberar integração posterior

- [ ] Confirmar checks disparados por este PR de consolidação na base R99.
- [ ] Reavaliar revisão do diff consolidado e eventuais comentários.
- [ ] Verificar visualmente no Windows os controles já aprovados de trabalhadores, globo físico gradual e rotas de comércio.
- [ ] Definir estratégia para a cadeia histórica anterior ao R99, evitando merge monolítico não revisável.
- [ ] Obter autorização explícita para qualquer mudança na `main`.

**Decisão operacional:** nenhum merge é permitido neste ciclo; o próximo desenvolvimento jogável deve se apoiar na ponta R110 aprovada, sem reabrir auditorias matemáticas de baixo retorno.

# GA-VS60 — Consulta integrada de comércio na sessão

Base VS59 #166, `dc60720a488455d9273ba738411824cf8cf5cdda`.
PR draft empilhado; sem merge na main e sem SETTLE real.

`CanonicalPhysicalTacticalSession.PreviewTrade(CanonicalTradeScenario)` é a
operação única de consulta do host. O cenário explicita fluxos, catálogo,
âncoras, preços, políticas, serviços físicos, quantum e limite de busca.
As coleções são copiadas uma vez. Não aceita projeção pronta de poder de compra,
recibo de seleção, concessão externa nem callback de aprovação.

A operação deriva moradores/poder de compra/H novamente pela VS52, monta as
âncoras verificadas, obtém a prova da rede da sessão, seleciona pela VS58 e
verifica independentemente pela VS59. Só retorna `CanonicalTradePreview` após
as duas etapas e a confirmação final de hash físico/revisão da rede.
O construtor do resultado não é público. `RequireCurrent(session)` rejeita
resultado antigo ou de outra sessão; recarregar um save exige nova consulta.

Reutilizar um cenário recalcula orçamento com os moradores atuais. Alterar os
arrays originais não altera o cenário; mudar preços requer outro cenário.
Validade de um resultado é relativa àquele cenário e à sessão, não uma promessa
de que políticas externas nunca mudarão. Fluxos, preços e tecnologia continuam
inputs explícitos de laboratório; a API não descobre automaticamente mercados
nem altera a política econômica de atendimento e desempate VS58.

Execução serial da sessão. Nenhum save, caixa V14, população, direito ou turno
é alterado pela consulta. Falha de orçamento pode resultar em zero comércio;
falha de autoridade, unidade ou limite recusa a consulta. A operação não efetua
reservas, despacho, liquidação, lançamento contábil ou nova UI de micromanagement.
`CanAuthorizeRealSettlement=false` permanece incondicional.

QA: equivalência com seleção auditada, dois bens compartilhando corredor e
orçamento, migração de moradores, cópia dos preços, enumeração única, ausência
de rede, revogação, fechamento, desativação, restore, unidade incompatível,
limite de busca e invariância do save.

Benchmark `--vs60-session-preview` inclui reconstrução VS52, prova de sessão,
seleção e auditoria: 8/32/128 consultas de uma rota com três configurações,
não 128 cidades. CI Linux VS40–VS60 + benchmark + Godot Release; Windows
integral + benchmark + Godot Debug. Homologação final registrada no PR.

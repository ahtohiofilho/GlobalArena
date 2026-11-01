# GA-VS76 — Seleção simultânea local/frete, referência finita

**Contrato:** GA-ADR-ECON-006 R2, issue #181. **Tarefa:** issue #184.
**Base:** PR #183 VS75, SHA homologado \`ddb68a963434522865cf6a866ab4594c0e7bf6bf\` (41/41 checks).
**Escopo:** laboratório *read-only*. NÃO é mercado executável, saldo ou liquidação.

## Uma seleção física e econômica, não dois mercados

\`CanonicalPrimitiveFreightPortfolio.SelectJointLocalFreight(...)\` recebe alternativas de uso local declaradas (\`CanonicalPrimitiveLocalUseProposal\` como capacidade máxima autorizável, não lote já comprado) e remessas de frete cujas identidades vêm do inventário autenticado \`portfolio.CandidateUses\`. Todas as alternativas entram no **mesmo** espaço finito de combinação.

- A alternativa local é autenticada contra oferta física \`(povoamento,commodity,activity)\` e demanda do mesmo tile \`(povoamento,commodity)\`. Não usa nem fabrica \`ServiceId\`, transporte, estrada ou operador.
- O frete exige a **identidade exata de objeto autenticado** pelo portfolio. Todo subconjunto de fretes selecionados é encaminhado ao gate existente \`PreviewJointBudget\` (VS65), que audita serviço, direitos, infraestrutura, concorrência, congestionamento, compra nominal e proteção H. Não há autorização simplificada para a via comercial.
- Para cada origem \`(source,commodity,activity)\`, \`Σ locais + Σ fretes ≤ fluxo físico\`. Para cada \`(buyer,commodity)\`, \`Σ locais + Σ fretes ≤ demanda física\`. Oferta residual NÃO vira estoque automaticamente.
- Para cada comprador, \`Σ[preço_postado×quantidade_local] + compromisso_total_de_frete_e_mercadoria ≤ SpendableCash = GrossCash − H\`. Não existem carteiras por produto, renda futura ou autofinanciamento hipotético.
- Política D1: a compra local participa do orçamento pelo mesmo preço postado, **sem frete local**. Hipótese alternativa de subsistência gratuita da VS75 não é executada.

## Função objetivo exata D2

Uma vez preservadas todas as restrições:

1. Maximizar \`V = Σ preço_postado(b,c)×q_atendido(b,c) − frete_total_da_avaliação_conjunta\`. Preços e custos na escala física nominal existente; \`V\` é proxy de decisão, **não** lucro do produtor, receita disponível ou equilíbrio. Numerador pode ser negativo; aritmética racional exata.
2. Empate exato de \`V\`: maior soma das frações individuais de demanda atendidas.
3. Persistindo empate: menor compromisso financeiro agregado de compradores, protegido H.
4. Persistindo empate: comparação lexicográfica determinística de vetores de lotes no domínio canônico, **sem regra operacional local-first ou export-first**.

As quantidades são múltiplos de \`rawQuantum\`. Opções recebem de 0 a \`maximumQuantaPerCandidate\` quanta, limitadas pela capacidade autenticada. Domínio máximo = **8 alternativas / 4096 combinações** da VS66; excedentes falham fechados. Não há expansão automática, aproximação ou fatoração enganosa.

O domínio inclui o lote zero. A pontuação econômica pode rejeitar oportunidades com valor líquido negativo na **referência**, sem com isso revogar o desenho futuro de absorção da oferta por preço marginal decrescente.

## Auditoria, proveniência e limitações

\`CanonicalPrimitiveIntegratedSelection.VerifyIndependent()\` reenumera a mesma matriz finita por mecanismo distinto (índice mixed-radix em vez de busca recursiva), revalida demanda/origem/orçamento e reconstrói valor líquido, cobertura, desempates, número de configurações viáveis e lote vencedor. \`RequireCurrent(session)\` impede uso após restore ou mutação física/network pelo certificado do portfolio.

**Sem** alteração das APIs freight-only VS66/VS67, preço do mercado, trabalho/residência, rota, Godot, UI, saves, consumo, cash, estoque, tributo, conta, crédito ou SETTLE.

Esta é a **primeira implementação bounded conjunta** e não se declara motor geral de economia. A fatoração de componentes VS67 foi preservada para freight-only; não afirmamos fatoração escalável dos locais nesta PR. O gate futuro de escala deverá acoplar corretamente mesmas fontes, compradores e redes antes de qualquer aumento de limites.

## QA e condições de promoção

- Testes específicos: conservação de origem e demanda, autoprovisão sem frete, sinal externo de preço, invariância de ordem, restrição H, falsificação de identidade/quantidade, cap fail-closed, restore invalida recibo, não mutação de sessão.
- Benchmark 8/32/128 em sessão física com estrada real e custo comercial autenticado, sem critério de performance artificial.
- CI Ubuntu/Windows, teste completo e direcionado, Godot Mono.
- Homologar **somente** quando 100% dos workflows do SHA final estejam \`completed/success\`. PR Draft empilhada sobre VS75, sem merge automático.

**Bloqueio canônico inalterado:** a diretriz de *toda oferta efetivamente posta à venda ser absorvida sob preço marginal decrescente* ainda requer reconciliação com demanda finita, poder de compra H e receitas, antes da execução. Não confundir potencial produtivo com lote ofertado. Não inventar consumidor monetário/estoque/renda para forçar consumo.

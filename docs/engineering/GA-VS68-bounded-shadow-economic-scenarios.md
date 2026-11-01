# GA-VS68 — Comparação limitada de cenários econômicos hipotéticos

**Baseline:** GA-VS67 PR #174, head `a5da381c0c41e769e134540bba903905b12fa433`, 33/33 workflows aprovados. Novo PR empilhado em Draft, `main` inalterada.

## Objetivo e contrato

A VS67 já calcula uma seleção conjunta ótima **dentro de cada domínio finito declarado** e demonstra independência entre compradores, produção, transportadoras e corredores. A VS68 compara **1 a 6 cenários hipotéticos mutuamente exclusivos** sobre o mesmo conjunto de candidatos autenticados do snapshot físico atual. Cada cenário tem um identificador único, preços nominais para todos os produtos demandados, política experimental de poder de compra/H com identidade e versão compatíveis e política de congestionamento declarada.

Nova API `CanonicalPrimitiveFreightPortfolio.CompareFreightScenarios(candidates, scenarios, baselineScenarioId, rawQuantum, maximumQuantaPerCandidate, maximumConfigurationsPerComponent)`.

Cada hipótese resolve novamente o seletor VS67, com orçamento/H por comprador **uma só vez por hipótese**, produção e demanda físicas agregadas, serviços institucionais e infraestrutura existentes. Os cenários são **alternativas, não ciclos consecutivos**: nenhum deles financia outro, nem muda saldo, estoque, produção, rotas ou direitos.

## Invariantes de modelagem

- O comprador segue sendo o trabalhador-residente do tile: produção e consumo são aspectos da mesma população, sem entidade separada de cidade. O H é um **resíduo invisível protegido de consumo**, não depósito, carteira, crédito ou obrigação futura.
- Os valores numéricos de poder de compra e preço são **parâmetros explícitos e provisórios de laboratório**; não substituem a diretriz congelada de poder de compra abstrato. Identificador/versão da política instituem compatibilidade **do referencial experimental**; um coeficiente alterado no cenário NÃO é uma alteração da política efetiva do jogo.
- A alteração de pesos de saturação simula um cenário de custo, **não constrói capacidade física**, corredores, pontes ou serviços. As propostas usam somente as ofertas reais VS63/64 e opções validadas do mundo corrente.
- Comparações registram os sentidos (`-1`, `0`, `+1`) da mudança em fração agregada de demanda atendida e gasto nominal dos compradores relativamente ao cenário de referência. **Não ranqueiam bem-estar entre políticas com preços distintos**; são deltas descritivos e não recomendações de governo.
- Uma solução nula (zero lotes) é resultado legítimo quando H ou preços inviabilizam todas as compras. Não há venda garantida, dinheiro negativo, endividamento ou receitas do turno futuro.
- Identificadores de cenário devem ser únicos, com até 64 caracteres; um baseline explícito deve pertencer ao conjunto. Inputs preguiçosos são materializados e tabelas de preços congeladas antes da primeira avaliação. Candidatos falsificados, cobertura incompleta de preços, política estrangeira, excesso de cenários ou espaços de busca fora dos limites VS66/67 falham fechados.
- A ordenação de cenários usa o ID ordinal; a ordem inicial de cenários e candidatos não altera resultados. O mundo atual, versão de oferta e rights snapshot seguem vinculados à sessão; restore/save ou fechamento de rede invalida todos os resultados.

## Auditoria e evidência

A classe `CanonicalPrimitiveFreightScenarioComparison.VerifyRecomputed()` reexecuta a seleção VS67 **para cada cenário individual** a partir dos preços congelados, H e congestionamento correspondentes. Compara seleção de lotes por identidade/quantidade, score racional, gasto agregado, configurações e quantidade de componentes, carga por corredor e livro dos compradores (incluindo H) com valores exatos. Cada VS67 invoca seu auditor de fronteiras e cada VS66 reenumera o domínio exaustivo por um algoritmo independente; a auditoria nova é replay/reconciliação, **não** um novo otimizador independente nem uma autorização de SETTLE.

As regressões VS68 cobrem:
- um comprador com três produtos e um corredor, sob baseline, preço excessivo, H integralmente protegido e congestionamento elevado; os choques são alternativas, não cumulativos;
- determinismo sob inversão de inputs, congelamento de listas de preços mutáveis fornecidas externamente e replay completo;
- limite de seis cenários, baseline ausente, duplicação de IDs, políticas incompatíveis, candidatos não autênticos, preços faltantes e extrapolação do domínio;
- invalidação por restauração de save e alteração de infraestrutura da sessão.

Benchmark `--vs68-scenarios`: cenário **conectado e fixo** com três mercadorias e três hipóteses, 8/32/128 **consultas** após aquecimento. Esse benchmark não implica desempenho linear em população, número de cidades ou número de vias.

Gate GitHub Actions Linux/Windows: testes dirigidos VS40–68 e suíte integral, benchmark Release e Godot Mono Release/Debug. Homologação somente quando **todos** os workflows do head final estiverem `completed/success`.

**Governança:** `CanAuthorizeRealSettlement=false` e `IsCommitted=false`. Nenhum merge em `main`, nenhum contrato, despacho, estoque persistente, conta bancária, empréstimo ou liquidação real. A próxima etapa não pode interpretar cenários hipotéticos como mudanças institucionais automáticas.

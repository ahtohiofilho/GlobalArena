# R169 — acoplamento nativo das frações R168 ao mercado físico existente

## Regra de negócio
A R168 produz a presença temporal, demanda por perfil de atividade e esforço produtivo de trabalhadores reais, por nó. A R169 encaminha essas quantidades **diretamente** à estrutura `FoundationPhysicalCommodityMarketInputs`, consumida pelo motor canônico de distribuição marginal e pelo auditor de frete; não cria trabalhadores artificiais nem um segundo solucionador.

## Primeira integração
- `FoundationTemporalNativeMarketInputs.FromTemporal` monta lotes físicos por nó/civilização/ocupação/mercadoria, com capacidade igual à produção fracionária, e demanda positiva por nó/civilização/mercadoria derivada da R164. Cada pessoa continua identificada no calendário R168 e não recebe múltiplos corpos.
- `FoundationPhysicalMarginalMarketDistributor.Distribute` possui entrada tipada adicional de market inputs. A entrada histórica via censo passa pelo mesmo núcleo.
- `FoundationPhysicalGlobalMarginalCoordinator.Coordinate` possui entrada tipada adicional para as mesmas fontes reais, preservando a resposta simultânea entre vendedores e a política de convergência.
- `FoundationPhysicalDeliveryAllocationAuditor.Audit` aplica exatamente as mesmas restrições de entrega total, roteamento, mercadoria e frete às novas entradas, sem assumir que o censo final represente todo o turno.
- `FoundationTemporalNativeMarginalMarketPlanner.Coordinate` lê frete e infraestrutura reais do WorldState, preserva arestas bloqueadas do worldgen assinado e fornece a cotação universal p = sqrt(D/Q) para cada mercado, sem multiplicador local.
- Regresso integrado com jornada de 1,4 turno e mercado canônico: trabalhador que viaja não produz no primeiro turno, mas demanda no local de partida; os demais produtores abastecem mercados pelo mesmo water-filling.

## Limites de promoção
A R169 é um **candidato auditado**, não um fechamento monetário. Ainda faltam adaptar `FoundationPhysicalEconomicAccountingReconciler` e `FoundationEconomicSalesSettlementEventExecutor` às mesmas entradas temporais e garantir publicação de recibos journalados de vendas reais. A proteção R167 que impede settlement antigo com travel fracionário continua ligada. A entrada tipada deve ser oferecida à fase econômica de maneira autoritativa, sem aceitar um calendário fabricado pelo cliente.

Quantidade produzida inferior ao quantum físico de carga é rejeitada pelo mesmo mecanismo de granularidade; nenhuma mercadoria pode ser arredondada para cima ou esquecida. A política de demanda e de conversão física permanece explícita.

## Governança
PR Draft empilhada sobre R168 (#561), não efetuar merge antes do CI e validação do próximo gate de settlement.

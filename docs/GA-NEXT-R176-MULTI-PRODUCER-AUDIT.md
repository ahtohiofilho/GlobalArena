# GA-Next — R176: custo-benefício da redistribuição com três produtores reais

## Escopo
Continuação da medição R175, sem alterar a economia autoritativa ou o solucionador. Ampliamos o caso de dois para três lotes produtivos reais, distribuídos entre dois nós físicos e dois proprietários, com um consumidor real adicional e uma aresta com custo físico fixo.

## Evidência coletada
- Produção de 5 ou 20 unidades por lote, sem inventário ou limites artificiais de mercado.
- Cotações auxiliares de planejamento universais (P0=1), demanda real por atividade e custo físico de transporte constante.
- Passagem zero (primeira distribuição) e todas as passagens subsequentes observadas sem alterar suas decisões.
- Receita monetária canônica por nó/mercadoria, frete físico real, ganho líquido absoluto e em basis points por passagem.
- Conservação de produção e verificação de passagem terminal sem alteração.

## Guardrails
Não reintroduzir congestionamento, não mudar preço, não aumentar número de repetições no jogo, não usar limiares de tempo como gate. Os dados são diagnósticos de cenários pequenos; nenhuma decisão de desativar redistribuição será tomada apenas com eles.

A PR continua Draft, empilhada sobre a R175. Windows e Linux são obrigatórios, macOS facultativo.

## Medição observada — CI Ubuntu, 2026-11-23

O workflow dedicado concluiu com **7 testes diagnósticos aprovados**, incluindo os dois novos casos de três lotes. A passagem terminal não movimentou bens, e as quantidades permaneceram conservadas.

| Produção por lote (micro) | Passagens totais | Saldo líquido inicial (scaled) | Depois do 1º ajuste | Saldo líquido final (scaled) | Ganho líquido final |
|---|---:|---:|---:|---:|---:|
| 5.000.000 | 3 | 141316112000000000 | 142199509346320000 | 142287050000000000 | +0,687% |
| 20.000.000 | 2 | 267076552167540000 | 270540800000000000 | 270540800000000000 | +1,297% |

O primeiro ajuste capturou aproximadamente 91,0% do ganho no cenário menor e 100% no maior. A passagem final de certificação não proporcionou benefício monetário, mas verifica estabilidade do coordenador.

Carga física de frete inicial → final, em microunidades: **8.608.842 → 7.612.449** (cenário menor) e **32.854.109 → 28.529.707** (maior). O ganho é consistente com redução de transporte, sem qualquer regra de congestionamento.

**Conclusão limitada:** três lotes produtivos em apenas dois mercados reforçam a captura rápida do benefício; não comprovam desempenho nem justiça em milhares de nós, múltiplas rotas e diferentes mercadorias. Por isso não foi introduzido critério de parada por valor monetário ou por número fixo de passagens. O solucionador e replay continuam intactos.

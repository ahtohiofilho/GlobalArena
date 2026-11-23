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

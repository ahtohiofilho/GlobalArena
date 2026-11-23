# GA-Next R177 — hipótese de parada econômica simples

## Decisão em avaliação
O water-filling, a função universal de preço, o frete de infraestrutura e a presença temporal são preservados. As medições R172–R176 mostram que as primeiras passagens de redistribuição tendem a capturar grande parcela do ganho líquido nos cenários analisados. **Isso não é evidência suficiente para substituir o ponto fixo na economia autoritativa.**

## Regra candidata, estritamente diagnóstica
- Tomar o resultado econômico líquido real da distribuição inicial, `N0 > 0`.
- Realizar pelo menos uma passagem global adicional.
- Após cada passagem completa, comparar o aumento `N_t − N_(t−1)` com `N0 × epsilon`.
- Uma hipótese é `epsilon = 1/10000` (1 basis point, 0,01%); outra é `10/10000` (10 basis points, 0,1%).
- Se o ganho da passagem não superar essa tolerância, **marcar apenas como candidato a parar**; senão, continuar a observar. A passagem sem movimento do coordenador original também encerra a comparação.
- Comparar a economia resultante com a liquidação integralmente estabilizada, incluindo ganho que seria perdido e número de passagens economizadas.

Os limites de 1 e 10 basis points são parâmetros de pesquisa em escala monetária compreensível, **não um limiar matematicamente provado ou política de gameplay aprovada**.

## Riscos e próximos controles
- Ganhos negativos ou pequenos numa passagem podem anteceder ganhos positivos relevantes em outra. Parar ao primeiro sinal pode causar perdas globais importantes.
- A receita líquida disponível ao avaliador é uma grandeza exata baseada em preço canônico e frete; isso não substitui o potencial integrado do objetivo marginal. O teste deve investigar as diferenças antes de uma implementação definitiva.
- Um plano interrompido sem convergência **não pode** afirmar que alcançou ponto fixo, nem passar por journal/replay sem revisão explícita dos contratos e invariantes.
- A instrumentação só observa traços completos já aprovados: não acelera a liquidação de produção, não introduz preço alternativo e não cria congestionamento.
- O limite atual `MaxProducerPasses` continua exatamente como antes; Windows/Linux continuam obrigatórios, macOS permanece opt-in.

## Critério de avanço
Propor alteração em produção somente se a hipótese reduzir trabalho comprovadamente em cenários diversos, com conservação, determinismo, justiça econômica e perda residual conhecida/aceitável. Caso a economia medida não justifique parar antes, **não** ampliar o sistema de limiares e preferir o algoritmo atual.

# Global Arena — Gate 09 R22: diagnóstico da origem do gap residual (Draft)

Data: 13/11/2026. R21 PR #310 (CI Windows/Linux aprovada), base experimental.
Status: diagnóstico de laboratório, não homologação de equilíbrio.

## Hipótese de trabalho

No R21, o gap agregado diminuiu de forma relevante com mais iterações,
mas não atingiu <= 1e-4. O diagnóstico R22 não deve presumir que um
amortecimento específico corrige o problema. Primeiro precisamos separar:
(a) contribuição de grupos individuais ao arrependimento agregado;
(b) estabilidade simultânea de preços, quantidades e congestionamento;
(c) alternâncias de rotas ótimas e possíveis saturações físicas;
(d) empates comerciais/financeiros ainda sujeitos a F-059.

## Método e contratos

- Reutilizar o solucionador R20 e os cronogramas R21 de atualização **sem
  nenhuma alteração**.
- Reconstruir o gap F-072 por contribuição de cada grupo usando a mesma
  ponderação por oferta e melhor escore: não confundir maior gap individual com
  principal contribuição ao gap global.
- Rastrear maiores contribuidores por bem, civilização e origem; pressão de
  congestionamento por aresta, trocas de melhores rotas e volatilidade.
- Critério de estabilidade exploratório: gap relativo, pior grupo, variações
  de preços, cargas e quantidades <= 1e-4 por três passos consecutivos.
  Mesmo satisfeitos, resultados são **observações numéricas**, não
  comprovação de existência, unicidade ou convergência global.
- Confirmação de paridade dos snapshots do R21; validações de integridade
  dos 16 arquivos R20; verificação independente dos preços, caminhos e rateio
  financeiro em checkpoints.
- Preservar F-041/F-043/F-049/F-060/F-062/F-071/F-072/F-081/F-084F;
  nenhuma demanda artificial, saldo de comprador, estoque físico, venda
  parcial, exceção física costeira ou tarifa não aprovada.
- **F-059 continua pendente**, assim como paridade de solucionador C# integrado,
  desempenho do jogo, persistência e WorldState.

## Gate de decisão após a CI

Se poucos grupos concentram a maior parte do gap, identificar seus mercados,
origens e custos de caminho para orientar um experimento seguinte. Se o gap
é disperso, avaliar política numérica global. Se preços/quantidades/cargas
não estabilizam, não confundir gap baixo momentâneo com um equilíbrio estável.
Se for observado congestionamento alto e alternância de destinos, priorizar
análise de sensibilidade antes de mudar a penalidade física.

**Governança:** PR R22 Draft empilhado sobre R21, sem merge nem alterações
nos arquivos autoritativos do Global Arena.

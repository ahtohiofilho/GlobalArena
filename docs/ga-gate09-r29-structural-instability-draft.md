# Global Arena — Gate 09 R29: diagnóstico estrutural de instabilidade (Draft)

Data: 13/11/2026. Base: R28 PR #317. R20–R28 e motor C# preservados.

## Direção

Após inúmeras políticas numéricas (R21–R28), permanece a ausência
de um checkpoint que satisfaça simultaneamente os cinco indicadores
de estabilidade. O próximo investimento será em atribuição
descritiva, não em mais um ajuste de alfa.

## Instrumentação

Acompanhar cada passo de políticas originais, com os seguintes
indicadores independentes:

1. Mudança de mercado, mudança exclusiva de rota, ambas e nenhuma
   para o melhor destino/caminho de cada grupo produtor.
2. Reversões do alvo A→B→A em dois passos e coincidências exatas
   do vetor completo de destinos/rotas em defasagens 2 e 3.
   Não confundir ciclos de melhores respostas com ciclo de fluxos,
   preços ou equilíbrio geral.
3. Preços mais afetados, diferença de cargas e quantidades,
   concentração do arrependimento F-072 e grupos em tie-break.
4. Interseção dos caminhos trocados com as cinco arestas de maior
   carga/capacidade no snapshot anterior, relatando exposição
   e sobrecarga, sem atribuição causal automática.
5. Correlações estatísticas descritivas de alternâncias, cargas,
   preços e aumentos de gap; relatar amostra e variância nula
   sem forçar coeficientes.
6. Sondagens contrafactuais com alfa 1/8 e 1/16 em checkpoints
   congelados: recalcular oráculo R14 integral, contar custo e
   **não aplicar** as propostas na trajetória-base.

## Controles

Usar diretamente R21 decaimento, alfa fixo 1/8, backtracking,
e R26 progressive_global, sem alterar nenhum desses módulos.
Conferir manifesto R20 SHA256 (16 arquivos), replay original
R21 passo a passo, paridade independente R13/R14,
100% das vendas e conservação financeira F-084F.
Cenários CI: 42 nós/96 passos e 162 nós/48 passos.
Custo das sondagens separado do custo das propostas aceitas.

## Critérios de saída e limites

Aprovar o R29 como diagnóstico somente se determinístico, paritário
e auditável em Windows e Ubuntu. Resultados de correlação e
reversões não autorizam provar causalidade, existência de um
ciclo real na economia nem ausência global de equilíbrio.
O gate numérico segue exigindo cinco métricas <= 1e-4
por três atualizações seguidas.

Orientar um futuro R30 conforme os dados: se houver reversão
recorrente de melhores respostas, estudar seleção de rota/
desempate e continuidade matemática; se não, estudar
condicionamento e concentração do arrependimento.
Não criar regras ad hoc de demanda, oferta, preferência ou tarifa.

F-059, convergência global, paridade de um solucionador integrado C#
e produção continuam pendentes. PR Draft empilhado em R28,
sem merge nem alteração do jogo.

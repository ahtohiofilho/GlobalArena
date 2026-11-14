# Global Arena — Gate 09 R30: margem comercial e testemunho de rota (Draft)

Data: 14/11/2026. Baseline PR #318/R29; artefatos R20–R29 preservados.

## Pergunta decisiva

O R29 encontrou escolhas comerciais que retornam A→B→A e
repetições do vetor completo de melhores respostas em defasagem 2.
Ainda não sabemos se essas mudanças surgem por opções quase
indiferentes ou se preços/custos se movem o suficiente para
inverter a preferência entre opções substancialmente diferentes.

## Limitação do oráculo, explicitamente preservada

O R14 oferece a menor rota canônica de CADA mercado,
mais a contagem truncada de rotas equivalentes.
**Não devolve a segunda melhor rota de cada destino.**
Fingir conhecê-la produziria uma conclusão matemática falsa.

O R30 adotará duas métricas sem confundi-las:
- Diferença entre o melhor e segundo melhor MERCADO, usando
  suas rotas canônicas de menor custo e a mesma fórmula
  de preço / (1 + custo).
- Diferença entre a rota vencedora ATUAL e a rota concreta
  anteriormente selecionada, reprecificada com cargas
  e tarifas atuais. Quando ambas terminam no mesmo mercado,
  esse testemunho isola a perda da rota anterior; não
  certifica o segundo menor caminho entre todos os possíveis.

Outros dados: multiplicidade de caminhos mínimos limitada a 2,
empates de mercados conforme tolerância original 1e-10*max(1,score),
margem pequena *descritiva* de 1e-4, quantidade de inversões de
destino/rota e presença de padrões A→B→A.

## Diretriz de integridade

- Reconstituir primeiro colocado e score usando cache congelado
  original R14, sem alterar o módulo. A lista de grupos empatados
  DEVE coincidir exatamente com R14 a cada snapshot.
- Reconciliar manifesto R20 SHA-256, paridade independente R13/R14,
  reprodução de fluxos e gaps R21/R26, produção vendida integralmente
  e conservação financeira F-084F.
- Testar pequenas mudanças contrafactuais de alfa 1/8 e 1/16
  de maneira não intrusiva, cobrando suas chamadas R14
  e registrando scans adicionais de caminho mínimo.
- Não transformar limiar descritivo 1e-4 em regra econômica
  nem em mecanismo de seleção de mercado.

## Gate de saída

Se grande proporção das mudanças de MERCADO tiver margem
quase nula, estudar no R31 robustez de tie-break/precisão.
Se inversões ocorrerem com margens substanciais e choques
de preços, priorizar sensibilidade de preços e saturação
física da rede. Se as mudanças forem principalmente de
ROTA para o mesmo destino, avaliar mais profundamente
os custos de aresta e a multiplicidade dos caminhos mínimos.

Essas são hipóteses a comparar experimentalmente,
não constatações causais do R30. F-059, prova de equilíbrio,
solver C# integrado e autorização de produção permanecem
abertos. PR Draft sobre R29 sem merge; todo o jogo intacto.

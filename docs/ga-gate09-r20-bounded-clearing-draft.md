# Gate 09 R20 — A/B integrado, orçamento fixo (Draft)

**Data:** 2026-11-12. **Escopo:** laboratório Python isolado; não motor C#, não produção, não merge.

## Objetivo
Medir o ciclo completo de clearing sintético de múltiplos produtos (Goldberg; F-072, F-084F), comparar o avaliador R13 original e o cache R14 mantendo preço, congestionamento, rotas, fluxo, venda integral e rateio idênticos. Testar prefixos de trajetórias e amortecimento, sem presumir convergência.

## Evidência local preliminar (Python 3.13)
- Três testes próprios aprovados; manifesto SHA-256 de **16 arquivos** e proveniência dos **11 módulos originais** do pacote R18.
- Perfis CI: 42, 162 e 642 nós, repetição A/B e auditoria R2/F-084F em 42 nós; todos passaram.
- Perfil ampliado: 42 / 162 / 642 / 1.442 nós, com 2 / 3 / 4 / 6 produtos e 12 / 24 / 40 / 72 grupos; correspondência exata dos checkpoints, reconciliação financeira em todos.
- Ganho observacional do cache no ciclo completo, por porte: **2,89× / 3,44× / 3,00× / 2,64×**. Tempos em ambiente de desenvolvimento Python, sem extrapolação para C#.
- Gap final no perfil ampliado: **0,070244 / 0,183124 / 0,358071 / 0,422464**, todos **acima de 0,0001**. Não declarar equilíbrio.
- Variações de alpha 1/16, 1/8, 1/4: trajetórias comparadas, sem ratificação de F-059 ou monotonicidade universal.

## Dependências e fronteiras
- PR #308 R19 aprovou separadamente o conversor C# IEEE-754 e seus 270 vetores Windows/Linux. R20 **não** conecta esse conversor à simulação Python; é benchmark integrado do algoritmo de clearing Python anterior.
- F-059 financeiro continua em aberto; fixtures utilizadas não exercitaram empates financeiros significativos. Convergência global, taxa de desempenho da simulação real, journal, saves, tesouros e multi-turnos permanecem fora do escopo.
- Não editar o código autoritativo nem PR #307 ou #308.

## Próximo passo
Versionar os 11 módulos upstream idênticos, runner R20, três testes, manifesto, documentação e workflow Python Windows/Linux, todos em `experiments/gate09/r20/`, e verificar a CI antes de interpretar o R20 como homologação multiplataforma.

**Governança:** PR deve permanecer **Draft**. Nenhum merge automático. Este documento inicial **não significa que o laboratório e workflow já foram publicados**.

# Global Arena — Gate 09: encerramento do diagnóstico e transição a soluções

**Marco:** R37, 14/11/2026. Base PR #325/R36. O conteúdo de
R20–R36 permanece intocado.

## Decisão executiva de engenharia

O ciclo R30–R37 respondeu suficientemente à pergunta
"como e em que condições o ranking comercial se inverte?"
A existência de nuances adicionais não justifica, por si
só, mais uma geração de PRs exclusivamente diagnósticos.

A partir da homologação deste PR, o plano passa a ser
**construir, comparar e rejeitar ou promover algoritmos
numéricos concretos**. O objetivo de produto é um
solucionador verificável e integrável ao kernel C# do
Global Arena, e NÃO o aumento indefinido do número de PRs.

## Último diagnóstico necessário (R37)

Reproduzir as mudanças observadas do R36 e reconstruir
o ranking de TODOS os mercados em pontos predefinidos
da mesma atualização aceita, inclusive ao redor dos
intervalos de igualdade de dois scores.

Distinguir com clareza:
- apenas A→B nos pontos amostrados;
- A→C→B com terceiro mercado vencedor transitório;
- reentradas A↔B no ranking observado;
- empate de duas alternativas sem liderança global;
- empate comercial global vs equivalência de menor caminho;
- alteração de rota canônica, mesmo com score contínuo.

O resultado é uma classificação de AMOSTRAS, não prova
de ausência de cruzamentos fora da grade nem unicidade
da raiz; desempate financeiro F-059 continua em aberto.

O passo aceito original, preço P=sqrt(D/Q), custo físico
de congestionamento, tarifas, oferta, receita e política
econômica não serão alterados. Janela de 24 trajetórias
sobre mapas de 42/162 nós, capacidades x0,2, x0,4 e x1,
dois regimes de demanda/oferta e duas políticas R21
originais. O trabalho real dos oráculos R14 e dos
caminhos mínimos é integralmente contabilizado.

## R38: primeiro candidato efetivo, não outro diagnóstico

Propor e implementar na RAMIFICAÇÃO EXPERIMENTAL um
método de **troca coordenada de fluxo conservativa**
entre mercados de um produtor, com avaliação integral
do ganho comercial pelo F-072 e controle do tamanho
do passo. Diferentemente da relaxação global antiga,
o método altera parte do fluxo de um grupo por vez;
sua viabilidade precisa ser testada, não assumida.

Confrontar com políticas R21/R26 intactas nas MESMAS
fixtures, mesma quantidade permitida de consultas
ao R14 (incluindo tentativas descartadas) e mesma
métrica de erro comercial. Incluir pelo menos condição
normal e congestionamento severo. Preservar demanda,
função preço, custo, tarifa, propriedade, recursos
financeiros e 100% de alocação do trabalhador.

## Critérios objetivos: promover ou descartar

1. Métricas obrigatórias (sem relaxar): gap relativo
   global F-072, pior grupo, mudança relativa de preço,
   carga de arestas e quantidade por mercado, todas
   <=1e-4 durante TRÊS passos consecutivos.
2. Validação independente R13/R14 e F-084F; integridade
   original 16/16. Aproximar o erro movendo alfa a
   zero sem reduzir o arrependimento não aprova.
3. Avaliação por orçamento de oráculos comparável,
   além de orçamento de passos. A estabilidade
   observada não equivale à prova de existência,
   unicidade ou convergência geral.
4. Se um método não atingir o alvo no orçamento
   predeclarado, registrar NO-GO; não criar
   calibrações indefinidas para disfarçar falha.
5. No máximo três FAMÍLIAS matematicamente diferentes
   de solução numérica antes de formalizar decisão
   sobre redesenho do problema ou do método.

Sequência planejada: R38 primeiro candidato concreto;
R39/R40 somente se houver razão técnica para trocar
de família; escolha de engenharia ao final de R40,
com decisão GO ou necessidade de reformulação.
F-059 constitui trilha específica de regra financeira
a ser deliberada, sem contaminar o mérito matemático.

## Estado e riscos do projeto

**Não aprovar** merge automático, alteração do motor
autoritativo C#/Godot, liberação à produção, nem
ratificação de F-059 enquanto os gates correspondentes
não estiverem satisfeitos. O R37 fecha uma FASE DE
PESQUISA, não homologa a solução econômica.

O encerramento não bloqueia correção de um erro de
replay, integridade, conservação ou fórmula canônica
que uma futura CI eventualmente identifique.

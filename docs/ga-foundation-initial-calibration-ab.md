# GA-Next — Inicialização comercial em tempo zero: experimento A/B

**Data:** 15/11/2026. **Base:** PR #351, primeiro ciclo executável .NET 10 da nova fundação. **Status:** hipótese operacional **opt-in**, não homologada para produção nem ativada por padrão. Um único ensaio comparativo, sem série de micro-ADRs.

## Problema identificado

No PR #351, o primeiro turno do cenário demonstrativo começava com a oferta física distribuída uniformemente entre mercados alcançáveis. Isso é uma inicialização arbitrária da **alocação**, não uma alteração artificial da **demanda**. O maior desvio relativo de oportunidade comercial no primeiro turno, medido depois de 128 ajustes adaptativos, era 0,072555 (7,2555%). Os turnos seguintes usaram o estado anterior, mas também houve modificações nos trabalhadores, então a melhora numérica observada não provava a eficácia causal do *warm start*.

O objetivo deste checkpoint é testar a sugestão de uma única resolução **pré-turno** que prepara o estado comercial para o turno 1, preservando todos os trabalhadores, atividades, oferta física, demanda, geografia, preços e custo de congestionamento. **Não existe um segundo turno de jogo.**

## Implementação e contratos

`PhysicalWorkerTradeCycle.CalibrateInitialMarket()`:
- executa **exatamente uma resolução econômica integral** usando os mesmos parâmetros do ciclo, sem ordens, antes de qualquer turno real;
- conserva somente as alocações comerciais e o conjunto de mercados precificados como referência para a primeira resolução real;
- deixa `CurrentTurn == 0` e o conjunto de trabalhadores completamente intacto; não produz novo lote, não duplica demanda, não desloca trabalhadores, não cobra tributos nem liquida dinheiro (a liquidação monetária nem foi implementada nesse protótipo);
- retorna relatório explícito (`FoundationInitialCalibration`) com oferta, vendas, passes, quantidade de mercados precificados e gap observado, sem publicar `FoundationTurn` autoritativo;
- rejeita calibração repetida ou iniciada depois do turno 1;
- não altera a lei `P=sqrt(D/Q)`, custo de congestionamento quártico, tarifas, regras de rotas, tolerância, orçamento de passes ou qualquer atributo de tile;
- mantém a calibração **desativada por padrão**: executar `Advance()` diretamente permanece idêntico ao PR #351.

A rigor, calibração não é uma previsão de vendas diferentes nem uma alteração de preço externo. É o uso de um **snapshot comercial preliminar** obtido das mesmas decisões econômicas para inicializar a primeira resolução publicada. Preços, cargas e receitas do turno real são sempre recalculados com base na alocação real desse turno.

## Ensaio A/B: mesmas condições e orçamento

```powershell
dotnet run --project GlobalArena.Console/GlobalArena.Console.csproj -- --foundation-calibration-ab
```

- **Controle A:** instanciar a rede/trabalhadores originais e executar `Advance()`; registra o primeiro turno real.
- **Candidato B:** instanciar uma rede/trabalhadores **idênticos**, executar `CalibrateInitialMarket()` em tempo zero, depois `Advance()`; registra o primeiro turno real.
- Comparar gap final e estado físico, todos no mesmo turno 1. A comparação NÃO usa o segundo turno do controle como se fosse um jogo equivalente; esse segundo turno serve apenas em teste de regressão para confirmar a equivalência do estado matemático *warm-started* quando não há novas ordens.
- Não inserir novos produtores/demandantes, mudar o tempo, reiniciar trabalhadores em locais especiais nem permitir que o `fake` receba efeitos militares.
- Verificar que a oferta e demanda de cada cenário são rigorosamente iguais, que preço decorre de `sqrt(D/Q)`, que a massa produzida é comercializada integralmente e que as arestas somam os fluxos reais.
- Reexecutar suite completa C# histórica em Windows e Ubuntu e todos os gates já existentes do repositório.

**Critério de interpretação:** a hipótese só é vantajosa se o desvio no primeiro turno REAL cair a valor economicamente desprezível **sem elevar indevidamente o custo de inicialização**. Mesmo um gap comercial baixo não comprova erro de preço nem qualidade das decisões econômicas; medições de preços/receitas/rotas e aproximação de referência independente seriam requisitos posteriores. A CI certifica segurança do mecanismo e reproduz o A/B, **não aprova automaticamente a estratégia** caso sua precisão continue insuficiente.

## Risco controlado

Resolver duas vezes uma equação de ajuste que oscila pode simplesmente manter o desequilíbrio ou deslocá-lo para outro mercado. A calibração não conserta por decreto as limitações do algoritmo. A prova da melhoria precisa ser empírica. Se o resultado não for convincente, NÃO ativar por padrão nem executar novos passes indefinidamente: priorizar o motor C# integrado e uma estratégia numérica mais estável quando justificável.

**Fora do escopo:** F072 certificado, liquidação financeira, Godot, combates, catálogo agrícola, orçamentos reais da partida e a escala definitiva da geografia Goldberg.

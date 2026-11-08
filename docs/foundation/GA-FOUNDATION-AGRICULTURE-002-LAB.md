# GA-FOUNDATION-AGRICULTURE-002 — laboratório de adequação agrícola

**Estado:** implementação isolada opt-in, perfis numéricos experimentais, sem ativação no jogo.
**Base:** GA-FOUNDATION-AGRICULTURE-001 e GA-FOUNDATION-000 (nova arquitetura).
**Escopo:** código próprio em `GlobalArena.Foundation.Agriculture`, testes e benchmark independentes. Sem referências a GlobalArena.World, Runtime, Simulation ou V2.

## Interfaces

- `CropCatalog`: seis IDs explícitos e chaves estáveis, na ordem milho, sorgo, trigo, arroz, soja, cevada.
- `AgriculturalNode`: temperatura normalizada [-1e6,+1e6], umidade [0,1e6], declividade [0,1e6] e classificação terrestre/água; não consome biomas ou disponibilidade hídrica.
- `AgriculturalLogicalSlope.Estimate`: indicador de declividade baseado na média das diferenças absolutas de elevação do nó para os vizinhos do grafo lógico (arestas uniformes, sem distância geodésica).
- `AgriculturalSuitability.Evaluate`: índice positivo de adequação para qualquer cultivo em qualquer nó terrestre; água = 0.
- `AgriculturalSuitability.EvaluateAll`: preenche seis resultados em `Span<int>` do chamador, sem alocação por nó e com ordem documentada. Resultados são números adimensionais, **não** volume colhido ou receita.

## Curva candidata

O avaliador usa notas térmica/hídrica lineares por distância ao ótimo e nota de declividade linear, ponderadas 45% / 45% / 10% e transformadas em índice terrestre entre 120.000 e 1.000.000 (escala 1.000.000). Esses números são **parâmetros de laboratório sem aprovação de balanço**; a escolha dos seis cultivos e o princípio de adequação contínua são as decisões canônicas.

A média ponderada evita multiplicar três penalidades em direção a zero. A declividade não utiliza altitude absoluta. O campo de temperatura deve incorporar os efeitos de altitude que couberem ao worldgen, sem reaplicá-los no avaliador.

Para preservar semântica contínua, o fixed-point pode produzir pequenos degraus inteiros de arredondamento, não mudanças macroscópicas associadas a biomas. O índice de um tile terrestre é sempre positivo, mesmo em extremos térmicos/hídricos.

## QA e benchmark

Executar sem compilar a simulação anterior:

```powershell
dotnet test .\GlobalArena.Foundation.Agriculture.Tests\GlobalArena.Foundation.Agriculture.Tests.csproj -c Release
dotnet run --project .\GlobalArena.Foundation.Agriculture.Benchmark\GlobalArena.Foundation.Agriculture.Benchmark.csproj -c Release
```

O teste de checksum congela uma amostra determinística de 1.024 nós avaliados nas seis culturas, e deve coincidir nas plataformas. Outras verificações incluem identidade estável, todos os cultivos positivos em terra, água sem lavoura, seis especializações regionais diferentes, ausência de cliffs por umidade/temperatura, declividade independente de altitude absoluta, permutação dos vizinhos e entradas inválidas.

O benchmark executa 10.000 e 100.000 nós com três repetições, produzindo CSV com milissegundos observados e checksum. **Nenhuma meta de desempenho foi ratificada**; tempos são observacionais e não justificam, isoladamente, promover a implementação.

O workflow `GA Foundation Agriculture Lab` executa testes no Ubuntu, Windows e macOS e benchmark no Ubuntu. Não presumir execução ou aprovação sem os respectivos resultados do Actions.

## Fronteira da próxima etapa

Para integrar ao motor, primeiro definir entrada dos atributos de nós lógicos do novo worldgen e calcular cache por nó/semear somente quando o mundo mudar. Depois validar a diversidade de mapas gerados e aprovar parâmetros numéricos. A conexão com trabalhadores, quantidades e mercados virá em gates separados, sem duplicar o item agregado alimentos nem multiplicar automaticamente a demanda.

**Não altera os contratos de tesouro, preços, estoques ou impedância logística.**

# GA-VS41 — Melhor itinerário de frete comercial com custo total previsto (laboratório)

**Data:** 31/10/2026. **Base:** GA-VS40 PR #146, head `159b7c3`, 7/7 workflows success. **Governança:** PR draft empilhado, `main` intacta.

## Problema

`EconomicFineTransportServiceItineraryDiscovery` é uma descoberta **BFS por menor número de arestas**, não por custo de frete. Na economia emergente, um caminho com mais trechos pode ser financeiramente preferível em função de distância real, subida e umidade, desde que exista uma **rede construída e um serviço comercial explicitamente oferecido**.

## Solução técnica optativa

`ExperimentalCostOptimalFreightItineraryDiscovery` implementa **Dijkstra de custo monetário geográfico exato**, sobre as travessias reais da rede fina Goldberg, com ativos comerciais já instalados, operacionais e acessíveis; cada serviço mantém `TechnologyTypeId` e `TechnologyCatalogVersion` explícitos. Não cria uma estrada, serviço, terminal ou tarifa não declarados.

### Prova do objetivo válido — restrita à VS40

O custo por grama de um itinerário experimental da VS40 é

```
C(path,q) = Σ geographicEdgeNumerator(e) / 1_000_000
          + serviceOverhead / (q + overheadSmoothing)
          + saturationWeight × q² / saturationScale²
```

Para **um serviço já declarado**, com modalidade/tecnologia, parâmetros e carga ex-ante fixos, as duas últimas parcelas são **constantes em relação ao caminho**. Logo, encontrar a menor soma geográfica por aresta encontra também o menor **preço completo** entre os caminhos elegíveis daquela **mesma tecnologia e serviço**. O termo `geographicEdgeNumerator` é uma função **compartilhada pelo mecanismo VS40 de preço completo e pelo Dijkstra VS41**, evitando divergências de arredondamento.

Isso **não se generaliza** automaticamente para preços arbitrários não aditivos, descontos condicionais, congestionamento que muda a cada rota, transbordo, mudança de modal ou escolha conjunta interdependente de múltiplos serviços. Essas situações exigirão solver de itinerário completo ou expansão de estado econômico noutro marco. Não alegar solução ótima geral além das condições explicitadas.

### Busca e determinismo

- Primeiro minimizar o custo monetário geográfico (BigInteger exato); em empate, preferir **menos arestas**; persistindo empate, o caminho com sequência lexicograficamente menor de IDs de ativos. Sem RNG ou números de ponto flutuante.
- Aresta com custo zero é válida no laboratório, mas **não cria ciclo vantajoso**: menos saltos desempata.
- A busca ocorre **uma vez por origem + tecnologia + versão do catálogo**, servindo múltiplos destinos e previsões de carga no mesmo snapshot, em vez de uma árvore inteira para cada par origem–destino.
- Diferentes tecnologias para a mesma relação podem produzir ofertas distintas; o `EconomicTransportServiceQuoteResolver` já existente escolhe a menor tarifa completa entre os serviços oferecidos e validados, conforme seus próprios critérios.
- O novo componente retorna as mesmas `EconomicTransportServiceOffer` de sempre; depois usa `ExperimentalGeographicFreightItineraryPricing` VS40 para confirmar independentemente o **custo completo do caminho escolhido**. Cotação monetária permanece congelada por turno econômico apenas via contratos existentes.
- O antigo BFS com `EconomicFineTransportPathCache` não é alterado, nem seus caches reinterpretados por objetivo diferente. Não reutilizar um cache BFS como se fosse ótimo monetário.

## Segurança / escopo de infraestrutura

- Qualificador de malha fina obrigatório; somente ativos construídos, operacionais e acessíveis sob `StrategicEdgeAccessSnapshot`, com tecnologia/versionamento declarados.
- Rede física e infraestrutura VS38/VS39 são consultadas separadamente, sem atribuir preços de frete a **pontos de movimento** e sem transpor água, mudanças de modal ou terminal fantasma.
- Nenhuma mutação do mundo, turnos econômicos, save/hash/replay V13–V17, consumo, ordens da UI ou escrituração financeira.
- Não autoriza bancos/crédito, contratação financeira, transportes aquáticos ou microgerenciamento.

## Medidas de processamento

`GlobalArena.Benchmarks` recebe o argumento:

```powershell
dotnet run --project GlobalArena.Benchmarks/GlobalArena.Benchmarks.csproj -c Release -- --vs41-freight-routing
```

Benchmark usa **grades sintéticas e reprodutíveis** de tamanhos 20², 32², 40², com 8, 32 e 72 destinos. Compara (a) **uma árvore de origem compartilhada** e (b) **uma busca isolada completa por destino**. Faz aquecimento + três amostras, coleta mediana de tempo e alocações por thread e verifica assinatura exata de caminhos/pesos. Métricas de operações são determinísticas: 1 árvore versus N árvores, sem limite de tempo que possa fragilizar o gate.

Esse benchmark **não reproduz automaticamente a densidade de redes comerciais reais**, nem mede abertura de mercado, distribuição de produtos ou custos de pré-cálculo do WorldGen. Medições de integração com ambientes econômicos populosos seguem recomendadas antes de habilitar política real. Não alegar ganho de CPU fora dos cenários medidos.

## Gates

1. No grafo unitário, rota de 3 trechos baratos vence rota de 2 trechos caros; empates lexicográficos e menor número de trechos estáveis sob reordenação de entrada.
2. Custos zero e BigInteger enorme sem caminho cíclico ou overflow.
3. 3 serviços mesma origem/tecnologia com diferentes destinos/cargas exigem **1 única árvore** e 3 preços completos.
4. Resolver comercial existente seleciona o menor frete entre **duas tecnologias reais e explicitamente oferecidas**.
5. Interrupção de infraestrutura, ausência de ativo, serviço indisponível ou versão não mapeada não cria conectividade gratuita.
6. Custo geográfico selecionado confere exatamente com preço recalculado independentemente VS40; não muta save/hash/journal.
7. Benchmark sintético verifica as mesmas assinaturas entre árvore compartilhada e buscas isoladas, e registra medianas observáveis.
8. CI Linux/Windows com xUnit e Godot, regressão integral Windows. Nenhum merge automático à `main`.

## Próximo planejamento

- Benchmark geográfico/econômico fim a fim em mundo grande com muitos polos e serviços concorrentes.
- Política multimodal explicitamente versionada com terminais/transbordo, custos fixos e preços do percurso inteiro.
- Somente após gates, ativação em partidas com feedback e ausência de micromanagement.

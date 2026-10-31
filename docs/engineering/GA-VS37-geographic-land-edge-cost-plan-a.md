# GA-VS37 — Custos geográficos terrestres por aresta, plano A experimental

**Data:** 31/10/2026. **Base:** GA-VS36 PR #142, 7/7 workflows aprovados. PR em draft e empilhada.

## Diretriz de produto

O jogador continua a selecionar uma unidade e um destino. O motor resolve caminhos sem microcomandos. A profundidade militar potencial do mapa é uma **oportunidade futura**, não uma autorização para transformar a jogabilidade em uma planilha tática. Recuperar fichas de massa e velocidade em níveis 1–5 foi levantado como hipótese, **sem implementação ou balanceamento neste PR**.

## Fonte física efetiva

`CanonicalGeographicLandEdgeCostProvider` implementa `IScalarPhysicalEdgeCostProvider` da VS36. Obtém exclusivamente dados da mesma geografia 01C do mundo atual: centros esféricos por tile, elevação procedural, umidade e tipo de vegetação. Nenhum mapa paralelo é gerado.

`GeographicLandEdgeCostCalculator` oferece cotação pura com itens separados:
- **Distância base:** corda entre centros sobre esfera unitária, arredondada para 1.000.000 unidades inteiras por raio e convertida em pontos. Não é uma distância em metros, não fixa o tamanho do planeta e não afirma representar exatamente a geodésica do arco. A corda é suficiente como aproximação local em células adjacentes.
- **Declividade aproximada:** diferença *direcional* dos valores de elevação dos tiles dividida pela separação entre centros; subir e descer podem custar valores diferentes. Altitude absoluta sem diferença relativa **não** é penalizada.
- **Solo/vegetação:** umidade elevada como aproximação de solo difícil, floresta como aproximação de obstrução. Aplica o **máximo** dos efeitos, não soma duas proxies parcialmente correlacionadas; evita dupla contagem arbitrária.
- **Rounding:** quantização de centros e soma percentual em inteiros, sem saldos de movimentação fracionários. `Math.Sqrt` é usado apenas como estimativa inicial da raiz quadrada de um inteiro; o resultado é corrigido por comparações inteiras exatas.
- **Limites:** parâmetros de elevação e umidade devem respeitar domínios fixos 01C; saída positiva, aritmética `checked`; somente arestas terrestres legais e adjacentes.

## Coeficientes experimentais, não canônicos

Escalas atuais *somente para laboratório*:
- 1 ponto-base para cada 1.000 milionésimos de raio da corda;
- sobrecarga de gradiente até +200% em subida; metade em descida;
- umidade acima de 600.000 gera até +20% de custo superficial;
- obstrução florestal +15%, **não aditiva** ao efeito de umidade.

Esses números não são calibrados em testes de experiência humana, em metros reais ou em fichas militares; podem ser substituídos mediante benchmark/ADR. A geografia procedural atual não é altitude topográfica em metros: chamá-la de inclinação real em graus seria incorreto.

## Arquitetura preservada

- VS37 apenas conecta a cotação geográfica **se o chamador optar por ela** na prévia de rota VS36. Nenhum `OrderLongDistanceTransit`, `AdvanceMovementTurn`, alimentação, compra, logística agregada, UI, batalha ou save/hash V13–V17 é alterado.
- A rota de menor custo projetada não é automaticamente o roteiro da viagem legada; não reinterpretar os saves antigos.
- Rios em arestas, pontes, estradas, veículos, montarias, carga, clima dinâmico, embarcações e estreitos são camadas posteriores. Especialmente para o mar, **não** confundir velocidade alta com conectividade aquática.
- A ficha militar de massa/velocidade 1–5 permanece hipótese para validar ergonomia e equilíbrio, e deve ser incorporada sem multiplicar comandos por unidade.

## Gates

1. Variação de distância física para arestas igualmente classificadas; diferenças de relevo geram custos diferentes sem cobrar altitude absoluta.
2. Subida e descida dirigidas; solo úmido e vegetação sem dupla contagem.
3. Dados inválidos rejeitados, cálculo repetível e resultados inteiros.
4. VS36 usa a política real para traçar uma rota sem mutar save, hash ou journal.
5. Oceanos indisponíveis independentemente de orçamento de pontos.
6. xUnit Windows/Linux, regressões VS35/VS36, build Godot. Proposta permanece draft até gates do mesmo commit.

**Próxima decisão antes de ativar movimento:** calibração por bioma e resolução, serviço de infraestrutura, perfil unitário e progressos gravados em save versionado. Não antecipar via constantes definitivas.

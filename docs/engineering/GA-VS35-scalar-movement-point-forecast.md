# GA-VS35 — Prévia escalar determinística de pontos de movimento

**Data:** 31/10/2026. **Base:** GA-VS34 PR #139 (stacked draft; gate V17 em fechamento).

## Diretriz de produto

O jogador seleciona a unidade, escolhe destino e emite uma ordem, como numa experiência simples de tabuleiro. O motor resolve múltiplas arestas, custos, prazos e futuro transporte sem exigir microcomandos. Profundidade econômica e logística **não** deve se transformar em micromanagement.

Conversa de desenho sobre mares, estreitos, embarcações artesanais e gradientes costeiros registrada em GA-DESIGN-NAV-001 / issue #140; **não implementar** naval, novos biomas ou travessias no marco atual.

## Implementação incremental

Adicionar somente o planejador **puro e não ativado** `GlobalArena.Runtime.ScalarMovementPointForecast`.

- Cada unidade terá, futuramente, uma capacidade inteira de pontos por turno; cada aresta física terá um custo inteiro positivo determinado por serviço de custo, sem expoentes de 2 obrigatórios.
- Um único comando pode percorrer uma sequência inteira de arestas. O orçamento de um mesmo turno continua disponível na aresta seguinte (não se perde ao completar um trecho).
- **Exemplo:** aresta com custo 250 e unidade com 200 pontos/turno termina no segundo turno, com **150 pontos disponíveis naquele turno**, não uma reserva permanente que possa ser acumulada ao ficar parada. A prontidão de combate é separada e **não inferida** do orçamento.
- Arestas curtas podem compartilhar o mesmo turno. A chegada geral é o teto de soma(custos)/pontos por turno. O cronograma guarda conclusão por trecho, pontos ainda livres no turno e total da rota.
- 100% inteiro e determinístico, operações `checked`, sem dependência de RNG, cadência econômica, distância arbitrária, classe de terreno ou UI.
- Nenhum tipo de tile aquático, custo de carga, produção, capacidade artesanal, eficiência militar ou regra de engajamento é presumido. Esses módulos serão injetados posteriormente, com regras explícitas.

## Compatibilidade / limitação

**Nenhuma viagem real usa o planejador VS35 por ora.** O jogo em V13–V17 mantém `PrimitiveFixedLegMovementTimeline` a dois turnos por aresta e todo save/hash/journal/replay sem alteração. Não adicionar controle de movimentação na interface, progresso subturno persistente ou formato de save. A ativação exige ADR de custos, integração com roteamento e versão de save específica, sobretudo quando há paradas e consumo econômico.

## Critérios de QA

1. Para uma unidade 200/turno com custo 250: chegada em 2 turnos, 150 pontos restantes **no turno da chegada**.
2. Custos 60+80 em um orçamento de 200 terminam no mesmo turno; a próxima aresta recebe o saldo.
3. Unidades com capacidades diferentes têm prazos diferentes na mesma rota, sem custo de tile fixo.
4. Entradas sem velocidade, sem percurso ou com custos zero rejeitadas; overflow rejeitado sem aritmética de ponto flutuante.
5. Previsão repetida gera o mesmo cronograma; as APIs de movimento físico legadas permanecem inalteradas.
6. Windows/Linux: xUnit completo e compilação Godot Debug/Release, sem mudança visual.

**Governança:** PR draft empilhado sobre #139. Não integrar `main` enquanto a cadeia e gates estiverem pendentes. A prévia VS35 serve como seam técnico, não como habilitação de movimentação escalar na jogabilidade.

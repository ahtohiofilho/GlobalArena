# GA-Next — Fonte física dos insumos econômicos dos trabalhadores (v11)

**17/11/2026 · incremento experimental sobre PR #382 · Draft sem merge · sem nova interface**

## Contexto de decisão

O jogador homologou a coluna do PR #382 como boa o suficiente no ciclo; o refinamento visual foi postergado. Retoma-se a função central do trabalhador: **um indivíduo físico produz quando está produzindo e demanda sempre**, inclusive durante deslocamento, serviço militar e outras atividades.

A fase de turnos físicos, realocações e journal v11 já existe. Entretanto, o laboratório de mercado flutuante é separado do `WorldState` e o gerador de atributos agrícolas autoritativos do GA-Next ainda não está pronto. Vincular agora coeficientes de cultivo, preço, receita ou cesta de consumo aos dados meramente visuais criaria regras sem validação. Este corte entrega **a interface matemática determinística de entradas reais** para os futuros mercados, não um mercado fictício.

## Implementação

- `FoundationPhysicalWorkforceEconomicProjector.Project(census)`: aceita **somente** censo físico autoritativo v11 com localização exata em tile Goldberg tático e ocupação explícita. v9/v10 ou censo ausente falham de forma explícita.
- **Pontos de demanda:** agrupa por `(tile físico, civilização)`; cada `PhysicalWorkerId` pertence a exatamente um ponto; soma o multiplicador de demanda em microunidades `ulong` por trabalhador, incluindo `Traveling`, `Military`, `Other`, `Producing` e pessoas com capacidade zero. Registra a divisão de atividades **sem multiplicar demandas**.
- **Oferta potencial de trabalho:** agrupa por `(tile físico, civilização, ocupação, CommodityId existente)`, somente trabalhadores com `Activity == Producing` e `OutputMicroPerTurn > 0`. Soma capacidade fixa da pessoa em microunidades com `checked`, não lança estoque, receita, lucro, preços ou produção comercial.
- **Identidade original preservada:** ocupação `Agriculture/Forestry/Mining` continua distinta de `CommodityId`. O cenário de seis trabalhadores ainda usa IDs de mercadoria experimentais; esta projeção **não** valida que o ID corresponde à profissão ou a um grão, madeira ou minério. É um dado bruto para consumo posterior quando o catálogo do mundo e os perfis físicos estiverem fechados.
- **Sem compradores artificiais:** o resultado registra **intensidade-base total da demanda dos trabalhadores**, não `D[categoria]`. As preferências positivas por todos os produtos e as intensidades por atividade precisam de uma etapa de calibração separada.
- **Determinismo:** entrada imutável, agregação por identificadores estáveis, classificação canônica e ordenação por tile/civ/ocupação/commodity. Todos os valores monetários ou de fluxo ficam fora deste PR. Overflow numérico lança exceção antes de publicar resultado incoerente.
- **Integração autoritativa:** `FoundationTacticalHumanAiTurnHost.GetPhysicalEconomicInputs()` calcula a entrada **exclusivamente a partir do último snapshot confirmado**; ordens pendentes, inclusive um lote do PR #380, não mudam o resultado. Depois do encerramento coletivo `E`, o próximo snapshot determina automaticamente o novo conjunto de entradas.

## Gates

1. Cenário real G(18,0) com seis trabalhadores por civilização: **12 pontos de demanda individualizados contabilmente, agregados em dois tiles**; 11 pessoas produzindo e 11 unidades de capacidade potencial (a 12ª está em deslocamento e continua demandando). Nenhuma pessoa duplicada.
2. Durante a preparação de mudança de ocupação e deslocamento em um mesmo turno, censo, hash, origem da demanda e oferta projetada permanecem inalterados. Após E e retomada da IA, mover #1 cria terceiro ponto de demanda na chegada, sem produção na viagem; mudar #4 de agricultura para mineração conserva a mercadoria e a capacidade já declaradas, alterando apenas o agrupamento profissional. Replay v11 coincide.
3. Trabalhadores com capacidade zero, atividade militar, viagem e `Other` produzem **zero** e demandam uma vez conforme multiplicador próprio. `Unassigned` não gera oferta.
4. **10 mil trabalhadores individuais**, agrupados em poucas localizações, conservam todos os IDs e microunidades, sem criar 10 mil mercados fictícios nem componentes Godot. Ordenação/permutação de entrada não altera o resultado.
5. v9/v10 rejeitados; censo físico v11 vazio resulta em conjuntos vazios sem mercado inventado; overflow da capacidade falha explicitamente.
6. Testes Windows/Ubuntu C# e regressão Godot 4.7.2 real permanecem aprovados, sem mudanças de cena ou interface.

## Próximo gate econômico (ainda NÃO implementado)

Consumir esses pontos e lotes num mercado de commodities com funções de demanda por atividade e sem preferência artificial de origem, separando mercadoria/profissão e atributos reais (temperatura, umidade e relevo) quando estiverem autoritativos. Conectar o clearing e as rotas ao grafo lógico, aplicar tarifa/custo por aresta, distribuir a receita pelos tesouros e preservar venda integral. Nenhuma dessas etapas é reivindicada pela presente projeção.

**Governança:** PR Draft empilhado sobre #382. Não inserir code-path no Godot para gerar insumos econômicos nem mesclar no `main` automaticamente.

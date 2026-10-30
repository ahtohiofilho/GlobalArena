# GA-VS20 — Povoamentos emergentes derivados de ocupação e economia

Data: 30/10/2026; base: GA-VS19C/PR #118. Incide somente em projeções de leitura e no Godot. Nenhum passo de fundação.

## Regra canônica, não uma cidade inventada
Uma célula física com **ao menos um trabalhador residente e produtor** é um núcleo habitado observável. A célula deixa de aparecer como povoamento quando o último trabalhador parte; seus estoques e dinheiro existentes **não desaparecem**. Viajantes não estabelecem um novo núcleo antes de chegar ao destino. Portanto:
- no turno 0 há um núcleo com oito trabalhadores (agricultura quatro, madeira dois, pedra dois);
- ao ordenar a viagem de dois trabalhadores, a origem reduz sua produção residente mas conserva a demanda alimentar dos viajantes até a chegada, sob regra V13;
- durante a viagem não existe núcleo no destino; quando a viagem termina, surgem dois núcleos, um com seis e outro com dois trabalhadores, sem ordem de fundação;
- após deixar o último trabalhador partir, o núcleo original deixa de ser mostrado, mesmo com saldo/estoques remanescentes; bens em local desabitado são discriminados no diagnóstico.

**Civilização e propriedade:** a identidade civilizacional existente desde o mundo V14 é associada aos núcleos. As contas seguem pertencendo a famílias/produtores locais, não a um tesouro estatal. A existência de núcleo não conquista um território inteiro, não implica reino, governo ou guarnição.

## Arquitetura
- `EmergentSettlementProjection.FromSession` consulta o snapshot físico canônico: alocações agrupadas por tile, contas reais `FoodCash`, demanda alimentar incluindo viagens de origem e estoques reais de alimento/pedra/madeira. Sites têm identidade de tile estável (não índice arbitrário); lista ordenada deterministicamente.
- Checa conservação: **residentes + viajantes = trabalhadores totais**; todos os grupos pertencem a uma das atividades possíveis; **dinheiro em núcleos + dinheiro em locais desabitados = emissão monetária inicial**. Conta os registros de estoques presentes em locais sem população, sem criar cidade invisível.
- No tático Godot `TacticalContextPreview.Settlements.cs`, cada núcleo aparece como **anel fino ocre**, centralizado no mesmo polígono físico já usado para trabalhadores, com altura superficial pequena e sem capturar clique. Anéis são redesenhados após mudança de chart/projeção e após movimentos; não escondem marcadores verdes/marrons/cinzas nem as viagens âmbar.
- No HUD operacional, uma linha mostra **quantidade de núcleos, residentes e viajantes**, e o tile de origem mostra trabalhadores por atividade, dinheiro local privado e estoques. Atelemetria aprofundada sobre contas/estoques em tiles desabitados permanece no painel recolhível, não no quadro de comandos.
- Não cria edifícios, cidades, novos itens ou demanda; não há modelo de densidade/duração/porte urbano ainda. A resposta visual é a consequência observável de produtores instalados em novas células.

## Persistência
- Novas partidas continuam em **V14**, antigos saves V13 continuam legíveis sem atribuição de civilização retroativa. **Nenhuma alteração em WorldState, journal, hash, formato, dinheiro, stocks, cadência, movimento ou duração**. Toda informação do núcleo é recalculada após restore V14 a partir do mesmo estado canônico.
- Sidecar de câmera VS18 intacto; voltar ao save do meio da viagem deve mostrar apenas o núcleo de origem, depois o segundo aparece ao chegar.

## QA / gate
1. Executar worktree VS20 usando `tools/launch-vs20.ps1`: compilação Godot `Debug`, jogo direto sem F5. Barra de título `Global Arena — VS20 | Povoamentos emergentes`, log `GA_VS20_RUNTIME_ACTIVE`.
2. Localizar povoamento inicial no tático: **um anel ocre** ao redor dos três marcadores de atividade, HUD 1 núcleo, 8 residentes. Cabeçalho/rodapé já removidos, mapa quadrado preservado.
3. Ordenar 2 trabalhadores de Agricultura para Madeira em tile adjacente: durante 2 turnos, destino sem anel e linha âmbar em trânsito; origem 6 residentes, 2 viajantes. Chegada → 2 anéis, 6 e 2 residentes, demanda e dinheiro local aparecendo no tile de destino ao selecioná-lo. Verificar preservação do total privado 320 créditos, sem controle territorial automático.
4. Salvar em V14 após primeiro turno, mudar zoom/pan e avançar até chegada; reabrir e conferir **1 núcleo, grupo em trânsito, mesma câmera do instante do save**; avançar até chegada para recriar 2 núcleos.
5. Reabrir um save V13 legado: indicadores de núcleos continuam existindo, mas identidade da civilização não é inventada.
6. CI Windows/Linux: casos de núcleo inicial, trânsito, chegada, abandono do tile, preservação de bens/contas em local desabitado, replay V13/V14, compilações Godot Debug/Release, launcher PowerShell.

**Fora do escopo**: vários povos iniciais, organização política, soberania emergente, transporte comercial, serviços públicos, construção, facções, agricultura melhorada, infraestrutura, impostos, crédito e guerras. Mantêm-se decisões econômicas vigentes e congeladas. PR draft empilhada sobre VS19C, sem merge da main.

# GA-ECON-REBASE-12 — Ciclo físico completo visível na UI tática

**Base homologada:** GA-ECON-REBASE-11, PR #259, HEAD `f59390d01a5ad0bc62831d63610edd12452136c6`.
**Objetivo:** sair da sequência de incrementos de kernel sem visibilidade e entregar um ciclo **interativo no Godot** que o jogador consiga conferir diretamente no tile selecionado.

## Escopo / decisão de governança

Os incrementos REBASE-08..11 já compõem um subsistema coerente: a população faz trabalho primitivo sem instalações; produção e consumo realizam-se no fechamento econômico; estoques físicos são mantidos; madeira/pedra podem ser debitados e incorporados; pessoas efetivamente alocadas trabalham na construção durante um ciclo, sem produzir na atividade de origem, mas continuam consumidoras; instalações concluídas são ativos no `WorldState` e podem ser operadas; produção assistida compõe parte exata da produção real.

O `GA-ADR-ECON-007` **não autorizou coeficiente de produtividade adicional para instalações**. REBASE-12 não criará multiplicadores hipotéticos. Em vez disso, estabelecerá o **marco observável de ponta a ponta**. A UI deve mostrar **evidências reais da mudança dos estados**, não prometer ganhos que ainda não existem.

## Novo painel tático Godot (primeiro marco visual)

O painel `GAWorksiteCockpit` é incluído na **barra direita já rolável**; não adiciona painéis acima ou abaixo da malha, nem reduz o viewport quadrado. A linha compacta `GAWorksitePulse` fica junto aos indicadores centrais. O jogador vê no tile/atividade selecionados:
- moradores, pessoas efetivamente alocadas, madeira e pedra **físicas**, e número global de instalações/obras/operações;
- fase explícita `SEM INSUMOS → INSUMOS DISPONÍVEIS → MONTAGEM → CONSTRUÇÃO → CONCLUÍDA → OPERAÇÃO`;
- ordinal de montagem quando existe, equipe reservada, conclusão no turno do fechamento efetivo, operadores ativos;
- medição de produção física assistida **apenas quando houver comprovante válido do último fechamento econômico e para a mesma atividade/tile**. A UI identifica explicitamente que a quantidade assistida já está dentro da produção normal e **não** representa receita ou aumento de produtividade.

Os comandos funcionais não possuem efeitos próprios: invocam o `CanonicalPhysicalTacticalSession` **da partida ativa**:
1. `1 · MONTAR`: madeira/pedra em quantidades físicas escolhidas pelo jogador no SpinBox; os padrões visuais de 1 unidade **não são uma receita canônica**; a ação é desabilitada se não houver stock ou pessoas.
2. `2 · CONSTRUIR`: seleciona a montagem já incorporada e reserva uma quantidade válida de trabalhadores do grupo produtivo no tile; a conclusão depende do **próximo fechamento econômico real**, acionado pelos botões `AVANÇAR 1 CICLO` já existentes.
3. `3 · OPERAR`: vincula pessoas reais ao abrigo já concluído, até a capacidade construída; um ciclo econômico posterior fornece o comprovante de trabalho assistido.
4. `PAUSAR OPERAÇÃO`: libera operadores ao trabalho primitivo sem demolir instalação, sem restituir materiais.

As regras e a interface usam a atividade selecionada no seletor de origem já existente. Nenhum botão funda uma cidade, cria caixa, trabalhadores, instalações gratuitas, produção extra, receitas ou saldo `N`. Em caso de insumo ou pessoal insuficiente, a opção é indisponível e o estado preservado.

## Projeção do núcleo e testes

`CanonicalPrimitiveWorksiteTacticalProjection.Read(session, tile, activity)` é pura e read-only: consolida os eventos de montagem, instalações do `WorldState`, alocações/estoques físicos, último ciclo, site e comprovante vigente `CanonicalFacilityAssistedWorkWitness`. Não usa parâmetros do renderer ou Godot. O retorno registra `PrimitiveWorksiteStage` e dados suficientes para renderizar a timeline sem converter previsões em produção.

`GaVs121TacticalWorksiteVisibilityTests` valida:
- consulta sem qualquer mutação de hash, revisão ou save;
- sequência integral montagem / construção / conclusão temporal / operação;
- só expõe produção assistida de um fechamento efetivo; invalida leitura da produção anterior após novo evento;
- dados idênticos no replay de saves em obra, concluído e operado;
- seleção de outra atividade/tile não herda ficticiamente a instalação, embora a soma global permaneça correta.

## Experiência de validação visual a executar no Windows

No Godot Mono: abra o **mapa tático**, selecione o tile ocupado e a atividade; avance ciclos até obter madeira e pedra. Em **CONSTRUÇÃO E PRODUÇÃO**, ajuste quantidades para valores presentes em estoque, clique **1 · MONTAR**, depois **2 · CONSTRUIR**. Verifique o estado **EM CONSTRUÇÃO** e a redução temporária da produção, avance um ciclo, observe **ABRIGO CONCLUÍDO**, clique **3 · OPERAR**, avance um ciclo e confirme a produção atribuída. Salve e reabra, verificando mesma instalação e tile. Clique **PAUSAR OPERAÇÃO** e confira a liberação dos operadores.

A informação de estado deve permanecer **na lateral**, sem regredir a UI com cabeçalhos ou rodapés altos. Importante: **CI de compilação Godot não substitui homologação visual com screenshots da execução local**. O PR só pode ser considerado visualmente aprovado com a verificação do usuário.

## Próximas fases

1. **GA-UI-TACTICAL-FACILITIES-02:** marcadores discretos de obra/conclusão/operação sobre os tiles do mapa tático (alinhados à superfície), quadro resumido de produção por tile e histórico comparável de ciclos. É a prioridade visual após o primeiro painel; não acrescentar novos blocos econômicos desconectados.
2. **ADR de produtividade instalada:** calibrar a função `Q(L,G,K,I,T)` com regras explícitas de manutenção, ganhos e capacidade por tecnologia. Sem fórmula aprovada, a diferença física contabilizada entre abrigo operado e trabalho rudimentar deve ser **zero**; não simular renda extra.
3. **N:** permanecer abstrato e não emitido até produção final real e preços-base autenticados, sem crédito fictício.

**Gates:** FAST Godot + testes direcionados, seis etapas reais (kernel e hash: Linux, Windows e macOS Intel x64), benchmark e todos os workflows do HEAD corrente; **homologação visual adicional** em Windows/Godot. macOS ARM64 nativo permanece pendente.

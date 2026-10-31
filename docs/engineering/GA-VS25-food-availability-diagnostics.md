# GA-VS25 — interpretar oferta física e consumo alimentar sem ampliar a contabilidade

Data: 31/10/2026. Base: GA-VS24 / PR #124, homologada no Windows. Incremento de **leitura estratégica**, não de alteração da política econômica.

## Problema de jogabilidade
O HUD até a VS24 mostra consumo, falta alimentar e estoques. Não permite, porém, distinguir os motivos *observáveis* de uma demanda não atendida. Estoque final igual a zero não prova inexistência de produção; pode significar que a comida produzida foi integralmente consumida. E a existência de alimentos disponíveis não obriga o mercado vigente a absorvê-los.

## Produto
No tile físico selecionado, clicar **DIAGNÓSTICO ALIMENTAR** ao lado do comparador VS24, sem avançar a partida. Uma linha condicional informa o próximo ciclo econômico já previsto pelo relógio do save.

- **Estoque inicial + produção física = oferta disponível no próximo ciclo**;
- **Demanda alimentar efetiva** no tile no turno de liquidação (incluindo chegadas de viajantes);
- **Consumo efetivo** vendido/entregue no mercado local;
- **Falta física**: demanda acima de toda a quantidade física disponível;
- **Demanda não atendida apesar de oferta**: parte que seria fisicamente atendível, mas não foi consumida na liquidação real vigente;
- **Estoque final** após aquele ciclo.

A segunda categoria não é rotulada automaticamente como `falta de dinheiro`, porque a venda e os preços atuais são produzidos por um solver de mercado. A mensuração **não discrimina razões individuais**, e não cria fatos inexistentes sobre orçamento, salários ou PIB. As duas parcelas de demanda não atendida são disjuntas e totalizam a falta observada.

## Projeto técnico
`PhysicalFoodAvailabilityProjection.PreviewNextCycle(session, tile)` exporta o save canônico atual, restaura uma **sessão descartável** e executa `PhysicalEconomicCycleProgression.AdvanceToNext`. Lê a demanda do tile após o ciclo (para abranger chegadas), recibos de alimento da liquidação `FoodCash.Receipts` exatamente naquele turno/tile e os estoques antes/depois.

Como **a versão atual ainda não possui transporte físico de alimentos**, o balanço local exato do ciclo fica:

`oferta = estoque final + consumido = estoque inicial + produzido`.

`fisicamente atendível = min(demanda, oferta)`.

`falta física = demanda - fisicamente atendível`.

`não consumido apesar da oferta = fisicamente atendível - consumido`.

As identidades são validadas em aritmética inteira, com rejeição explícita de balanços impossíveis. A versão direta do mercado anterior ao V12 não possui recibos por tile; para ela, o diagnóstico V25 declara suporte indisponível, **sem inventar quantidades**. Isso não afeta salvar/reabrir V9/V13/V14.

## Governança e desempenho
- Projeção acionada **somente pelo clique**, nunca por frame ou redesenho automático. Não altera o hash, o journal, o turno, a população, os estoques ou as contas da sessão real.
- O botão do diagnóstico e o comparador VS24 dividem a mesma linha do HUD para preservar o viewport quadrado e reduzir espaço vertical. Apenas um dos dois resultados permanece aberto; qualquer atualização de estado esconde prévias antigas.
- Não altera a cadência adaptativa atual nem a duração da viagem; a cadência absoluta permanece congelada na issue #121.
- Sem mudanças no mercado V12, nos 320 créditos experimentais, no significado de preço ou na demanda macroeconômica: diretrizes #110/#111 e #103 permanecem próprias de futuros ciclos.
- Não adiciona campos de save, evento, controle político, infraestruturas, mercado entre tiles ou novas fontes de consumo.
- PR draft encadeada à VS24; nenhuma integração isolada em `main`.

## Gate Windows
1. Abrir `tools/launch-vs25.ps1` com worktree VS25; conferir runtime **Global Arena — VS25 | Diagnóstico alimentar** e marcador `GA_VS25_RUNTIME_ACTIVE`.
2. Inspecionar povoamento inicial (8 trabalhadores, 4 agricultura, 2 madeira, 2 pedra); clicar **DIAGNÓSTICO ALIMENTAR**. Sem avançar a partida, deve mostrar **turno 1**, oferta, demanda, consumo, falta física, eventual demanda não atendida apesar de oferta e estoque final. A soma de componentes deve reconciliar com a demanda.
3. Conferir que a partida real ainda está no **turno 0, ciclos 0, oito trabalhadores e 4/2/2**.
4. Avançar **1 ciclo**; conferir que o consumo e a falta do ciclo correspondem às quantidades da prévia no mesmo tile, ressalvados arredondamentos visuais em duas casas.
5. Mudar dois trabalhadores de agricultura para madeira (2/4/2); consultar novamente. A oferta de alimento por produção deve cair e as duas categorias de falta devem refletir o motor existente. Comparador de cinco ciclos VS24 continua disponível.
6. Salvar V14, avançar, reabrir, conferir distribuição, turno, ciclo, câmera e que prévias hipotéticas não são persistidas.
7. CI Linux/Windows: novos testes VS25 e regressões VS24–VS20; chegada em trânsito, fronteira 30→32, V13, legado direto V9, equivalência física de recibos e estoque e compilação Godot Debug/Release.

## Restrição de escopo
Uma análise de **cobertura física versus consumo realizado** é legítima no modelo atual. Transformá-la em explicação causal completa sobre renda, salário, bancos, crédito ou mercados multicommodity não é objetivo da VS25.

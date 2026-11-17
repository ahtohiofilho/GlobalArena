# GA-Next — Gate 01B.2D-10: resultado explícito de convergência e stress Goldberg ampliado

**17/11/2026 · C# de pesquisa opt-in · PR Draft empilhado sobre #402 · sem merge e sem alterações no Godot**

## Baseline homologada e pendência anterior

O PR #402 reproduziu em Windows/Ubuntu o caso adversarial de 8 produtores do mesmo grão, 104 mercados demandantes reais e 112 tiles Goldberg conectados, com conservação, potencial crescente e rejeição segura de orçamento. O teste severo admitia corretamente dois finais: **passagem completa sem ganho** ou **orçamento esgotado**. No entanto, o `ITestOutputHelper` não oferecia, no log resumido, uma comprovação inequívoca de **qual ramo ocorreu**, inviabilizando contabilizar o caso severo como convergência efetiva. O Gate 01B.2D-10 torna essa distinção parte do contrato de CI.

O #402 estava com C# (1.929 testes Windows/Ubuntu), Godot e benchmark histórico aprovados no último status verificado; os jobs macOS de StateHash e Kernel ainda estavam na fila. Não há merge.

## Contrato da telemetria

Os próprios testes, **somente depois de verificar os invariantes físicos, a monotonicidade e a integridade de host**, geram registros JSON por cenário caso a variável opcional `GA_JOINT_STRESS_REPORT_DIR` esteja definida. Sem a variável, uma execução local de testes não grava arquivos. Nenhuma rota, identidade pessoal do jogador, estoque, tesouro, lucro ou ciclo econômico é publicada.

Os três arquivos esperados têm nomes fixos:
- `eight-sellers-104-markets-mild.json`;
- `eight-sellers-104-markets-severe.json`;
- `sixteen-sellers-192-markets-scale.json`.

Cada registro inclui versão de schema, cenário, **resultado literal** `BLOCK_STATIONARY` ou `BUDGET_EXHAUSTED`, número de produtores, mercados e tiles testados, unidades de oferta total, escala de capacidade por aresta, orçamento e passagens completas, total de propostas aceitas e recusadas (com subtipos **nulos quando desconhecidos**), duração observada do runner e série de potenciais econômicos **apenas dos estados aceitos**. A marca `CandidateSettledEconomically=false` é obrigatória.

O script PowerShell `scripts/GA-Next-Joint-Stress-Report.ps1` valida os **três** resultados e seus parâmetros, incluindo tamanho da série de potenciais, crescimento estrito `BigInteger`, budget, ausência de liquidação e resultado obrigatório `BLOCK_STATIONARY` no cenário moderado. Se falta um arquivo ou a validação falha, o job é reprovado. Em caso válido, imprime o resultado literal e escreve tabela no `GITHUB_STEP_SUMMARY`, com tempo, número de passagens e mercado; a CI Windows e Ubuntu executam o mesmo contrato.

**Interpretação estrita:** `BLOCK_STATIONARY` não é equilíbrio econômico global nem Wardrop; é apenas ausência de ganho nos dois operadores de proposta do #401. `BUDGET_EXHAUSTED` é **não homologação da convergência para aquele orçamento**, mesmo quando a história aceita é estritamente crescente. Não existe aprovação de plano parcial.

## Sondagem de escala ampliada

A regressão original de 8/104/112 permanece **idêntica**. O novo ensaio independente cria outro mundo físico Goldberg **G(5,0)**, seleciona 208 tiles conectados por arestas verdadeiras e instala:
- **16** trabalhadores produtores de um mesmo grão, cada um ofertando `1_000_000` micro-unidades;
- **176** trabalhadores demandantes adicionais, totalizando **192 mercados reais**;
- conversão física de carga idêntica à baseline, custos livres de `100_000` por aresta e capacidade de referência declarada de `10_000_000` micro-carga;
- até **3 passagens completas**, **4096** aumentos de fluxo por proposta, 4 segmentos médios por mercado, duas parcelas por entrega e **8192** parcelas máximas.

O ensaio aceita somente os dois estados formalmente distintos acima. Qualquer outro erro, divergência de oferta, frete, topologia, propriedade, orçamento intermediário ou auditoria reprova o teste. Se converge, devolve plano conservado e integralmente auditado; se esgota o budget, devolve **apenas a exceção**, sem alocação parcial. Em ambos os casos exige snapshot/StateHash inalterados e `NoClosedCycle` das duas civilizações.

O tempo de execução é **telemetria descritiva**, não uma restrição arbitrária de milissegundos; a comparação com 8/104 é diagnóstico preliminar, não benchmark estatístico padronizado (hardware e load de CI variam). A ampliação para 16/192 ainda está muito abaixo de milhares de produtores e não pode validar extrapolação linear.

## Integração e governança

Mudanças restritas ao fixture de teste adversarial, publicação de diagnóstico em script PowerShell, workflow de C# e documentação. **Nenhuma alteração** no modelo econômico, função de preço, concentração de carga, tarifa, algoritmo do #401/#402, infraestrutura, StateHash, `main` ou Godot.

Este gate somente será homologado após verificar que a **CI principal**, o **script de resumo** e as regressões multiplataforma terminam corretamente. Draft empilhado sobre #402, sem merge.

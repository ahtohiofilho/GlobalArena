# GA-Next R156 — Publicação de evidência econômica numérica

**Status:** relatório de pesquisa sobre R155, sem modificação da economia autoritativa.

## Entrega

A R155 já calculava o diagnóstico, mas o `ITestOutputHelper` do xUnit ficava oculto nas execuções normais que passavam. A R156 torna a evidência de uma execução real da comparação A/B/C **inspecionável sem abrir o Godot**:

- `.github/workflows/ga-next-r156-economic-diagnostic.yml` executa especificamente `QuantitativeTransferAndLogisticsEffectsReconcileWithoutAssumingProfit` em .NET 10;
- o teste escreve `r156-economic-diagnostic.json` com o identificador de cenário, seed, regra de transferência, parâmetros de referência, valor inteiro exato de `MoneyScale`, volumes, receitas, frete, impostos, créditos e diferenças econômicas;
- `r156-economic-diagnostic.md` mostra uma tabela de A, B e C e a decomposição do custo de oportunidade, benefício operacional e saldo combinado;
- o workflow apresenta o Markdown no **GitHub Job Summary**, imprime o JSON e o Markdown nos logs e publica ambos como **artifact**;
- a execução falha caso os arquivos esperados não existam ou estejam vazios.

Todos os valores representam um mesmo cenário determinístico com trabalhadores reais, infraestrutura física imutável, geografia G18 assinada, mercado normalizado e uma realocação `Producing → LandTransport`. Não são previsões da economia futura nem métricas da população real do jogador.

## Conservação monetária

A medida agregada `GrossSales - PhysicalFreight` corresponde ao crédito econômico total das civilizações, sem contabilizar impostos ou pedágios transferidos entre elas como perda líquida global. Também são mostrados `NetSellerReceipts` para separar receita individual do vendedor de ganho coletivo.

Os valores monetários do JSON permanecem **inteiros decimais de precisão exata**, sempre acompanhados de `moneyScale`. O Markdown exibe os valores monetários convertidos para moeda com até 16 dígitos fracionários, sem float.

## Gate de aceitação

- Workflow verde, relatório JSON e Markdown publicados sem valores inventados;
- análise de `A→B`, `B→C`, `A→C` e lacuna para equilíbrio feita sobre o resultado **produzido pelo teste**, não sobre uma hipótese;
- nenhuma alteração em oferta, consumo, workers, frete autoritativo, caixa ou journal;
- **realocação e nascimento continuam decisões distintas**. O teste R156 ainda não compara a criação legítima de um novo trabalhador com a alocação inicial dele.

Na sequência, inspecionar os números reais da comparação e avaliar se a fórmula de frete sombra encontra ganhos econômicos relevantes, sem precipitar sua integração ao mercado ou calibrar parâmetros para forçar a vitória de um cenário.

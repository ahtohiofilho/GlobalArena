# GA-Next — R155: diagnóstico quantitativo de realocação e serviço logístico

**Status:** leitura matemática sobre os três mercados contrafactuais R154; não altera o mercado autoritativo, não cria trabalhadores, não aplica descontos de frete e não comanda realocações.

## Objetivo desta etapa

Distinguir quantitativamente:
- **A→B:** efeito de retirar trabalhadores existentes da produção e destiná-los à logística, com o mesmo frete fixo;
- **B→C:** efeito experimental de alterar o frete devido ao serviço logístico já calculado para os trabalhadores de B;
- **A→C:** saldo combinado, sem pressupor que a logística compense a produção perdida.

O diagnóstico reporta deltas exatos de produção vendida, entregas, arestas utilizadas, carga transportada, receita bruta, frete físico, tarifas, impostos e receitas líquidas dos vendedores.

### Critério econômico coletivo

Além de receitas líquidas dos vendedores, há o indicador **equivalente total de crédito às civilizações**, em escala monetária racional exata, definido como:

`CreditoTotalEquivalente = ReceitaBruta - FreteFisico`

Esse indicador evita subtrair impostos e tarifas **duas vezes** no agregado: impostos e pedágios redistribuem receita entre civilizações, enquanto o frete físico é um custo operacional. O indicador **não é** caixa já creditado, **não** atribui a uma civilização específica o resultado e **não** considera eventual despesa para construir ou manter infraestrutura fora do contrato atual.

O ganho B→C representa o benefício simulado do desconto logístico; a perda A→B representa o custo de oportunidade econômico de realocar trabalhadores existentes. O resultado A→C é a soma algébrica dos dois.

`GanhoNecessarioParaEquilibrio = max(0, -(Credito_B - Credito_A))`

`LacunaParaEquilibrio = max(0, GanhoNecessarioParaEquilibrio - (Credito_C - Credito_B))`

Se a perda A→B for positiva (realocação já vantajosa antes do desconto), o ganho necessário é zero. A classificação `Worse / Equivalent / Better` é baseada **somente** no sinal do saldo combinado total. Ela não é um comando automático para o jogador, nem uma conclusão de que cada civilização ficou melhor.

Um teste integrado produz uma linha `GA_NEXT_R155_ECONOMIC_DIAGNOSTIC` com os números **reais do cenário de homologação**. Não preencher relatório com valores inventados: os números só devem ser interpretados depois de aparecerem em execução de teste aprovado.

## Dois problemas de alocação diferentes — regra arquitetural explícita

**1. Transferência de um trabalhador existente (realocação):**
- a população total e o conjunto de trabalhadores permanecem **iguais**;
- ele deixa de produzir na atividade anterior e passa a gerar produção ou serviço na nova;
- a demanda de consumo daquele trabalhador **continua existindo**, no nó físico correspondente;
- avalia-se `EconomiaComTransferencia - EconomiaAntesDaTransferencia`.

**2. Entrada de um novo trabalhador (crescimento populacional/nascimento econômico):**
- o conjunto de trabalhadores aumenta de forma autoritativa e legítima;
- o trabalhador novo traz **nova oferta potencial de esforço**, nova demanda positiva de consumo e localização física inicial válida;
- não existe custo de oportunidade automático de produção anterior, pois ele **não estava empregado em A**;
- avalia-se `EconomiaComNovoTrabalhadorNaAtividadeX - EconomiaSemEsseTrabalhador`, com o novo consumo considerado;
- o sistema nunca deve usar a mera troca de ocupação de um indivíduo antigo como simulação de nascimento.

Os dois casos poderão compartilhar motor de mercado, função de avaliação marginal e capacidade de classificar alternativas elegíveis, mas exigem **construtores de cenários e verificações de conservação diferentes**. A R154/R155 implementa **somente a modalidade 1**. O nascimento econômico ainda não está implementado e não é autorizado implicitamente por este documento.

## Limitações e decisão seguinte

- A comparação é um exercício **contrafactual de curto prazo**, com um trabalhador deslocado, e não a maximização global da economia.
- Ela não demonstra automaticamente viabilidade do investimento: o capital pode gerar efeitos pelo desconto autoritativo atual, pela distribuição de trabalho, pela produtividade e pelo desconto experimental. É preciso avaliar retorno total para evitar atribuir o mesmo ganho duas vezes.
- Não há congestionamento ou frete dependente de carga.
- O usuário não precisa abrir Godot para este gate. Primeiro homologar CI e extrair os números do cenário de testes; depois decidir se devemos calibrar os parâmetros ou ampliar o teste a situações em que o trabalhador possui mais opções.

# GA-Next R153 — Gate econômico de cotação fixa, sem intervenção no mercado

**Status:** comparação contrafactual read-only. Nenhuma cotação desta etapa é aceita pelo fechamento econômico autoritativo.

## Por que este gate existe

R145–R152 já definem trabalho real, localização em nós físicos, distribuição proporcional à infraestrutura, esforço conservado, serviço por ligação e produtividade com capital confirmado. A economia autoritativa atual já reduz o frete unitário por investimento através da fórmula `c(I) = c0/sqrt(1+I/B)`; a oferta comercial ainda é resolvida com fretes **fixos antes do water-filling**. Introduzir um segundo desconto de frete ligado ao trabalho exige garantir que o mercado não receba cotação baseada em sua própria carga.

A R153 cria um **shadow quote** estritamente unidirecional:

`mundo autoritativo → esforço operacional → serviço operacional → cotação alternativa fixa`

Não há caminho de retorno `carga/receita/mercado → produtividade ou cotação` neste incremento.

## Fórmula de comparação, não canonizada

Dado o frete unitário `C` já calculado pela infraestrutura existente, o serviço físico `S` da aresta (R150–R152) e uma referência positiva explícita `R` para sua classe LAND ou PORT:

`C_sombra = max(1, floor(C * R/(R+S)))`

A R153 **não afirma** que esta é a fórmula final de frete. O parâmetro R não é escala de congestionamento, limite de carga, estoque ou orçamento de capacidade. Trata-se apenas de uma forma bounded e monotônica de examinar como o trabalho poderia reduzir o custo unitário. A aritmética é inteiramente determinística e utiliza `BigInteger`. Os parâmetros `R_LAND` e `R_PORT` precisam ser informados explicitamente; sem serviço operacional real (`S=0`), não há desconto adicional, independentemente do capital.

## Invariantes obrigatórios

- O frete autoritativo de entrada `C` não é modificado; a cotação comparativa é retornada separadamente.
- Arestas marítimas ou outras sem serviço LAND/PORT permanecem com o custo natural autoritativo.
- Tarifas externas, posse, identificação da aresta e localização física permanecem exatamente iguais.
- Todo serviço positivo precisa se referir a uma aresta real de frete fixo; duplicações e arestas ausentes são rejeitadas.
- Não há qualquer argumento de cargo, volume usado, receita, preço de mercado, congestionamento ou itinerário nas funções de comparação.
- A R153 não cria um operador onde não há trabalhadores, não distribui capital novamente e não modifica estado, diário, rodadas, caixa ou UI.

## Decisões ainda abertas antes do mercado real

1. **Unidade física de serviço:** serviço por turno é capacidade de operação ou eficiência de movimentação? Evitar converter mecanicamente em limite por volume.
2. **Dupla incidência de capital:** `C` já possui o benefício direto do capital; a produtividade e a alocação operacional também crescem com investimento. Esses canais são distintos no protótipo, mas o ganho econômico combinado precisa ser calibrado contra um mesmo baseline para não gerar benefício excessivo.
3. **Benefício sem trabalho:** a malha natural tem frete positivo e pode operar na ausência de trabalhadores explicitamente destinados à logística; a nova função não pode bloquear retrospectivamente esse transporte mínimo.
4. **Escala econômica:** aferir a importância da menor produção (trabalhadores retirados de minas/fazendas) contra qualquer benefício logístico; evitar um incentivo a realocar todos os trabalhadores para uma atividade apenas.
5. **Replay integral:** uma eventual habilitação no mercado deve registrar política de referência e conferir os hashes/replay em Linux, Windows e Godot, com gates de tráfego e receita explícitos.

## Limite do escopo R153

Este incremento **não chama o otimizador de mercado com preços sombra**, nem os divulga ao jogador como tarifas reais. Apenas calcula uma proposta de cotação por aresta sobre dados físicos imutáveis, testando monotonicidade, independência de carga, zero trabalhadores e preservação de tarifas. O gate seguinte pode comparar resultados econômicos em uma execução totalmente não autoritativa, sem aplicar a proposta ao fechamento real.

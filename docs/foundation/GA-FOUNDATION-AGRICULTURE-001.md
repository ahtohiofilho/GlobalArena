# GA-FOUNDATION-AGRICULTURE-001 — Seis cultivos e adequação agrícola contínua

**Estado:** decisão de desenho ratificada; fórmula e calibração pendentes.
**Data:** 2026-11-08.
**Integração:** complemento de GA-FOUNDATION-000, especialmente F-002 (potencial produtivo do nó), F-003 (família alimentos), F-011 (lei quantitativa de produção), F-023 (mercado abstrato) e F-081 (grafo lógico unitário).

## 1. Catálogo canônico

São **exatamente seis cultivos iniciais**: **milho, sorgo, trigo, arroz, soja e cevada**. A soja é uma leguminosa incluída intencionalmente no conjunto de grãos alimentares do jogo, sem afirmação de que seja um cereal botânico.

A escolha preserva os seis nomes recuperados das deliberações anteriores. Na versão histórica, a especialização aproximada associava pradaria a trigo, floresta a arroz, estepe a soja e deserto a sorgo; esse mapeamento é **proveniência**, não regra executável. Nenhuma exclusividade cultivo–bioma é herdada.

**Alimentos** continua sendo a família econômica abrangente de F-003. Os seis cultivos são identidades potenciais de mercadorias **dentro dessa família**, e não sete produtos simultâneos (seis grãos mais um agregado duplicado). A ativação de mercados e demanda individualizados deve reconciliar explicitamente o contrato econômico vigente; não multiplicar automaticamente a demanda basal apenas porque o catálogo foi desagregado.

## 2. Fonte física da adequação

A produtividade específica de cada cultivo em cada nó deriva de atributos físicos **contínuos**, sem chaveamento por biomas discretos:

- **Temperatura:** adequação térmica própria da cultura.
- **Umidade:** atributo hídrico canônico do nó, diretamente utilizado na agricultura.
- **Relevo/declividade:** condição geométrica local derivada das diferenças de elevação e vizinhança do grafo lógico.

**Altitude não é penalidade agrícola independente por padrão** quando seu efeito térmico já está representado na temperatura. O relevo deve refletir declividade/dificuldade local, não uma segunda punição genérica à altitude. A geometria esférica de renderização não cria modificadores agrícolas por extensão geodésica da aresta; F-081 preserva a identidade e conectividade lógica.

A versão precedente também representava **disponibilidade de água** como campo derivado de atributos físicos para hidrologia. Essa estrutura pode continuar existindo; a agricultura consome **umidade** e não exige um quarto campo hídrico nem nova amostragem aleatória. Se houver irrigação futura, seu impacto deve ser explícito e não contar duas vezes umidade/rios.

**Fertilidade do solo não é atributo obrigatório desta fase.** Só adicioná-la se experiências com mapas demonstrarem ganho de jogabilidade, não para preencher uma lista de fatores agronômicos.

## 3. Cultivabilidade e especialização

- Todos os seis cultivos são possíveis em **todos ou quase todos os nós terrestres**, com **produtividade diferente** por cultura e local.
- Condições muito desfavoráveis **não implicam proibição automática**. O modelo buscará um piso produtivo positivo nos terrenos terrestres ordinários; exceções raras de inviabilidade física deverão ser expressas separadamente, não inferidas de classificação de bioma.
- Nós aquáticos não têm produtividade agrícola de plantio terrestre.
- Adequações são contínuas, com sobreposição: dois ou mais cultivos podem ter elevada adequação no mesmo nó. Não dividir o clima em uma grade rígida 3×3.
- Os seis perfis precisam oferecer **vantagens comparativas territoriais reais**, porém nenhum deve ser globalmente superior aos demais sob todas as condições.
- A escolha econômica entre produzir localmente ou adquirir de outra localidade emerge de rendimentos, preços e impedância logística; o atributo físico não fixa preços nem cria comércio obrigatório.

**Perfis de intenção (não parâmetros numéricos ratificados):**
- Arroz: relativamente quente/úmido, favorecido por terreno plano.
- Sorgo: relativamente quente/seco, mais tolerante à aridez.
- Milho: relativamente quente/ameno, umidade intermediária.
- Trigo: relativamente ameno/fresco, umidade intermediária.
- Cevada: relativamente fresco, tolerante a condição seca.
- Soja: ameno/quente, umidade suficiente e boa drenagem.

## 4. Contrato matemático e de execução

Para cultivo c e nó n, calcular uma **adequação adimensional A(c,n)** a partir de temperatura, umidade e relevo. A produção quantitativa resulta da combinação entre potencial físico, trabalhador/tempo efetivo e demais fatores aprovados na F-011; **adequação não equivale a produção, oferta comercial, preço nem receita**.

A forma funcional deve ser determinística, suave/contínua e numericamente estável (preferência por escala fixa de inteiros na simulação autoritativa). Seus perfis e seus coeficientes são **calibráveis**, e os percentuais ilustrativos das discussões não são valores canônicos.

Evitar o produto ingênuo de três penalidades muito severas que torne quase todos os locais improdutivos. A parametrização deve preservar piso viável e diferenciação territorial sem favorecer automaticamente um cultivo em todos os ambientes.

Como os atributos físicos são estáveis entre mudanças relevantes do mundo, adequações podem ser calculadas uma vez por geração e cacheadas por identidade de nó/cultura; recalcular apenas após alteração física que afete o cálculo. Complexidade esperada **O(6N)** para N nós, além do cálculo local do relevo.

Não construir seis camadas completas persistentes por LOD visual. O mundo lógico, não a troca de apresentação, é a identidade das produtividades. Reexecução/ordem de consulta não pode alterar resultados.

## 5. Fronteiras econômicas

A decomposição de alimentos em seis mercadorias exige uma etapa própria de integração com a demanda e o clearing da nova fundação:

1. Não criar uma mercadoria agregada alimentos cuja oferta some as seis e também negociar individualmente as mesmas quantidades.
2. Não multiplicar seis vezes a demanda de referência de cada trabalhador apenas pelo número de grãos.
3. Não inventar estoques físicos persistentes, fretes executados, carteiras consumidoras ou receitas fiscais ao introduzir cultivos.
4. Preservar os contratos gerais de preço/mercado e tesouros da fundação em vigor; o cálculo da adequação é anterior à valoração econômica.

A parametrização de substituição alimentar entre os seis bens e a forma de demanda específica **permanecem abertas** para calibração econômica posterior.

## 6. Gates de aceitação antes de promoção ao motor

1. Catálogo contém os seis identificadores estáveis, sem duplicações.
2. Cada cultura oferece adequação positiva na grande maioria dos nós terrestres amostrados; água não produz cultivo terrestre.
3. Climas contrastantes produzem especializações contrastantes, sem dominância universal de uma única cultura.
4. Pequena variação contínua de temperatura/umidade/relevo não causa saltos artificiais por fronteira de bioma.
5. Semente, topologia e atributos idênticos reproduzem os mesmos resultados independentemente da ordem de avaliação, do runtime e do LOD.
6. A inclinação tem efeito sem reaplicar uma penalidade de altitude já codificada termicamente.
7. Benchmark com mapas de várias densidades: custo/memória proporcionais aos seis cultivos, sem revarredura do globo em cada ciclo econômico.
8. Integração econômica posterior não duplica demanda, quantidade vendida, receita ou produto agregado.

## 7. O que esta decisão NÃO faz

Não ativa retroativamente a implementação agrícola experimental V2, não substitui o contrato hidrológico já existente, não define curvas numéricas finais, não fixa rendimento base, não introduz estações/irrigação/fertilidade, não altera a economia autorizada e não concede homologação de runtime.

**Próximo incremento recomendado:** um avaliador determinístico puro dos seis perfis, acompanhado de testes de diferenciação e benchmark, em laboratório opt-in; somente depois integrar produção e mercado.

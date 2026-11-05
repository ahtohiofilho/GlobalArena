# GA-NUTR-ARCH-01 — Arquitetura nutricional candidata, isolada do motor

**Data:** 2026-11-05
**Base empilhada:** PR #264 — GA-ECON-CORRECTION-01, HEAD inicial 8757a0ee794a0fd33155e74fcdb2a720fa0d3d9d9.
**Estado:** experimento matemático + testes, em PR Draft, sem efeito econômico ativo.

## 1. Decisões preservadas

1. Pessoas são simultaneamente residentes, consumidoras e, quando alocadas, produtoras; não há cidade obrigatória, bônus por era ou trabalhador fictício.
2. Demanda econômica para preços NÃO é ingestão nutricional. A razão oferta/demanda forma preços, mas não define quantidade fisiológica obrigatória por trabalhador.
3. O coeficiente histórico FoodDemandRawPerWorker = 500_000UL permanece exclusivamente referência física provisória do circuito econômico legado, não norma nutricional nem quantidade saudável comprovada. O indicador de atendimento alimentar (ex.: 88,7%) não pode ser apresentado como qualidade nutricional ou eficiência de trabalho.
4. Apenas alimentos fisicamente consumidos, por tipo, população e ciclo encerrado, poderão futuramente alimentar a avaliação nutricional real.
5. Seis tipos alimentares parcialmente substituíveis integram a arquitetura candidata. Ausência de registro por variedade não significa consumo zero certificado; os seis tipos precisam ter identidades e escalas explicitamente fornecidas. Não dividir a mercadoria alimentar agregada em seis tipos imaginários.
6. A produtividade não recebe hoje novo multiplicador. A passagem de estado nutricional para produção real é incremento separado após calibração e homologação.

## 2. Equações candidatas

Para seis alimentos identificados, consumo físico c_j, conversão explícita k_j em unidade nutricional comum por unidade física raw e população residente P > 0:

~~~text
z_j = c_j * k_j / P
f   = sum(z_j)
g(z) = z / (z + sigma)
D   = [sum(g(z_j)) - g(f)] / 5
E(f) = f^4 / (1 + f^4)
V   = 1 + nu * E(f) * D
Q   = A(f) * V, somente se A(f) for fornecida explicitamente
~~~

- f: ingestão total normalizada per capita, não demanda de mercado.
- D: diversidade, sob seis variedades reconhecidas, sem bonificação em consumo monovarietal.
- E(f): ativação do benefício de diversidade; tende a zero na escassez.
- sigma > 0: curvatura da substituição/variedade, experimental.
- nu >= 0: intensidade do benefício de diversidade, experimental. O ensaio nu=2,8 não é valor aprovado nem padrão de execução.
- A(f): curva de suficiência ainda não calibrada nem aprovada. Deve prever penalidades não lineares na escassez e ganhos marginais decrescentes. Não fixar curvas como leis históricas.

Os k_j também não são aprovados. Não assumir que 1 raw equivale a 1 grama, 1 caloria ou 1 unidade alimentar normalizada. A escolha dependerá da classificação alimentar e do contrato de massa/nutrientes. A fórmula não é apresentada como validação fisiológica.

## 3. Contrato implementado neste incremento

- CanonicalNutritionFoodIntake: identidade (CommodityId), quantidade física fornecida (ConsumedRaw) e conversão explícita (UnitsPerRaw).
- CanonicalNutritionCandidateParameters: sigma e nu validados como parâmetros experimentais.
- CanonicalNutritionCandidateEvaluator: avaliador puro; exige seis identidades distintas, ordena deterministicamente as entradas, rejeita grandezas não finitas e nunca modifica estado, mercado ou produção.
- CanonicalNutritionCandidateResult: retorna f, D, E(f), V e CandidateQuality opcional. Sem A(f), a qualidade não é calculada.
- A API não certifica proveniência física das quantidades fornecidas; até existir integração verificada com lotes consumidos, não é permitido apresentar o resultado como qualidade nutricional real.

## 4. Integração física futura: GA-NUTR-INTEGRATION-01 (fora deste PR)

1. Obter consumos de seis variedades da execução real, a partir de saídas físicas atestadas por tipo, tile, população e ciclo, e não de estoques, ofertas, demanda, entrega não consumida ou caixa monetário.
2. Definir unidades nutricionais comparáveis com conversões explicitamente calibradas; rejeitar estados incompletos, nunca inventar faltantes.
3. Diferenciar população estacionária de expedições que consomem suas próprias rações. Não duplicar alimento nem pessoas.
4. Produzir projeção nutricional read-only com proveniência e sinalizar resultado indisponível se o motor só dispuser do alimento agregado legado.
5. Em etapa posterior aprovada, aplicar eficiência aos trabalhadores do ciclo seguinte, não no ciclo que ainda está produzindo/consumindo, para evitar dependência circular.
6. Preservar poder de compra abstrato: nenhum crédito, orçamento inventado ou receita derivada de Q neste PR.

## 5. Compatibilidade / limites

Nenhum código de CanonicalPhysicalTacticalSession, PhysicalTacticalEconomyRuntimeState, CanonicalWorldStateHasher, circuito produtivo, EconomicFoodPreferenceEvaluator, Godot ou persistência é alterado nesta etapa.

Os arquivos novos ficam somente em GlobalArena.Simulation, GlobalArena.Tests e neste documento. Não há nova versão de save, novos eventos, mutação de hash, alteração de FoodDemandRawPerWorker, telas ou indicadores de produtividade.

A implementação de D, E(f) e V é confrontada em teste com EconomicFoodPreferenceEvaluator, que já formalizava estrutura matemática no domínio de preferências econômicas; avaliação nutricional não utiliza orçamento H, preço ou disposição marginal a pagar.

## 6. Gates específicos

GaNutrArch01CandidateNutritionTests cobre:
- consumo zero, monovariedade e seis variedades;
- diversidade, escassez e ativação não linear;
- A(f) explícita e qualidade opcional;
- correspondência matemática com avaliador econômico, sem acoplamento monetário;
- invariância de ordenação de alimentos;
- rejeição de tipos faltantes/duplicados e conversões inválidas;
- snapshots, hashes, saves e ciclos existentes sem mutação;
- parâmetros e resultados não finitos.

**Critério de promoção:** dotnet test, gates FAST, kernel/hash e homologação de compatibilidade histórica no HEAD do PR; nenhum teste isolado autoriza ativar produtividade nutricional. O PR permanece Draft até avaliação dos checks reais.

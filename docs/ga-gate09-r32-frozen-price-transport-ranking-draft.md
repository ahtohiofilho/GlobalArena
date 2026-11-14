# Global Arena — Gate 09 R32: contrafactuais algebraicos do ranking

Data 14/11/2026. Base PR #320/R31; R20-R31 intactos.

## Pergunta e escopo

R31 encontrou que nas 915 inversoes observadas entre mercados
a magnitude da mudanca relativa de preco supera a de transporte.
A observacao nao permite atribuir causalidade.

O R32 comparara quatro classificacoes de mercado para cada produtor:
- preco antigo, custo antigo (estado efetivo anterior);
- preco novo, custo antigo (hipotese somente preco);
- preco antigo, custo novo (hipotese somente transporte);
- preco novo, custo novo (estado efetivo posterior).

Os dois rankings hibridos NAO sao estados de equilibrio simultaneo:
a cotacao podera ser inconsistente com volumes da producao, e
nao deve entrar no solucionador, economia ou pagamentos do jogo.

## Metodologia e metricas

Reconstituir todas as alternativas de mercado usando menor rota
canonica por vendedor e custo de arestas original. Exigir igualdade
exata da escolha, caminho e score original R14 na fotografia
inicial e final. Empates e desempates originais nao mudam.

Para cada troca de destino A→B, distinguir:
- cruzamento B contra A so ao atualizar precos;
- cruzamento B contra A so ao atualizar transportes;
- ambos separadamente;
- nenhum separadamente, mas o estado completo muda.
Tambem verificar qual mercado vence entre TODOS os destinos na
hipotese; pode ser um terceiro mercado.

Confirmar identidades de log scores e de precos sob D constante:
    delta ln(SB/SA) = delta ln(PB/PA)
                    - delta ln((1+CB)/(1+CA));
    delta ln(PB/PA) = -1/2 ln(QB_novo/QB_antigo)
                    +1/2 ln(QA_novo/QA_antigo).

Contar cada chamada do oraculo da politica original. Os rankings
hibridos usam dados congelados e zero chamadas adicionais de R14,
mas scans de caminhos minimos independentes com custo real;
contabilizar essa carga e nao alegar custo computacional zero.

## Governanca

Validar manifesto R20 16/16, replay R21/R26/R31, venda integral,
paridade R13/R14, conservacao F-084F, caminho canonico e
prefixo deterministico. Os cinco criterios de estabilidade
<=1e-4 por tres atualizacoes consecutivas nao serao relaxados.

Um predomino no ranking hipotetico de preco orienta futura
analise de robustez sob demanda, alocacao e capacidade. Nao
demonstra causalidade nem autoriza alterar a curva P=sqrt(D/Q).
Sem validacao de F-059, equilibrio global, C# integrado ou
producao. PR Draft em cima de R31, sem merge.

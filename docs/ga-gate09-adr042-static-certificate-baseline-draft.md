# Global Arena — ADR-042: contrato de certificação estática F-072

**Estado:** D1–D3 adotados como linha-base de engenharia para um certificador experimental; D4 (escolha do solucionador) continua pendente.
**Data:** 14/11/2026 (Pernambuco).
**Base:** ADR-041 / PR #330 e decisão das três famílias R38–R40 / PR #329.

## 1. Decisões aplicadas

**D1 — Complementaridade comercial:** cada grupo g possui oferta s_g positiva e colunas de rota viáveis c, com fluxos x_gc não negativos somando exatamente s_g. Usar o score ORIGINAL F-072:

    Q_m(x) = sum_gc_destino_m x_gc
    B_e(x) = sum_gc_usando_e x_gc
    P_m(x) = sqrt(D_m / Q_m(x))
    C_gc(x) = sum_e_na_rota [c_free,e * (1+(B_e/K_e)^4) + tarifa_e(vendedor_g)]
    S_gc(x) = P_destino(x) / (1 + C_gc(x))
    lambda_g = max_c_viavel S_gc(x)
    x_gc * (lambda_g - S_gc(x)) = 0

Nenhum preço, imposto, preferência, logística, demanda ou oferta foi alterado para obter esta formulação.

**D2 — Certificação estática independente da dinâmica:** o certificado de UM snapshot de fluxos depende do arrependimento relativo global F-072 e do pior grupo, ambos no máximo 1e-4, mais conservação física, preços finitos, separação global por menor caminho e paridade original R13/R14. Nenhum passo de solução é simulado. As exigências históricas R24 de cinco métricas por três passos continuam intactas para os experimentos passados. O verificador não reescreve retrospectivamente os NO-GO R38–R40.

**D3 — Singularidade em Q=0:** mercado estruturalmente inalcançável é identificado; mercado alcançável sem fluxo positivo BLOQUEIA o certificado. Não inserir max(Q,epsilon), demanda mínima, carteira de compradores, estoques ou venda fictícia.

**D4 — Escolha de solucionador:** NÃO ratificada. O presente PR implementa somente auditoria estática: nenhuma nova família de atualização comercial, nenhuma autorização de integrar ao C#/Godot.

## 2. Implementação e indicadores

O arquivo experiments/gate09/static_certificate_v1/ci/verify_complementarity.py importa o motor experimental original congelado para:
- verificar cada coluna e toda oferta via Network.snapshots;
- classificar M+ e mercados inacessíveis com _reachable;
- reavaliar R14 e R13 INDEPENDENTES no mesmo fluxo, conferindo preços, quantidades, cargas, destinos, empates e arrependimentos;
- fazer NOVA separação de rotas por vendedor com FrozenRouteCache, comparando todos os mercados economicamente alcançáveis;
- calcular, por coluna financiada, o produto x_gc * max(0, lambda_g - S_gc) e confrontar gaps normalizados com o R14 original;
- verificar F-084F: total vendido e gross_micro = credits_micro, inclusive em empate;
- confirmar identidade SHA-256 da alocação antes/depois.

Resultados distintos:
- PASS_STATIC_F072_FIRST_ORDER_ONLY: aproximação comercial F-072 em UM snapshot; NÃO prova equilíbrio exato nem convergência;
- FAIL_STATIC_F072_COMMERCIAL_REGRET: fluxo viável, mas arrependimento comercial superior à tolerância;
- BLOCKED_REACHABLE_Q_ZERO_OR_NONFINITE_PRICE: não há preços finitos, portanto NÃO existe certificado F-072 válido naquele snapshot.

Mesmo quando há PASS e R14 sinaliza empate comercial, F-059 FINANCEIRO não é ratificado. A conservação das receitas F-084F não decide a distribuição econômica de tarifas em empate.

## 3. Cenários analíticos e segurança

Em rede simétrica de um produtor e dois mercados, x=(0,5;0,5) é um equilíbrio com lambda = 16*sqrt(2)/33 e GAP=0. Em x=(0,6;0,4), o arrependimento é positivo. Com dois produtores idênticos, qualquer a em [0,1] nas alocações (a,1−a) e (1−a,a) produz Q_1=Q_2=1, B_1=B_2=1, S=1/3, e satisfaz o equilíbrio, embora a distribuição por produtor não seja única. Q=0 em um mercado alcançável impede a certificação. Uma rota paga e subótima pode produzir FAIL mesmo quando todas as cotações são finitas.

Fixtures originais R34 também são auditadas em testes, sem serem modificadas ou resolvidas. Verificar manifesto R20 SHA256 16/16, replay de R21, paridade R13/R14 e conservação F-084F. CI Windows e Ubuntu, testes de contrato e artefatos JSON/ZIP.

## 4. Limites e próximo gate

Este incremento é o PRIMEIRO instrumento de VERIFICAÇÃO da arquitetura revista, não o quarto solucionador. A passagem do certificador não demonstra existência nem unicidade global. A documentação técnica do próximo algoritmo D4 deverá estabelecer variáveis e domínio, resíduo complementar e normalização, oráculo de separação de rotas, tratamento de Q=0, limite de computação e invariantes de produto antes de qualquer implementação.

PR Draft empilhado sobre ADR-041, sem merge. Código C#/Godot, economia F-072, decisões passadas e política financeira F-059 intactos.

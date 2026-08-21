# ADR-003 - Impressão Somente de Nota Aberta

> Status: Aprovada
> Data: 2026-08-16
> Substitui parcialmente: ADR-001, somente quanto à possibilidade de reimpressão

---

## Contexto

O ADR-001 separou emissão e impressão e admitiu reimpressão de uma nota fechada sem nova baixa. A auditoria contra o enunciado identificou que essa possibilidade contraria o requisito explícito de não permitir impressão de notas com status diferente de `Aberta`.

---

## Decisão

- Somente uma nota com status `Aberta` pode iniciar a impressão.
- O botão de impressão inicia o processamento de baixa de estoque e fechamento.
- Durante o processamento, o frontend apresenta um indicador ao usuário.
- Após a confirmação da baixa, a nota passa para `Fechada` e o documento é disponibilizado para impressão.
- Uma nota `Fechada` não pode ser impressa novamente.
- O frontend deve impedir a ação e o backend deve rejeitar qualquer tentativa de impressão de uma nota com status diferente de `Aberta`.
- A rejeição nunca pode produzir nova movimentação de estoque.

---

## Consequências

- Reimpressão não faz parte do produto.
- A ação de impressão também representa o comando de emissão e fechamento exigido pelo desafio.
- O sistema permanece estritamente aderente aos dois estados de negócio solicitados.
- Uma evolução futura que permita reimpressão exigirá nova decisão e mudança explícita de escopo.


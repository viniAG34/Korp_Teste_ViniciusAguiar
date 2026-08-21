# ADR-011 - Regras de Domínio de Produtos e Notas

> Status: Aprovada
> Data: 2026-08-16
> Dependências: ADR-001, ADR-003, ADR-008, ADR-009 e ADR-010

---

## Product

```text
Product
- Id: Guid
- Code: string
- Description: string
- Balance: int
- Version: uint
- CreatedAtUtc: DateTimeOffset
- UpdatedAtUtc: DateTimeOffset
```

Regras:

- criado por `Product.Create`;
- code obrigatório, alfanumérico, aceitando hífen, underscore e ponto;
- code normalizado com trim e uppercase invariant;
- code com no máximo 50 caracteres e único sem distinção de caixa;
- description obrigatória, sem espaços externos e com no máximo 200 caracteres;
- balance inteiro, podendo iniciar em zero;
- balance nunca negativo;
- code imutável;
- sem edição ou exclusão pública nesta feature;
- sem entrada ou ajuste manual de estoque.

---

## StockMovement

Toda baixa confirmada cria uma movimentação auditável:

```text
StockMovement
- Id
- ProductId
- InvoiceId
- Quantity
- BalanceBefore
- BalanceAfter
- Type
- EventId
- CreatedAtUtc
```

O único tipo desta feature será `InvoiceDeduction`. A movimentação permite comprovar saldo anterior, saldo posterior e idempotência do processamento.

---

## Invoice

```text
Invoice
- Id: Guid
- Number: long
- Status: InvoiceStatus
- CreatedAtUtc: DateTimeOffset
- ClosedAtUtc: DateTimeOffset?
- Version: uint
- Items: collection
```

```text
InvoiceStatus
- Open
- Closed
```

Regras:

- criada por `Invoice.Create`;
- número positivo, único e crescente, gerado pelo backend;
- sequence PostgreSQL com lacunas permitidas;
- status inicial `Open`;
- `ClosedAtUtc` nulo enquanto aberta;
- `ClosedAtUtc` preenchido somente após confirmação da baixa;
- nota fechada é imutável;
- nota não pode ser reaberta, cancelada ou excluída nesta feature.

---

## InvoiceItem

```text
InvoiceItem
- Id
- InvoiceId
- ProductId
- ProductCode
- ProductDescription
- Quantity
```

Regras:

- quantity inteira e positiva;
- product aparece no máximo uma vez na mesma invoice;
- índice único em `(invoice_id, product_id)`;
- tentativa duplicada retorna conflito, sem soma silenciosa;
- item só pode ser criado, alterado ou removido com invoice `Open` e sem emissão em andamento;
- product code e description são snapshots obtidos no momento da inclusão;
- saldo não é copiado para o Faturamento.

---

## Operações permitidas

### Inventory

```text
CreateProduct
GetProduct
ListProducts
```

### Invoice aberta e sem emissão ativa

```text
CreateInvoice
GetInvoice
ListInvoices
AddInvoiceItem
UpdateInvoiceItemQuantity
RemoveInvoiceItem
PrintInvoice
```

### Invoice fechada

```text
GetInvoice
ListInvoices
```

Invoice fechada rejeita alteração de itens e nova impressão.

---

## Criação e edição da invoice

- invoice pode ser criada vazia e aberta;
- uma invoice vazia não pode ser impressa;
- itens são adicionados individualmente;
- usuário pode alterar quantidade ou remover item antes da emissão;
- emissão ativa bloqueia alterações até conclusão ou rejeição.

---

## Validação e snapshot do produto

Ao adicionar item, Billing realiza consulta HTTP interna direta ao Inventory:

```text
Billing.Application
    -> Inventory internal HTTP API
        -> Id, Code, Description
```

Rota conceitual:

```text
GET /api/v1/internal/products/{id}
```

Regras:

- chamada interna não passa pelo API Gateway;
- resposta não expõe saldo;
- Billing persiste code e description como snapshot;
- produto inexistente impede inclusão;
- Inventory indisponível retorna `503` e nenhum item é adicionado;
- saldo só é validado no momento da baixa assíncrona.

---

## Pré-condições de PrintInvoice

- invoice existe;
- status `Open`;
- possui ao menos um item;
- não existe emissão ativa;
- header `Idempotency-Key` válido foi fornecido.

Ao aceitar, Billing cria `InvoiceIssuanceProcess`, bloqueia alterações, persiste Outbox e retorna `202 Accepted`.

---

## Idempotência de PrintInvoice

- mesma chave conhecida retorna referência ao processo existente sem reexecutar ou imprimir novamente;
- chave nova para invoice fechada retorna `409 invoice_not_open`;
- chave nova durante emissão ativa retorna `409 invoice_issuance_in_progress`;
- mesma chave associada a intenção lógica diferente retorna `409 idempotency_key_reused`;
- nova tentativa após rejeição exige nova chave.

O retorno de uma chave conhecida é recuperação do resultado anterior, não reimpressão.

---

## Baixa atômica

Inventory processa todos os itens na mesma transação:

1. verifica Inbox;
2. carrega produtos;
3. valida existência, quantidades e saldos;
4. aplica todas as baixas;
5. registra StockMovements;
6. registra resultado na Outbox;
7. confirma a transação;
8. confirma a mensagem.

Falha de qualquer item reverte toda a baixa. O resultado de negócio é persistido como `StockDeductionRejected` antes do ack.

---

## Conclusão e rejeição

Ao consumir `StockDeductionCompleted`, Billing registra Inbox, fecha a invoice, preenche `ClosedAtUtc`, conclui o processo e confirma tudo atomicamente.

Ao consumir `StockDeductionRejected`, Billing mantém a invoice aberta, desbloqueia edição, registra o motivo e permite correção seguida de nova tentativa com nova chave.


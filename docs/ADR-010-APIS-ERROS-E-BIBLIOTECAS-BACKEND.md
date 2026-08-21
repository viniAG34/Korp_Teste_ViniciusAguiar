# ADR-010 - APIs, Erros e Bibliotecas do Backend

> Status: Aprovada
> Data: 2026-08-16
> Dependências: ADR-004 a ADR-009 e ADR-013
> Atualizada em: 2026-08-17 para incorporar autenticação e OpenAPI do Identity.Service

---

## Stack HTTP e infraestrutura

- ASP.NET Core Minimal APIs;
- validação nativa de Minimal APIs do .NET 10 para contratos HTTP;
- `IExceptionHandler` e `ProblemDetails` para erros HTTP;
- `Microsoft.AspNetCore.OpenApi` para geração OpenAPI;
- YARP para o API Gateway;
- `RabbitMQ.Client` 7.x para mensageria;
- `ILogger<T>` para logs estruturados;
- Options Pattern para configuração;
- `System.Text.Json` para serialização;
- sem MediatR inicialmente.

---

## Organização das APIs

Endpoints serão Minimal APIs organizadas por feature e delegarão imediatamente para casos de uso da Application.

```text
Endpoints/
|-- Products/
|   |-- CreateProductEndpoint.cs
|   |-- GetProductEndpoint.cs
|   `-- ListProductsEndpoint.cs
`-- Invoices/
    |-- CreateInvoiceEndpoint.cs
    |-- GetInvoiceEndpoint.cs
    `-- PrintInvoiceEndpoint.cs
```

Endpoints não contêm regras de domínio, acesso direto ao `DbContext` ou publicação direta no RabbitMQ.

---

## Casos de uso sem MediatR

Casos de uso serão classes explícitas, injetadas diretamente nos endpoints. O método convencional será `HandleAsync` e sempre receberá `CancellationToken`.

MediatR não será utilizado inicialmente porque o escopo não justifica outra abstração de despacho. Sua adoção futura depende de nova necessidade aprovada.

---

## Validação em camadas

### Contrato HTTP

A validação nativa trata obrigatoriedade, formato, tamanho, faixa e payload malformado. Falhas retornam `400 Bad Request` com `ValidationProblemDetails`.

### Application

Valida coordenação do caso de uso, existência de referências e conflitos que dependam de persistência.

### Domain

Protege invariantes, saldos e transições de estado independentemente da origem da chamada.

Data Annotations e validação HTTP nunca substituem invariantes de domínio.

---

## Contratos HTTP

- prefixo `/api/v1`;
- endpoints e JSON em inglês;
- JSON em `camelCase`;
- DTOs de request e response como records dedicados;
- entidades nunca são serializadas diretamente;
- datas em ISO 8601 UTC;
- IDs em UUID;
- cada endpoint documenta respostas possíveis.

Rotas iniciais seguem ADR-004.

---

## Comando público de impressão

`PrintInvoice` será o único caso de uso público para a ação solicitada no desafio.

```text
POST /api/v1/invoices/{id}/print
    -> PrintInvoice
        -> cria InvoiceIssuanceProcess
        -> persiste Outbox
        -> retorna 202 Accepted
```

Não haverá caso de uso público separado chamado `IssueInvoice`. `InvoiceIssuanceProcess` é o nome do processo técnico interno iniciado por `PrintInvoice`.

---

## Status HTTP

| Situação | Status |
|---|---|
| Recurso criado | `201 Created` |
| Consulta bem-sucedida | `200 OK` |
| Impressão aceita para processamento | `202 Accepted` |
| Contrato inválido | `400 Bad Request` |
| Credencial ausente, inválida ou expirada | `401 Unauthorized` |
| Identidade válida sem permissão | `403 Forbidden` |
| Recurso inexistente | `404 Not Found` |
| Duplicidade ou estado inválido | `409 Conflict` |
| Solicitação não persistida por indisponibilidade | `503 Service Unavailable` |
| Erro inesperado | `500 Internal Server Error` |

Saldo insuficiente descoberto pelo consumer não retorna `422` ao `POST /print`, porque a requisição já terminou com `202`. O processo assíncrono será atualizado para estado de recusa e consultado pelo frontend.

---

## ProblemDetails

Erros HTTP utilizarão `ProblemDetails` ou `ValidationProblemDetails`, incluindo:

- `type`;
- `title`;
- `status`;
- `detail` em português quando apresentado ao usuário;
- `instance`;
- extensão `code` estável em inglês;
- extensão `traceId`.

Stack traces, SQL, nomes de filas, hosts e detalhes sensíveis não serão retornados.

---

## Tratamento central de exceções

Implementações de `IExceptionHandler` serão registradas em ordem para validação, domínio, infraestrutura e falhas inesperadas.

Como `IExceptionHandler` possui lifetime Singleton:

- não injeta `DbContext`, repositório ou serviço Scoped;
- não armazena estado de requisição;
- utiliza o `HttpContext` recebido pelo método;
- depende somente de serviços compatíveis com Singleton;
- mantém diagnósticos para falhas técnicas e inesperadas.

---

## Falhas de negócio em consumers

Falhas determinísticas, como saldo insuficiente, produto inexistente ou quantidade inválida:

```text
publish StockDeductionRejected
    -> persist result and Outbox atomically
    -> acknowledge original delivery
    -> no retry
    -> no DLQ
```

Falhas técnicas temporárias, como banco indisponível, timeout e conexão interrompida, provocam retry. Após o limite, a mensagem segue para DLQ.

---

## RabbitMQ.Client

- uma conexão de longa duração por processo;
- channels duradouros com ownership explícito;
- um channel não é compartilhado por publishers concorrentes sem sincronização;
- cada consumer reconhece entregas no channel que as recebeu;
- channel com erro de protocolo é descartado e recriado;
- conexões e channels não são criados por mensagem;
- mensagens de integração são persistentes;
- consumers utilizam manual acknowledgement;
- ack ocorre somente após commit do banco;
- publishers utilizam publisher confirms;
- prefetch é configurado e limitado;
- redelivery é esperada e consumers são idempotentes;
- retry e DLQ possuem topologia explícita.

---

## Outbox e entrega at-least-once

Publisher confirms não tornam o fluxo exactly-once. Se o broker confirmar e o publicador cair antes de marcar a Outbox, a mensagem poderá ser publicada novamente.

Consequentemente:

- a entrega será assumida como at-least-once;
- Outbox é a fonte de republicação;
- Inbox é obrigatória no consumidor;
- duplicidade é comportamento esperado e testado.

Quando o banco do Faturamento persistir processo e Outbox, `POST /print` retorna `202` mesmo se RabbitMQ estiver indisponível. Se o banco não conseguir persistir a solicitação, retorna `503` e nenhum processamento é considerado aceito.

---

## OpenAPI

Cada serviço gera seu próprio documento:

```text
Identity API  -> /openapi/v1.json
Inventory API -> /openapi/v1.json
Billing API   -> /openapi/v1.json
```

O Gateway roteia documentos em caminhos distintos:

```text
/openapi/identity/v1.json
/openapi/inventory/v1.json
/openapi/billing/v1.json
```

O Gateway não combina os contratos e não gera um contrato fictício de negócio.

---

## Logging e correlação

Logs usam templates estruturados de `ILogger<T>`, nunca interpolação de strings. Requisições e mensagens carregam `traceId`, `correlationId` e, para mensagens, `causationId`.

Quando aplicável, logs incluem identificadores do agregado, evento, operação, resultado e duração, sem segredos ou payloads sensíveis.

---

## Referências

- [ASP.NET Core error handling](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0)
- [ASP.NET Core OpenAPI](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/overview?view=aspnetcore-10.0)
- [RabbitMQ .NET client](https://www.rabbitmq.com/client-libraries/dotnet-api-guide)
- [RabbitMQ reliability](https://www.rabbitmq.com/docs/reliability)

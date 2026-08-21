# SDD-06 - Billing Service

> Status: Aprovado
> Versão: 1.0
> Data: 2026-08-18
> Gate A aprovado em: 2026-08-19
> Dependências: SDD-01, SDD-02, SDD-03, SDD-04, SDD-05, ADR-001, ADR-006, ADR-007, ADR-008, ADR-009, ADR-010, ADR-011, ADR-012 e ADR-014

---

## 1. Objetivo

Especificar o microsserviço responsável por criar e consultar invoices, gerenciar itens enquanto a nota estiver aberta, iniciar `PrintInvoice` de modo idempotente e persistir o acompanhamento técnico da emissão.

O documento transforma contratos e modelo aprovados em casos de uso, regras de domínio, responsabilidades por camada, persistência, concorrência, falhas e testes implementáveis. O transporte RabbitMQ e a coordenação operacional completa permanecem no SDD-07.

---

## 2. Requisitos rastreados

- `OBR-006` a `OBR-013`, `OBR-015` e `OBR-018`;
- `OBR-019` a `OBR-021`, no limite do comportamento de Billing;
- `OPA-002`;
- `DIF-002` a `DIF-006`, `DIF-008` e `DIF-009`;
- `QLT-001` a `QLT-008`;
- `APR-009` e `APR-010`.

---

## 3. Escopo previsto

- propriedade de Invoice, InvoiceItem e InvoiceIssuanceProcess;
- responsabilidades por camada e casos de uso;
- criação, consulta individual e listagem de invoices;
- inclusão, alteração e remoção de itens;
- obtenção e persistência de snapshot de produto;
- estados `Open` e `Closed` e bloqueio operacional;
- concorrência HTTP por ETag e `xmin`;
- idempotência de `PrintInvoice`;
- criação e consulta do processo de emissão;
- transições provocadas por resultados de Inventory;
- limites transacionais locais e futura integração com Inbox e Outbox;
- segurança, falhas, observabilidade, OpenAPI e LINQ;
- critérios de aceite e testes.

---

## 4. Fora do escopo

- cálculo tributário, preço, total, pagamento ou autorização fiscal externa;
- fornecedor, cliente, compra ou venda completa;
- edição, cancelamento, exclusão ou reabertura de invoice fechada;
- edição de código ou descrição do snapshot;
- consulta direta ao banco de Inventory;
- alteração de saldo pelo Billing;
- geração de PDF ou armazenamento de documento binário;
- publishers, consumers, topologia RabbitMQ, retry e DLQ;
- reconciliador administrativo automático;
- comportamento visual e `window.print()` do Angular;
- composição final do Docker Compose.

---

## 5. Blocos de decisão

1. responsabilidade, casos de uso e limites por camada;
2. criação e consultas de Invoice;
3. gestão de itens e snapshot de Product;
4. `PrintInvoice`, idempotência e bloqueio;
5. processo de emissão e transições de resultado;
6. persistência, transações, concorrência e falhas;
7. segurança, observabilidade, OpenAPI e LINQ;
8. critérios de aceite, testes, riscos e marcadores.

Nenhuma implementação funcional será iniciada durante esta macroetapa documental.

---

## 6. Decisões herdadas

- Billing é proprietário exclusivo de Invoice, InvoiceItem e InvoiceIssuanceProcess;
- PostgreSQL exclusivo `billing_db` e migrations próprias;
- Invoice possui somente status `Open` e `Closed`;
- `IsIssuanceInProgress` é bloqueio operacional, não status fiscal;
- Invoice é Aggregate Root de seus itens e usa `xmin` como versão;
- processo de emissão é Aggregate Root separado;
- numeração usa sequence PostgreSQL e aceita lacunas;
- snapshots de produto vêm da rota interna de Inventory;
- saldo não é consultado ao incluir item;
- nota fechada é imutável;
- `PrintInvoice` é o único comando público de emissão;
- processo, bloqueio e Outbox são persistidos atomicamente antes do `202`;
- JWT, políticas e autoria seguem SDD-04;
- contratos HTTP e eventos seguem SDD-03;
- Inventory permanece responsável pela baixa atômica;
- Inbox, Outbox e transporte completo serão integrados no SDD-07;
- testes de persistência e concorrência utilizam PostgreSQL real.

---

## 7. Decisões em elaboração

Os blocos aprovados serão registrados nesta seção.

### 7.1 Bloco 1 - Responsabilidade, casos de uso e limites por camada

> Estado: Aprovado pelo engenheiro em 2026-08-18

#### Responsabilidade funcional

Billing é a única autoridade capaz de criar e numerar invoices, gerenciar itens e snapshots, proteger estado e bloqueio, iniciar `PrintInvoice`, acompanhar o processo distribuído, fechar a invoice após confirmação de Inventory e desbloqueá-la após rejeição funcional.

Billing não consulta diretamente o banco de Inventory, não calcula saldo autoritativo e nunca o altera.

#### Casos de uso HTTP

```text
CreateInvoice
GetInvoiceById
ListInvoices
AddInvoiceItem
UpdateInvoiceItemQuantity
RemoveInvoiceItem
PrintInvoice
GetInvoiceIssuanceProcess
```

Não existem comandos públicos `IssueInvoice`, `CloseInvoice`, `ChangeInvoiceStatus`, `ReopenInvoice`, `CancelInvoice` ou `DeleteInvoice`. Fechamento é consequência exclusiva da confirmação da baixa.

#### Casos de uso internos

```text
MarkInvoiceIssuanceAwaitingStock
CompleteInvoiceIssuance
RejectInvoiceIssuance
MarkInvoiceIssuanceForManualIntervention
```

| Caso | Responsabilidade |
|---|---|
| `MarkInvoiceIssuanceAwaitingStock` | Registrar publisher confirm da solicitação |
| `CompleteInvoiceIssuance` | Fechar invoice após `StockDeductionCompleted` |
| `RejectInvoiceIssuance` | Manter invoice aberta e desbloquear edição |
| `MarkInvoiceIssuanceForManualIntervention` | Encerrar automação mantendo bloqueio |

Esses casos não são endpoints públicos. Seus adapters pertencem ao SDD-07.

#### Separação de estados

```text
InvoiceStatus
  Open
  Closed

InvoiceIssuanceProcessStatus
  Pending
  AwaitingStock
  Completed
  Rejected
  ManualIntervention
```

| Processo | Invoice |
|---|---|
| `Pending` | `Open`, bloqueada |
| `AwaitingStock` | `Open`, bloqueada |
| `Completed` | `Closed`, desbloqueada |
| `Rejected` | `Open`, desbloqueada |
| `ManualIntervention` | `Open`, bloqueada |

Estados do processo nunca são reutilizados como status fiscal.

#### Responsabilidade por camada

```text
Billing.Api
  -> endpoints Minimal API
  -> autenticação, autorização, ETag e If-Match
  -> contratos HTTP, Problem Details e OpenAPI
  -> composição e hospedagem futura dos adapters RabbitMQ

Billing.Application
  -> commands, queries, handlers e validações
  -> coordenação transacional
  -> porta de consulta de Product
  -> resultados independentes de transporte

Billing.Domain
  -> Invoice, InvoiceItem e InvoiceIssuanceProcess
  -> estados, transições, bloqueio e invariantes
  -> imutabilidade e erros de domínio

Billing.Infrastructure
  -> BillingDbContext, configurações e migrations
  -> repositórios e projeções
  -> cliente HTTP de Inventory
  -> transações PostgreSQL
  -> persistência futura de Inbox e Outbox
```

#### Limites arquiteturais

- Domain não referencia EF Core, HTTP, JWT ou RabbitMQ;
- Application não conhece `HttpContext`, Problem Details, delivery tag ou cliente RabbitMQ;
- Api não altera entidades diretamente;
- Infrastructure não decide se uma invoice pode ser fechada;
- cliente de Inventory implementa porta pertencente à Application;
- snapshot de Product não se torna entidade externa em Billing;
- não existe repositório genérico;
- entidades não são serializadas diretamente;
- Billing acessa somente `billing_db`;
- não existem foreign keys para Inventory ou Identity.

#### Limite de `PrintInvoice`

```text
validar invoice e chave
    -> criar InvoiceIssuanceProcess
        -> bloquear invoice
            -> persistir intenção na Outbox
                -> retornar processo
```

O caso não chama Inventory para baixar saldo, não aguarda RabbitMQ, não fecha a invoice, não gera PDF e não abre diálogo do navegador.

### 7.2 Bloco 2 - Criação e consultas de Invoice

> Estado: Aprovado pelo engenheiro em 2026-08-18

#### Criação

```text
POST /api/v1/invoices
Política: AdminOnly
Body: ausente
```

O comando recebe somente `CreatedByUserId`, extraído de `sub`. A sequência é:

```text
obter próximo número
    -> criar Invoice
        -> persistir
            -> retornar representação e ETag
```

#### Numeração

A porta conceitual `IInvoiceNumberGenerator.GetNextAsync` é implementada por Infrastructure sobre `invoice_number_seq`.

- PostgreSQL `bigint` e .NET `long`;
- início 1, incremento 1 e sem ciclo;
- valor sempre positivo e nunca fornecido pelo cliente;
- número não é reutilizado;
- lacunas após rollback ou falha são válidas;
- exaustão ou indisponibilidade da sequence é falha técnica.

#### Estado inicial

```text
Invoice.Create(id, number, createdByUserId, createdAtUtc)

Status = Open
IsIssuanceInProgress = false
Items = vazio
ClosedAtUtc = null
CreatedAtUtc = now
UpdatedAtUtc = now
```

ID e instante vêm de abstrações testáveis. Invoice vazia pode ser criada e consultada, mas não pode iniciar emissão.

#### Response da criação

```text
201 Created
Location: /api/v1/invoices/{invoiceId}
ETag: "<opaque-version>"
```

O corpo é `InvoiceResponse` com `items: []`. A versão persistente não aparece no JSON e o cliente não interpreta o ETag.

#### Consulta individual

```text
GET /api/v1/invoices/{invoiceId}
Política: AuthenticatedUser
```

Retorna invoice, itens ordenados por `productCode` e `id`, `closedAtUtc` somente quando preenchido e ETag atual. Não inclui autoria, chaves idempotentes, histórico de processos, estruturas técnicas, versão numérica ou estado atual do Inventory.

| Situação | Status | `code` |
|---|---:|---|
| UUID inválido | 400 | `invalid_invoice_id` |
| Invoice inexistente | 404 | `invoice_not_found` |
| Banco indisponível | 503 | `billing_unavailable` |

#### Listagem

```text
GET /api/v1/invoices?pageNumber=1&pageSize=20
Política: AuthenticatedUser
```

Ordenação fixa por `createdAtUtc` descendente e depois `id`. Cada resumo contém ID, número, status, bloqueio, quantidade de itens e timestamps, sem carregar a coleção.

- página mínima 1;
- tamanho entre 1 e 100, padrão 20;
- página além do final retorna `200` e lista vazia;
- `totalPages` vale zero sem registros;
- não existem filtros nem ordenação escolhida pelo cliente.

Contagem e página podem representar instantes ligeiramente diferentes durante criação concorrente. Não será usado isolamento elevado apenas para uma fotografia perfeita da listagem.

#### Estratégia de leitura

Consultas usam `AsNoTracking`, projeções SQL e cancellation token. `itemCount` é calculado no banco; itens e processos não são carregados para a listagem. Nenhuma consulta chama Inventory ou executa uma chamada por item.

Não existe consulta pública por número nesta versão, pois o requisito já é atendido pela rota estável por ID.

### 7.3 Bloco 3 - Gestão de itens e snapshot de Product

> Estado: Aprovado pelo engenheiro em 2026-08-18

#### Regras comuns

Inclusão, alteração e remoção exigem `AdminOnly`, `If-Match`, invoice existente, status `Open`, ausência de emissão ativa e versão correspondente ao `xmin`. Toda alteração atualiza `Invoice.UpdatedAtUtc`, disputa a versão da raiz e devolve novo ETag.

```text
validar rota, body e headers
    -> carregar Invoice com itens
        -> comparar versão
            -> validar estado e bloqueio
                -> executar regra específica
```

| Situação | Status | `code` |
|---|---:|---|
| `If-Match` ausente | 428 | `invoice_version_required` |
| `If-Match` malformado | 400 | `invalid_if_match` |
| Versão desatualizada | 412 | `invoice_version_mismatch` |
| Invoice inexistente | 404 | `invoice_not_found` |
| Invoice fechada | 409 | `invoice_not_open` |
| Emissão ativa | 409 | `invoice_issuance_in_progress` |

Api decodifica o ETag opaco. Application recebe a versão esperada tipada e não conhece sintaxe HTTP.

#### Adicionar item

```text
POST /api/v1/invoices/{invoiceId}/items
```

O request contém `productId` e `quantity`. ID precisa ser válido e não vazio, quantidade fica entre 1 e `int.MaxValue` e o produto não pode estar presente na invoice.

Somente depois de validar invoice, versão, estado, bloqueio e duplicidade, Billing chama a rota interna de Inventory. Uma solicitação que já falharia localmente não produz chamada remota desnecessária.

#### Cliente e bearer propagado

Application depende somente de:

```text
IProductCatalogClient.GetSnapshotAsync(productId, cancellationToken)
```

Ela não recebe nem manipula token. Um handler HTTP escopado, composto na borda, anexa o `Authorization` original sem registrá-lo ou persistir seu valor.

Política da chamada:

- timeout de três segundos por tentativa;
- no máximo uma repetição;
- repetição somente para conexão, timeout ou `5xx`;
- intervalo aproximado de 100 ms;
- sem retry para `400`, `401`, `403` ou `404`;
- cancelamento original interrompe o fluxo.

#### Snapshot

Billing persiste exclusivamente ProductId, ProductCode, ProductDescription e Quantity. Código e descrição vêm da resposta interna e são verificados quanto a vazio e limites estruturais antes de persistir.

Cliente não fornece nem substitui snapshot. O snapshot permanece imutável e Billing não consulta saldo nesse fluxo.

#### Falhas da dependência

| Resultado interno | Resposta pública de Billing |
|---|---|
| Product inexistente | `404 product_not_found` |
| Inventory indisponível ou timeout | `503 product_catalog_unavailable` |
| Inventory retorna `401` ou `403` | `503 product_catalog_unavailable` |
| Contrato interno inválido | `503 product_catalog_unavailable` |

Recusa do bearer interno representa falha de integração ou configuração, não uma nova decisão pública sobre o usuário já autorizado em Billing. O diagnóstico preserva internamente o status real sem repassar Problem Details cegamente. Nenhum item parcial é persistido.

#### Resultado da inclusão

O agregado impede repetição de ProductId, cria InvoiceItem e atualiza a raiz. Sucesso retorna Invoice completa, `200 OK` e novo ETag. Duplicidade retorna `409 product_already_added`.

#### Atualizar quantidade

```text
PUT /api/v1/invoices/{invoiceId}/items/{itemId}
```

O request contém somente `quantity`, entre 1 e `int.MaxValue`. ProductId, código e descrição não são aceitos nem alterados. Não existe chamada a Inventory. Sucesso retorna Invoice completa, `200 OK` e novo ETag.

#### Remover item

```text
DELETE /api/v1/invoices/{invoiceId}/items/{itemId}
```

Não chama Inventory. Remove o item, atualiza a raiz e pode deixar a invoice vazia. Sucesso retorna `204 No Content` com novo ETag.

Para atualização e remoção, UUID malformado retorna `400 invalid_invoice_item_id` e item que não pertence à invoice retorna `404 invoice_item_not_found`. A API não revela existência sob outra invoice.

### 7.4 Bloco 4 - `PrintInvoice`, idempotência e bloqueio

> Estado: Aprovado pelo engenheiro em 2026-08-18

#### Comando tipado

```text
PrintInvoice
- InvoiceId
- IdempotencyKey
- ExpectedVersion
- RequestedByUserId
- CorrelationId
```

Api valida a sintaxe dos identificadores e headers. Application recebe valores tipados e não depende dos nomes HTTP `If-Match`, `Idempotency-Key` ou `X-Correlation-ID`.

#### Ordem idempotente

```text
validar sintaxe
    -> procurar IdempotencyKey globalmente
        -> chave conhecida: recuperar intenção original
        -> chave nova: validar invoice e iniciar emissão
```

A chave é consultada antes do estado e da versão atual da invoice.

Para chave conhecida na mesma invoice, `If-Match` permanece obrigatório e sintaticamente válido, mas versão antiga não impede recuperação. O processo original é retornado sem criar processo, alterar bloqueio, criar Outbox ou chamar Inventory. Processo ativo retorna `202`; terminal retorna `200`; ambos devolvem o mesmo `Location` e o ETag atual.

Chave associada a outra invoice retorna `409 idempotency_key_reused` sem revelar detalhes do vínculo original.

#### Chave nova

1. carregar invoice e itens;
2. comparar versão esperada;
3. exigir status `Open`;
4. exigir pelo menos um item;
5. exigir ausência de emissão ativa;
6. criar `InvoiceIssuanceProcess` em `Pending`;
7. marcar `IsIssuanceInProgress = true`;
8. serializar `StockDeductionRequested`;
9. persistir a mensagem na Outbox;
10. confirmar tudo na mesma transação.

O payload contém IssuanceProcessId, InvoiceId, InvoiceNumber, RequestedByUserId e pares ProductId/Quantity atuais. Código e descrição não são enviados. O bloqueio impede mudança dos itens durante a operação.

#### Atomicidade

```text
Invoice bloqueada
InvoiceIssuanceProcess criado
Outbox persistida
```

Os três efeitos são confirmados ou revertidos juntos. Serialização ocorre antes do commit. RabbitMQ indisponível não invalida a intenção já confirmada no banco; dispatcher posterior é responsável pela publicação.

#### Response inicial

```text
202 Accepted
Location: /api/v1/invoice-issuance-processes/{processId}
Retry-After: 1
ETag: "<current-invoice-version>"
```

O corpo representa o processo em `pending`.

#### Disputas concorrentes

Mesma chave e mesma invoice:

- uma transação confirma;
- a outra reconhece a constraint global específica;
- recarrega a chave;
- retorna o processo original.

Mesma chave e invoices diferentes:

- uma transação confirma;
- a outra recarrega o vínculo e retorna `idempotency_key_reused`.

Chaves diferentes e mesma invoice:

- `xmin` e índice parcial de processo ativo permitem apenas uma;
- a outra reavalia e retorna `invoice_issuance_in_progress`.

Constraint inesperada nunca é traduzida genericamente como idempotência.

#### Novas tentativas e impressão concluída

- depois de `Rejected`, invoice aberta e desbloqueada aceita chave nova;
- em `Pending` ou `AwaitingStock`, chave diferente é rejeitada;
- depois de `Completed`, invoice fechada rejeita nova emissão;
- em `ManualIntervention`, bloqueio impede nova emissão pública.

Nova chave não representa reimpressão. A visualização de uma invoice já concluída usa seus dados existentes no frontend e não inicia outra baixa.

### 7.5 Bloco 5 - Processo de emissão e transições de resultado

> Estado: Aprovado pelo engenheiro em 2026-08-18

#### Transições permitidas

```text
Pending
  -> AwaitingStock
  -> Completed
  -> Rejected
  -> ManualIntervention

AwaitingStock
  -> Completed
  -> Rejected
  -> ManualIntervention
```

`Completed`, `Rejected` e `ManualIntervention` são terminais. Resultados de Inventory podem ser aplicados ainda em `Pending`, pois o broker pode entregar a solicitação antes de Billing persistir `AwaitingStock` após o publisher confirm.

#### Marcar publicação confirmada

`MarkInvoiceIssuanceAwaitingStock` é executado depois do publisher confirm de `StockDeductionRequested`.

- `Pending` passa para `AwaitingStock` e atualiza timestamp;
- invoice permanece aberta e bloqueada;
- processo terminal nunca regride;
- processo desconhecido ou associação incompatível é inconsistência técnica.

Outbox publicada e tentativa de transição são confirmadas na mesma transação local. Se resultado já tornou o processo terminal, somente a Outbox é marcada como publicada.

#### Conclusão

```text
registrar Inbox
    -> Invoice.Close(processedAtUtc)
        -> remover bloqueio
            -> Process.Complete(processedAtUtc)
                -> commit
```

Invoice passa para `Closed`, `ClosedAtUtc` recebe o instante local de processamento e o processo passa para `Completed` com `FinishedAtUtc`. OutcomeCode e OutcomeDescription permanecem nulos.

#### Rejeição funcional

`RejectInvoiceIssuance` mantém invoice `Open`, remove bloqueio, move processo para `Rejected`, grava código e descrição sanitizados e preenche `FinishedAtUtc`. Itens não são alterados automaticamente e nova tentativa exige chave nova.

#### Intervenção manual

`MarkInvoiceIssuanceForManualIntervention` mantém invoice `Open` e bloqueada, move o processo para `ManualIntervention`, persiste resultado sanitizado e preenche `FinishedAtUtc`. Não existe endpoint público de resolução nesta entrega.

#### Identidade do resultado

Todo evento precisa corresponder simultaneamente a IssuanceProcessId e InvoiceId. Processo inexistente, invoice divergente ou associação persistente incompatível é falha técnica. Billing não tenta inferir destino por número fiscal ou processo mais recente.

#### Duplicidades e atraso

Inbox intercepta o mesmo `messageId`. Para nova mensagem referente a processo terminal:

- resultado equivalente: registrar consumo sem repetir efeitos;
- resultado incompatível: inconsistência técnica;
- nunca sobrescrever terminal;
- nunca fechar invoice rejeitada por resultado contraditório tardio;
- nunca afetar nova tentativa da mesma invoice.

Eventos atuam exclusivamente sobre IssuanceProcessId explícito.

#### Atomicidade do resultado

Conclusão, rejeição e intervenção confirmam Inbox, Invoice e InvoiceIssuanceProcess na mesma transação. Falha em qualquer etapa reverte tudo e impede acknowledgment.

#### Consulta do processo

```text
GET /api/v1/invoice-issuance-processes/{processId}
Política: AuthenticatedUser
```

A consulta projeta diretamente o contrato do SDD-03.

```text
isDelayed = status ativo e agora - UpdatedAtUtc > 5 segundos

Retry-After = 1, se ativo e idade total < 10 segundos
Retry-After = 3, se ativo e idade total >= 10 segundos
Retry-After ausente, se terminal
```

Idade total usa CreatedAtUtc; tempo no estado atual usa UpdatedAtUtc. `isDelayed` não é persistido.

Tempo decorrido nunca fecha invoice, rejeita processo, remove bloqueio ou move para intervenção. Polling do frontend pode terminar enquanto o backend continua aguardando resultado explícito.

### 7.6 Bloco 6 - Persistência, transações, concorrência e falhas

> Estado: Aprovado pelo engenheiro em 2026-08-19

#### Portas específicas

```text
IInvoiceRepository
IInvoiceReadService
IInvoiceIssuanceProcessRepository
IBillingUnitOfWork
IInvoiceNumberGenerator
```

- repositório de Invoice carrega agregado e itens para mutação;
- serviço de leitura executa projeções;
- repositório de processo consulta por ID, chave e invoice;
- Unit of Work coordena transação e persistência;
- gerador de número acessa a sequence.

Não existem repositório genérico nem `IQueryable` exposto por Infrastructure.

#### Codec do ETag

```text
uint xmin
    -> 4 bytes unsigned em big-endian
        -> Base64Url sem padding
            -> valor entre aspas
```

Exemplo ilustrativo:

```text
xmin = 123
ETag = "AAAAew"
```

Somente ETag forte e único é aceito. Prefixo `W/`, wildcard `*`, múltiplos valores, ausência de aspas, Base64Url inválido ou conteúdo diferente de quatro bytes retornam `400 invalid_if_match`.

O codec pertence à borda HTTP. Application recebe `uint ExpectedVersion`. Depois de `SaveChanges`, o provider atualiza `xmin`, que é codificado no response.

#### Limites por operação

`CreateInvoice` reserva número e persiste a invoice. Falha posterior mantém lacuna válida e não exige transação explícita adicional.

Mutações de item carregam agregado e versão. Inclusão pode consultar Inventory antes do commit; `SaveChanges` ainda verifica o `xmin` originalmente lido. Alteração concorrente durante a chamada remota retorna `412`, sem repetir consulta ou mutação.

`PrintInvoice` usa transação explícita `ReadCommitted` para invoice, processo e Outbox, com serialização anterior ao commit.

Resultados assíncronos usam transação explícita para Inbox, invoice e processo. Nenhum acknowledgment ocorre antes do commit.

Depois do publisher confirm, atualização da Outbox e transição segura para `AwaitingStock` são persistidas em uma transação local.

#### Constraints reconhecidas

Infrastructure traduz somente constraints aprovadas e identificadas pelo nome: produto único por invoice, chave idempotente global, processo ativo único por invoice, relações locais e combinações válidas de estado e timestamps. Constraint desconhecida permanece falha técnica.

#### Concorrência HTTP

Em mutação de item, `DbUpdateConcurrencyException` provoca rollback e `412 invoice_version_mismatch`, sem merge ou retry.

Em `PrintInvoice`, violação conhecida ou conflito exige nova leitura para distinguir repetição idempotente, chave usada em outra invoice, processo concorrente ou versão desatualizada. A tradução só ocorre quando o estado recarregado comprovar a causa.

#### Concorrência dos consumers

Conflito durante transição assíncrona reverte a transação, não confirma a mensagem e não inventa resultado funcional. O SDD-07 definirá a reavaliação em novo escopo. Terminal compatível é reconhecido idempotentemente; terminal incompatível é inconsistência técnica.

#### Validação do resultado recebido

- OutcomeCode precisa ser conhecido e possuir até 100 caracteres;
- OutcomeDescription precisa ser sanitizado e possuir até 500 caracteres;
- excesso não é truncado silenciosamente;
- contrato incompatível é falha técnica.

#### Tradução de falhas

| Situação | Tratamento |
|---|---|
| Invoice ou processo inexistente em HTTP | `404` específico |
| Item inexistente | `404 invoice_item_not_found` |
| Produto duplicado na invoice | `409 product_already_added` |
| Estado incompatível | `409` específico |
| Versão HTTP desatualizada | `412 invoice_version_mismatch` |
| Banco indisponível antes da confirmação | `503 billing_unavailable` |
| Serialização da Outbox falha | `500 unexpected_error`, sem persistência parcial |
| Conflito em consumer | Falha técnica recuperável |
| Evento incompatível | Falha técnica encaminhada ao SDD-07 |
| Cancelamento | Propagado |

#### Migrations

Migrations pertencem exclusivamente a Billing, incluem sequence, tabelas, constraints e índices aprovados, não executam no startup da API e são testadas em PostgreSQL real. SQL gerado é revisado antes da execução controlada.

### 7.7 Bloco 7 - Segurança, observabilidade, OpenAPI e LINQ

> Estado: Aprovado pelo engenheiro em 2026-08-19

#### Segurança das rotas

```text
POST /api/v1/invoices
POST /api/v1/invoices/{invoiceId}/items
PUT /api/v1/invoices/{invoiceId}/items/{itemId}
DELETE /api/v1/invoices/{invoiceId}/items/{itemId}
POST /api/v1/invoices/{invoiceId}/print
    -> AdminOnly

GET /api/v1/invoices
GET /api/v1/invoices/{invoiceId}
GET /api/v1/invoice-issuance-processes/{processId}
    -> AuthenticatedUser
```

Billing valida JWT, claims e política localmente. CreatedByUserId e RequestedByUserId vêm exclusivamente de `sub`.

Consulta de processo não aplica ownership por usuário porque o sistema não possui esse modelo e o único papel inicial é Admin. Regra fictícia de proprietário não será criada.

#### Segurança do cliente Inventory

O endereço-base vem de configuração controlada, não de input. Cliente monta rota com UUID validado, propaga bearer, não segue redirecionamento arbitrário, não registra Authorization e valida o contrato antes de persistir snapshot.

#### Segurança dos consumers

Consumers não usam JWT. Validam tipo, versão, envelope, IDs, correlação, causalidade, vínculo processo/invoice, estado e Inbox. Credenciais RabbitMQ pertencem ao serviço e vêm do ambiente. RequestedByUserId transportado não concede autorização.

#### Tratamento de textos

Snapshots e descrições de resultado são texto, nunca HTML; respeitam limites, rejeitam controles quando aplicável e são apresentados com escape no Angular. Não são concatenados em SQL, logs ou nomes de filas.

#### Logs

```text
invoice_created:
  invoiceId, invoiceNumber, createdByUserId, correlationId, duration

invoice_item_added|updated|removed:
  invoiceId, invoiceItemId, productId, correlationId, duration

invoice_issuance_accepted:
  invoiceId, issuanceProcessId, messageId, requestedByUserId, correlationId, duration

invoice_issuance_transitioned:
  invoiceId, issuanceProcessId, previousStatus, currentStatus,
  outcomeCode, messageId, correlationId, duration
```

Bearer, chave JWT, idempotency key completa, corpo da invoice, coleção de itens, descrições, payload de eventos, connection string e conteúdo técnico persistido não são registrados. Stack trace fica apenas no diagnóstico interno.

#### Métricas

```text
invoices_created_total
invoice_item_operations_total{operation="add|update|remove", outcome="success|failure"}
invoice_issuance_requests_total{outcome="accepted|replayed|rejected|technical_failure"}
invoice_issuance_transitions_total{status="completed|rejected|manual_intervention"}
invoice_issuance_duration_seconds
product_catalog_requests_total{outcome="success|not_found|unavailable"}
```

Invoice, processo, usuário, produto, mensagem e chave idempotente não são labels.

#### Correlação

Cada request HTTP recebe ou cria seu `requestCorrelationId`, devolvido em `X-Correlation-ID` e usado nos logs daquela requisição. Chamada a Inventory preserva esse valor.

Uma nova emissão usa o `requestCorrelationId` também como `operationCorrelationId`, persistido na Outbox e propagado pelos eventos. Repetição idempotente não reescreve esse identificador histórico. Se o replay chegar com outra correlação, seus logs distinguem `requestCorrelationId` atual e `operationCorrelationId` original. `traceId` permanece separado de ambos.

#### OpenAPI

```text
/openapi/v1.json
```

O documento descreve bearer, políticas, rotas, paginação, ETag, `If-Match`, `Idempotency-Key`, `Location`, `Retry-After`, DTOs, campos opcionais, Problem Details e respostas aplicáveis entre `200`, `201`, `202`, `204`, `400`, `401`, `403`, `404`, `409`, `412`, `428`, `500` e `503`. Exemplos não contêm token ou chave real.

#### LINQ justificável

```text
Listagem:
OrderByDescending(CreatedAtUtc) -> ThenBy(Id) -> Skip -> Take
    -> Select com Items.Count

Detalhe:
Items.OrderBy(ProductCode) -> ThenBy(Id)

Busca idempotente:
SingleOrDefault por IdempotencyKey

Histórico:
Where(InvoiceId) -> OrderByDescending(CreatedAtUtc)

Validação de conjunto:
Select(ProductId) -> Distinct -> Count
```

Consultas ficam em Infrastructure e são traduzidas para SQL antes de materialização. Mutações usam métodos explícitos; LINQ não esconde efeitos colaterais.

### 7.8 Bloco 8 - Critérios de aceite, testes, riscos e marcadores

> Estado: Aprovado pelo engenheiro em 2026-08-19

---

## 8. Critérios de aceite

### CA-BIL-01 - Propriedade do faturamento

**Dado** o conjunto de serviços,  
**quando** modelos, bancos e referências forem inspecionados,  
**então** somente Billing persistirá Invoice, InvoiceItem e InvoiceIssuanceProcess.

### CA-BIL-02 - Criação de invoice

**Dado** um comando válido,  
**quando** a invoice for criada,  
**então** receberá número positivo e único, nascerá aberta, vazia e desbloqueada e registrará autoria e timestamps.

### CA-BIL-03 - Semântica da sequence

**Dado** reserva concorrente ou transação falha,  
**quando** os números forem observados,  
**então** continuarão únicos e crescentes, lacunas serão aceitas e cliente nunca fornecerá número.

### CA-BIL-04 - Consultas

**Dado** invoices persistidas,  
**quando** detalhe ou listagem forem consultados,  
**então** campos, ordenação, paginação, contagem de itens e ETag corresponderão aos contratos aprovados.

### CA-BIL-05 - Snapshot autoritativo

**Dado** inclusão válida de item,  
**quando** Billing obtiver o produto,  
**então** ProductId, código e descrição serão persistidos exclusivamente a partir do contrato interno de Inventory.

### CA-BIL-06 - Falha da dependência

**Dado** ausência, timeout, indisponibilidade, recusa interna ou contrato inválido de Inventory,  
**quando** a inclusão for tentada,  
**então** o erro será traduzido conforme especificado e nenhum item parcial será persistido.

### CA-BIL-07 - Produto único na invoice

**Dado** um Product já presente,  
**quando** nova inclusão for solicitada,  
**então** será rejeitada sem soma silenciosa ou nova consulta desnecessária.

### CA-BIL-08 - Alteração limitada do item

**Dado** item existente em invoice mutável,  
**quando** atualização ou remoção for executada,  
**então** somente quantidade poderá ser atualizada e remoção poderá deixar a invoice vazia.

### CA-BIL-09 - Imutabilidade e bloqueio

**Dado** invoice fechada ou com emissão ativa,  
**quando** mutação de item for tentada,  
**então** nenhum estado será alterado e o conflito específico será retornado.

### CA-BIL-10 - Concorrência HTTP

**Dado** o codec aprovado e uma versão observada,  
**quando** `If-Match` for validado ou houver disputa,  
**então** formato inválido retornará `400`, ausência `428` e versão desatualizada `412`, sem merge ou retry.

### CA-BIL-11 - Aceite durável de PrintInvoice

**Dado** invoice elegível, chave nova e versão atual,  
**quando** `PrintInvoice` retornar `202`,  
**então** bloqueio, processo Pending e Outbox terão sido confirmados atomicamente.

### CA-BIL-12 - Replay HTTP

**Dado** chave já associada à mesma invoice,  
**quando** o comando for repetido em qualquer estado,  
**então** retornará o processo original sem novo efeito.

### CA-BIL-13 - Chave em outra invoice

**Dado** chave já associada,  
**quando** for usada em outra invoice,  
**então** será rejeitada sem expor a associação original.

### CA-BIL-14 - Exclusão mútua da emissão

**Dado** chaves diferentes concorrendo pela mesma invoice,  
**quando** forem persistidas,  
**então** no máximo um processo ativo será criado.

### CA-BIL-15 - Confirmação de publicação

**Dado** publisher confirm,  
**quando** Billing registrar a publicação,  
**então** Pending passará a AwaitingStock sem regredir processo já terminal.

### CA-BIL-16 - Conclusão atômica

**Dado** resultado Completed compatível,  
**quando** for consumido,  
**então** Inbox, fechamento, desbloqueio e conclusão do processo serão confirmados juntos.

### CA-BIL-17 - Rejeição atômica

**Dado** resultado Rejected compatível,  
**quando** for consumido,  
**então** Inbox, processo rejeitado, invoice aberta e desbloqueio serão confirmados juntos.

### CA-BIL-18 - Intervenção manual

**Dado** falha técnica terminal compatível,  
**quando** for consumida,  
**então** processo terminará em ManualIntervention e invoice permanecerá aberta e bloqueada.

### CA-BIL-19 - Terminal idempotente

**Dado** resultado duplicado ou atrasado equivalente,  
**quando** alcançar processo terminal,  
**então** será reconhecido sem repetir ou regredir efeitos.

### CA-BIL-20 - Resultado incompatível

**Dado** resultado contraditório, processo desconhecido ou vínculo divergente,  
**quando** for processado,  
**então** nenhuma invoice ou tentativa posterior será alterada e ocorrerá falha técnica.

### CA-BIL-21 - Consulta derivada do processo

**Dado** qualquer processo persistido,  
**quando** for consultado,  
**então** representação, `isDelayed` e `Retry-After` serão calculados conforme estado e relógio.

### CA-BIL-22 - Tempo sem efeito de negócio

**Dado** processo ativo prolongado,  
**quando** o tempo passar sem resultado confiável,  
**então** invoice e processo não serão fechados, rejeitados, desbloqueados ou movidos automaticamente.

### CA-BIL-23 - Autorização e autoria

**Dado** rota pública ou chamada interna,  
**quando** for executada,  
**então** política, validação local, autoria por `sub` e bearer propagado seguirão os contratos aprovados.

### CA-BIL-24 - Falhas classificadas

**Dado** falha HTTP, remota, persistente ou assíncrona,  
**quando** for tratada,  
**então** sua classificação corresponderá à causa e detalhes técnicos não serão expostos.

### CA-BIL-25 - Observabilidade segura

**Dado** operações e transições,  
**quando** logs e métricas forem inspecionados,  
**então** haverá correlação suficiente sem tokens, payloads, descrições ou chaves idempotentes completas.

### CA-BIL-26 - OpenAPI fiel

**Dado** o documento OpenAPI de Billing,  
**quando** comparado aos endpoints,  
**então** rotas, políticas, headers, contratos e respostas estarão sincronizados.

### CA-BIL-27 - Independência das camadas

**Dado** Domain e Application,  
**quando** referências e tipos públicos forem inspecionados,  
**então** não dependerão de EF Core, ASP.NET Core ou RabbitMQ nem exporão `IQueryable`.

### CA-BIL-28 - Ausência de escopo fiscal especulativo

**Dado** o serviço implementado,  
**quando** casos de uso e modelo forem inspecionados,  
**então** não existirão cálculo tributário, preço, pagamento, cliente, cancelamento, PDF ou alteração de saldo.

---

## 9. Estratégia de testes planejada

| ID | Teste planejado | Nível | Critérios |
|---|---|---|---|
| TST-BIL-001 | Validar ownership e referências de camadas | Arquitetura | CA-BIL-01, CA-BIL-27 |
| TST-BIL-002 | Criar Invoice pela factory | Unitário | CA-BIL-02 |
| TST-BIL-003 | Criar invoices concorrentes e validar sequence | Integração | CA-BIL-02, CA-BIL-03 |
| TST-BIL-004 | Confirmar lacuna após rollback | Integração | CA-BIL-03 |
| TST-BIL-005 | Consultar detalhe, ordenação dos itens e ETag | Integração | CA-BIL-04 |
| TST-BIL-006 | Paginar resumos sem carregar coleção | Integração | CA-BIL-04 |
| TST-BIL-007 | Codificar e decodificar ETag | Unitário | CA-BIL-10 |
| TST-BIL-008 | Rejeitar ETag fraco, wildcard, múltiplo e malformado | Integração | CA-BIL-10 |
| TST-BIL-009 | Adicionar item com snapshot de Inventory | Integração | CA-BIL-05 |
| TST-BIL-010 | Propagar bearer e correlação à chamada interna | Integração | CA-BIL-23 |
| TST-BIL-011 | Rejeitar Product inexistente sem persistência parcial | Integração | CA-BIL-06 |
| TST-BIL-012 | Simular timeout e `5xx`, verificando uma repetição | Integração | CA-BIL-06, CA-BIL-24 |
| TST-BIL-013 | Traduzir recusa e contrato interno inválido | Integração | CA-BIL-06, CA-BIL-24 |
| TST-BIL-014 | Rejeitar Product duplicado sem nova chamada remota | Unitário/integração | CA-BIL-07 |
| TST-BIL-015 | Atualizar somente quantidade | Unitário/integração | CA-BIL-08 |
| TST-BIL-016 | Remover item e permitir invoice vazia | Unitário/integração | CA-BIL-08 |
| TST-BIL-017 | Rejeitar mutação fechada ou bloqueada | Unitário/integração | CA-BIL-09 |
| TST-BIL-018 | Disputar duas mutações com o mesmo ETag | Integração | CA-BIL-10 |
| TST-BIL-019 | Iniciar `PrintInvoice` e inspecionar transação | Integração | CA-BIL-11 |
| TST-BIL-020 | Falhar serialização e comprovar rollback | Integração | CA-BIL-11, CA-BIL-24 |
| TST-BIL-021 | Repetir chave antes e depois do terminal | Integração | CA-BIL-12 |
| TST-BIL-022 | Reutilizar chave em outra invoice | Integração | CA-BIL-13 |
| TST-BIL-023 | Disputar mesma chave concorrentemente | Integração | CA-BIL-12, CA-BIL-13 |
| TST-BIL-024 | Disputar chaves diferentes na mesma invoice | Integração | CA-BIL-14 |
| TST-BIL-025 | Transicionar Pending para AwaitingStock | Unitário/integração | CA-BIL-15 |
| TST-BIL-026 | Concluir a partir dos dois estados ativos | Unitário/integração | CA-BIL-16 |
| TST-BIL-027 | Rejeitar a partir dos dois estados ativos | Unitário/integração | CA-BIL-17 |
| TST-BIL-028 | Mover os dois estados ativos para intervenção | Unitário/integração | CA-BIL-18 |
| TST-BIL-029 | Reentregar resultado terminal equivalente | Integração | CA-BIL-19 |
| TST-BIL-030 | Entregar resultado contraditório ou de tentativa anterior | Integração | CA-BIL-20 |
| TST-BIL-031 | Rejeitar processo/invoice incompatíveis | Integração | CA-BIL-20 |
| TST-BIL-032 | Calcular `isDelayed` e `Retry-After` com relógio controlado | Unitário | CA-BIL-21 |
| TST-BIL-033 | Comprovar que tempo não altera estado | Unitário/integração | CA-BIL-22 |
| TST-BIL-034 | Diferenciar políticas e status HTTP | Integração | CA-BIL-23, CA-BIL-24 |
| TST-BIL-035 | Simular banco indisponível e cancelamento | Integração | CA-BIL-24 |
| TST-BIL-036 | Inspecionar logs e métricas com sentinelas | Segurança/integração | CA-BIL-25 |
| TST-BIL-037 | Comparar OpenAPI aos endpoints | Snapshot/arquitetura | CA-BIL-26 |
| TST-BIL-038 | Inspecionar ausência de funcionalidades excluídas | Arquitetura | CA-BIL-28 |
| TST-BIL-039 | Aplicar migrations em PostgreSQL vazio | Integração | CA-BIL-01 |

PostgreSQL real é obrigatório para sequence, `xmin`, constraints, transações e concorrência. Integração HTTP usa servidor controlado e, na validação conjunta, Inventory real em containers. Código relevante permanece sujeito ao gate mínimo de 80%.

---

## 10. Riscos e mitigações

| Risco | Impacto | Mitigação |
|---|---|---|
| Chamada a Inventory prolongar mutação | Maior janela concorrente | Timeout, uma repetição e `xmin` no commit |
| Snapshot incompatível | Dados históricos inválidos | Validação estrutural antes de persistir |
| ETag ser interpretado pelo cliente | Acoplamento ao PostgreSQL | Codec opaco e testes de formato |
| Processos concorrentes | Baixas duplicadas | `xmin`, chave única e índice parcial |
| Resultado chegar antes de AwaitingStock | Transição aparentemente fora de ordem | Resultado aceito também a partir de Pending |
| Evento tardio fechar tentativa rejeitada | Estado fiscal incorreto | Processo explícito e terminais imutáveis |
| Invoice ficar bloqueada indefinidamente | Intervenção operacional necessária | Estado atrasado, logs, DLQ e limitação explícita |
| Retry remoto aumentar latência | Request lento | Uma repetição somente para falha transitória |
| Logs exporem documento ou token | Vazamento de dados | Allowlist de campos e testes com sentinelas |
| Escopo fiscal crescer | Atraso e domínio fictício | Lista negativa e teste arquitetural |

---

## 11. Marcadores de qualidade

| Marcador | Exigência neste SDD |
|---|---|
| ESP | Oito blocos aprovados antes da implementação |
| RAS | Requisitos de nota e impressão ligados a critérios e testes |
| ARC | Billing mantém ownership e dependências corretas |
| DOM | Invoice e processo protegem estados e transições |
| ERR | Erros HTTP, remotos, funcionais e técnicos permanecem distintos |
| SEG | JWT, bearer propagado, snapshots e logs protegidos |
| TST | Domínio e infraestrutura relevante cobertos em pelo menos 80% |
| INT | PostgreSQL e HTTP interno verificados com infraestrutura real |
| OBS | Fluxo completo possui correlação e eventos identificáveis |
| DOC | OpenAPI, ETag, LINQ e limites fiscais documentados |
| QA | Concorrência, idempotência e eventos atrasados exercitados |

---

## 12. Limites para a futura implementação

Uma implementação deste SDD poderá criar:

- agregados Invoice e InvoiceIssuanceProcess e entidade InvoiceItem;
- casos de uso HTTP e transições internas aprovadas;
- cliente interno de Inventory e bearer propagation;
- codec de ETag, portas específicas e projeções;
- BillingDbContext, configurações, sequence e migrations;
- criação transacional da Outbox por `PrintInvoice`;
- segurança, falhas, logs, métricas e OpenAPI do Billing;
- testes descritos neste documento.

Não poderá criar antecipadamente:

- publishers, consumers ou topologia RabbitMQ completos;
- retry, DLQ ou reconciliador;
- acesso ao banco ou entidades de Inventory;
- alteração de saldo;
- cálculo fiscal, preço, cliente ou pagamento;
- PDF, armazenamento de documento ou comportamento Angular;
- composição final do ambiente.

Inbox e transições por adapters reais serão conectadas ao transporte somente após o SDD-07.

---

## 13. Condição para Gate A

O SDD-06 estará apto ao Gate A quando:

- os oito blocos estiverem aprovados;
- não houver contradição material com SDD-02 a SDD-05 e ADRs vigentes;
- cada critério possuir ao menos um teste planejado;
- matriz de rastreabilidade estiver atualizada;
- ETag, idempotência, concorrência e transições forem implementáveis sem decisão implícita;
- limites entre Billing, Inventory, SDD-07 e frontend estiverem explícitos;
- nenhuma funcionalidade fiscal além do desafio tiver sido inventada.

A aprovação estabiliza o comportamento do Billing, mas não autoriza implementação antes da baseline documental conjunta.

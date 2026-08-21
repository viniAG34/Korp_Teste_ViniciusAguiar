# Plano de Implementação - SDD-06

> Gate: B - Plano de implementação
> Status: Aprovado pelo engenheiro
> Data: 2026-08-21
> SDD: `SDD-06-BILLING-SERVICE.md`
> Dependências: SDD-01 a SDD-05 validados; contratos do SDD-03 implementados

---

## 1. Objetivo

Implementar o Billing Service como proprietário exclusivo de Invoice, InvoiceItem e InvoiceIssuanceProcess. A entrega cobre criação e consultas de notas, gestão de itens com snapshot autoritativo do Inventory, concorrência HTTP por ETag, aceite idempotente e durável de `PrintInvoice`, consulta do processo e transições internas de resultado.

O SDD-06 persistirá a mensagem `StockDeductionRequested` na Outbox, porém não publicará nem consumirá mensagens. Publisher, consumers, Inbox operacional, retry, DLQ, reconciliador e topologia RabbitMQ pertencem ao SDD-07.

## 2. Auditoria da baseline

- baseline auditada no commit `2eb3441`, após aprovação do Gate C do SDD-05;
- Domain já contém Invoice, InvoiceItem, InvoiceIssuanceProcess, estados, invariantes e transições principais;
- PostgreSQL, sequence, `xmin`, constraints, índice idempotente global e índice parcial de processo ativo foram validados no SDD-02;
- DTOs HTTP, codec de ETag, parser de Idempotency-Key e contratos de eventos foram estabilizados pelo SDD-03;
- Outbox e Inbox existem estruturalmente, mas não possuem adapters operacionais;
- Application ainda não possui casos de uso ou portas;
- Infrastructure ainda não possui repositórios, projeções, unidade de trabalho, cliente HTTP ou composição;
- API expõe somente OpenAPI e não possui autenticação, endpoints, Problem Details ou correlação;
- a baseline possui 13 testes unitários e 25 testes de integração do Billing;
- não existe dependência RabbitMQ nem acesso ao banco do Inventory.

Não será criada nova migration, salvo incompatibilidade comprovada entre o modelo aprovado e a migration existente.

## 3. Arquitetura

```text
HTTP -> Billing.Api -> handlers da Application -> portas específicas
                                              -> Infrastructure/EF Core/PostgreSQL
                                              -> HTTP interno/Inventory.Api

PrintInvoice -> transação local -> Invoice bloqueada + Process Pending + Outbox

SDD-07 futuro -> publisher/consumers -> transições internas já implementadas
```

### 3.1 Domain

- preservar Invoice como raiz das mutações de itens e do bloqueio fiscal;
- preservar InvoiceIssuanceProcess como máquina de estados explícita;
- completar apenas invariantes ou operações necessárias aos critérios aprovados;
- impedir regressão de estados terminais e alterações sem emissão ativa;
- manter Domain sem dependências de EF Core, ASP.NET Core ou RabbitMQ.

### 3.2 Application

Casos de uso HTTP:

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

Transições internas:

```text
MarkInvoiceIssuanceAwaitingStock
CompleteInvoiceIssuance
RejectInvoiceIssuance
MarkInvoiceIssuanceForManualIntervention
```

Portas específicas:

```text
IInvoiceRepository
IInvoiceReadService
IInvoiceIssuanceProcessRepository
IProductCatalogClient
IBillingUnitOfWork
IInvoiceNumberGenerator
IStockDeductionOutbox
IClock
IGuidGenerator
```

Application define comandos, queries, resultados discriminados, projeções e exceções técnicas sanitizadas. Não expõe `IQueryable`, `DbContext`, HTTP ou RabbitMQ.

### 3.3 Infrastructure

- repositórios de escrita e read service com projeções `AsNoTracking`;
- listagem determinística `CreatedAtUtc DESC -> Id`, com `Items.Count` traduzido para SQL;
- detalhe com itens ordenados por código e ID;
- geração de número pela sequence PostgreSQL;
- unidade de trabalho e transações `ReadCommitted` delimitadas por caso de uso;
- tradução somente das constraints conhecidas de idempotência e exclusão mútua;
- cliente HTTP do Inventory com timeout de três segundos e no máximo uma repetição para conexão, timeout e `5xx`;
- ausência de repetição para `4xx`, contrato inválido ou cancelamento do chamador;
- serialização determinística do envelope V1 e gravação da Outbox na mesma transação de `PrintInvoice`;
- nenhuma publicação, consumo ou infraestrutura RabbitMQ nesta fase.

### 3.4 API

Rotas:

```text
POST   /api/v1/invoices                                      AdminOnly
GET    /api/v1/invoices                                      AuthenticatedUser
GET    /api/v1/invoices/{invoiceId}                          AuthenticatedUser
POST   /api/v1/invoices/{invoiceId}/items                    AdminOnly
PUT    /api/v1/invoices/{invoiceId}/items/{itemId}           AdminOnly
DELETE /api/v1/invoices/{invoiceId}/items/{itemId}           AdminOnly
POST   /api/v1/invoices/{invoiceId}/print                    AdminOnly
GET    /api/v1/invoice-issuance-processes/{processId}        AuthenticatedUser
```

A API validará JWT localmente, extrairá autoria exclusivamente de `sub`, propagará bearer e correlação ao Inventory, aplicará ETag e Idempotency-Key, emitirá Problem Details sanitizado e documentará headers e respostas no OpenAPI.

## 4. Dependências

Adicionar ao `Korp.Billing.Api` a referência já aprovada e centralizada a `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.11. O pacote é necessário para a validação JWT local e manterá a mesma configuração usada por Identity e Inventory.

Referenciar `Microsoft.AspNetCore.Mvc.Testing` e `Microsoft.IdentityModel.JsonWebTokens`, já aprovados e centralizados, somente nos testes de integração que exigirem servidor HTTP e tokens controlados.

Não será adicionado pacote de retry, mediator, validação, mock, mensageria ou métricas. Retry HTTP será explícito e limitado; métricas usarão `System.Diagnostics.Metrics`.

## 5. Arquivos previstos

### Domain

```text
Invoices/Invoice.cs                         (operações ampliadas somente se necessário)
Issuance/InvoiceIssuanceProcess.cs          (idempotência terminal, se necessária)
```

### Application

```text
Common/
|- IClock.cs
|- IGuidGenerator.cs
|- BillingServiceUnavailableException.cs
|- BillingConsistencyException.cs

Invoices/Common/
|- InvoiceDetails.cs
|- InvoiceSummary.cs
|- InvoicePage.cs
|- IInvoiceRepository.cs
|- IInvoiceReadService.cs
|- IBillingUnitOfWork.cs
|- IInvoiceNumberGenerator.cs

Invoices/Create/
Invoices/GetById/
Invoices/List/
Invoices/AddItem/
Invoices/UpdateItemQuantity/
Invoices/RemoveItem/

ProductCatalog/
|- IProductCatalogClient.cs
|- ProductSnapshot.cs
|- ProductCatalogException.cs

Issuance/Common/
|- IssuanceProcessDetails.cs
|- IInvoiceIssuanceProcessRepository.cs
|- IStockDeductionOutbox.cs

Issuance/PrintInvoice/
Issuance/GetProcess/
Issuance/MarkAwaitingStock/
Issuance/Complete/
Issuance/Reject/
Issuance/RequireManualIntervention/
```

Tipos pequenos poderão ser agrupados por feature quando isso melhorar coesão, sem criar arquivos artificiais.

### Infrastructure

```text
DependencyInjection.cs
Persistence/Repositories/InvoiceRepository.cs
Persistence/Repositories/InvoiceIssuanceProcessRepository.cs
Persistence/Queries/InvoiceReadService.cs
Persistence/Queries/IssuanceProcessReadService.cs
Persistence/UnitsOfWork/BillingUnitOfWork.cs
Persistence/InvoiceNumberGenerator.cs
Persistence/DatabaseErrorClassifier.cs
Persistence/Messaging/StockDeductionOutbox.cs
ProductCatalog/ProductCatalogClient.cs
ProductCatalog/ProductCatalogOptions.cs
```

### API

```text
Program.cs                                  (alterado)
Security/                                   (JWT, policies e usuário atual)
Errors/                                     (Problem Details e exception handler)
Correlation/                                (header e contexto da requisição)
Features/Invoices/                          (oito endpoints, validação e mapeamento)
Features/Issuance/                          (consulta e mapeamento)
ProductCatalog/ForwardAuthorizationHandler.cs
Observability/BillingMetrics.cs
```

### Testes

```text
tests/Billing/Korp.Billing.UnitTests/
|- Invoices/                                (ampliado)
|- Issuance/                                (ampliado)
|- Application/

tests/Billing/Korp.Billing.IntegrationTests/
|- AssemblyInfo.cs
|- Authentication/
|- Invoices/
|- Issuance/
|- ProductCatalog/
|- Persistence/                             (ampliado)
|- OpenApi/

tests/Architecture/Korp.ArchitectureTests/
|- ProjectReferenceRulesTests.cs            (ampliado se necessário)
```

## 6. Critério → implementação → prova

| Critérios | Implementação | Provas |
|---|---|---|
| CA-BIL-01 a CA-BIL-04 | ownership, sequence, criação e projeções | TST-BIL-001 a TST-BIL-006, TST-BIL-039 |
| CA-BIL-05 a CA-BIL-08 | cliente Inventory, snapshot e mutações | TST-BIL-009 a TST-BIL-016 |
| CA-BIL-09, CA-BIL-10 | invariantes, ETag e concorrência `xmin` | TST-BIL-007, TST-BIL-008, TST-BIL-017, TST-BIL-018 |
| CA-BIL-11 a CA-BIL-14 | transação de impressão, Outbox e constraints | TST-BIL-019 a TST-BIL-024 |
| CA-BIL-15 a CA-BIL-20 | máquina de estados e handlers internos | TST-BIL-025 a TST-BIL-031; transporte acumulado no SDD-07 |
| CA-BIL-21, CA-BIL-22 | consulta derivada e relógio controlado | TST-BIL-032, TST-BIL-033 |
| CA-BIL-23, CA-BIL-24 | JWT, bearer, políticas e erros | TST-BIL-010, TST-BIL-034, TST-BIL-035 |
| CA-BIL-25 | correlação, logs e métricas | TST-BIL-036; fluxo distribuído acumulado no SDD-07 |
| CA-BIL-26 | endpoints e metadata OpenAPI | TST-BIL-037 |
| CA-BIL-27, CA-BIL-28 | referências de camadas e escopo negativo | TST-BIL-001, TST-BIL-038 |

## 7. Estratégia de testes

### Unitários

- invariantes de Invoice, itens e processo;
- criação, consultas e validações dos handlers;
- produto duplicado sem chamada remota;
- tradução do catálogo indisponível e contrato inválido;
- ETag e precedência da idempotência;
- replays, estados terminais e resultados incompatíveis;
- cálculo de atraso e Retry-After com relógio controlado;
- cancelamento e classificação de resultados.

### Integração PostgreSQL/API

- migration, sequence, lacuna após rollback e constraints existentes;
- criação concorrente, detalhe e paginação;
- chamada HTTP interna, bearer, correlação, timeout, única repetição e validação de contrato;
- inclusão, atualização, remoção, bloqueio e concorrência real por `xmin`;
- transação atômica de `PrintInvoice`, rollback na serialização e conteúdo da Outbox;
- replay sequencial e concorrente da chave idempotente;
- disputa de chaves diferentes pela mesma invoice;
- transições internas atômicas; a prova de Inbox e transporte real acumula no SDD-07;
- JWT válido, ausente, adulterado, sem role e sem claims mínimas;
- `400`, `401`, `403`, `404`, `409`, `412`, `428`, `500` e `503`;
- OpenAPI e inexistência de rotas fiscais excluídas;
- logs e respostas sem sentinelas sensíveis.

Os testes que limpam o mesmo `billing_db` serão serializados somente no assembly de integração do Billing.

## 8. Ordem de implementação

1. aprovar este plano e o uso das dependências já centralizadas;
2. completar invariantes e testes do Domain;
3. implementar projeções, portas e casos de uso de notas e itens;
4. implementar persistência e cliente HTTP do Inventory;
5. validar consultas, snapshot, ETag e concorrência em PostgreSQL;
6. implementar `PrintInvoice`, Outbox e transições internas;
7. validar idempotência e atomicidade em PostgreSQL;
8. implementar JWT, policies, correlação, Problem Details e endpoints;
9. validar HTTP, OpenAPI, logs e métricas;
10. executar regressão e cobertura Docker-first;
11. atualizar matriz, índice e relatório Gate C.

## 9. Riscos e contenções

| Risco | Contenção |
|---|---|
| Chamada remota prolongar disputa | timeout de três segundos, uma repetição seletiva e `xmin` no commit |
| Snapshot parcial ou inválido | validar contrato completo antes de alterar a Invoice |
| Bearer vazar | handler dedicado, logs por allowlist e testes com sentinela |
| Replay depender do estado atual | consultar idempotência antes da elegibilidade e da versão |
| Duas emissões ativas | constraint parcial conhecida e recarga determinística do processo vencedor |
| Aceitar `202` sem durabilidade | Invoice, Process e Outbox na mesma transação |
| Resultado atrasado alterar tentativa nova | validar ProcessId, InvoiceId, estado e causalidade |
| Antecipar mensageria | nenhuma referência RabbitMQ, publisher ou consumer no SDD-06 |
| ETag expor detalhe do banco | codec opaco forte e testes de formatos rejeitados |
| Escopo fiscal crescer | teste arquitetural negativo para preço, tributo, cliente, pagamento e PDF |
| Cobertura artificial | exclusões somente conforme ADR-014, com valores brutos publicados |

## 10. Gate C previsto

- build Release com zero erro e sem novo warning injustificado;
- 39 testes planejados com prova ou deferimento cumulativo explícito;
- PostgreSQL real comprova sequence, constraints, transações, `xmin` e idempotência;
- Inventory HTTP comprova snapshot, bearer, correlação e retry limitado;
- autenticação e policies são validadas localmente;
- OpenAPI contém exatamente as oito rotas aprovadas;
- nenhuma rota fiscal, PDF ou acesso direto ao banco do Inventory existe;
- line coverage manual aplicável mínima de 80% por assembly;
- branch coverage publicada;
- regressão integral permanece aprovada;
- documentação e matriz refletem implementação e limites reais.

## 11. Decisões solicitadas

Aprovar ou ajustar:

1. persistir a Outbox no SDD-06, deixando publisher, consumers e Inbox operacional para o SDD-07;
2. implementar as quatro transições internas agora, testáveis diretamente pela Application, e conectar o transporte somente no SDD-07;
3. usar `HttpClient` com timeout de três segundos e uma repetição seletiva explícita, sem Polly;
4. propagar Authorization em handler da API e manter o cliente de catálogo na Infrastructure;
5. reutilizar JwtBearer, Mvc.Testing e JsonWebTokens já centralizados, sem nova família de dependências;
6. métricas via `System.Diagnostics.Metrics`, sem pacote adicional;
7. considerar TST-BIL-025 a TST-BIL-031 parcialmente cumulativos até o SDD-07 somente nos aspectos que exigirem publisher confirm, Inbox ou consumer real.

Nenhum código funcional do SDD-06 será escrito antes da aprovação deste plano pelo engenheiro.

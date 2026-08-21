# Plano de Implementação - SDD-02

> Status: Aprovado
> Data: 2026-08-20
> SDD: `SDD-02-MODELAGEM-DE-DADOS.md`
> Gate: B - Plano de implementação
> Baseline: `AUDITORIA-DOCUMENTAL-002.md` aprovada
> Aprovação do Gate B: 2026-08-20

---

## 1. Resultado esperado

Implementar a modelagem de domínio e persistência aprovada para Identity, Inventory e Billing, com bancos PostgreSQL, migrations e testes reais, sem antecipar APIs, mensageria ou casos de uso completos dos SDDs posteriores.

Ao final:

- cada serviço possuirá modelo, `DbContext` e migration inicial independentes;
- invariantes puras estarão protegidas no domínio;
- constraints, índices, sequences e tokens `xmin` complementarão o domínio;
- Inbox e Outbox existirão somente em Inventory e Billing;
- os 14 critérios `CA-DATA-*` possuirão provas executáveis;
- nenhum serviço conhecerá banco, entidade ou migration de outro serviço.

---

## 2. Estado atual auditado

O SDD-01 deixou disponíveis:

- solution .NET 10 e gerenciamento central de pacotes;
- projetos `Domain`, `Application`, `Infrastructure` e `Api` dos três serviços;
- projetos unitários e de integração por serviço;
- regras arquiteturais iniciais;
- Docker Compose apenas com comandos de tooling e hosts mínimos.

Ainda não existem entidades, `DbContext`, pacotes EF Core, migrations, PostgreSQL no Compose ou testes funcionais dos três modelos. Isso é coerente com a baseline e permite implementar o SDD-02 sem preservar comportamento legado conflitante.

Não foi identificada divergência entre o código atual e o SDD aprovado.

---

## 3. Dependências a adicionar

As versões estáveis compatíveis com .NET/EF Core 10 serão verificadas e fixadas centralmente em `Directory.Packages.props` antes da restauração.

| Dependência | Projetos | Justificativa aprovada |
|---|---|---|
| `Microsoft.EntityFrameworkCore` | Infrastructures e testes necessários | Modelo, transações e migrations EF Core |
| `Microsoft.EntityFrameworkCore.Design` | Infrastructures | Geração controlada de migrations; `PrivateAssets=all` |
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | Identity.Infrastructure | Persistência oficial de usuários e papéis |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | Infrastructures | Provider PostgreSQL, `xmin`, `jsonb`, sequence e índices específicos |
| `EFCore.NamingConventions` | Infrastructures | Convenção automática `snake_case` aprovada |

Não será adicionada biblioteca de repositório, mediator, faker, assertion, container orchestration ou migration automática. Os testes acessarão instâncias PostgreSQL descartáveis fornecidas pelo perfil Docker desta fase.

---

## 4. Arquivos centrais e infraestrutura de teste

### Alterar

| Arquivo | Alteração |
|---|---|
| `Directory.Packages.props` | Fixar as cinco dependências aprovadas |
| `compose.yaml` | Acrescentar perfil isolado de persistência/testes com três PostgreSQL, runners e volumes descartáveis delimitados |
| projetos `*.Infrastructure.csproj` | Referenciar provider e pacotes próprios de cada serviço |
| seis projetos `*.UnitTests.csproj` e `*.IntegrationTests.csproj` aplicáveis | Referências estritamente necessárias ao modelo e testes |

### Criar em testes compartilhados apenas por código-fonte

Não será criado assembly compartilhado de persistência. Pequenas fixtures específicas permanecem em cada projeto de integração para impedir que configuração ou modelo de um serviço seja reutilizado por outro.

Cada fixture:

- recebe connection string do ambiente de teste;
- cria um banco descartável com nome delimitado;
- aplica migrations por API de testes controlada;
- remove somente o banco criado pela própria execução;
- nunca usa `EnsureCreated`;
- desabilita paralelismo somente quando o mesmo recurso exigir serialização.

---

## 5. Identity

### Arquivos a criar em `Korp.Identity.Infrastructure`

```text
Persistence/
  ApplicationUser.cs
  IdentityDbContext.cs
  IdentityModelConfiguration.cs
  IdentityDatabaseInitializer.cs
  Migrations/
    <timestamp>_InitialIdentity.cs
    <timestamp>_InitialIdentity.Designer.cs
    IdentityDbContextModelSnapshot.cs
```

Responsabilidades:

- `ApplicationUser : IdentityUser<Guid>` sem campos especulativos;
- `IdentityDbContext` baseado em `IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`;
- tabelas Identity em nomes canônicos `snake_case`;
- e-mail normalizado único e constraints do framework preservadas;
- inicializador idempotente de `Admin`, usando configuração fornecida pelo chamador;
- senha processada somente pelo password hasher e nunca embutida em migration;
- usuário existente não recebe redefinição silenciosa de senha.

Identity.Domain continuará vazio porque não há regra de domínio própria aprovada nesta fase.

### Testes

```text
tests/Identity/Korp.Identity.IntegrationTests/Persistence/
  IdentityMigrationTests.cs
  IdentityDatabaseIsolationTests.cs
  IdentitySeedTests.cs
  IdentitySchemaTests.cs
```

---

## 6. Inventory

### Arquivos a criar em `Korp.Inventory.Domain`

```text
Products/
  Product.cs
  ProductCode.cs
  ProductErrors.cs
StockMovements/
  StockMovement.cs
  StockMovementType.cs
```

Responsabilidades:

- factory de `Product` valida UUIDs, código, descrição, saldo e timestamps;
- normalização de código por trim e uppercase invariant;
- alteração de saldo protege quantidade positiva e saldo não negativo;
- criação do movimento ocorre junto ao resultado da baixa, com saldos anterior e posterior consistentes;
- `StockMovement` não expõe mutações funcionais posteriores.

`ProductCode` será Value Object porque concentra normalização, formato e igualdade canônica reais. Não serão criados Value Objects para todo campo primitivo.

### Arquivos a criar em `Korp.Inventory.Infrastructure`

```text
Persistence/
  InventoryDbContext.cs
  Configurations/ProductConfiguration.cs
  Configurations/StockMovementConfiguration.cs
  Messaging/InboxMessage.cs
  Messaging/OutboxMessage.cs
  Messaging/InboxMessageConfiguration.cs
  Messaging/OutboxMessageConfiguration.cs
  Migrations/
    <timestamp>_InitialInventory.cs
    <timestamp>_InitialInventory.Designer.cs
    InventoryDbContextModelSnapshot.cs
```

Responsabilidades:

- mapear `Product.Version` e `OutboxMessage.Version` para `xmin`;
- mapear constraints, unicidades e índices exatamente como o SDD;
- armazenar enum como texto canônico e payload Outbox como `jsonb`;
- manter autoria externa sem FK;
- representar leases da Outbox e identidade/hash da Inbox;
- não implementar consumer, publisher ou retry RabbitMQ.

### Testes

```text
tests/Inventory/Korp.Inventory.UnitTests/
  Products/ProductTests.cs
  Products/ProductCodeTests.cs
  StockMovements/StockMovementTests.cs

tests/Inventory/Korp.Inventory.IntegrationTests/Persistence/
  InventoryMigrationTests.cs
  ProductConstraintTests.cs
  ProductConcurrencyTests.cs
  StockMovementPersistenceTests.cs
  InboxPersistenceTests.cs
  OutboxPersistenceTests.cs
  InventorySchemaTests.cs
```

---

## 7. Billing

### Arquivos a criar em `Korp.Billing.Domain`

```text
Invoices/
  Invoice.cs
  InvoiceItem.cs
  InvoiceStatus.cs
  InvoiceErrors.cs
Issuance/
  InvoiceIssuanceProcess.cs
  InvoiceIssuanceProcessStatus.cs
  IssuanceErrors.cs
```

Responsabilidades:

- `Invoice` nasce `Open`, desbloqueada e controla integralmente seus itens;
- inclusão, alteração e remoção protegem snapshot, quantidade, duplicidade, fechamento e bloqueio;
- toda alteração de coleção atualiza `UpdatedAtUtc`, disputando a versão da raiz;
- início, conclusão, rejeição e intervenção protegem combinações de estado e timestamps;
- estados terminais não podem ser sobrescritos;
- nenhuma operação de impressão, HTTP ou mensageria será implementada aqui.

### Arquivos a criar em `Korp.Billing.Infrastructure`

```text
Persistence/
  BillingDbContext.cs
  Configurations/InvoiceConfiguration.cs
  Configurations/InvoiceItemConfiguration.cs
  Configurations/InvoiceIssuanceProcessConfiguration.cs
  Messaging/InboxMessage.cs
  Messaging/OutboxMessage.cs
  Messaging/InboxMessageConfiguration.cs
  Messaging/OutboxMessageConfiguration.cs
  Migrations/
    <timestamp>_InitialBilling.cs
    <timestamp>_InitialBilling.Designer.cs
    BillingDbContextModelSnapshot.cs
```

Responsabilidades:

- criar `invoice_number_seq` e usá-la como default de `Invoice.Number`;
- mapear coleção de itens com FK local e cascade;
- mapear processos com FK restrita, idempotency key global e índice parcial de processo ativo;
- mapear `xmin`, constraints temporais, estados textuais e índices operacionais;
- manter referências de Product e usuário como UUIDs externos sem FK;
- manter Inbox e Outbox locais, sem abstração persistente compartilhada.

### Testes

```text
tests/Billing/Korp.Billing.UnitTests/
  Invoices/InvoiceTests.cs
  Invoices/InvoiceItemTests.cs
  Issuance/InvoiceIssuanceProcessTests.cs

tests/Billing/Korp.Billing.IntegrationTests/Persistence/
  BillingMigrationTests.cs
  InvoiceSequenceTests.cs
  InvoiceConstraintTests.cs
  InvoiceConcurrencyTests.cs
  IssuanceConcurrencyTests.cs
  IssuanceProcessPersistenceTests.cs
  InboxPersistenceTests.cs
  OutboxPersistenceTests.cs
  BillingSchemaTests.cs
```

---

## 8. Mapa de critérios para implementação e prova

| Critério | Implementação principal | Provas planejadas |
|---|---|---|
| `CA-DATA-01` | três `DbContext`, migrations e credenciais independentes | `TST-DATA-001`, `TST-DATA-002` |
| `CA-DATA-02` | modelo Identity e inicializador Admin | `TST-DATA-002`, `TST-DATA-003` |
| `CA-DATA-03` | `Product`, `ProductCode` e configuração/constraints | `TST-DATA-004`, `TST-DATA-005` |
| `CA-DATA-04` | `Product.Version`, `xmin` e reavaliação explícita no teste | `TST-DATA-006` |
| `CA-DATA-05` | `StockMovement`, transação e duas unicidades | `TST-DATA-007`, `TST-DATA-008` |
| `CA-DATA-06` | `Invoice`, sequence e constraints de estado | `TST-DATA-009`, `TST-DATA-010` |
| `CA-DATA-07` | coleção controlada, snapshots e concorrência da raiz | `TST-DATA-010`, `TST-DATA-011` |
| `CA-DATA-08` | trava da invoice, processo ativo e Outbox na mesma transação de teste | `TST-DATA-012` |
| `CA-DATA-09` | máquina de estados de `InvoiceIssuanceProcess` | `TST-DATA-013` |
| `CA-DATA-10` | Inbox local e confirmação transacional dos efeitos de teste | `TST-DATA-014` |
| `CA-DATA-11` | Outbox, lease, falha e confirmação explícita abstrata | `TST-DATA-015`, `TST-DATA-016` |
| `CA-DATA-12` | migrations iniciais e detecção de model change | `TST-DATA-001`, `TST-DATA-017` |
| `CA-DATA-13` | três UUIDs de autoria sem relacionamento externo | `TST-DATA-018` |
| `CA-DATA-14` | schemas mínimos e lista negativa automatizada | `TST-DATA-019` |

Os IDs `TST-DATA-*` serão usados nos nomes/display names dos testes e registrados na matriz após execução.

---

## 9. Ordem de implementação

1. fixar dependências e preparar PostgreSQL descartável de testes;
2. implementar e testar regras puras de Inventory;
3. implementar e testar regras puras de Billing;
4. configurar e migrar Identity;
5. configurar e migrar Inventory;
6. configurar e migrar Billing;
7. implementar modelos técnicos locais de Inbox e Outbox;
8. executar testes de constraints, sequences e concorrência real;
9. gerar scripts idempotentes e inspecionar SQL;
10. verificar ausência de model changes pendentes;
11. executar build, testes, arquitetura e cobertura;
12. atualizar matriz, índice e relatório Gate C.

---

## 10. Validações do Gate C

Todas serão executadas por Docker Compose:

- restore e build Release com warnings como erros;
- 19 provas `TST-DATA-*` com PostgreSQL real;
- aplicação das três migrations do zero;
- repetição idempotente do seed do Identity;
- concorrência com contextos e transações independentes;
- inspeção de tabelas, colunas, constraints, índices, FKs e sequence;
- geração de script idempotente por serviço;
- detecção de model changes sem migration;
- testes de arquitetura da solution;
- cobertura por assembly manual relevante, com gate mínimo de 80% quando aplicável;
- busca por segredo, `EnsureCreated`, entidade externa, tabela especulativa e dependência entre serviços.

Se a versão efetiva das ferramentas não oferecer detecção confiável de model changes por comando, será usada comparação determinística entre modelo e snapshot em teste, e a limitação será registrada.

---

## 11. Riscos e respostas

| Risco | Resposta planejada |
|---|---|
| `xmin` divergir da API esperada do provider EF Core 10 | Criar uma prova mínima do mapeamento antes das demais configurações; não trocar estratégia silenciosamente |
| Identity exigir customização extensa para nomes físicos | Aplicar NamingConventions e somente overrides necessários, preservando constraints do framework |
| Factory de domínio conflitar com materialização EF | Construtor privado protegido e configuração explícita, sem setters públicos para facilitar o ORM |
| Teste de concorrência produzir falso positivo | Usar contextos e transações realmente independentes e verificar estado final no banco |
| Fixtures apagarem recurso incorreto | Banco UUID por execução, validação de prefixo e remoção apenas do alvo conhecido |
| Modelo técnico virar abstração compartilhada | Manter Inbox/Outbox duplicadas por propriedade de serviço, compartilhando apenas conceitos documentados |
| SDD-02 antecipar casos de uso posteriores | Implementar apenas métodos de domínio e coordenação mínima necessária às provas de persistência |
| Cobertura incentivar teste artificial de migrations | Excluir código gerado e medir somente lógica manual relevante |

---

## 12. Fora deste plano

- endpoints, DTOs e OpenAPI;
- login e emissão de JWT;
- repositories e casos de uso funcionais completos;
- publishers, consumers, exchanges, filas ou RabbitMQ.Client;
- roteamento do Gateway;
- frontend Angular;
- dados demonstrativos de Product;
- aplicação automática de migration no startup;
- ambiente final do SDD-11;
- documentação e vídeo finais.

---

## 13. Arquivos documentais ao concluir

- `docs/RELATORIO-IMPLEMENTACAO-SDD-02.md`;
- `docs/MATRIZ-RASTREABILIDADE.md`;
- `docs/README.md`;
- `docs/SDD-02-MODELAGEM-DE-DADOS.md`, somente se uma descoberta exigir atualização previamente aprovada;
- este plano, apenas se o Gate B for revisado antes da execução.

---

## 14. Condição para iniciar

A implementação começa somente após aprovação explícita deste Gate B. A aprovação autoriza os arquivos, dependências e validações delimitados neste documento e não autoriza funcionalidades dos SDDs 03 a 13.

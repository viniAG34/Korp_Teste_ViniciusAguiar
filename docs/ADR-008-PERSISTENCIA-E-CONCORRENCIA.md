# ADR-008 - Persistência, Transações e Concorrência

> Status: Aprovada
> Data: 2026-08-16
> Dependências: ADR-005, ADR-006, ADR-007 e ADR-013
> Atualizada em: 2026-08-17 para incorporar o banco do Identity.Service

---

## Decisão

- PostgreSQL será o banco relacional.
- Identity, Inventory e Billing terão bancos lógicos e credenciais exclusivos.
- Os três bancos poderão compartilhar a mesma instância PostgreSQL no ambiente local.
- Entity Framework Core 10 será o ORM.
- `Npgsql.EntityFrameworkCore.PostgreSQL` 10 será o provider.
- A modelagem seguirá Code First com Fluent API.
- Migrations serão separadas por serviço, versionadas e revisadas.
- `EnsureCreated` não será utilizado.
- Migrations serão executadas por processos controlados, separados das APIs.
- Consultas somente leitura utilizarão `AsNoTracking`.
- Lazy loading não será habilitado.
- Operações assíncronas propagarão `CancellationToken`.
- `DbContext` exercerá Unit of Work.
- Não haverá transação distribuída entre bancos.

---

## Bancos por serviço

```text
Identity.Service  -> identity_db
Inventory.Service -> inventory_db
Billing.Service   -> billing_db
```

Nenhum serviço poderá usar credenciais ou consultar tabelas do outro banco. O API Gateway não possuirá banco. Identity não participa das transações ou da mensageria de emissão.

---

## Migrations

- uma sequência de migrations por `DbContext`;
- arquivos de migration versionados no Git;
- migrations geradas somente após alteração consciente do modelo;
- SQL e operações destrutivas revisados antes da aplicação;
- verificação de model changes pendentes no pipeline;
- execução local por containers migrators no Docker Compose;
- API executada com permissões normais, sem depender de permissão permanente para alterar schema.

---

## Transações locais

O processamento de todos os itens de uma nota no Estoque será atômico:

```text
validate all products
    -> deduct all balances
        -> record stock movements
            -> persist Outbox message
                -> commit
```

Qualquer falha provoca rollback completo. Não haverá baixa parcial.

No Faturamento, estado do processo e mensagem Outbox também serão persistidos na mesma transação local.

---

## Concorrência otimista

O controle de concorrência do saldo utilizará a coluna de sistema `xmin` do PostgreSQL, mapeada como token de concorrência pelo provider Npgsql.

Em conflito, o EF Core deverá lançar `DbUpdateConcurrencyException`. O fluxo recarregará o estado em novo escopo e reavaliará as regras. No cenário de duas notas disputando a última unidade, apenas uma será confirmada; a outra será recusada por saldo insuficiente após ler o estado atualizado.

---

## Constraints e índices

O banco funcionará como segunda linha de defesa, com garantias incluindo:

- product code único;
- invoice number único;
- idempotency key única no escopo definido;
- event identifier único na Inbox;
- saldo não negativo;
- quantidade positiva;
- campos obrigatórios como `NOT NULL`;
- tamanhos e tipos explícitos;
- índices orientados às consultas reais.

O domínio continua sendo a primeira linha de proteção.

---

## Identificadores, sequência e tempo

- entidades utilizarão UUID;
- eventos utilizarão UUID;
- invoice number utilizará sequence do PostgreSQL;
- lacunas na numeração serão permitidas;
- sequência garante unicidade e ordenação, não ausência absoluta de lacunas;
- datas serão armazenadas em UTC;
- nomes físicos utilizarão `snake_case`.

---

## Referências

- [EF Core 10](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew)
- [EF Core migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying)
- [EF Core transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions)
- [Npgsql concurrency tokens](https://www.npgsql.org/efcore/modeling/concurrency.html)

# Relatório de Implementação - SDD-05

> Gate: C - Conclusão
> Status: Aprovado pelo engenheiro
> Data: 2026-08-21
> SDD: `SDD-05-INVENTORY-SERVICE.md`
> Plano: `PLANO-IMPLEMENTACAO-SDD-05.md`

---

## 1. Resultado

O Inventory Service implementa cadastro e consultas de produtos, snapshot interno para Billing e baixa atômica de estoque. Inventory permanece proprietário exclusivo de Product, Balance e StockMovement.

Fornecedores, compras, entradas, ajustes manuais, edição, exclusão, endpoint de baixa e RabbitMQ não foram introduzidos.

## 2. Funcionalidades entregues

- `POST /api/v1/products`, protegido por `AdminOnly`;
- `GET /api/v1/products`, protegido por `AuthenticatedUser` e paginado por código/ID;
- `GET /api/v1/products/{productId}`, protegido por `AuthenticatedUser`;
- `GET /api/v1/internal/products/{productId}`, protegido por `AdminOnly` e sem saldo;
- normalização e unicidade de código;
- descrição com trim, limites e rejeição de caracteres de controle;
- saldo inicial inteiro e não negativo, sem movimento fictício;
- autoria exclusivamente pela claim `sub`;
- JWT HS256 validado localmente com issuer, audience, lifetime, algoritmo e claims mínimas;
- baixa de múltiplos produtos em transação `ReadCommitted`;
- movimentos consistentes e imutáveis;
- reavaliação de conflito `xmin` em até três unidades de trabalho novas;
- repetição lógica equivalente sem novo efeito;
- conteúdo divergente para a mesma invoice como inconsistência técnica;
- Problem Details sanitizados;
- métricas nativas de criação, outcomes da baixa, conflitos e duração.

## 3. Arquitetura

Application recebeu portas específicas de escrita, leitura e unidade de trabalho, sem `DbContext`, `IQueryable`, HTTP ou RabbitMQ. Infrastructure implementa projeções `AsNoTracking`, repositório EF Core, transações e classificação de erros. Domain continua responsável pelas invariantes e pela mutação do saldo.

Não foi criada migration: o esquema aprovado no SDD-02 já atendia ao comportamento implementado.

## 4. Dependência aprovada

Foi adicionado `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.11 somente à API. `Microsoft.AspNetCore.Mvc.Testing` e `Microsoft.IdentityModel.JsonWebTokens` são usados somente pela suíte de integração.

Nenhuma biblioteca de mediator, validação, mocks, métricas ou retry foi adicionada.

## 5. Critérios de aceite

| Critérios | Resultado |
|---|---|
| CA-INV-01 a CA-INV-05 | Atendidos por ownership, criação, saldo, unicidade e consultas reais |
| CA-INV-06 | Snapshot atendido; prova de não exposição pelo Gateway diferida ao SDD-08 |
| CA-INV-07 a CA-INV-10 | Atendidos por transação, rejeições sem efeito parcial e movimentos persistidos |
| CA-INV-11 e CA-INV-14 | Atendidos pela combinação de conflito PostgreSQL real e handler com três unidades novas |
| CA-INV-12 e CA-INV-13 | Atendidos por repetição equivalente e divergência testadas em PostgreSQL |
| CA-INV-15 e CA-INV-16 | Atendidos por classificação de falhas, JWT, policies e autoria |
| CA-INV-17 | Métricas e log de criação atendidos; logs correlacionados da baixa acumulam no SDD-07 |
| CA-INV-18 | Rotas e escopo do OpenAPI atendidos; apresentação consolidada acumula no SDD-08/12 |
| CA-INV-19 | Atendido por referências, portas específicas e ausência de `IQueryable` público |

## 6. Testes e validações

### Inventory

- unitários: 19 aprovados;
- integração com PostgreSQL e API real: 23 aprovados;
- total: 42 aprovados, 0 falhas, 0 ignorados.

### Regressão

A regressão integral anterior à última prova HTTP executou 121 testes com 0 falhas. A prova adicional elevou a baseline lógica para 122 testes. Depois dela:

- Inventory: 42/42 aprovado;
- build Release integral: 0 erros e 0 warnings.

A suíte de Gateway permanece vazia até o SDD-08.

### Cobertura final

| Assembly | Linhas manuais aplicáveis | Branches |
|---|---:|---:|
| `Korp.Inventory.Domain` | 80,50% | 50,00% |
| `Korp.Inventory.Application` | 87,82% | 63,89% |
| `Korp.Inventory.Infrastructure` | 84,55% | 35,71% |
| `Korp.Inventory.Api` | 84,69% | 56,98% |

Código gerado do OpenAPI, migrations, snapshot e factory de design time foram excluídos somente do cálculo manual aplicável, conforme ADR-014. Percentuais brutos publicados pelo Coverlet: Domain 80,50%, Application 87,82%, Infrastructure 51,62% e API 65,61%.

## 7. Ocorrências técnicas

1. O teste de três conflitos revelou que a última `InventoryConcurrencyException` escapava sem classificação estável; o handler agora encerra com `InventoryConsistencyException` após o limite.
2. Projeções de `ProductCode` materializam somente o valor convertido necessário e continuam aplicando ordenação/paginação no banco.
3. Testes que truncam o mesmo `inventory_db` foram serializados apenas no assembly de integração do Inventory.
4. A classificação de indisponibilidade percorre a cadeia interna de exceções, mas somente reconhece erros de banco e timeout.

Nenhuma ocorrência alterou o escopo ou introduziu retry cego.

## 8. Limitações e deferimentos

- a baixa ainda não possui consumer RabbitMQ, Inbox/Outbox operacional ou evento de resposta; pertencem ao SDD-07;
- a rota interna existe e exige `AdminOnly`, mas sua ausência na borda só poderá ser comprovada após a configuração do Gateway no SDD-08;
- logs distribuídos da baixa exigem envelope, correlação e adapter do SDD-07;
- não existe operação de entrada de estoque; `InitialBalance` continua sendo a origem inicial simplificada aprovada;
- nenhuma garantia exactly-once é declarada.

## 9. Avaliação do Gate C

Recomendação: **aprovar o SDD-05 com deferimentos cumulativos explícitos para SDD-07 e SDD-08**.

O primeiro conjunto funcional obrigatório do desafio está implementado, protegido, persistido e testado. Inventory está apto a fornecer catálogo ao Billing e a receber posteriormente o adapter de mensageria sem alterar suas regras centrais.

## 10. Próximo passo

Após aprovação e commit deste ponto-chave, iniciar o Gate B do SDD-06 - Billing Service.

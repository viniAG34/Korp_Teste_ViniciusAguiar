# ADR-004 - Fronteiras entre API Gateway e Mensageria

> Status: Aprovada
> Data: 2026-08-16
> Atualizada em: 2026-08-17 para incorporar o serviço de Identidade aprovado no ADR-013 e o versionamento do ADR-010

---

## Contexto

O sistema possui dois caminhos de comunicação com finalidades diferentes:

- requisições HTTP iniciadas pelo usuário no Angular;
- mensagens assíncronas trocadas entre Estoque e Faturamento durante a emissão.

É necessário impedir que o API Gateway assuma responsabilidades de mensageria ou que o Serviço de Faturamento se transforme em proxy para operações pertencentes ao Estoque.

---

## Decisão

O API Gateway será a entrada HTTP única do Angular e terá conexão HTTP direta com as APIs de Identidade, Estoque e Faturamento.

```text
Angular
   -> API Gateway
       -> Identity API
       -> Inventory API
       -> Billing API
```

O RabbitMQ será utilizado exclusivamente para mensagens internas entre as aplicações de Estoque e Faturamento.

```text
Billing Publisher
   -> RabbitMQ
       -> Inventory Consumer

Inventory Publisher
   -> RabbitMQ
       -> Billing Consumer
```

Não haverá conexão de mensageria entre RabbitMQ e API Gateway.

Billing poderá consultar diretamente uma API HTTP interna do Inventory para validar a existência do produto e obter seu snapshot ao incluir um item. Essa chamada serviço-a-serviço não passa pelo Gateway e não é utilizada para executar a baixa de estoque.

---

## Motivo das conexões HTTP do Gateway

O Angular precisa executar operações que pertencem ao domínio de Estoque:

- cadastrar produtos;
- listar produtos;
- consultar um produto e seu saldo;
- obter produtos para seleção durante a montagem de uma nota.

Essas requisições são encaminhadas diretamente pelo Gateway ao Serviço de Estoque. O Serviço de Faturamento não atua como proxy dessas operações.

Exemplos de roteamento previstos:

```text
POST /api/v1/auth/login          -> Identity.Service

GET  /api/v1/products             -> Inventory.Service
POST /api/v1/products             -> Inventory.Service
GET  /api/v1/products/{id}        -> Inventory.Service

GET  /api/v1/invoices             -> Billing.Service
POST /api/v1/invoices             -> Billing.Service
POST /api/v1/invoices/{id}/print  -> Billing.Service
```

Os caminhos definitivos e o versionamento serão formalizados no SDD de contratos HTTP.

---

## Limites do API Gateway

O Gateway:

- recebe e encaminha chamadas HTTP;
- aplica políticas pertencentes à borda;
- conhece rotas e destinos, não regras de negócio;
- não acessa bancos;
- não publica nem consome eventos de emissão;
- não coordena a baixa de estoque;
- não agrega persistência de Identity, Inventory ou Billing;
- não consulta nem armazena usuários ou credenciais.

---

## Limites do RabbitMQ

O RabbitMQ:

- recebe mensagens publicadas pelos serviços;
- entrega mensagens aos consumers dos serviços;
- não chama endpoints HTTP do Gateway;
- não acessa os bancos de negócio;
- não aplica regras de domínio;
- não atualiza saldo ou status de nota diretamente.

Publishers e consumers pertencem às aplicações dos microsserviços. Cada consumer aplica os efeitos da mensagem exclusivamente no banco do serviço ao qual pertence.

---

## Fluxo da emissão

```text
Angular
   -> Gateway
       -> Faturamento API
           -> publica StockDeductionRequested
               -> RabbitMQ
                   -> Estoque Consumer
                       -> atualiza Banco Estoque
                       -> publica resultado
                           -> RabbitMQ
                               -> Faturamento Consumer
                                   -> atualiza Banco Faturamento
```

O caminho HTTP termina quando o Faturamento aceita o início do processamento. O restante do fluxo ocorre internamente por mensageria e pode continuar mesmo que o usuário feche a página.

---

## Consequências

- O Angular utiliza uma única origem HTTP.
- Identidade permanece proprietária de usuários e emissão de tokens, sem participar do RabbitMQ.
- Estoque e Faturamento permanecem donos de suas APIs e bancos.
- O Faturamento não acumula responsabilidade de cadastro ou consulta de produtos.
- O Gateway permanece stateless e independente da mensageria.
- Falhas do RabbitMQ não impedem necessariamente consultas HTTP já independentes da emissão.
- A observabilidade deverá correlacionar a requisição HTTP inicial com as mensagens posteriores.

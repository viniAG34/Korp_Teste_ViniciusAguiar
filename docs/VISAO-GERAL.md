# Visão Geral - Sistema de Emissão de Notas Fiscais

> Status: Aprovado
> Última atualização: 2026-08-17
> Fonte: `../teste tecnico KORP ERP.pdf`

---

## 1. Contexto

Este projeto atende ao desafio técnico da Korp ERP para construção de uma aplicação Angular integrada a uma arquitetura de microsserviços. O sistema deve permitir cadastrar produtos, criar notas fiscais simplificadas, emitir e imprimir essas notas, atualizar o estoque e demonstrar recuperação diante da falha de um dos serviços.

Todo o backend será desenvolvido exclusivamente em C# com .NET 10 LTS, conforme [`ADR-002-BACKEND-E-REQUISITOS-OPCIONAIS.md`](ADR-002-BACKEND-E-REQUISITOS-OPCIONAIS.md) e [`ADR-005-VERSAO-DOTNET-E-LEGADO.md`](ADR-005-VERSAO-DOTNET-E-LEGADO.md).

O projeto será desenvolvido por Spec-Driven Development (SDD). Regras, critérios de aceite e decisões técnicas serão aprovados antes da implementação.

O prazo indicado no enunciado é de sete dias corridos a partir do recebimento do desafio. A priorização deverá favorecer um fluxo principal completo, verificável e bem apresentado.

---

## 2. Interpretação adotada

O termo "nota fiscal" é interpretado como um documento interno e simplificado de saída de estoque, e não como uma nota fiscal eletrônica com validade jurídica ou integração tributária.

O sistema implementa somente esta etapa do ciclo de um ERP:

```text
Produto previamente disponível em estoque
    -> produto incluído em uma nota
        -> nota emitida
            -> estoque baixado
                -> documento disponibilizado para impressão
```

O saldo informado ao cadastrar um produto representa seu estoque inicial. Compra, recebimento e origem desse saldo não são modelados.

Esta interpretação está formalizada em [`ADR-001-ESCOPO-NOTA-FISCAL-SIMPLIFICADA.md`](ADR-001-ESCOPO-NOTA-FISCAL-SIMPLIFICADA.md).

---

## 3. Objetivo do produto

Entregar uma aplicação web capaz de:

1. cadastrar e consultar produtos com saldo;
2. criar notas fiscais abertas com numeração sequencial;
3. adicionar múltiplos produtos e suas quantidades;
4. emitir uma nota aberta;
5. baixar os produtos utilizados sem permitir inconsistência de estoque;
6. fechar a nota após a confirmação da baixa;
7. disponibilizar uma representação imprimível;
8. impedir a impressão de uma nota fechada;
9. informar ao usuário o andamento e as falhas da emissão;
10. recuperar o processamento após uma falha temporária entre os serviços.

---

## 4. Escopo funcional

### 4.1 Produtos e estoque

- cadastro de produto;
- código, descrição e saldo como campos obrigatórios;
- consulta dos produtos cadastrados;
- consulta do saldo atual;
- baixa de vários produtos durante a emissão da nota;
- proteção contra saldo negativo;
- controle de concorrência;
- registro idempotente da baixa.

### 4.2 Notas fiscais

- criação de nota com número sequencial;
- status inicial `Aberta`;
- inclusão de múltiplos produtos;
- quantidade por item;
- consulta e detalhamento da nota;
- emissão somente quando aberta;
- fechamento após confirmação da baixa;
- representação imprimível;
- rejeição de qualquer tentativa de imprimir uma nota fechada.

### 4.3 Falhas e recuperação

- cenário demonstrável de indisponibilidade do Serviço de Estoque;
- indicação de processamento no frontend;
- feedback compreensível durante indisponibilidade ou recusa;
- preservação da solicitação de emissão;
- retomada automática quando o serviço voltar;
- prevenção contra processamento duplicado;
- tratamento separado de falha técnica e falha de negócio.

---

## 5. Fora do escopo

- fornecedores e compras;
- clientes e pedidos de venda;
- entrada de mercadoria;
- tipos, modelos e séries fiscais;
- preços, descontos e valores totais;
- impostos e regras tributárias;
- integração com serviços fiscais externos;
- cancelamento, devolução e carta de correção;
- contas a pagar ou receber;
- inteligência artificial;
- deploy em nuvem, salvo decisão posterior;
- requisitos de escala incompatíveis com o porte do desafio.

Autenticação e autorização não fazem parte do enunciado original, mas foram incluídas deliberadamente como diferencial técnico. O recorte implementará login administrativo, JWT e proteção em profundidade, sem transformar o desafio em uma plataforma completa de identidade, conforme [`ADR-013-AUTENTICACAO-E-SERVICO-DE-IDENTIDADE.md`](ADR-013-AUTENTICACAO-E-SERVICO-DE-IDENTIDADE.md).

---

## 6. Arquitetura macro

O sistema terá dois microsserviços de domínio, um serviço de apoio para Identidade e um API Gateway. Os fluxos são separados abaixo para não sugerir conexões inexistentes.

### Fluxo HTTP externo

```text
Angular SPA
    -> HTTP -> API Gateway
                   |-- HTTP -> Identity API
                   |-- HTTP -> Estoque API
                   `-- HTTP -> Faturamento API
```

### Fluxo assíncrono interno

```text
Billing Publisher
    -> RabbitMQ
        -> Inventory Consumer

Inventory Publisher
    -> RabbitMQ
        -> Billing Consumer
```

### Fluxo síncrono interno de referência

```text
Billing.Service
    -> HTTP interno -> Inventory.Service
        -> valida Product e retorna Id, Code e Description
```

Essa consulta ocorre somente ao incluir um item para validar a referência e obter o snapshot. Ela não passa pelo Gateway, não consulta saldo e não substitui a baixa assíncrona via RabbitMQ.

### Fluxo de persistência

```text
Identity.Service  -> Identity Database
Inventory.Service -> Inventory Database
Billing.Service   -> Billing Database
```

Não existe conexão entre API Gateway e RabbitMQ. Também não existe conexão entre RabbitMQ e os bancos de negócio.

O RabbitMQ se comunica exclusivamente com as aplicações dos serviços. Ele não consulta, replica nem atualiza diretamente os bancos de Estoque ou Faturamento.

O Gateway possui conexão HTTP direta com os três serviços. Login é encaminhado à Identidade; cadastro e consulta de produtos são encaminhados ao Estoque; criação, consulta e impressão de notas são encaminhadas ao Faturamento. Essa conexão não permite ao Gateway acessar bancos ou executar regras de negócio.

Inventory e Billing são responsáveis por:

1. gravar seus próprios dados no seu banco;
2. publicar mensagens no RabbitMQ;
3. consumir mensagens do RabbitMQ;
4. aplicar em seu próprio banco os efeitos decorrentes do consumo.

Quando Outbox for utilizada, o serviço grava a mensagem em seu próprio banco na mesma transação do dado de negócio. Um publicador pertencente ao serviço lê essa Outbox e envia a mensagem ao RabbitMQ. O broker continua sem acesso ao banco.

Os limites completos estão registrados em [`ADR-004-FRONTEIRAS-GATEWAY-E-MENSAGERIA.md`](ADR-004-FRONTEIRAS-GATEWAY-E-MENSAGERIA.md).

### 6.1 API Gateway

Responsável por:

- fornecer uma entrada única ao frontend;
- rotear chamadas HTTP;
- aplicar CORS e políticas de borda;
- propagar ou criar identificadores de correlação;
- padronizar preocupações transversais que pertençam à borda.

Não possui banco, não executa regras de negócio, não acessa os bancos dos serviços e não intermedeia mensagens do RabbitMQ.

### 6.2 Serviço de Identidade

É o único dono de usuários e credenciais. Responsável por:

- autenticar o usuário administrativo;
- armazenar senha por meio do ASP.NET Core Identity;
- emitir JWT de curta duração;
- fornecer claims mínimas para autorização;
- manter seu banco e migrations isolados.

Não participa do RabbitMQ nem conhece produtos, notas ou processos de emissão. O Gateway e as APIs validam tokens sem consultar seu banco a cada requisição.

### 6.3 Serviço de Estoque

É o único dono de produtos, saldos e movimentações. Responsável por:

- cadastrar e consultar produtos;
- proteger invariantes de saldo;
- validar disponibilidade;
- processar a baixa completa de uma nota;
- controlar concorrência;
- responder de forma idempotente às solicitações do Faturamento.

### 6.4 Serviço de Faturamento

É o único dono das notas fiscais e do processo de emissão. Responsável por:

- gerar numeração sequencial;
- criar notas e seus itens;
- controlar os estados da nota;
- iniciar e acompanhar a emissão;
- coordenar a solicitação de baixa ao Estoque;
- fechar a nota após confirmação;
- disponibilizar a impressão após a confirmação da baixa e impedir nova impressão depois do fechamento.

### 6.5 Frontend Angular

Responsável por:

- telas de produtos e notas;
- autenticação, sessão e logout;
- validação para experiência do usuário;
- envio das operações ao Gateway;
- indicador de processamento;
- acompanhamento do resultado da emissão;
- apresentação clara de erros e estados;
- impressão do documento concluído.

O frontend não é uma fronteira confiável. Todas as entradas são validadas novamente pelo serviço responsável.

---

## 7. Comunicação entre componentes

### 7.1 Comunicação externa

O Angular utiliza HTTP e acessa exclusivamente o API Gateway. O Gateway encaminha cada chamada ao serviço responsável.

### 7.2 Comunicação interna síncrona

Chamadas HTTP entre serviços serão evitadas no caminho crítico da emissão quando criarem acoplamento temporal desnecessário. Consultas simples podem ser avaliadas nos SDDs de contratos e serviços.

### 7.3 Comunicação interna assíncrona

RabbitMQ será utilizado no processo de emissão e baixa de estoque. A escolha é adequada para:

- comandos e eventos de integração;
- confirmação de consumo;
- retry;
- dead-letter queue;
- recuperação após indisponibilidade temporária.

Kafka não será adotado porque o sistema não exige alto volume, retenção extensa, replay de log ou múltiplos consumidores analíticos.

---

## 8. Fluxo principal de emissão

```text
1. Usuário solicita emissão de uma nota aberta.
2. Gateway encaminha a solicitação ao Faturamento.
3. Faturamento valida a nota e registra o processo.
4. Faturamento publica uma solicitação de baixa de estoque.
5. Estoque valida todos os produtos e quantidades.
6. Estoque baixa todos os itens atomicamente ou recusa toda a operação.
7. Estoque publica o resultado.
8. Faturamento fecha a nota somente após confirmação da baixa.
9. Frontend encerra o indicador e disponibiliza a impressão.
```

Os estados técnicos, a política de recuperação, retry, DLQ e acompanhamento estão decididos no [`ADR-012-ESTADOS-RECUPERACAO-E-ACOMPANHAMENTO-DA-EMISSAO.md`](ADR-012-ESTADOS-RECUPERACAO-E-ACOMPANHAMENTO-DA-EMISSAO.md). Os contratos e critérios de aceite completos serão definidos no `SDD-07-EMISSAO-E-CONSISTENCIA-DISTRIBUIDA.md`.

---

## 9. Estados de negócio e processamento

O status da nota seguirá exatamente os estados pedidos:

- `Aberta`;
- `Fechada`.

O andamento distribuído da emissão será representado por `InvoiceIssuanceProcess`, separado do status da nota. Isso permite informar processamento e falha sem criar estados fiscais não solicitados.

O processo utiliza `Pending`, `AwaitingStock`, `Completed`, `Rejected` e `ManualIntervention`. A nota permanece aberta e bloqueada durante os dois primeiros estados, fecha somente em `Completed`, volta a permitir correção em `Rejected` e permanece bloqueada em `ManualIntervention` até diagnóstico. Tempo decorrido pode marcar o processo como atrasado para fins de apresentação, mas nunca decide sucesso ou falha.

---

## 10. Persistência e propriedade dos dados

- cada microsserviço possui banco próprio;
- nenhum serviço consulta ou altera o banco de outro;
- o Gateway não possui banco;
- o Faturamento referencia produtos por identificador, mas não é dono do saldo;
- o Estoque não altera nem consulta notas diretamente;
- informações históricas necessárias para uma nota não devem depender de alterações futuras no cadastro do produto;
- contratos de integração carregam somente os dados necessários ao fluxo.

Identity, Inventory e Billing utilizarão bancos lógicos PostgreSQL próprios, Entity Framework Core 10 e provider Npgsql 10, conforme ADR-008.

---

## 11. Requisitos obrigatórios do desafio

| Requisito | Tratamento planejado |
|---|---|
| Aplicação Angular | Frontend Angular |
| Cadastro de produtos | Serviço de Estoque |
| Código, descrição e saldo | Modelo de produto |
| Nota com número sequencial | Serviço de Faturamento |
| Status aberta ou fechada | Agregado da nota |
| Múltiplos produtos e quantidades | Itens da nota |
| Botão de impressão | Tela de detalhe da nota |
| Indicador de processamento | Estado de emissão no frontend |
| Fechamento após impressão | Processo de emissão |
| Impedir impressão inválida | Frontend bloqueia e backend rejeita nota diferente de `Aberta` |
| Atualização do saldo | Baixa atômica no Estoque |
| No mínimo dois microsserviços | Estoque e Faturamento |
| Falha e recuperação | RabbitMQ, retry e feedback ao usuário |
| Banco real | Banco isolado por serviço |
| Detalhamento técnico | SDD-12 |
| Vídeo de apresentação | SDD-12 |

---

## 12. Requisitos originalmente opcionais

Concorrência e idempotência eram opcionais no enunciado, mas passam a ser requisitos comprometidos deste projeto após a aprovação do ADR-002.

### 12.1 Concorrência

Será obrigatoriamente implementada. O cenário mínimo é duas notas tentando consumir simultaneamente a última unidade de um produto. Somente uma baixa poderá ser confirmada.

### 12.2 Idempotência

Será obrigatoriamente implementada. Repetições de solicitações HTTP ou mensagens não poderão criar outro processo nem baixar o estoque novamente.

### 12.3 Inteligência artificial

Não será implementada. A decisão prioriza consistência, recuperação, testes e completude do fluxo principal.

---

## 13. Qualidade e evidências

O projeto deverá demonstrar:

- regras de domínio testadas;
- persistência real;
- integração real com RabbitMQ;
- isolamento entre bancos;
- falha e recuperação reproduzíveis;
- concorrência sem saldo negativo;
- idempotência em requisições e consumidores;
- logs correlacionados entre serviços;
- documentação das APIs;
- execução local reproduzível;
- critérios de aceite rastreáveis até testes e evidências.

Os gates transversais estão definidos em [`README.md`](README.md), seção "Marcadores de qualidade".

---

## 14. Apresentação e detalhamento técnico

O vídeo e o detalhamento técnico da solução deverão apresentar:

- as telas desenvolvidas;
- as funcionalidades implementadas;
- explicação dos ciclos de vida Angular utilizados;
- explicação do uso de RxJS;
- bibliotecas utilizadas e a finalidade de cada uma;
- bibliotecas utilizadas para componentes visuais;
- frameworks adotados no backend C#/.NET;
- tratamento de erros e exceções;
- explicação do uso de LINQ com exemplos reais da solução;
- gerenciamento de dependências em Golang marcado como não aplicável.

O README final, o detalhamento técnico e o vídeo também deverão declarar os limites estabelecidos no ADR-001. Instruções logísticas de entrega, como nome do repositório, destinatário do e-mail e hospedagem dos links, não fazem parte do escopo dos SDDs.

---

## 15. Glossário

O vocabulário canônico de domínio, integração, segurança, persistência e qualidade está consolidado em [`GLOSSARIO.md`](GLOSSARIO.md). SDDs, código, contratos e interface devem reutilizar esses termos e propor atualização do glossário antes de introduzir um sinônimo incompatível.

---

## 16. Decisões pendentes

As decisões fundamentais identificadas durante a elaboração da visão geral foram concluídas. Novas lacunas encontradas na auditoria ou nos SDDs deverão ser apresentadas e aprovadas, nunca resolvidas implicitamente durante a implementação.

---

## 17. Trade-offs assumidos até o momento

| Decisão | Consequência aceita |
|---|---|
| Nota simplificada, sem domínio fiscal real | Maior foco técnico e menor abrangência funcional |
| Dois serviços de domínio | Evita decomposição artificial |
| API Gateway como terceiro componente | Mais infraestrutura em troca de entrada única e políticas de borda |
| RabbitMQ em vez de Kafka | Menor complexidade e melhor aderência ao fluxo de comandos |
| Bancos isolados | Consistência distribuída precisa ser tratada explicitamente |
| Concorrência e idempotência incluídas | Mais esforço, mas protegem o fluxo crítico e atendem opcionais relevantes |
| IA excluída | Esforço concentrado em requisitos diretamente avaliáveis |
| Backend exclusivamente em C#/.NET | Elimina alternativas de implementação e torna Golang não aplicável |
| .NET 10 LTS em vez de .NET 6 | Mantém suporte e segurança em um projeto novo; conhecimento de legado será demonstrado pela estratégia de manutenção e migração |
| Clean Architecture com quatro projetos por serviço | Mais estrutura inicial em troca de isolamento explícito entre domínio e infraestrutura |
| PostgreSQL e EF Core 10 | Persistência relacional, migrations e integração idiomática com .NET 10 |
| Desenvolvimento Docker-first | Ambiente reproduzível em troca de maior cuidado com imagens, volumes e tempo de build |
| Cobertura mínima de 80% | Gate mensurável que não substitui testes derivados dos critérios de aceite |
| Minimal APIs sem MediatR | Menor indireção e casos de uso explicitamente injetados nos endpoints |
| RabbitMQ.Client direto | Maior controle e transparência em troca de implementação explícita da confiabilidade |
| Consulta HTTP interna ao adicionar item | Garante referência e snapshot válidos; indisponibilidade do Inventory impede edição temporariamente |
| Processo técnico separado da nota | Preserva `Open`/`Closed` e permite representar andamento e falhas distribuídas |
| Polling progressivo em vez de canal em tempo real | Menor infraestrutura, com atualização suficiente para o porte do desafio |
| Retry escalonado e DLQ sem redrive automático | Recupera falhas transitórias e impede ciclos infinitos; falhas persistentes exigem diagnóstico |
| Serviço de Identidade e JWT | Amplia o escopo, mas protege operações sensíveis e demonstra um caminho realista de evolução |
| Validação do token no Gateway e nas APIs | Duplica configuração em troca de defesa em profundidade e menor risco de contorno |
| Coverlet com gate por assembly | Impede que a média global esconda baixa cobertura em um componente relevante |
| ReportGenerator auxiliar | Facilita localizar lacunas e produzir evidências sem fazer do HTML parte da execução diária |
| Angular Material com tema próprio | Acelera controles acessíveis sem abrir mão da identidade visual da aplicação |
| Uma única biblioteca completa de UI | Evita estilos e dependências sobrepostos; componentes específicos continuam próprios |
| Impressão HTML via navegador | Atende ao botão utilizável sem introduzir geração, armazenamento ou download de PDF |

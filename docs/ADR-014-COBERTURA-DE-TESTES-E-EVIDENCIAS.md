# ADR-014 - Cobertura de Testes e Evidências

> Status: Aprovada
> Data: 2026-08-17
> Dependências: ADR-006, ADR-009, ADR-010 e ADR-013

---

## 1. Contexto

O projeto estabeleceu cobertura mínima de 80% e testes unitários e de integração obrigatórios. Um número de testes aprovados, isoladamente, não demonstra quais partes do código foram exercitadas. Também não impede que muitos testes percorram apenas caminhos simples enquanto regras críticas, falhas e transições permanecem sem prova.

Cobertura é um indicador de alcance dos testes: informa quais linhas e ramificações foram executadas durante a suíte. Ela não prova que as asserções estão corretas, não mede a qualidade dos cenários e não substitui critérios de aceite. Por isso será usada como gate complementar à rastreabilidade e à revisão dos testes.

---

## 2. Decisão

O backend utilizará:

- Coverlet como coletor obrigatório de cobertura;
- Cobertura XML como formato interoperável de saída;
- ReportGenerator como ferramenta auxiliar para consolidação e visualização;
- HTML e resumo textual ou Markdown como evidências geradas sob demanda;
- gate mínimo de 80% de linhas para cada assembly de produção relevante;
- branch coverage coletada e publicada, inicialmente sem gate percentual.

As versões exatas serão fixadas no gerenciamento central de pacotes durante o SDD de setup e somente serão alteradas de forma deliberada.

---

## 3. Objetivo do indicador

A cobertura será usada para:

- revelar regras e caminhos de erro nunca executados pelos testes;
- impedir regressão abaixo do limite aprovado;
- orientar a revisão de áreas críticas;
- produzir evidência objetiva e reproduzível da execução;
- comparar o alcance da suíte entre serviços sem confundir quantidade de testes com qualidade.

O indicador não será usado para justificar testes sem valor, asserções superficiais ou acesso artificial a membros apenas para elevar percentuais.

---

## 4. Regra do gate

Cada assembly relevante escrito pelo projeto deve alcançar ao menos 80% de line coverage. A média global será informada, mas não poderá ocultar um assembly individual abaixo do limite.

Domain e Application de Identity, Inventory e Billing estão sempre sujeitos ao gate. Infrastructure, APIs e Gateway também serão medidos; seu enquadramento por assembly será detalhado no SDD de testes considerando o que é código manual executável e o que é composição declarativa.

Uma camada com poucas linhas não pode compensar baixa cobertura em outra. Da mesma forma, atingir 80% não autoriza ignorar regras críticas nos 20% restantes.

---

## 5. Branch coverage e caminhos críticos

Branch coverage será publicada para mostrar decisões condicionais não percorridas. Não haverá limiar numérico inicial porque a métrica varia significativamente com código de infraestrutura, bibliotecas e código gerado.

Apesar disso, todos os ramos relevantes dos seguintes comportamentos precisam de testes derivados de critérios de aceite:

- autenticação e autorização;
- transições da nota e do processo de emissão;
- idempotência HTTP e de consumidores;
- saldo suficiente e insuficiente;
- concorrência sobre a última unidade;
- processamento atômico de vários itens;
- retry, redelivery, rejeição e DLQ;
- indisponibilidade e recuperação dos serviços;
- mapeamento de erros esperado e inesperado.

Ausência de um desses cenários é uma lacuna, mesmo que o percentual total permaneça acima de 80%.

---

## 6. Exclusões

Poderão ser excluídos mediante configuração explícita:

- migrations geradas;
- código comprovadamente gerado por ferramenta;
- arquivos de designer;
- bootstrap puramente declarativo sem decisão própria.

Não poderão ser excluídos para facilitar o gate:

- entidades e value objects;
- casos de uso e validações;
- handlers e tratamento de erros escrito pelo projeto;
- repositories e adapters manuais;
- consumers e publishers;
- Outbox, Inbox e Process Manager;
- autenticação e políticas de autorização;
- lógica própria do Gateway;
- qualquer código que implemente regra ou critério de aceite.

Toda exclusão deverá ser visível na configuração e justificável na revisão de qualidade.

---

## 7. Fluxo Docker-first

A coleta e o gate serão executados dentro do ambiente Docker, pelo mesmo fluxo utilizado localmente e na integração contínua:

```text
container de testes
    -> executa testes unitários e de integração
    -> coleta cobertura
    -> valida o gate por assembly
    -> produz Cobertura XML
    -> opcionalmente gera relatório consolidado
```

O comando oficial será definido no `SDD-10-TESTES.md` e exposto por Docker Compose. O host não precisará instalar Coverlet ou ReportGenerator globalmente.

---

## 8. Artefatos e versionamento

Os resultados poderão ser gerados sob uma estrutura conceitual como:

```text
artifacts/coverage/
|- cobertura.xml
|- summary.md
`- report/
   `- index.html
```

Arquivos gerados não serão versionados. A documentação final registrará o comando reproduzível, o percentual obtido, a data da validação e o resultado do gate. Um pipeline futuro poderá publicar a pasta como artefato temporário.

---

## 9. ReportGenerator

ReportGenerator não participa da execução das regras nem determina se os testes passaram. Ele transforma os dados brutos do Coverlet em uma visualização compreensível por serviço, assembly, classe e linha.

Seu uso é auxiliar:

- localizar lacunas durante o desenvolvimento;
- consolidar múltiplos arquivos Cobertura;
- gerar HTML para inspeção;
- produzir resumo para evidência final.

Uma falha no gate não pode ser contornada deixando de gerar o relatório. Da mesma forma, o relatório HTML não é requisito para toda execução rápida da suíte.

---

## 10. Consequências

### Positivas

- gate mensurável e reproduzível;
- cobertura baixa não fica escondida pela média de outros projetos;
- lacunas podem ser localizadas visualmente;
- evidência pode ser apresentada sem versionar arquivos gerados;
- fluxo idêntico no Docker local e no CI.

### Limitações

- cobertura mede execução, não a qualidade da asserção;
- instrumentação aumenta o tempo da suíte;
- o gate por assembly exige configuração cuidadosa;
- branch coverage dependerá também de avaliação qualitativa;
- relatórios são artefatos auxiliares e precisam ser regenerados para refletir o código atual.

---

## 11. Alternativas não adotadas

### Apenas contar testes aprovados

Rejeitada porque quantidade não revela quais caminhos foram exercitados.

### Gate somente pela média global

Rejeitada porque assemblies simples poderiam mascarar um serviço crítico mal testado.

### Versionar relatórios HTML

Rejeitada porque produz grande quantidade de arquivos derivados e facilmente obsoletos.

### Exigir 100% de cobertura

Rejeitada porque incentiva testes artificiais e oferece retorno decrescente. Regras críticas continuam exigindo cobertura comportamental completa, independentemente da meta geral de 80%.


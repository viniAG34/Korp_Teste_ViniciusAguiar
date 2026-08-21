# ADR-016 - Impressão via Navegador

> Status: Aprovada
> Data: 2026-08-17
> Dependências: ADR-001, ADR-003, ADR-011, ADR-012 e ADR-015

---

## 1. Contexto

O desafio exige um botão de impressão visível e intuitivo. Ao acioná-lo, a interface deve indicar processamento; ao final, a nota deve ser fechada, o estoque atualizado e novas impressões de notas diferentes de `Open` devem ser impedidas.

O enunciado não exige PDF, armazenamento de documento, integração com impressora, layout fiscal regulamentado ou endpoint de download. Introduzir essas capacidades criaria um subsistema documental sem relação direta com os critérios avaliados.

Ao mesmo tempo, o botão não deve apenas possuir o nome "Imprimir": depois da conclusão do fluxo ele precisa oferecer uma ação de impressão real ao usuário.

---

## 2. Decisão

Após a conclusão bem-sucedida de `PrintInvoice`, o frontend apresentará a nota em HTML e abrirá o diálogo nativo do navegador por meio de `window.print()`.

Uma folha de estilos mínima para mídia de impressão ocultará navegação, botões e elementos interativos que não pertencem ao conteúdo da nota.

```text
PrintInvoice
    -> indicador de processamento
    -> baixa confirmada
    -> invoice fechada
    -> renderização HTML
    -> window.print()
```

Não haverá:

- PDF gerado pelo backend;
- biblioteca de geração de PDF;
- armazenamento de arquivos;
- endpoint de download;
- template fiscal regulamentado;
- confirmação de impressão física;
- funcionalidade pública de reimpressão.

O usuário poderá escolher "Salvar como PDF" caso o próprio navegador e sistema operacional ofereçam essa opção. Essa capacidade pertence ao ambiente do usuário, não ao backend.

---

## 3. Conteúdo mínimo imprimível

A visualização conterá somente dados existentes no escopo:

- número da nota;
- status concluído;
- data de criação e fechamento;
- lista de produtos;
- código e descrição capturados no item;
- quantidade utilizada.

Preço, totais financeiros, cliente, fornecedor, impostos, série, chave fiscal e demais informações fora do domínio não serão inventados para preencher o documento.

---

## 4. Limite técnico da impressão

Depois que o diálogo nativo é aberto, a aplicação não sabe se o usuário confirmou, cancelou, salvou como PDF ou selecionou várias cópias. O navegador também não fornece confirmação confiável de impressão física.

A regra de negócio será aplicada ao comando controlado pela aplicação:

- somente invoice `Open` aceita `PrintInvoice`;
- nova execução para invoice `Closed` é rejeitada;
- o frontend não exibe nova ação de impressão para nota fechada;
- a aplicação não oferece rota pública de reimpressão;
- nenhum cancelamento do diálogo reabre a nota ou desfaz a baixa.

O projeto não alegará controlar ações externas realizadas depois que o conteúdo foi entregue ao subsistema de impressão do navegador.

---

## 5. Tratamento de bloqueio do navegador

`window.print()` deve ser acionado como continuação visível do fluxo iniciado pelo usuário. Caso o navegador bloqueie ou não abra o diálogo automaticamente, a interface informará que a emissão foi concluída sem alterar novamente o estado da nota.

O SDD do frontend definirá a alternativa de experiência compatível com a proibição de uma nova operação de negócio. Essa alternativa não poderá chamar `PrintInvoice` novamente nem produzir outra baixa.

---

## 6. Testes e evidências

Deverão ser demonstrados:

- botão disponível apenas para nota aberta e elegível;
- indicador enquanto o processo estiver ativo;
- chamada de impressão somente após `Completed`;
- ausência da chamada em `Rejected`, atraso ou falha;
- conteúdo mínimo corretamente apresentado;
- elementos de navegação ocultos em mídia de impressão;
- ausência de novo comando para nota fechada;
- backend rejeitando tentativa direta de nova impressão.

Testes automatizados verificarão a solicitação de abertura do diálogo, e não uma impressão física que o navegador não consegue comprovar.

---

## 7. Consequências

### Positivas

- atende ao comportamento explicitamente solicitado;
- mantém um botão realmente utilizável;
- evita dependências e persistência sem requisito;
- permite demonstração simples no vídeo;
- aproveita a capacidade nativa de salvar como PDF;
- mantém o foco em consistência, falhas e estoque.

### Limitações

- aparência e recursos dependem parcialmente do navegador;
- o sistema não confirma a impressão física;
- não existe documento PDF oficial ou arquivado;
- cancelar o diálogo não desfaz a emissão já concluída;
- paginação complexa não faz parte da entrega.

---

## 8. Alternativas não adotadas

### PDF no backend

Rejeitada por exigir biblioteca, template, fontes, paginação, endpoint e testes sem requisito correspondente.

### PDF no frontend

Rejeitada porque duplica a renderização e ainda introduz dependência desnecessária.

### Botão sem ação real de impressão

Rejeitada porque demonstraria apenas o fluxo de estado, mas não entregaria uma ação de impressão utilizável.

### Documento fiscal completo

Rejeitada porque o projeto trata uma nota simplificada e não implementa domínio tributário.

---

## 9. Impacto nos SDDs

- `SDD-06-BILLING-SERVICE.md` fornecerá os dados necessários da nota concluída;
- `SDD-09-FRONTEND-ANGULAR.md` definirá a visualização e o comportamento de `window.print()`;
- `SDD-10-TESTES.md` definirá provas do gatilho e do CSS de impressão;
- `SDD-12-DOCUMENTACAO-VIDEO-E-ENTREGA.md` explicará os limites e demonstrará o fluxo.


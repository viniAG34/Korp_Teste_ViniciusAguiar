# ADR-015 - Biblioteca Visual do Angular

> Status: Aprovada
> Data: 2026-08-17
> Dependências: ADR-009, ADR-013 e ADR-014

---

## 1. Contexto

O frontend precisa apresentar cadastros, tabelas, estados de emissão, autenticação, erros e impressão de maneira consistente e intuitiva. Construir todos os controles básicos do zero consumiria tempo sem agregar valor ao domínio; combinar várias bibliotecas visuais aumentaria o bundle, criaria conflitos de estilo e dificultaria testes e manutenção.

Ao mesmo tempo, utilizar uma biblioteca sem personalização produziria uma interface genérica e pouco adequada à apresentação de um sistema empresarial. A decisão precisa equilibrar produtividade, acessibilidade, qualidade visual e domínio técnico do Angular.

---

## 2. Decisão

Angular Material será a única biblioteca completa de componentes visuais. A aplicação utilizará tema personalizado e SCSS próprio para layout, responsividade, identidade visual, estados do domínio e impressão.

A versão do Angular Material acompanhará a mesma major version do Angular definida no SDD de setup. Dependências serão fixadas e instaladas pelo ambiente Docker.

```text
Angular Material
|- controles de formulário
|- botões e menus
|- tabelas e paginação
|- dialogs e snackbars
|- indicadores de progresso
`- primitivas acessíveis de interação

SCSS da aplicação
|- shell administrativo
|- responsividade
|- identidade visual
|- estados específicos do domínio
`- mídia de impressão
```

---

## 3. Componentes previstos

O escopo poderá utilizar, quando exigido pelas telas:

- form field, input e validação;
- button e icon button;
- table, sort e paginator;
- toolbar, sidenav, menu e list;
- card, divider e tooltip;
- dialog para confirmações relevantes;
- snackbar para feedback breve;
- progress spinner e progress bar;
- chips para estados;
- select e autocomplete para escolha de produtos.

A lista não autoriza importar toda a biblioteca antecipadamente. Cada componente standalone importará somente os módulos que realmente utiliza.

---

## 4. Identidade visual

Não será adotado o tema padrão sem adaptação. O SDD do frontend definirá:

- paleta e tokens semânticos de cor;
- tipografia;
- escala de espaçamento;
- densidade apropriada para tabelas administrativas;
- hierarquia de títulos e ações;
- largura e comportamento da navegação;
- breakpoints responsivos;
- aparência de foco, seleção, carregamento, vazio e erro;
- contraste mínimo e estados que não dependam apenas de cor.

O objetivo não é criar um design system genérico. Serão definidos somente tokens e padrões usados pelas telas deste produto.

---

## 5. Componentes próprios

Componentes da aplicação serão criados quando expressarem significado recorrente ou comportamento próprio, por exemplo:

```text
InvoiceStatusChip
IssuanceProgress
StockBalanceIndicator
ConfirmationDialog
EmptyState
ErrorState
```

Não será criado um wrapper genérico para cada componente do Angular Material. Abstrações próprias devem reduzir repetição real ou representar conceito do produto, e não apenas renomear uma API externa.

---

## 6. Acessibilidade

O uso de componentes acessíveis da biblioteca é ponto de partida, não garantia automática. As telas deverão preservar:

- labels associadas aos campos;
- ordem lógica de foco;
- navegação por teclado;
- foco visível;
- texto alternativo e nomes acessíveis;
- contraste adequado;
- mensagens de validação associadas;
- anúncio de estados assíncronos relevantes;
- ações que não dependam exclusivamente de ícones ou cores.

Dialogs, menus e indicadores deverão ser verificados no contexto real da tela.

---

## 7. Testabilidade

Quando disponíveis e úteis, Angular Material Component Harnesses serão preferidos em testes de componentes. Eles permitem interagir com controles por sua API pública e sem acoplar o teste à estrutura interna de HTML da biblioteca.

Componentes próprios continuam testando comportamento observável, estados de carregamento, erros, acessibilidade e integração com formulários. Snapshots visuais extensos não substituirão asserções de comportamento.

---

## 8. Ícones e recursos externos

A adoção do Angular Material não autoriza dependência de fontes ou ícones carregados de CDN em tempo de execução. O ambiente deve permanecer reproduzível e funcional sem internet.

Ícones necessários serão empacotados localmente ou fornecidos por mecanismo aprovado no SDD do frontend. Recursos visuais devem possuir licença compatível e não podem introduzir uma segunda biblioteca completa de UI.

---

## 9. Alternativas não adotadas

### Componentes próprios do zero

Rejeitada porque aumentaria tempo, risco de acessibilidade e esforço de testes em controles já resolvidos pelo ecossistema Angular.

### PrimeNG

É uma opção válida para sistemas administrativos, mas não foi escolhida para evitar uma segunda direção visual e manter integração mais próxima ao ecossistema oficial do Angular.

### Bootstrap

Não adotado porque Angular Material já fornece controles e padrões de interação necessários. Utilizá-lo em conjunto duplicaria responsabilidades de estilo e layout.

### Tailwind CSS

Não adotado inicialmente. SCSS e o sistema de tema do Material atendem ao escopo sem introduzir outra linguagem de estilização e configuração adicional.

### Mais de uma biblioteca completa

Rejeitada por aumentar inconsistência visual, dependências, tamanho e custo de manutenção.

---

## 10. Consequências

### Positivas

- componentes maduros e integrados ao Angular;
- menor tempo para construir interações básicas;
- base acessível e testável;
- linguagem visual consistente;
- tema customizado sem criar controles primitivos;
- menor quantidade de dependências sobrepostas.

### Custos e limitações

- customizações devem respeitar as APIs de tema da biblioteca;
- aparência pode ficar genérica se o tema e o layout forem negligenciados;
- upgrades do Angular e Material precisam permanecer alinhados;
- acessibilidade ainda exige validação das telas completas;
- componentes muito específicos continuarão sendo responsabilidade da aplicação.

---

## 11. Impacto nos SDDs

- `SDD-01-SETUP-E-ARQUITETURA.md` fixará versões e instalação;
- `SDD-09-FRONTEND-ANGULAR.md` definirá tema, shell, componentes, telas e estados;
- `SDD-10-TESTES.md` definirá testes de componentes e acessibilidade;
- `SDD-12-DOCUMENTACAO-VIDEO-E-ENTREGA.md` explicará a biblioteca, customizações e componentes próprios.


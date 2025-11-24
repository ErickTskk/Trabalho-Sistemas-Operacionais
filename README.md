DiskEditor – Editor Hexadecimal de Setores de Disco (C# WinForms)

O DiskEditor é uma aplicação Windows Forms desenvolvida em C# que permite ler, visualizar, editar e gravar setores brutos (raw sectors) de discos físicos ou arquivos.
O programa exibe os bytes em formato hexadecimal + ASCII, permitindo navegação por setor, edição byte a byte e escrita direta no disco recurso extremamente poderoso e perigoso.

AVISO IMPORTANTE:

Este aplicativo permite escrever diretamente setores físicos do disco, o que pode corromper o sistema operacional, arquivos, partições ou até tornar o PC não inicializável.
Use-o somente em ambientes controlados e com conhecimento adequado.

Funcionalidades:

Leitura e Navegação

Seleção de unidades físicas disponíveis (exceto CD/DVD).

Leitura setor a setor (512 bytes por setor).

Exibição dos dados em formato hexadecimal organizado em linhas de 16 bytes.

Exibição simultânea em ASCII.

Navegação:

Setor anterior.

Próximo setor.

Ir para offset global (hex ou decimal).

Busca por sequência hexadecimal dentro do setor atual (ex: DEADBEEF).

Seleção automática da linha correspondente.

Edição:

Edição direta dos bytes em HEX no DataGridView.

Validação automática do valor digitado.

Atualização automática da coluna ASCII.

Destaque no status indicando qual offset foi alterado.

Requisitos:

Windows

Visual Studio 2022

Suporte à linguagem C#

.NET SDK 8.0 ou superior

Abrir o Visual Studio como Administrador

Clone o repositório:

Na pagina principal tem uma opção que diz clonar repositório só clicar nela e colar o link do projeto.

Clique em Start ou pressione F5 para executar.


Como Usar o DiskEditor:

Abertura

Clique em Open

Selecione o arquivo binário ou imagem de disco que deseja visualizar

Navegação

O conteúdo é automaticamente dividido em setores de 512 bytes.

Use:

Prev Sector

Next Sector

O setor atual é exibido na barra inferior.

Busca:

Digite uma sequência em hexadecimal no campo Find

Exemplo: FF 00 1A

Ir para Offset

Aceita decimal ou hexadecimal

Exemplo: 512 ou 0x200

Edição:

Clique em qualquer byte na tabela para alterar seu valor

Clique em Save para gravar as mudanças no arquivo

Tecnologias Utilizadas:

C#

.NET 8

Windows Forms

DataGridView

Manipulação de arquivos binários

Objetivo Acadêmico

O projeto tem como propósito demonstrar:

Como setores representam unidades reais de leitura/escrita

Como dados são exibidos em Hex e ASCII
Funcionamento de editores de baixo nível
Interação entre software e armazenamento físico

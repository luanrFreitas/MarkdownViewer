<div align="center">
  <img src="assets/logo.svg" width="120" height="120" alt="Markdown Viewer logo" />

  # Markdown Viewer

  **Duplo clique em um `.md`. Leia o Markdown como ele deveria ser lido — formatado, na hora, sem abrir um editor.**

  ![License: GPL v3 or later](https://img.shields.io/badge/license-GPL--3.0--or--later-blue.svg)
  ![Platform: Windows 11](https://img.shields.io/badge/platform-Windows%2011-0078D6.svg)
</div>

---

## Por que usar

O Windows não sabe o que fazer com um arquivo `.md` — ou abre num editor de texto cru, cheio de `#` e `**`, ou pergunta qual programa usar. O **Markdown Viewer** resolve isso da forma mais direta possível: você dá duplo clique, e o conteúdo aparece **já formatado**, como se fosse uma página.

- **Instantâneo** — abre e renderiza um arquivo típico em menos de 1 segundo.
- **100% offline** — nenhuma etapa depende de internet; nada é enviado para lugar nenhum.
- **Zero configuração extra** — nada além do Windows 11 precisa estar instalado (usa o WebView2 que já vem com o sistema).
- **Atualização ao vivo** — edite o arquivo em qualquer editor e a janela do visualizador atualiza sozinha.
- **Suporte completo a GFM** — tabelas, listas de tarefas, blocos de código com destaque de sintaxe, links, imagens.
- **Só leitura, de propósito** — não é mais um editor de Markdown disputando espaço com o que você já usa; é uma janela pra *ler*.

## Instalação

1. Baixe o `MarkdownViewer.exe` mais recente na página de **[Releases](../../releases)**.
2. Rode o executável uma vez e clique em **"Definir como visualizador padrão de .md"**.
3. Pronto. A partir de agora, dar duplo clique em qualquer `.md` (ou `.markdown`) no Explorer abre o Markdown Viewer.

> **Nota:** como o executável não é assinado digitalmente, o Windows SmartScreen pode exibir um aviso de "aplicativo desconhecido" no primeiro uso. Clique em **"Mais informações" → "Executar assim mesmo"** para prosseguir.

Nenhuma instalação adicional é necessária — o executável é autocontido (não depende de você já ter o .NET instalado).

## Como usar

| Ação | Resultado |
|---|---|
| Duplo clique em um `.md` no Explorer | Abre o conteúdo renderizado numa nova janela |
| Arrastar um `.md` para dentro da janela já aberta | Troca o conteúdo exibido |
| `Ctrl+O` (ou botão "Abrir arquivo") | Abre um seletor para escolher outro `.md` |
| Salvar o arquivo aberto em outro programa | A janela atualiza sozinha, sem precisar fechar/reabrir |
| Clicar num link externo | Abre no seu navegador padrão |
| Clicar num link relativo para outro `.md` | Abre esse arquivo no próprio visualizador |

## Requisitos

- Windows 11 (build 10.0.26200 ou superior).
- Nada mais. O WebView2 Runtime, usado para renderizar o conteúdo, já vem nativo no Windows 11.

## Compilando a partir do código-fonte

Requer o [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet publish src/MarkdownViewer/MarkdownViewer.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

O executável final fica em `src/MarkdownViewer/bin/Release/net8.0-windows/win-x64/publish/MarkdownViewer.exe`.

## Licença

Distribuído sob a **GNU General Public License v3.0 ou posterior** — veja [`LICENSE`](LICENSE) para o texto completo.

Em resumo: você pode usar, estudar, modificar e redistribuir este programa livremente, inclusive para fins comerciais — desde que qualquer distribuição (modificada ou não) permaneça sob a mesma licença, com o código-fonte disponível.

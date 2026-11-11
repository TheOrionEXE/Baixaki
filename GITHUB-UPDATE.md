# Como publicar uma versão do Baixaki no GitHub

Este guia vale para qualquer versão. O aplicativo oferece a atualização **sozinho** a quem já o usa, mas só enxerga uma versão nova quando existe uma **release publicada** com o instalador anexado. O código sozinho no repositório não basta.

Repositório: `TheOrionEXE/Baixaki`. Versão atual: **1.2.1** (tag `v1.2.1`).

## Como o atualizador decide

O programa consulta `https://api.github.com/repos/TheOrionEXE/Baixaki/releases/latest` e procura:

1. o `tag_name` (por exemplo `v1.2.0`), comparado com a versão do aplicativo instalado;
2. um anexo chamado exatamente **`BaixakiSetup.exe`**.

Se a tag for maior que a versão instalada, o aplicativo pergunta se deve atualizar, baixa o instalador e o executa em modo silencioso. Por isso a tag, a versão e o nome do anexo precisam bater.

## Passo a passo para uma nova versão

1. **Escolha o número** (`major.minor.patch`) e use o mesmo em tudo. A versão fica em `version.txt`; o `build-installer.ps1` grava `Version.cs` a partir dela, e `baixaki.iss` recebe o número do script. Atualize também o texto do `README.md` e do `GUIA-AVANCADO.md` quando citarem a versão, e o teste `Application version` em `Tests.cs`.
2. **Teste:** `powershell -NoProfile -ExecutionPolicy Bypass -File .\test.ps1`.
3. **Gere o instalador** (precisa do Inno Setup 6):

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-installer.ps1 -Version 1.2.1 -NonInteractive -NoOpen
   ```

   A saída é `Output\BaixakiSetup.exe`. Grave o hash em `Output\SHA256SUMS.txt` e no `IMPLEMENTATION-REPORT.md`.
4. **Envie o código** para o repositório (somente código, documentação e imagens; executáveis, motores, `Output` e `test-output` ficam fora, o `.gitignore` já cuida disso).
5. **Publique a release** em GitHub > Releases > Draft a new release:
   - Tag: `v1.2.1` (criada ao publicar, na branch `main`).
   - Título: `Baixaki v1.2.1 — Corrige download em pastas redirecionadas`.
   - Anexos: `BaixakiSetup.exe` e `SHA256SUMS.txt`.
   - Marque como **latest release**. Pré-lançamentos não são vistos pelo atualizador.
6. **Confira:** abra a lista de releases e veja se o anexo `BaixakiSetup.exe` aparece; instale a versão anterior em um computador de teste e use Configurações > Baixaki > Verificar.

Nunca republique uma tag existente com outro instalador: quem já atualizou não receberia a correção. Se corrigir algo, gere uma versão nova (por exemplo 1.2.2 e `v1.2.2`).

## Notas da release 1.2.1

> Correção para o erro "O caminho não pode ser atravessado porque contém um ponto de montagem não confiável" (WinError 448), que impedia downloads em PCs onde a pasta Downloads foi redirecionada.
>
> - O Baixaki grava no endereço real da pasta de destino, sem cópia extra, também para arquivos grandes.
> - Downloads com erro ganharam um botão de **detalhes** com o erro completo.
> - A pasta padrão em instalações novas agora é **Vídeos**. Quem já escolheu outra pasta mantém a dela.
>
> SHA-256 do instalador: `29D9AC50C8EACA2684F1427FD4C9DFAAA12DE60DCFC4E7F5D288354A6D4C433C`

## Notas da release 1.2.0

> O Baixaki 1.2.0 traz uma interface totalmente nova, miniaturas, novos formatos e uma análise de links sem espera.
>
> **Novidades**
> - Campo único para colar o link; formato, qualidade e pasta à vista.
> - Abas **Fila** e **Concluídos** separadas; concluídos saem da lista após 12 dias (os arquivos ficam).
> - Miniaturas em todos os vídeos. Texto e linhas maiores, mais fáceis de ler.
> - Novos formatos: **GIF** (com escolha do trecho), **WAV** e **Miniatura**, além de MP4, Premiere e MP3. Qualidade: melhor, 1080p, 720p e 480p.
> - Playlists com seleção de vídeos (padrão: todos; também 10 e 50). Espera entre itens automática: 1 s abaixo de 10 vídeos, 5 s acima.
> - Análise de links sem espera; links de um único vídeo entram na fila na hora.
> - Janela estável (não muda de tamanho sozinha) e lista que não trava ao redimensionar.
> - Perfis do Instagram são recusados na hora, com aviso (o yt-dlp não suporta). Posts e reels continuam funcionando.
> - O yt-dlp se atualiza sozinho uma vez por dia. Novo ícone. Central de ajuda reescrita.
>
> **Instalação:** baixe e execute `BaixakiSetup.exe`. Quem já usa o Baixaki recebe a atualização dentro do aplicativo. A fila e as configurações são mantidas.
>
> SHA-256 do instalador 1.2.0: `40E2FEFB1EA73F0887AFF01C1673330BBBCD426A6BF688EE87BF63F699C098D0`

## Descrição do repositório (About)

> Gerenciador de downloads para Windows com yt-dlp e FFmpeg. Vídeos, playlists, GIF, MP3 e WAV, com fila persistente e miniaturas, em uma interface nativa.

Tópicos sugeridos: `windows` `winforms` `csharp` `yt-dlp` `ffmpeg` `download-manager` `playlist-downloader` `gif` `mp3` `video-downloader`.

Não anuncie suporte ilimitado ou garantido a todos os sites e perfis: a extração depende do motor, das permissões e do serviço.

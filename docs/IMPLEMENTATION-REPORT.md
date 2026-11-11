# Relatório de implementação — Baixaki 1.2.0

Este relatório descreve o que mudou na versão 1.2.0, como foi validado e o que **não** foi validado.

## Entregas

- `Baixaki.exe`: aplicativo v1.2.0; versão de arquivo e assembly `1.2.0.0`.
- `Output/BaixakiSetup.exe`: instalador v1.2.0, com o mesmo AppId e o mesmo nome esperado pelo atualizador.
- `Output/SHA256SUMS.txt`: hash do instalador.

SHA-256 do `BaixakiSetup.exe` (79.794.755 bytes):

```
40E2FEFB1EA73F0887AFF01C1673330BBBCD426A6BF688EE87BF63F699C098D0
```

Se o instalador for recompilado, o hash muda: atualize este arquivo e o `SHA256SUMS.txt`.

## O que mudou

- **Interface nova**, escrita do zero: campo único para colar o link, etiquetas de formato, qualidade e pasta, abas **Fila** e **Concluídos**, miniaturas, painel de configurações lateral e janela de tamanho estável. A lista é desenhada sob medida (só as linhas visíveis), o que elimina o travamento ao redimensionar. Um fator único de escala (`Theme.Zoom`) controla fontes e medidas.
- **Análise de links sem espera:** a análise deixou de usar `--sleep-requests`. Links certamente de um único vídeo entram na fila na hora e recebem título, miniatura e duração quando a análise chega.
- **Formatos:** MP4, Premiere, GIF (trecho com início, duração e largura), MP3, WAV e Miniatura, mais qualidade (melhor, 1080p, 720p, 480p).
- **Playlists:** listagem automática no YouTube, seleção de vídeos com miniaturas, limite padrão **Todos** (também 10 e 50). Fora do YouTube, a listagem pede confirmação.
- **Espera entre itens automática:** 1 s com menos de 10 itens abertos, 5 s a partir disso; também é possível fixar o valor.
- **Concluídos** saem da lista após 12 dias, sem apagar arquivos. Playlists antigas sem itens também saem.
- **Perfis do Instagram** são recusados antes de qualquer requisição.
- **yt-dlp** se atualiza sozinho no máximo uma vez por dia, só quando nada está em andamento.
- **Ícone novo**, embutido no programa. Central de ajuda e guia avançado reescritos para a interface atual.

O motor de fila (`DownloadManager`, estado persistente, limites de acesso, duplicatas e recuperação) foi mantido e estendido, não reescrito.

## Validação realizada

- Compilação no Windows com o compilador C# do .NET Framework (`build.bat` e `build-installer.ps1`).
- `test.ps1`: **111** verificações do motor, **29** verificações de fluxo da interface e da ajuda, e renderização do layout em vários tamanhos (`test-output`).
- Teste com o yt-dlp e o FFmpeg reais, em um vídeo público curto: análise concluída em cerca de 4,8 s; GIF de 5 s, WAV, miniatura JPG e MP4 720p concluídos.
- Instalação silenciosa do `BaixakiSetup.exe` sobre uma instalação existente: sem erro, versão 1.2.0 no aplicativo e em Programas e Recursos, atalhos preservados.
- Captura real da tela (não apenas renderização) para conferir o painel de configurações, a barra de título escura e o ícone.

## Não validado

- Instalação em uma máquina limpa, sem versão anterior.
- Login com contas, cookies e download de perfis reais. O extrator `instagram:user` do yt-dlp 2026.10.21 está marcado como **CURRENTLY BROKEN**; perfis do Instagram não são suportados.
- Sites além do YouTube nos testes reais. O suporte depende do yt-dlp e das condições de cada serviço.
- Coleções muito grandes em serviços reais. A lógica foi testada com 2.005 entradas simuladas.
- A atualização automática a partir de uma versão antiga instalada por outra pessoa; o fluxo foi conferido na lógica e na release publicada, mas não ponta a ponta em outro computador.

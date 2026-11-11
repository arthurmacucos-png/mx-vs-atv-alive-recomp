# 🏍️ MX vs. ATV Alive — Port nativo para PC (continuação)

**A água funciona! 🌊** Este projeto continua a recompilação estática nativa de
**MX vs. ATV Alive** (Xbox 360, Title ID `5451087B`) para Windows x64 e corrige
o bug dos **"espinhos" na água e no terreno**. Esse bug existe no Xenia há mais
de dois anos
([xenia-canary/game-compatibility#596](https://github.com/xenia-canary/game-compatibility/issues/596)),
e até onde sabemos esta é **a primeira vez que alguém o corrigiu**.

Rodando liso a **60 FPS**, com Free Ride, praia, lama, água espirrando, DLCs
carregando e controle de PS5 funcionando. Ver a moto entrar na água sem o mapa
virar um campo de estalagmites foi muito bom. 😄

> [!IMPORTANT]
> **Nenhum arquivo do jogo está incluído.** Este repositório não contém o
> executável, os assets, as DLCs nem o código recompilado. Você precisa da sua
> **própria cópia** legítima do jogo de Xbox 360. Veja [Legal](#legal).

---

## 🙏 Agradecimento especial

Este projeto só existe por causa do **[gw1337x](https://github.com/gw1337x)**,
que fez o trabalho pesado em
[mx-vs-atv-alive-recomp](https://github.com/gw1337x/mx-vs-atv-alive-recomp):
recompilou as ~41,7 mil funções, fez o jogo dar boot, rodar a 60 FPS, salvar e
completar corridas, e documentou tudo com cuidado. Quando o projeto foi
publicado sem manutenção, ficou o pedido: *"forks são bem-vindos, alguém pode
continuar"*. Então continuamos. **Valeu demais, gw1337x!** 🏆

Obrigado também ao **rexglue-sdk** (Tom Clay e contribuidores), ao **Xenia**
(Ben Vanik, Triang3l e a equipe do Xenia Canary), à comunidade de engenharia
reversa citada em [docs/CREDITS.md](docs/CREDITS.md) e à **Rainbow Studios /
THQ Nordic** pelo jogo.

---

## ✨ O que há de novo

| | |
|---|---|
| 🌊 **Correção dos espinhos na água/terreno** | O shader de cópia (`resolve_full_16bpp`) gravava destinos `k_16` **com sinal** como se fossem **sem sinal**. A simulação de ondas lia os valores com o sinal trocado, se realimentava e explodia. Agora o empacotamento SNORM está certo. Detalhes completos em [patches/water-fix](patches/water-fix/README.md). |
| 🎮 **Launcher gráfico** | `MX Alive Launcher.exe`: resolução, FPS, upscaling (CAS/FSR), tela cheia, idioma, controles detectados, teclado e mouse, backup automático do save, logs e atalho na Área de Trabalho. Compila com o `csc` que já vem no Windows. |
| 🕹️ **Controle de PS5 e genéricos** | Backend SDL3 com HIDAPI (DualSense, DualShock 4, Switch Pro) e o [SDL_GameControllerDB](https://github.com/mdqinc/SDL_GameControllerDB) para controles genéricos. Não precisa de DS4Windows. |
| 📦 **Instalador de DLC** | Extrai pacotes STFS (LIVE/PIRS) da **sua** cópia para o formato que o rexglue espera, com o arquivo `.header` de licença. Pelo launcher (**Instalar DLC...**) ou por `tools/install-dlc.py`. |
| 🛠️ **Correções nos scripts** | Cvars do módulo gráfico agora são passados como `--nome=valor` (o formato `--nome valor` era ignorado em silêncio). O backup do save agora acha a pasta Documentos mesmo quando ela é redirecionada pro OneDrive. |
| 🔬 **Diagnóstico de GPU** | `--log_vs_texture_fetch` registra texturas lidas por vertex shaders, cada resolve distinto e recargas de textura. Foi o que permitiu achar o bug. |

## 🔍 Como o bug foi encontrado (versão curta)

1. Os espinhos apareciam **exatamente onde a moto passava** e se espalhavam,
   com a textura branca e azul da água.
2. O diagnóstico novo mostrou que o vertex shader da água lê uma textura `k_16`
   **com sinal** de 512×512, escrita por um **resolve** de um render target
   `k_32_FLOAT`.
3. Lendo essa textura direto da memória do jogo: um círculo em volta da moto
   cheio de **ruído de −32768 a +32767**. A simulação de ondas tinha explodido.
4. O assembly do shader de resolve mostrou `mov_sat` seguido de `* 65535`, ou
   seja, empacotamento **UNORM** num destino **SNORM**.
5. O fonte XeSL da mesma versão do Xenia compila para **exatamente o mesmo
   assembly** que o rexglue usa. Com a correção aplicada só nesse caso, a água
   voltou ao normal.

`--readback_resolve=full` **não** resolve. O problema nunca foi o atraso da
cópia, e sim a conversão.

---

## 🚀 Instalação fácil (um clique)

1. Baixe este repositório (**Code → Download ZIP**, ou `git clone`) e extraia.
2. Dê dois cliques em **`INSTALAR.bat`**.
3. Escolha a pasta da **sua** cópia do jogo de Xbox 360 (a que tem o `default.xex`).

O instalador confere se a versão é a certa, instala as ferramentas pelo
`winget`, baixa o rexglue, aplica a correção da água, recompila o jogo (a
primeira vez leva de 30 a 60 minutos) e cria o atalho **MX vs ATV Alive** na
Área de Trabalho. No fim, o launcher abre sozinho.

> **Por que não tem o `mx_alive.exe` pronto pra baixar?** Ele é o próprio
> código do jogo traduzido para PC, gerado a partir do seu `default.xex`.
> Distribuir esse arquivo seria distribuir o jogo. Por isso, como em todo
> projeto de recompilação (UnleashedRecomp, N64Recomp...), cada um gera o seu.
> O **`MX Alive Launcher.exe`** já vem pronto no repositório.

## 🧰 Compilação manual

### Requisitos

- Windows 10/11 x64 e uma GPU com DirectX 12
- Visual Studio 2022 Build Tools (workload **Desktop development with C++**)
- LLVM/Clang 18+, CMake 3.25+, Ninja, Git e Node.js 20+
- **Sua própria cópia** do MX vs. ATV Alive de Xbox 360 (disco USA/Europa,
  Media ID `5306E1D5`, versão `0.0.0.3`)

Todos podem ser instalados com o `winget`:

```bat
winget install Kitware.CMake Ninja-build.Ninja LLVM.LLVM
winget install Microsoft.VisualStudio.2022.BuildTools --override "--passive --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended"
```

### Passo a passo

```bat
git clone --recursive https://github.com/<seu-usuario>/<este-repo>.git
cd <este-repo>

rem 1. Compila o rexglue com o patch do autor + a correção da água
tools\build-rexglue.bat

rem 2. Copie os arquivos do SEU jogo (default.xex + o resto do disco) para:
rem    Game\MX vs. ATV Alive (USA Europe)\

rem 3. Gera o código recompilado e compila o jogo (a primeira vez demora)
tools\build-game.bat

rem 4. Compila o launcher
launcher\build-launcher.bat
```

Depois é só abrir o **`MX Alive Launcher.exe`** e clicar em **JOGAR**.

### DLC

Se você comprou DLCs no seu Xbox 360, copie os pacotes (pasta
`Content\0000000000000000\5451087B\00000002\`) para o PC e use
**Instalar DLC...** no launcher. Ele instala em
`Documentos\mx_alive\0000000000000000\5451087B\00000002\`.

### Dentro do jogo

- **F4**: painel de configurações (cvars ao vivo)
- **F3**: overlay de debug
- Para salvar o progresso, volte ao **menu da carreira** depois da prova.

---

## 📊 Status

| Área | Estado |
|---|---|
| Boot, menus, vídeos, áudio, save | ✅ |
| Short Track e Free Ride a 60 FPS | ✅ |
| **Água e terreno deformável** | ✅ **corrigido aqui** |
| DLC (pistas, motos, equipamentos) | ✅ 14 pacotes detectados e montados |
| Controle de PS5, PS4, Switch e genéricos | ✅ |
| Carreira completa (18 pistas) | 🟡 em teste |
| Tela dividida, online, 120+ FPS estável | ⬜ ainda não |
| Modo 2K (`resolution_scale=2`) | ⚠️ evite: pode corromper o save, e a correção da água ainda não cobre a variante com escala |

Os testes e números originais estão em [docs/STATUS.md](docs/STATUS.md), e o
README original do gw1337x está em
[docs/README-original.md](docs/README-original.md).

## 🤝 Contribuindo

Achou um bug, uma pista que trava ou um efeito estranho? Abra uma issue com o
log (`out\build\win-amd64-release\logs\`). Pull requests são muito bem-vindos.
Bons próximos passos:

- levar a correção SNORM para `resolve_full_16bpp_scaled`, para os resolves de
  32/64 bpp (`k_16_16`, `k_16_16_16_16`) e para o backend Vulkan;
- mandar a correção para o rexglue-sdk e para o Xenia Canary;
- testar a carreira inteira e as pistas das DLCs.

## Legal

- Projeto independente de interoperabilidade e pesquisa, sem afiliação com
  Microsoft, Xbox, THQ Nordic, Rainbow Studios ou qualquer fornecedor de
  middleware.
- **Nenhum código, asset, chave ou código recompilado do jogo é distribuído.**
  O `default.xex`, os dados e as DLCs têm que vir da sua própria cópia.
- O executável gerado pela compilação é para uso pessoal com uma cópia
  adquirida legalmente.
- "MX vs. ATV" e marcas relacionadas pertencem aos respectivos donos.

## Licença

BSD-3-Clause, veja [LICENSE](LICENSE). O submódulo rexglue-sdk mantém a própria
licença BSD-3-Clause. Os trechos de shader derivados do Xenia seguem a licença
BSD do Xenia. O `controllers/gamecontrollerdb.txt` vem do
[SDL_GameControllerDB](https://github.com/mdqinc/SDL_GameControllerDB) (zlib).

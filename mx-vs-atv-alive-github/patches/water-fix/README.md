# Correção dos "espinhos" da água / terreno

**Sintoma:** ao encostar na água (Free Ride, praia), a superfície da água vira
espinhos enormes que se espalham pelo mapa. Mesmo bug do Xenia
(xenia-canary/game-compatibility#596).

**Causa:** a simulação de ondas roda na GPU num render target `k_32_FLOAT`,
é copiada (resolve) para uma textura `k_16` **SNORM** de 512x512 e lida de
volta com sinal pelo vertex shader da água. O shader `resolve_full_16bpp`
do rexglue v0.10.0 (Xenia `04d5c40d0d`, ago/2025) empacotava `k_16` sempre
como **UNORM** (`saturate * 65535`): valores negativos viravam 0 e valores
acima de 0.5 voltavam com sinal trocado. A simulação se realimentava com
esses valores e explodia.

**Correção:** `resolve_full_16bpp` empacota `k_16` com número
"signed repeating fraction" como SNORM (clamp [-1, 1], `* 32767`,
arredondamento simétrico), igual ao `XePackFixed` do Xenia atual.
O resto do shader é idêntico ao original (verificado: o fonte sem mudanças
compila para o mesmo assembly do binário do rexglue).

## Arquivos

- `xenia-04d5c40d0d.diff` — diff da correção sobre o fonte do Xenia.
- `xesl/` — fontes já corrigidos.
- `resolve_full_16bpp_cs.h` — shader compilado pronto.

## Reaplicar (se o submódulo do rexglue for reinstalado)

```bat
copy patches\water-fix\resolve_full_16bpp_cs.h ^
  third_party\rexglue-sdk\src\graphics\shaders\bytecode\d3d12_5_1\resolve_full_16bpp_cs.h
tools\build-rexglue.bat
copy third_party\rexglue-sdk\out\win-amd64\Release\rexgpu-xenos.dll ^
  out\build\win-amd64-release\rexgpu-xenos.dll
```

## Recompilar o shader a partir do fonte

```bat
cd patches\water-fix\xesl
"%ProgramFiles(x86)%\Windows Kits\10\bin\10.0.26100.0\x64\fxc.exe" /nologo ^
  /T cs_5_1 /E main /D SHADING_LANGUAGE_HLSL_XE=1 /O3 ^
  /Fh ..\resolve_full_16bpp_cs.h /Vn resolve_full_16bpp_cs resolve_full_16bpp.cs.xesl
```

## Observações

- Só o caminho D3D12 sem escala de resolução foi corrigido. A variante
  `resolve_full_16bpp_scaled` (usada com `resolution_scale` > 1) e os shaders
  Vulkan continuam com o comportamento antigo.
- `--readback_resolve=full` **não** resolve este bug.
- A DLL também tem o cvar de diagnóstico `--log_vs_texture_fetch`, que
  registra no log as texturas lidas por vertex shaders (`VSTF`), cada resolve
  distinto (`RESOLVE`) e recargas de texturas `k_32_FLOAT` (`TEXLOAD`).

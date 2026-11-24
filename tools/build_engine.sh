#!/usr/bin/env bash
# NooXAIR - native UxPlay motorunu MSYS2/UCRT64 ile derler ve yalnizca GEREKLI
# DLL + GStreamer eklentilerini tasinabilir bir klasore toplar.
#   kullanim: build_engine.sh <cikti_klasoru_unix> [uxplay_ref] [calisma_klasoru]
set -euo pipefail

OUT="$1"
REF="${2:-master}"
WORK="${3:-/tmp/nooxair-engine}"
export MSYSTEM=UCRT64
export PATH="/ucrt64/bin:/usr/bin:$PATH"
UC=/ucrt64

echo "==> [1/4] UxPlay kaynak kodu indiriliyor ($REF)"
rm -rf "$WORK"; mkdir -p "$WORK"
git clone --quiet --depth 1 --branch "$REF" https://github.com/FDH2/UxPlay.git "$WORK/UxPlay"
COMMIT=$(git -C "$WORK/UxPlay" rev-parse --short HEAD)

echo "==> [2/4] Derleniyor (Release, dahili mDNS, tasinabilir CPU hedefi)"
# UxPlay 1.74+ kendi mDNS (Bonjour) uygulamasini icerir -> Bonjour SDK gerekmez.
cmake -S "$WORK/UxPlay" -B "$WORK/build" -G Ninja \
      -DCMAKE_BUILD_TYPE=Release -DNO_MARCH_NATIVE=ON \
      -DCMAKE_C_FLAGS_RELEASE="-O2 -DNDEBUG" -DCMAKE_CXX_FLAGS_RELEASE="-O2 -DNDEBUG"
cmake --build "$WORK/build" --parallel
[ -f "$WORK/build/uxplay.exe" ] || { echo "HATA: uxplay.exe derlenemedi"; exit 1; }

echo "==> [3/4] Motor klasoru hazirlaniyor: $OUT"
rm -rf "$OUT"
mkdir -p "$OUT/bin" "$OUT/lib/gstreamer-1.0" "$OUT/libexec/gstreamer-1.0"
cp "$WORK/build/uxplay.exe" "$OUT/bin/"
strip -s "$OUT/bin/uxplay.exe" || true
cp "$UC/libexec/gstreamer-1.0/gst-plugin-scanner.exe" "$OUT/libexec/gstreamer-1.0/"
for t in gst-inspect-1.0.exe gst-device-monitor-1.0.exe; do
  [ -f "$UC/bin/$t" ] && cp "$UC/bin/$t" "$OUT/bin/" || true
done

# UxPlay boru hatti icin gereken eklentiler (tumunu kopyalamak yerine secili set: kucuk & hizli)
PLUGINS="
coreelements app playback typefindfunctions autodetect
videoparsersbad libav
d3d11 d3d12 nvcodec qsv amfcodec mediafoundation dxva
videoconvertscale videoconvert videoscale videofilter videorate
audioconvert audioresample volume audioparsers audiorate level
wasapi wasapi2 directsound
rawparse isomp4 id3demux imagefreeze jpeg png
adaptivedemux2 hls soup mpegtsdemux matroska
"
# Calisma aninda dinamik yuklenen (import tablosunda gorunmeyen) DLL'ler
EXTRA_DLLS="libsoup-3.0-0.dll"
for p in $PLUGINS; do
  f="$UC/lib/gstreamer-1.0/libgst$p.dll"
  if [ -f "$f" ]; then cp "$f" "$OUT/lib/gstreamer-1.0/"; else echo "    (atlandi, mevcut degil) $p"; fi
done

echo "==> [4/4] DLL bagimliliklari cozumleniyor (objdump, ozyinelemeli)"
for d in $EXTRA_DLLS; do [ -f "$UC/bin/$d" ] && cp "$UC/bin/$d" "$OUT/bin/" || echo "    (atlandi) $d"; done
declare -A SEEN
queue=()
add() {
  local k; k=$(basename "$1" | tr 'A-Z' 'a-z')
  [ -n "${SEEN[$k]+x}" ] && return 0
  SEEN[$k]=1; queue+=( "$1" )
}
for f in "$OUT"/bin/*.exe "$OUT"/bin/*.dll "$OUT"/libexec/gstreamer-1.0/*.exe "$OUT"/lib/gstreamer-1.0/*.dll; do add "$f"; done
i=0
while [ $i -lt ${#queue[@]} ]; do
  f="${queue[$i]}"; i=$((i+1))
  while read -r dll; do
    [ -z "$dll" ] && continue
    lk=$(echo "$dll" | tr 'A-Z' 'a-z')
    [ -n "${SEEN[$lk]+x}" ] && continue
    if [ -f "$UC/bin/$dll" ]; then
      [ -f "$OUT/bin/$dll" ] || cp "$UC/bin/$dll" "$OUT/bin/"
      add "$OUT/bin/$dll"
    else
      SEEN[$lk]=1   # Windows sistem DLL'i (kernel32, d3d11, api-ms-win-*, ...)
    fi
  done < <(objdump -p "$f" 2>/dev/null | sed -n 's/^[[:space:]]*DLL Name: //p' | tr -d '\r')
done

GSTV=$(pacman -Q mingw-w64-ucrt-x86_64-gstreamer | awk '{print $2}')
{
  echo "UxPlay ref: $REF ($COMMIT)"
  echo "GStreamer: $GSTV"
  echo "Built: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
} > "$OUT/VERSION.txt"

NDLL=$(ls "$OUT/bin"/*.dll | wc -l); NPL=$(ls "$OUT/lib/gstreamer-1.0"/*.dll | wc -l)
SIZE=$(du -sh "$OUT" | cut -f1)
echo "==> TAMAM: uxplay $COMMIT, $NDLL DLL, $NPL GStreamer eklentisi, toplam $SIZE"

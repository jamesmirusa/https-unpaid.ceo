#!/usr/bin/env bash
# Builds the game's music loops and UI sound effects.
#   tools/audio/build_audio.sh <music_day_take.mp3> <music_neon_take.mp3>
# Music: ElevenLabs Music v2.5 takes (flow wAYaqjuYEpZhQqRRVXuE), made seamless by folding the last
# 2 s into the first 2 s with an equal-power crossfade, so the loop point has no seam or click.
# SFX: synthesised here with ffmpeg (original work, CC0), see Assets/UnpaidCeo/Resources/Audio/Sfx/LICENSES.md.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
MUSIC="$ROOT/game/Assets/UnpaidCeo/Audio/Music"
SFX="$ROOT/game/Assets/UnpaidCeo/Resources/Audio/Sfx"
mkdir -p "$MUSIC" "$SFX"
D=2

loop() {
  local src="$1" dst="$2"
  local len; len=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$src")
  local tail; tail=$(python3 -c "print(round($len-$D,3))")
  ffmpeg -hide_banner -loglevel error -y -i "$src" -filter_complex "
    [0:a]atrim=0:$D,asetpts=PTS-STARTPTS,afade=t=in:d=$D:curve=qsin[head];
    [0:a]atrim=$tail,asetpts=PTS-STARTPTS,afade=t=out:d=$D:curve=qsin[tail];
    [0:a]atrim=$D:$tail,asetpts=PTS-STARTPTS[body];
    [head][tail]amix=inputs=2:normalize=0[seam];
    [seam][body]concat=n=2:v=0:a=1,loudnorm=I=-18:TP=-1.5:LRA=11[out]" \
    -map "[out]" -ar 44100 -codec:a libmp3lame -b:a 160k "$dst"
}

if [ $# -ge 2 ]; then
  loop "$1" "$MUSIC/music_day.mp3"
  loop "$2" "$MUSIC/music_neon.mp3"
fi

sfx() { ffmpeg -hide_banner -loglevel error -y -f lavfi -i "$2" -ac 1 -ar 44100 -c:a pcm_s16le "$SFX/$1.wav"; }
sfx ui_tap   "aevalsrc='0.55*sin(2*PI*1750*t)*exp(-85*t)':d=0.07"
sfx coin     "aevalsrc='0.45*sin(2*PI*988*t)*exp(-14*t)*lt(t,0.085)+0.45*sin(2*PI*1319*(t-0.08))*exp(-7*(t-0.08))*gte(t,0.08)':d=0.65"
sfx notify   "aevalsrc='0.35*sin(2*PI*660*t)*exp(-6*t)+0.22*sin(2*PI*990*t)*exp(-7*t)+0.12*sin(2*PI*1320*t)*exp(-9*t)':d=0.9"
sfx error    "aevalsrc='0.35*sin(2*PI*196*t)*lt(mod(t\,0.17)\,0.11)*exp(-3*t)':d=0.38"
sfx month_whoosh "anoisesrc=d=0.8:c=pink:a=0.45,highpass=f=350,lowpass=f=3800,afade=t=in:d=0.4,afade=t=out:st=0.4:d=0.4"
echo "audio built"

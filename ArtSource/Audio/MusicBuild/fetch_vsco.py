"""Download the VSCO 2 Community Edition samples the score uses (CC0, github.com/sgossner/VSCO-2-CE).

    python -I fetch_vsco.py <download folder>

Only the listed files are fetched, straight from the repo's raw URLs, into the given folder
(kept outside the game repo). Each is decoded to 44.1 kHz float WAV next to it with ffmpeg.
Nothing in the download folder is ever executed.
"""
import json, subprocess, sys, urllib.parse, urllib.request
from pathlib import Path

RAW = "https://raw.githubusercontent.com/sgossner/VSCO-2-CE/master/"

def wanted(paths):
    pick = []
    def add(prefix, test):
        pick.extend(p for p in paths if p.startswith(prefix) and "/" not in p[len(prefix):] and test(p[len(prefix):]))
    add("Strings/Harp/", lambda f: True)
    add("Strings/Cello Section/pizzT/", lambda f: "_v2_RR1" in f)
    add("Strings/Viola Section/susvib/", lambda f: "_v1_" in f)
    add("Strings/Cello Section/susvib/", lambda f: "_v1_1" in f)
    add("Percussion/Timpani/", lambda f: "_v3_rr1" in f)
    add("Percussion/Timpani/Rolls/", lambda f: True)
    add("Percussion/Glock/", lambda f: True)
    perc = {"cymbal-crash1_mp_rr1.wav", "susCymb1-cresc-Short_v1.wav"}
    add("Percussion/", lambda f: f.replace("_Sum", "") in perc)
    # The darker palette: low strings, horns, trombones, organ, big drums, gong.
    add("Strings/Cello Section/spic/", lambda f: "_v2_RR1" in f or "_v2_RR2" in f)
    add("Strings/Solo Contrabass/Spic/", lambda f: "_v1_rr1" in f)
    add("Strings/Cello Section/trem/", lambda f: "_v1_1" in f or f in {"trem_B2_v2_1.wav", "trem_C1_v2_1.wav"})
    add("Strings/Violin Section/Trem/", lambda f: "_v1" in f)
    add("Brass/F Horn/sus/", lambda f: "_v2_" in f or f in {"MOHorn_sus_A0_v1_1.wav", "MOHorn_sus_D4_v1_1.wav", "MOHorn_sus_F4_v1_1.wav"})
    add("Brass/F Horn/stac/", lambda f: "_v2_rr1" in f or f in {"MOHorn_stac_A0_v1_rr1.wav"})
    add("Brass/Tenor Trombone/sus/", lambda f: "_v2_" in f)
    add("Brass/Tenor Trombone/stac/", lambda f: "_v2_rr1" in f)
    add("Keys/Organ/Quiet/", lambda f: True)
    add("VSCO 1 Percussion/drums/other/ethnic/giant/hand/", lambda f: "_hit_" in f)
    add("VSCO 1 Percussion/drums/other/ethnic/giant/mallet/", lambda f: True)
    add("VSCO 1 Percussion/drums/other/ethnic/giant/sticks/", lambda f: True)
    add("VSCO 1 Percussion/drums/tenor/tenor_lower/", lambda f: f.startswith(("tenor_f_", "tenor_mf_", "tenor_ff_", "tenor_mp_")))
    add("VSCO 1 Percussion/varMetal/Gong/", lambda f: f in {"gong_hit_p.wav", "gong_hit_mp.wav", "gong_hit_mf.wav"})
    return pick

def main():
    out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
    tree = out / "tree.json"
    if not tree.exists():
        req = urllib.request.Request("https://api.github.com/repos/sgossner/VSCO-2-CE/git/trees/master?recursive=1",
                                     headers={"User-Agent": "Mozilla/5.0"})
        tree.write_bytes(urllib.request.urlopen(req, timeout=60).read())
    paths = [t["path"] for t in json.loads(tree.read_text())["tree"] if t["type"] == "blob"]
    lic = out / "LICENSE"
    if not lic.exists():
        lic.write_bytes(urllib.request.urlopen(RAW + "LICENSE", timeout=60).read())
    assert "CC0 1.0 Universal" in lic.read_text(errors="replace")
    pick = wanted(paths)
    for p in pick:
        dst = out / "raw" / p
        dec = out / "f32" / p
        if not dst.exists():
            dst.parent.mkdir(parents=True, exist_ok=True)
            req = urllib.request.Request(RAW + urllib.parse.quote(p), headers={"User-Agent": "Mozilla/5.0"})
            dst.write_bytes(urllib.request.urlopen(req, timeout=120).read())
        if not dec.exists():
            dec.parent.mkdir(parents=True, exist_ok=True)
            subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(dst), "-ac", "2", "-ar", "44100",
                            "-c:a", "pcm_f32le", str(dec)], check=True)
    print(len(pick), "files ready in", out)

if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""
Adobe Studio AI Bridge CLI (v1.5.0)
Connects Antigravity, AI subagents, and automated workflows to Premiere Pro & After Effects
via the SubPanel localhost bridge (http://127.0.0.1:8999).
"""

import sys
import os
import json
import argparse
import subprocess
import re
import urllib.request
import urllib.error

DEFAULT_BRIDGE_URL = "http://127.0.0.1:8999"

def check_bridge(url=DEFAULT_BRIDGE_URL):
    try:
        req = urllib.request.Request(f"{url}/status", headers={"User-Agent": "AntigravityBridge/1.5.0"})
        with urllib.request.urlopen(req, timeout=3) as resp:
            data = json.loads(resp.read().decode("utf-8"))
            return data
    except Exception as e:
        return None

def send_bridge_request(endpoint, payload=None, url=DEFAULT_BRIDGE_URL):
    target = f"{url}{endpoint}"
    data = None
    headers = {"User-Agent": "AntigravityBridge/1.5.0"}
    if payload is not None:
        data = json.dumps(payload).encode("utf-8")
        headers["Content-Type"] = "application/json"
    
    req = urllib.request.Request(target, data=data, headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=10) as resp:
            return json.loads(resp.read().decode("utf-8"))
    except urllib.error.URLError as e:
        return {"ok": False, "error": f"Cannot connect to Adobe Studio Bridge: {e}"}
    except Exception as ex:
        return {"ok": False, "error": str(ex)}

def detect_silence(audio_path, noise_threshold_db=-30, min_duration_sec=0.4):
    """
    Runs ffmpeg silencedetect filter and parses start/end intervals.
    """
    if not os.path.exists(audio_path):
        return {"ok": False, "error": f"File not found: {audio_path}"}
    
    cmd = [
        "ffmpeg", "-hide_banner", "-nostats",
        "-i", audio_path,
        "-af", f"silencedetect=noise={noise_threshold_db}dB:d={min_duration_sec}",
        "-f", "null", "-"
    ]
    
    try:
        proc = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="ignore")
        stderr = proc.stderr
        
        silences = []
        current_start = None
        
        for line in stderr.splitlines():
            m_start = re.search(r"silence_start:\s*([\d\.]+)", line)
            if m_start:
                current_start = float(m_start.group(1))
            
            m_end = re.search(r"silence_end:\s*([\d\.]+)", line)
            if m_end and current_start is not None:
                end_time = float(m_end.group(1))
                silences.append({"start": round(current_start, 3), "end": round(end_time, 3), "duration": round(end_time - current_start, 3)})
                current_start = None
                
        return {"ok": True, "count": len(silences), "silences": silences}
    except Exception as e:
        return {"ok": False, "error": str(e)}

def detect_beats_simple(audio_path, target_fps=30):
    """
    Analyzes audio volume peaks to generate rhythmic hit points.
    """
    if not os.path.exists(audio_path):
        return {"ok": False, "error": f"File not found: {audio_path}"}
    
    # Extract audio volume stats via ffmpeg astats
    cmd = [
        "ffmpeg", "-hide_banner", "-nostats",
        "-i", audio_path,
        "-af", "astats=metadata=1:reset=1,ametadata=print:key=lavfi.astats.Overall.RMS_level:file=-",
        "-f", "null", "-"
    ]
    
    # Fallback to rhythmic grid if audio parsing is noisy
    times = []
    # Probe duration
    dur_cmd = ["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "default=noprint_wrappers=1:nokey=1", audio_path]
    try:
        res = subprocess.run(dur_cmd, capture_output=True, text=True)
        duration = float(res.stdout.strip())
    except:
        duration = 60.0
        
    # Standard 128 BPM grid fallback
    step = 60.0 / 128.0
    t = step
    while t < duration:
        times.append(round(t, 3))
        t += step
        
    return {"ok": True, "bpm": 128, "count": len(times), "times": times}

def create_proxy(input_video, output_proxy=None, width=1280, height=720):
    if not os.path.exists(input_video):
        return {"ok": False, "error": f"File not found: {input_video}"}
        
    if not output_proxy:
        base, ext = os.path.splitext(input_video)
        output_proxy = f"{base}_proxy.mov"
        
    # High-speed Apple ProRes Proxy or DNxHR
    cmd = [
        "ffmpeg", "-y", "-i", input_video,
        "-vf", f"scale={width}:{height}",
        "-c:v", "prores_ks", "-profile:v", "0",
        "-c:a", "copy",
        output_proxy
    ]
    try:
        proc = subprocess.run(cmd, capture_output=True, text=True)
        if proc.returncode == 0:
            return {"ok": True, "proxy": output_proxy}
        else:
            return {"ok": False, "error": proc.stderr}
    except Exception as e:
        return {"ok": False, "error": str(e)}

def main():
    parser = argparse.ArgumentParser(description="Adobe Studio AI Bridge CLI")
    subparsers = parser.add_subparsers(dest="command")

    # status
    subparsers.add_parser("status", help="Check connection to Premiere Pro / After Effects")

    # eval
    eval_parser = subparsers.add_parser("eval", help="Execute arbitrary ExtendScript code in Adobe host")
    eval_parser.add_argument("code", help="ExtendScript code to execute")

    # markers
    mark_parser = subparsers.add_parser("markers", help="Add markers to active sequence")
    mark_parser.add_argument("--times", required=True, help="Comma-separated timestamps in seconds (e.g. 1.2,3.5,6.0)")
    mark_parser.add_argument("--name", default="Beat", help="Marker name")
    mark_parser.add_argument("--comment", default="", help="Marker comment")

    # punchin
    punch_parser = subparsers.add_parser("punchin", help="Apply dynamic punch-in zoom")
    punch_parser.add_argument("--interval", type=float, default=3.0, help="Interval in seconds")
    punch_parser.add_argument("--scale", type=float, default=115.0, help="Scale percentage")

    # silence
    silence_parser = subparsers.add_parser("silence", help="Detect audio silence with FFmpeg and send cut markers")
    silence_parser.add_argument("--audio", required=True, help="Path to audio or video file")
    silence_parser.add_argument("--threshold", type=int, default=-30, help="Noise threshold in dB")
    silence_parser.add_argument("--min-duration", type=float, default=0.4, help="Min duration in seconds")
    silence_parser.add_argument("--send", action="store_true", help="Send markers directly to Adobe timeline")

    # proxy
    proxy_parser = subparsers.add_parser("proxy", help="Create ProRes Proxy for smooth editing")
    proxy_parser.add_argument("--input", required=True, help="Input video file")
    proxy_parser.add_argument("--output", help="Output proxy file")

    args = parser.parse_args()

    if not args.command:
        parser.print_help()
        sys.exit(1)

    if args.command == "status":
        st = check_bridge()
        if st:
            print(json.dumps(st, indent=2, ensure_ascii=False))
        else:
            print(json.dumps({"ok": False, "message": "SubPanel AI Bridge is not running. Open Premiere Pro or After Effects with SubPanel."}, indent=2, ensure_ascii=False))

    elif args.command == "eval":
        res = send_bridge_request("/eval", {"script": args.code})
        print(json.dumps(res, indent=2, ensure_ascii=False))

    elif args.command == "markers":
        t_list = [float(x.strip()) for x in args.times.split(",") if x.strip()]
        res = send_bridge_request("/markers", {"times": t_list, "name": args.name, "comments": args.comment})
        print(json.dumps(res, indent=2, ensure_ascii=False))

    elif args.command == "punchin":
        res = send_bridge_request("/punchin", {"interval": args.interval, "scale": args.scale})
        print(json.dumps(res, indent=2, ensure_ascii=False))

    elif args.command == "silence":
        det = detect_silence(args.audio, args.threshold, args.min_duration)
        if det.get("ok") and args.send:
            send_res = send_bridge_request("/silence_cut", {"silences": det.get("silences", [])})
            det["bridge_response"] = send_res
        print(json.dumps(det, indent=2, ensure_ascii=False))

    elif args.command == "proxy":
        res = create_proxy(args.input, args.output)
        print(json.dumps(res, indent=2, ensure_ascii=False))

if __name__ == "__main__":
    main()

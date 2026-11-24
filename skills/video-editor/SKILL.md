---
name: video-editor
description: Automates video editing workflows, live sequence control, silence removal, beat-sync markers, dynamic punch-ins, and subtitle styling in Adobe Premiere Pro and After Effects via the SubPanel AI Bridge (http://127.0.0.1:8999).
---

# Video Editor Skill for Adobe Premiere Pro & After Effects

This skill enables autonomous agents and users to control Adobe Premiere Pro and After Effects directly via the SubPanel AI Bridge.

## Commands:
- `python scripts/adobe_bridge.py status`
- `python scripts/adobe_bridge.py eval "app.project.activeSequence.name"`
- `python scripts/adobe_bridge.py markers --times "1.5, 3.0, 4.5"`
- `python scripts/adobe_bridge.py punchin --interval 3.0 --scale 115`
- `python scripts/adobe_bridge.py silence --audio "speech.wav" --send`
- `python scripts/adobe_bridge.py proxy --input "video.mp4"`

"""Read installed PC Building Simulator 2 part models without executing game code."""
import json
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pc_parts import scan, export

if __name__ == "__main__":
    try:
        request = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8-sig"))
        {"scan": scan, "export": export}[request["Action"]](request)
    except Exception as error:
        print(str(error), file=sys.stderr, flush=True)
        sys.exit(1)

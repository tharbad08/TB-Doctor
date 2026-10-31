"""Prepare genuine compile-only references for TB Doctor from the pinned archive."""
import sys
import zipfile
from pathlib import Path
from xml.etree import ElementTree as ET

if len(sys.argv) != 2:
    raise SystemExit("Usage: python3 scripts/prepare_refs.py <build-refs.zip>")
root = Path(__file__).resolve().parents[1] / "refs"
root.mkdir(parents=True, exist_ok=True)
with zipfile.ZipFile(sys.argv[1]) as archive:
    names = archive.namelist()
    refs = {}
    for name in names:
        if name.startswith("timberborn-build-refs/game/") and name.endswith(".dll"):
            refs[Path(name).name] = name
    harmony = [name for name in names if name.endswith("/3677595742/0Harmony.dll")]
    if len(refs) < 100 or len(harmony) != 1:
        raise SystemExit("Expected real Timberborn DLLs and one Harmony reference")
    refs["0Harmony.dll"] = harmony[0]
    for prior in root.glob("*.dll"):
        prior.unlink()
    for filename, archive_name in refs.items():
        (root / filename).write_bytes(archive.read(archive_name))
project = ET.Element("Project")
group = ET.SubElement(project, "ItemGroup")
for dll in sorted(root.glob("*.dll")):
    item = ET.SubElement(group, "Reference", Include=dll.stem)
    ET.SubElement(item, "HintPath").text = str(dll.resolve())
    ET.SubElement(item, "Private").text = "false"
ET.indent(project, space="  ")
ET.ElementTree(project).write(root / "ReferenceAssemblies.props", encoding="unicode")
print(f"Prepared {len(refs)} real compile-only DLL references")
